using System.Text;

namespace Optimizer.Core.Inventory;

/// <summary>
/// Read-only, bounded recursive resolver for system INF Include/Needs dependencies.
/// It verifies presence and section reachability, not signatures or semantic install effects.
/// </summary>
public sealed class WindowsSystemInfDependencyResolver : IDriverPackageSystemInfDependencyResolver
{
    public const int DefaultMaxFiles = 64;
    public const int DefaultMaxDepth = 32;
    public const int DefaultMaxSections = 256;
    public const int DefaultMaxParsedSections = 16_384;
    public const long DefaultMaxSingleFileBytes = 8L * 1024 * 1024;
    public const long DefaultMaxTotalBytes = 32L * 1024 * 1024;

    private readonly string? _testInfRoot;
    private readonly int _maxFiles;
    private readonly int _maxDepth;
    private readonly int _maxSections;
    private readonly int _maxParsedSections;
    private readonly long _maxSingleFileBytes;
    private readonly long _maxTotalBytes;

    public WindowsSystemInfDependencyResolver()
        : this(null, DefaultMaxFiles, DefaultMaxDepth, DefaultMaxSections, DefaultMaxParsedSections, DefaultMaxSingleFileBytes, DefaultMaxTotalBytes, isTestRoot: false)
    {
    }

    /// <summary>Allows deterministic tests against a disposable INF directory.</summary>
    public WindowsSystemInfDependencyResolver(
        string testInfRoot,
        int maxFiles = DefaultMaxFiles,
        int maxDepth = DefaultMaxDepth,
        int maxSections = DefaultMaxSections,
        int maxParsedSections = DefaultMaxParsedSections,
        long maxSingleFileBytes = DefaultMaxSingleFileBytes,
        long maxTotalBytes = DefaultMaxTotalBytes)
        : this(testInfRoot ?? throw new ArgumentNullException(nameof(testInfRoot)), maxFiles, maxDepth, maxSections, maxParsedSections, maxSingleFileBytes, maxTotalBytes, isTestRoot: true)
    {
    }

    private WindowsSystemInfDependencyResolver(
        string? testInfRoot,
        int maxFiles,
        int maxDepth,
        int maxSections,
        int maxParsedSections,
        long maxSingleFileBytes,
        long maxTotalBytes,
        bool isTestRoot)
    {
        if (maxFiles <= 0 || maxDepth <= 0 || maxSections <= 0 || maxParsedSections <= 0 || maxSingleFileBytes <= 0 || maxTotalBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxFiles), "Limites de resolução INF devem ser positivos.");
        _testInfRoot = isTestRoot && testInfRoot is not null ? Path.GetFullPath(testInfRoot) : null;
        _maxFiles = maxFiles;
        _maxDepth = maxDepth;
        _maxSections = maxSections;
        _maxParsedSections = maxParsedSections;
        _maxSingleFileBytes = maxSingleFileBytes;
        _maxTotalBytes = maxTotalBytes;
    }

    public DriverPackageSystemDependencyResult Verify(
        IReadOnlyList<DriverPackageInfDependencyRequirement> requirements)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        if (_testInfRoot is null && !OperatingSystem.IsWindows())
            return new(DriverPackageDependencyStatus.Unavailable, "A resolução dos INF do sistema exige Windows.");
        if (requirements.Count > _maxSections)
            return new(DriverPackageDependencyStatus.LimitExceeded, $"A análise excedeu o limite de {_maxSections} grupos Include/Needs.");

        try
        {
            var infRoot = Path.GetFullPath(_testInfRoot ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows), "INF"));
            var context = new ResolutionContext(infRoot, _maxFiles, _maxDepth, _maxSections, _maxParsedSections, _maxSingleFileBytes, _maxTotalBytes);
            foreach (var requirement in requirements)
            {
                if (requirement.RequiredSections.Count > 0 && requirement.IncludedInfNames.Count == 0)
                    return new(DriverPackageDependencyStatus.Unavailable, "Uma diretiva Needs aplicável não tem Include que identifique o INF proprietário.");
                var result = context.ResolveRequirement(requirement);
                if (result is not null)
                    return new(result.Status, result.Message);
            }

            return new(DriverPackageDependencyStatus.Available,
                $"Include/Needs resolvidos recursivamente: {context.LoadedInfCount} INF(s), {context.ResolvedSectionCount} seção(ões), {context.TotalBytesRead} bytes lidos; limite de profundidade {_maxDepth}. Assinaturas dos INF do Windows não foram revalidadas.");
        }
        catch (ResolutionException exception)
        {
            return new(exception.Status, exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException or DecoderFallbackException or ArgumentException)
        {
            return new(DriverPackageDependencyStatus.Unavailable,
                $"A leitura das dependências INF não pôde ser concluída ({exception.GetType().Name}); elegibilidade bloqueada.");
        }
    }

    private sealed class ResolutionContext(
        string infRoot,
        int maxFiles,
        int maxDepth,
        int maxSections,
        int maxParsedSections,
        long maxSingleFileBytes,
        long maxTotalBytes)
    {
        private readonly Dictionary<string, ParsedInf> _infs = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _activeSections = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _resolvedSections = new(StringComparer.OrdinalIgnoreCase);
        private int _visitedSectionCount;
        private int _parsedSectionCount;

        public int LoadedInfCount => _infs.Count;
        public int ResolvedSectionCount => _resolvedSections.Count;
        public long TotalBytesRead { get; private set; }

        public ResolutionException? ResolveRequirement(DriverPackageInfDependencyRequirement requirement)
        {
            var included = new List<string>();
            foreach (var include in requirement.IncludedInfNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var parsed = LoadInf(include);
                if (parsed.Error is not null) return parsed.Error;
                included.Add(include);
            }

            foreach (var section in requirement.RequiredSections.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var issue = ResolveSection(included, section, depth: 1);
                if (issue is not null) return issue;
            }
            return null;
        }

        private ResolutionException? ResolveSection(IReadOnlyList<string> includeNames, string section, int depth)
        {
            if (depth > maxDepth)
                return new(DriverPackageDependencyStatus.LimitExceeded, $"A dependência Needs [{section}] excedeu a profundidade máxima de {maxDepth}.");
            if (string.IsNullOrWhiteSpace(section) || section.Contains('%') || section.Length > 256)
                return new(DriverPackageDependencyStatus.Unavailable, "Uma seção Needs está vazia, parametrizada ou excede o tamanho seguro; elegibilidade bloqueada.");

            var owners = new List<(string Name, ParsedInf Inf)>();
            foreach (var name in includeNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var loaded = LoadInf(name);
                if (loaded.Error is not null) return loaded.Error;
                if (loaded.Inf!.Sections.ContainsKey(section)) owners.Add((name, loaded.Inf));
            }
            if (owners.Count == 0)
                return new(DriverPackageDependencyStatus.Missing, $"A seção Needs [{section}] não foi encontrada nos INF incluídos: {string.Join(", ", includeNames)}.");
            if (owners.Count > 1)
                return new(DriverPackageDependencyStatus.Ambiguous, $"A seção Needs [{section}] aparece em mais de um INF incluído: {string.Join(", ", owners.Select(owner => owner.Name))}.");

            var owner = owners[0];
            var node = owner.Name + "|" + section;
            if (_activeSections.Contains(node))
                return new(DriverPackageDependencyStatus.CycleDetected, $"Ciclo Include/Needs detectado em [{owner.Name}] seção [{section}]; elegibilidade bloqueada.");
            if (_resolvedSections.Contains(node)) return null;
            if (++_visitedSectionCount > maxSections)
                return new(DriverPackageDependencyStatus.LimitExceeded, $"A resolução excedeu o limite de {maxSections} seções Include/Needs.");

            _activeSections.Add(node);
            try
            {
                var directives = ParseDirectives(owner.Inf.Sections[section]);
                if (directives.Error is not null) return new(DriverPackageDependencyStatus.Unavailable, directives.Error);
                var childNames = new List<string>();
                foreach (var child in directives.Includes)
                {
                    var loaded = LoadInf(child);
                    if (loaded.Error is not null) return loaded.Error;
                    childNames.Add(child);
                }
                if (directives.Needs.Count > 0 && childNames.Count == 0)
                    return new(DriverPackageDependencyStatus.Unavailable,
                        $"A seção [{section}] em {owner.Name} declara Needs sem Include; não é possível determinar o INF requerido.");
                foreach (var childSection in directives.Needs)
                {
                    var issue = ResolveSection(childNames, childSection, depth + 1);
                    if (issue is not null) return issue;
                }
                _resolvedSections.Add(node);
                return null;
            }
            finally
            {
                _activeSections.Remove(node);
            }
        }

        private (ParsedInf? Inf, ResolutionException? Error) LoadInf(string name)
        {
            if (string.IsNullOrWhiteSpace(name) ||
                !string.Equals(Path.GetFileName(name), name, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetExtension(name), ".inf", StringComparison.OrdinalIgnoreCase))
                return (null, new(DriverPackageDependencyStatus.Unavailable, $"Nome Include inseguro ou não resolvido: {Path.GetFileName(name)}."));
            if (_infs.TryGetValue(name, out var cached)) return (cached, null);
            if (_infs.Count >= maxFiles)
                return (null, new(DriverPackageDependencyStatus.LimitExceeded, $"A resolução excedeu o limite de {maxFiles} INF(s)."));

            var path = Path.GetFullPath(Path.Combine(infRoot, name));
            var prefix = infRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return (null, new(DriverPackageDependencyStatus.Unavailable, "Um Include tentou sair da pasta INF autorizada."));
            if (!File.Exists(path))
                return (null, new(DriverPackageDependencyStatus.Missing, $"INF requerido {name} não existe em {infRoot}."));
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                return (null, new(DriverPackageDependencyStatus.Unavailable, $"O INF {name} redireciona por reparse point; a origem não foi seguida."));
            var length = new FileInfo(path).Length;
            if (length > maxSingleFileBytes || TotalBytesRead + length > maxTotalBytes)
                return (null, new(DriverPackageDependencyStatus.LimitExceeded,
                    $"A leitura de INF excederia o limite de {maxSingleFileBytes} bytes por arquivo ou {maxTotalBytes} bytes no total."));

            var (source, bytesRead) = ReadInf(path, maxSingleFileBytes, maxTotalBytes - TotalBytesRead);
            TotalBytesRead += bytesRead;
            var sections = ParseSections(source, maxParsedSections - _parsedSectionCount);
            if (sections.Count + _parsedSectionCount > maxParsedSections)
                return (null, new(DriverPackageDependencyStatus.LimitExceeded, $"A análise excedeu o limite de {maxParsedSections} seções INF."));
            var inf = new ParsedInf(sections);
            _infs.Add(name, inf);
            _parsedSectionCount += sections.Count;
            return (inf, null);
        }
    }

    private sealed record ParsedInf(Dictionary<string, List<string>> Sections);
    private sealed record DirectiveSet(IReadOnlyList<string> Includes, IReadOnlyList<string> Needs, string? Error);

    private sealed class ResolutionException(DriverPackageDependencyStatus status, string message) : Exception(message)
    {
        public DriverPackageDependencyStatus Status { get; } = status;
    }

    private static (string Text, long BytesRead) ReadInf(string path, long maxSingleFileBytes, long remainingTotalBytes)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > maxSingleFileBytes || stream.Length > remainingTotalBytes || stream.Length > int.MaxValue)
            throw new ResolutionException(DriverPackageDependencyStatus.LimitExceeded, "O INF excedeu o limite de bytes durante a leitura.");
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return (Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2), bytes.Length);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return (Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2), bytes.Length);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return (new UTF8Encoding(false, true).GetString(bytes, 3, bytes.Length - 3), bytes.Length);
        return (Encoding.Latin1.GetString(bytes), bytes.Length);
    }

    private static Dictionary<string, List<string>> ParseSections(string source, int maximumSections)
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
                if (pending.Length > 0) throw new InvalidDataException("Continuação INF interrompida antes do próximo cabeçalho.");
                current = line[1..^1].Trim();
                if (current.Length == 0) { current = null; continue; }
                if (current.Length > 256) throw new InvalidDataException("Nome de seção INF excede o limite seguro.");
                if (sections.ContainsKey(current)) throw new InvalidDataException($"A seção [{current}] está duplicada e não pode ser resolvida com segurança.");
                if (sections.Count >= maximumSections) throw new ResolutionException(DriverPackageDependencyStatus.LimitExceeded, "O INF excedeu o limite configurado de seções.");
                sections.Add(current, []);
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
        if (pending.Length > 0) throw new InvalidDataException("Continuação INF incompleta no fim do arquivo.");
        return sections;
    }

    private static DirectiveSet ParseDirectives(IEnumerable<string> lines)
    {
        var includes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var needs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            var separator = line.IndexOf('=');
            if (separator < 0) continue;
            var key = line[..separator].Trim();
            if (key.Equals("CopyINF", StringComparison.OrdinalIgnoreCase))
                return new(Array.Empty<string>(), Array.Empty<string>(),
                    "Uma seção Needs alcançável contém CopyINF; pacotes adicionais do Driver Store não são analisados por este resolvedor.");
            if (!key.Equals("Include", StringComparison.OrdinalIgnoreCase) && !key.Equals("Needs", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var value in SplitCsv(line[(separator + 1)..]))
            {
                var item = value.Trim().Trim('"');
                if (item.Length == 0 || item.Contains('%') || item.Length > 256)
                    return new(Array.Empty<string>(), Array.Empty<string>(), $"Diretiva {key} vazia, parametrizada ou excessiva; elegibilidade bloqueada.");
                if (key.Equals("Include", StringComparison.OrdinalIgnoreCase)) includes.Add(item);
                else needs.Add(item);
            }
        }
        return new(includes.ToArray(), needs.ToArray(), null);
    }

    private static IReadOnlyList<string> SplitCsv(string value)
    {
        var result = new List<string>();
        var quoted = false;
        var start = 0;
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '"') quoted = !quoted;
            else if (value[index] == ',' && !quoted)
            {
                result.Add(value[start..index]);
                start = index + 1;
            }
        }
        result.Add(value[start..]);
        return result;
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
