using System.Diagnostics;
using IOPath = System.IO.Path;

namespace PowerShell.ModuleForge;

internal static class DotnetRunner
{
    /// <summary>PATH (없으면 DOTNET_ROOT) 에서 dotnet 실행 파일을 찾습니다.</summary>
    public static string? Find()
    {
        var exe = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";

        var dirs = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(IOPath.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (Environment.GetEnvironmentVariable("DOTNET_ROOT") is { Length: > 0 } root)
            dirs.Add(root);

        return dirs
            .Select(d => IOPath.Combine(d, exe))
            .FirstOrDefault(File.Exists);
    }

    /// <summary>dotnet 을 실행하고 (종료 코드, 표준출력+표준오류) 를 돌려줍니다.</summary>
    public static (int ExitCode, string Output) Run(string dotnet, IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo(dotnet)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.Environment["DOTNET_NOLOGO"] = "1";
        psi.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException("dotnet 프로세스를 시작할 수 없습니다.");

        // 두 스트림을 동시에 비워야 파이프가 가득 차서 멈추는 일이 없습니다.
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        p.WaitForExit();

        return (p.ExitCode, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }
}
