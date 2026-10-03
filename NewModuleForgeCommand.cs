using System.Collections;
using System.Management.Automation;
using System.Text;
using IOPath = System.IO.Path;
// 네임스페이스 PowerShell.* 안에서는 'PowerShell' 이 네임스페이스로 먼저 해석되므로 별칭이 필요합니다.
using PS = System.Management.Automation.PowerShell;

namespace PowerShell.ModuleForge;

/// <summary>New-ModuleForge 가 돌려주는 결과 (-PassThru).</summary>
public sealed record ModuleForgeResult(
    string ModuleName,
    string Cmdlet,
    string Root,
    string OutputPath,
    string ManifestPath);

/// <summary>.NET 바이너리 PowerShell 모듈 뼈대를 만들고 빌드합니다.</summary>
[Cmdlet(VerbsCommon.New, "ModuleForge", SupportsShouldProcess = true)]
[OutputType(typeof(ModuleForgeResult))]
public sealed class NewModuleForgeCommand : PSCmdlet
{
    // 지원 하한: PowerShell 7.6 (.NET 10). 올릴 때는 세 값을 함께 바꾼다
    private const string Tfm = "net10.0";
    private const string SmaVer = "7.6.6";
    private const string PsMinVer = "7.6";

    /// <summary>모듈 이름. 점(.) 포함 가능 (예: ViVaKR.Demo)</summary>
    [Parameter(Position = 0, Mandatory = true)]
    [ValidatePattern(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$")]
    public string ModuleName { get; set; } = string.Empty;

    /// <summary>샘플 cmdlet 의 승인된 동사 (Get-Verb 참고). 탭 완성 지원.</summary>
    [Parameter(Position = 1, Mandatory = true)]
    [ValidateSet(typeof(VerbValues))]
    public string Verb { get; set; } = string.Empty;

    /// <summary>매니페스트 작성자 (생략 가능)</summary>
    [Parameter]
    public string? Author { get; set; }

    /// <summary>프로젝트 주소 (생략 가능)</summary>
    [Parameter]
    [Alias("ProjectUrl")]
    public string? ProjectUri { get; set; }

    /// <summary>라이선스 주소 (생략 가능)</summary>
    [Parameter]
    public string? LicenseUri { get; set; }

    /// <summary>모듈 폴더를 만들 위치 (기본: 현재 PowerShell 위치)</summary>
    [Parameter]
    public string? Path { get; set; }

    /// <summary>결과 객체를 출력합니다.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    protected override void ProcessRecord()
    {
        // Directory.GetCurrentDirectory() 는 프로세스 기준이라 PowerShell 의 현재 위치와 다를 수 있습니다.
        // ~ 같은 PowerShell 경로도 여기서 풀어 줍니다.
        var basePath = GetUnresolvedProviderPathFromPSPath(
            Path ?? SessionState.Path.CurrentFileSystemLocation.ProviderPath);

        var root = IOPath.Combine(basePath, ModuleName);
        if (Directory.Exists(root) || File.Exists(root))
        {
            ThrowTerminatingError(new ErrorRecord(
                new IOException($"이미 존재합니다: {root}"),
                "ModuleAlreadyExists", ErrorCategory.ResourceExists, root));
        }

        var dotnet = DotnetRunner.Find();
        if (dotnet is null)
        {
            ThrowTerminatingError(new ErrorRecord(
                new FileNotFoundException("dotnet SDK를 찾을 수 없습니다."),
                "DotnetNotFound", ErrorCategory.ObjectNotFound, "dotnet"));
            return;
        }

        if (!ShouldProcess(root, "모듈 생성")) return;

        var noun = ModuleName.Split('.')[^1];                       // ModuleForge
        var verb = PwshVerbs.Resolve(Verb) ?? Verb;                 // 표준 철자
        var cmdlet = $"{verb}-{noun}";                              // New-ModuleForge
        var className = $"{verb}{noun}Command";

        var projDir = IOPath.Combine(root, "src", ModuleName);
        var outDir = IOPath.Combine(root, "Output", ModuleName);    // 배포 폴더: DLL + psd1
        var manifestPath = IOPath.Combine(outDir, $"{ModuleName}.psd1");

        var succeeded = false;
        try
        {
            foreach (var d in new[] { projDir, outDir, IOPath.Combine(root, "Tests") })
                Directory.CreateDirectory(d);

            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);   // utf8NoBOM
            File.WriteAllText(IOPath.Combine(projDir, $"{ModuleName}.csproj"),
                Templates.Csproj(Tfm, SmaVer), utf8);
            File.WriteAllText(IOPath.Combine(projDir, $"{className}.cs"),
                Templates.Cmdlet(ModuleName, verb, noun, className), utf8);

            WriteVerbose($"dotnet build {projDir} -c Release -o {outDir}");
            var (exitCode, log) = DotnetRunner.Run(dotnet, ["build", projDir, "-c", "Release", "-o", outDir]);
            WriteVerbose(log);
            if (exitCode != 0)
                throw new InvalidOperationException($"dotnet build 실패 (exit {exitCode})\n{log}");

            var manifest = new Hashtable
            {
                ["Path"] = manifestPath,
                ["RootModule"] = $"{ModuleName}.dll",
                ["ModuleVersion"] = "0.1.0",
                ["Description"] = $"{ModuleName} 모듈",
                ["PowerShellVersion"] = PsMinVer,
                ["CompatiblePSEditions"] = new[] { "Core" },
                ["CmdletsToExport"] = new[] { cmdlet },
                ["FunctionsToExport"] = Array.Empty<string>(),
                ["AliasesToExport"] = Array.Empty<string>(),
                ["VariablesToExport"] = Array.Empty<string>(),
                ["Tags"] = new[] { "binary-module", "dotnet", "powershell" },
            };
            if (!string.IsNullOrWhiteSpace(Author)) manifest["Author"] = Author;
            if (!string.IsNullOrWhiteSpace(ProjectUri)) manifest["ProjectUri"] = ProjectUri;
            if (!string.IsNullOrWhiteSpace(LicenseUri)) manifest["LicenseUri"] = LicenseUri;

            InvokePowerShellCmdlet("New-ModuleManifest", manifest);
            InvokePowerShellCmdlet("Test-ModuleManifest", new Hashtable { ["Path"] = manifestPath });   // 매니페스트 검증

            succeeded = true;
        }
        finally
        {
            // 실패하거나 Ctrl+C 로 중단되면 만들다 만 폴더를 지웁니다. (위에서 존재하지 않음을 확인했으므로 안전)
            if (!succeeded) TryDeleteDirectory(root);
        }

        // Write-Host 와 같은 방식 (Information 스트림, PSHOST 태그)
        WriteInformation(new HostInformationMessage
        {
            Message = $"모듈 {ModuleName} 작성 완료: {outDir}",
            ForegroundColor = ConsoleColor.Green,
        }, ["PSHOST"]);
        WriteInformation(new HostInformationMessage
        {
            Message = $"확인: Import-Module '{manifestPath}'; Get-Command -Module {ModuleName}",
        }, ["PSHOST"]);

        if (PassThru)
            WriteObject(new ModuleForgeResult(ModuleName, cmdlet, root, outDir, manifestPath));
    }

    /// <summary>현재 런스페이스에서 PowerShell cmdlet 을 호출하고, 오류가 있으면 예외로 올립니다.</summary>
    private static void InvokePowerShellCmdlet(string name, IDictionary parameters)
    {
        using var ps = PS.Create(RunspaceMode.CurrentRunspace);
        ps.AddCommand(name).AddParameters(parameters);
        ps.Invoke();

        if (ps.HadErrors)
        {
            var message = string.Join(Environment.NewLine, ps.Streams.Error.Select(e => e.ToString()));
            throw new InvalidOperationException($"{name} 실패: {message}");
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
