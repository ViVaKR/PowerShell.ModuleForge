namespace PowerShell.ModuleForge;

/// <summary>생성될 프로젝트 파일 템플릿 (원본 스크립트의 here-string 에 해당).</summary>
internal static class Templates
{
    // csproj: 프레임워크와 SMA 버전 고정, SMA 는 배포물에 섞이지 않게
    public static string Csproj(string tfm, string smaVer) => $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>{tfm}</TargetFramework>
            <Nullable>enable</Nullable>
            <CopyLocalLockFileAssemblies>false</CopyLocalLockFileAssemblies>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="System.Management.Automation" Version="{smaVer}"
                              ExcludeAssets="runtime;contentFiles" PrivateAssets="all" />
          </ItemGroup>
        </Project>

        """;

    // $$""" : 보간은 {{ }}, 한 겹 중괄호는 생성될 C# 코드 그대로 출력됩니다.
    public static string Cmdlet(string module, string verb, string noun, string className) => $$"""
        using System.Management.Automation;

        namespace {{module}};

        [Cmdlet("{{verb}}", "{{noun}}", SupportsShouldProcess = true)]
        public class {{className}} : PSCmdlet
        {
            [Parameter(Position = 0, Mandatory = true)]
            public string Name { get; set; } = string.Empty;

            protected override void ProcessRecord()
            {
                if (ShouldProcess(Name, "{{verb}}"))
                {
                    WriteObject($"{Name} 처리 완료");
                }
            }
        }

        """;
}
