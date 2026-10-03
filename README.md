# PowerShell.ModuleForge

>- 파워쉘 모듈 작성기

## 파일구성

- `NewModuleForgeCommand.cs`: cmdlet 본체입니다. 스크립트의 New-PwshModule 흐름(검증, 폴더 생성, 파일 작성, 빌드, 매니페스트 생성과 검증, 실패 시 롤백)을 그대로 따릅니다.

- `PwshVerbs.cs`: Get-Verb에 해당하는 승인 동사 목록입니다. [ValidateSet(typeof(VerbValues))] 하나로 검증과 탭 완성을 함께 처리합니다.

- `DotnetRunner.cs`: dotnet 경로를 찾고 실행합니다. stdout과 stderr를 동시에 읽어서 출력이 많아도 멈추지 않습니다.

- `Templates.cs`: 생성될 csproj와 샘플 cmdlet 소스입니다. 스크립트의 here-string 자리이고, C# 11 이상의 raw string($$""")을 썼습니다.

- `PowerShell_ModuleForge.csproj`: AssemblyName을 PowerShell.ModuleForge로 지정했습니다. 파일명 때문에 DLL이 PowerShell_ModuleForge.dll이 되는 것을 막기 위해서입니다. ImplicitUsings도 켰습니다.


## 상세

- `ModuleType`이 **Binary**이고 `ExportedCommands`에 `New-Demo`만 보입니다. 매니페스트의 `CmdletsToExport`가 의도한 대로 동작한다는 뜻입니다.

- `-WhatIf` 출력(`Performing the operation "New" on target "안녕"`)은 `SupportsShouldProcess = true`와 `ShouldProcess(Name, "New")` 호출 덕분입니다. 이 부분이 바이너리 cmdlet에서 가장 기본이 되는 패턴입니다.


## 눈여겨 볼곳

1. `[ValidateSet(typeof(VerbValues))]`에 쓴 `IValidateSetValuesGenerator`는 스크립트의 `ValidateScript`와 `ArgumentCompleter` 두 개를 하나로 합친 것입니다.
2. `PS.Create(RunspaceMode.CurrentRunspace)`는 cmdlet 안에서 `New-ModuleManifest` 같은 다른 cmdlet을 호출하는 방법입니다.
3. `WriteInformation`과 `HostInformationMessage`에 `PSHOST` 태그를 단 것은 `Write-Host`가 내부에서 하는 일과 같습니다.

- 막힐때 : `New-ModuleForge` 자체의 매니페스트와 Pester 테스트를 `Tests` 폴더에 넣음

## 배포 (./publish.ps1 사용시)

```powershell

# 겔러리 모듈이름 중복 여부 확인 하기
Find-PSResource -Name PowerShell.ModuleForge -Repository PSGallery -Prerelease
Find-PSResource -Name PowerShell.ModuleForge -Repository PSGallery

# 빌드 부터 확인까지 (레포 루트에서 , psd1 을 루트에 두었다고 가정)
dotnet build -c Release -o ./Output/PowerShell.ModuleForge
Copy-Item ./PowerShell.ModuleForge.psd1 ./Output/PowerShell.ModuleForge/

Test-ModuleManifest ./Output/PowerShell.ModuleForge/PowerShell.ModuleForge.psd1
Import-Module ./Output/PowerShell.ModuleForge/PowerShell.ModuleForge.psd1 -Force
Get-Command -Module PowerShell.ModuleForge

# 빌드와 검증까지만, 게시와 태그는 건너뜀
./publish.ps1 -WhatIf

# 키보관함 확인
Get-SecretInfo -Name ViVaKRKey

# 게시전 커밋
git add .
git commit -m "feat: New-ModuleForge 0.1.0-alpha001"
git push origin main

# 게시 하기
./publish.ps1 -Tag

Get-Module -ListAvailable PowerShell.ModuleForge   # 설치 확인
Import-Module PowerShell.ModuleForge               # 명시적으로 불러오고 싶을 때
Get-Module                     # 지금 세션에 올라와 있는 것
Get-Module -ListAvailable      # 설치되어 있어서 불러올 수 있는 것

# 사용
New-ModuleForge ViVaKR.Demo New -Author 'Kim Bum Jun' -Path ~/Temp -Verbose
```


## 수동 처리

```powershell

# 배포 리허설
$apiKey = Get-Secret -Name ViVaKRKey -AsPlainText
Publish-PSResource -Path ./Output/ViVaKR.Demo -Repository PSGallery -ApiKey $apiKey -WhatIf

# 진짜 배포
Publish-PSResource -Path ./Output/ViVaKR.Demo -Repository PSGallery -ApiKey $apiKey
Remove-Variable apiKey      # 끝나면 메모리에서 정리


# 로컬 저장소에 배포 리허설
Register-PSResourceRepository -Name Local -Uri ~/LocalRepo -Trusted
Publish-PSResource -Path ./Output/ViVaKR.Demo -Repository Local
Install-PSResource ViVaKR.Demo -Repository Local
```

## 비밀번호 저장소 처리

```powershell
Install-PSResource -Name Microsoft.PowerShell.SecretManagement, Microsoft.PowerShell.SecretStore
Register-SecretVault -Name LocalVault -ModuleName Microsoft.PowerShell.SecretStore -DefaultVault

Set-Secret -Name ViVaKRKey -Secret (Read-Host 'PSGallery API Key' -AsSecureString)
```

## PowerShell Gallery 특징


- 갤러리는 패키지를 영구 삭제하지 않습니다. 문서에 따르면 'unlist'만 지원하는데, unlist하면 검색과 목록에서는 사라지지만 정확한 버전을 지정하면 계속 내려받을 수 있습니다. 그래서 모듈 이름과 올린 버전은 계속 남습니다.

-  게시할 때 버전은 기본적으로 이전에 올린 어떤 버전보다 높아야 합니다. 같은 버전을 덮어쓸 수 없으니, 실수하면 버전을 올려서 다시 올려야 합니다.

- 미완성이면 매니페스트의 PSData에 Prerelease = 'alpha' 같은 값을 넣는 방법이 있습니다. 이렇게 하면 prerelease로 표시되고, 설치할 때 -AllowPrerelease를 붙여야 받을 수 있습니다. 실수로 설치하는 사람을 막아 주는 장치입니다.

- 갤러리에 올릴 때는 Output/<모듈명> 폴더, 즉 DLL과 psd1이 든 폴더를 가리키면 됩니다. API 키는 갤러리 계정에서 발급받습니다.

- 진짜 모듈을 올릴 때는 Author, Description, ProjectUri, LicenseUri, Tags를 채우시면 좋습니다. New-ModuleForge에 이미 이 옵션들이 있습니다.
---
