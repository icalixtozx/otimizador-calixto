using System.Text;
using Optimizer.Core.Inventory;

namespace Optimizer.Windows;

/// <summary>
/// Read-only, bounded check that the INF files and Needs sections referenced by a third-party package
/// exist in the current Windows INF directory. This is an availability check, not a signature check.
/// </summary>
public sealed class WindowsSystemInfDependencyResolver : IDriverPackageSystemInfDependencyResolver
{
    public DriverPackageSystemDependencyResult Verify(
        IReadOnlyList<string> includedInfNames,
        IReadOnlyList<string> requiredSections)
    {
        if (!OperatingSystem.IsWindows())
            return new(DriverPackageDependencyStatus.Unavailable, "A resolução de INF do sistema exige Windows.");

        try
        {
            var infRoot = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "INF"));
            var infs = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.OrdinalIgnoreCase);
            foreach (var include in includedInfNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(include) ||
                    !string.Equals(Path.GetFileName(include), include, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(Path.GetExtension(include), ".inf", StringComparison.OrdinalIgnoreCase))
                    return new(DriverPackageDependencyStatus.Unavailable, $"O nome Include não é um INF simples e seguro: {Path.GetFileName(include)}.");

                var path = Path.GetFullPath(Path.Combine(infRoot, include));
                if (!path.StartsWith(infRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                    return new(DriverPackageDependencyStatus.Unavailable, "Um Include tentou sair de %WINDIR%\\INF.");
                if (!File.Exists(path))
                    return new(DriverPackageDependencyStatus.Missing, $"O INF do Windows {include} não foi encontrado em %WINDIR%\\INF.");
                if (new FileInfo(path).Length > 8L * 1024 * 1024)
                    return new(DriverPackageDependencyStatus.Unavailable, $"O INF do Windows {include} excede o limite de leitura de 8 MiB.");
                infs[include] = ParseSections(ReadInf(path));
            }

            foreach (var section in requiredSections.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var owners = infs.Where(pair => pair.Value.ContainsKey(section)).Select(pair => pair.Key).ToArray();
                if (owners.Length == 0)
                    return new(DriverPackageDependencyStatus.Missing, $"A seção Needs [{section}] não foi encontrada nos INF incluídos: {string.Join(", ", includedInfNames)}.");
                if (owners.Length > 1)
                    return new(DriverPackageDependencyStatus.Ambiguous, $"A seção Needs [{section}] aparece em mais de um INF incluído: {string.Join(", ", owners)}.");
            }

            return new(DriverPackageDependencyStatus.Available,
                $"INF incluídos encontrados em %WINDIR%\\INF e {requiredSections.Count} seção(ões) Needs resolvida(s). A assinatura dessas dependências do Windows não é revalidada por esta checagem.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException or DecoderFallbackException)
        {
            return new(DriverPackageDependencyStatus.Unavailable,
                $"A leitura das dependências de INF do Windows falhou ({exception.GetType().Name}).");
        }
    }

    private static string ReadInf(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return new UTF8Encoding(false, true).GetString(bytes, 3, bytes.Length - 3);
        return Encoding.Latin1.GetString(bytes);
    }

    private static Dictionary<string, List<string>> ParseSections(string source)
    {
        var sections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        string? current = null;
        var pending = new StringBuilder();
        foreach (var raw in source.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var line = StripComment(raw).Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                current = line[1..^1].Trim();
                if (current.Length == 0) { current = null; continue; }
                sections.TryAdd(current, []);
                continue;
            }
            if (current is null) continue;
            if (line.EndsWith('\\'))
            {
                pending.Append(line[..^1]).Append(' ');
                continue;
            }
            if (pending.Length > 0)
            {
                pending.Append(line);
                line = pending.ToString();
                pending.Clear();
            }
            sections[current].Add(line);
        }
        return sections;
    }

    private static string StripComment(string line)
    {
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            if (line[index] == '"') quoted = !quoted;
            else if (line[index] == ';' && !quoted) return line[..index];
        }
        return line;
    }
}
