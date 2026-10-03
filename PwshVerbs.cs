using System.Management.Automation;
using System.Reflection;

namespace PowerShell.ModuleForge;

/// <summary>Get-Verb 와 같은 승인된 동사 목록 (System.Management.Automation 의 Verbs* 상수에서 수집).</summary>
internal static class PwshVerbs
{
    private static readonly Lazy<string[]> all = new(Load);

    public static IReadOnlyList<string> All => all.Value;

    /// <summary>대소문자 무시로 찾아서 표준 철자를 돌려줍니다. 없으면 null.</summary>
    public static string? Resolve(string verb) =>
        All.FirstOrDefault(v => string.Equals(v, verb, StringComparison.OrdinalIgnoreCase));

    private static string[] Load() =>
        new[]
        {
            typeof(VerbsCommon), typeof(VerbsCommunications), typeof(VerbsData),
            typeof(VerbsDiagnostic), typeof(VerbsLifecycle), typeof(VerbsOther),
            typeof(VerbsSecurity),
        }
        .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
        .Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

/// <summary>[ValidateSet(typeof(VerbValues))] — 검증과 탭 완성(ArgumentCompleter)을 한 번에 해결합니다.</summary>
public sealed class VerbValues : IValidateSetValuesGenerator
{
    public string[] GetValidValues() => PwshVerbs.All.ToArray();
}
