using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Optimizer.Core.Inventory;

/// <summary>
/// Reads INF metadata and ZIP contents only. It never launches package content and only extracts ZIPs
/// into a uniquely-owned directory below the current user's temporary directory.
/// </summary>
public sealed class LocalDriverPackageAnalyzer
{
    public const long DefaultMaxArchiveBytes = 128L * 1024 * 1024;
    public const long DefaultMaxExpandedBytes = 512L * 1024 * 1024;
    public const long DefaultMaxSingleFileBytes = 128L * 1024 * 1024;
    public const int DefaultMaxEntries = 4096;

    private readonly IDriverPackageSignatureVerifier _signatureVerifier;
    private readonly IDriverPackageSystemInfDependencyResolver _systemInfDependencyResolver;
    private readonly long _maxArchiveBytes;
    private readonly long _maxExpandedBytes;
    private readonly long _maxSingleFileBytes;
    private readonly int _maxEntries;

    public LocalDriverPackageAnalyzer(
        IDriverPackageSignatureVerifier signatureVerifier,
        IDriverPackageSystemInfDependencyResolver? systemInfDependencyResolver = null,
        long maxArchiveBytes = DefaultMaxArchiveBytes,
        long maxExpandedBytes = DefaultMaxExpandedBytes,
        long maxSingleFileBytes = DefaultMaxSingleFileBytes,
        int maxEntries = DefaultMaxEntries)
    {
        _signatureVerifier = signatureVerifier ?? throw new ArgumentNullException(nameof(signatureVerifier));
        _systemInfDependencyResolver = systemInfDependencyResolver ?? new UnavailableDriverPackageSystemInfDependencyResolver();
        if (maxArchiveBytes <= 0 || maxExpandedBytes <= 0 || maxSingleFileBytes <= 0 || maxEntries <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxArchiveBytes), "Package analysis limits must be positive.");
        _maxArchiveBytes = maxArchiveBytes;
        _maxExpandedBytes = maxExpandedBytes;
        _maxSingleFileBytes = maxSingleFileBytes;
        _maxEntries = maxEntries;
    }

    public Task<LocalDriverPackageAnalysisResult> AnalyzeAsync(
        string selectedPath,
        IReadOnlyList<DriverInfo> localDevices,
        DriverPackageTargetPlatform platform,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Analyze(selectedPath, localDevices, platform, cancellationToken), cancellationToken);

    public LocalDriverPackageAnalysisResult Analyze(
        string selectedPath,
        IReadOnlyList<DriverInfo> localDevices,
        DriverPackageTargetPlatform platform,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedPath);
        ArgumentNullException.ThrowIfNull(localDevices);
        ArgumentNullException.ThrowIfNull(platform);
        cancellationToken.ThrowIfCancellationRequested();

        var path = Path.GetFullPath(selectedPath);
        var extension = Path.GetExtension(path);
        if (string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase))
            return AnalyzeZip(path, localDevices, platform, cancellationToken);
        if (string.Equals(extension, ".inf", StringComparison.OrdinalIgnoreCase))
            return AnalyzeInf(path, localDevices, platform, cancellationToken);
        throw new InvalidDataException("Selecione um arquivo .inf ou .zip. Instaladores executáveis não são aceitos.");
    }

    private LocalDriverPackageAnalysisResult AnalyzeZip(
        string archivePath,
        IReadOnlyList<DriverInfo> localDevices,
        DriverPackageTargetPlatform platform,
        CancellationToken cancellationToken)
    {
        var archiveInfo = new FileInfo(archivePath);
        if (!archiveInfo.Exists) throw new FileNotFoundException("O pacote selecionado não existe.", archivePath);
        if (archiveInfo.Length > _maxArchiveBytes)
            throw new InvalidDataException($"O ZIP excede o limite de {_maxArchiveBytes / 1024 / 1024} MiB.");

        var ownerRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "CalixtoOptimizer", "DriverAnalysis"));
        Directory.CreateDirectory(ownerRoot);
        var extractionRoot = Path.Combine(ownerRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(extractionRoot);
        try
        {
            var archiveDigest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archivePath)));
            if (archiveInfo.Length > int.MaxValue)
                throw new InvalidDataException("O ZIP é grande demais para a análise limitada desta compilação.");

            long declaredExpandedBytes = 0;
            long actualExpandedBytes = 0;
            var entryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var extractedFiles = new List<string>();
            var entryCount = 0;

            using (var archive = ZipFile.OpenRead(archivePath))
            {
                if (archive.Entries.Count > _maxEntries)
                    throw new InvalidDataException($"O ZIP excede o limite de {_maxEntries} entradas.");

                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    entryCount++;
                    var relative = NormalizeAndValidateEntryPath(entry.FullName);
                    if (!entryPaths.Add(relative))
                        throw new InvalidDataException($"O ZIP contém um caminho duplicado: {Path.GetFileName(relative)}.");
                    if (IsUnixSymbolicLink(entry))
                        throw new InvalidDataException("O ZIP contém um link simbólico; a análise foi recusada.");
                    if (entry.Length < 0 || entry.Length > _maxSingleFileBytes)
                        throw new InvalidDataException("Uma entrada do ZIP excede o limite individual de tamanho.");
                    declaredExpandedBytes = checked(declaredExpandedBytes + entry.Length);
                    if (declaredExpandedBytes > _maxExpandedBytes)
                        throw new InvalidDataException($"O conteúdo descompactado excede o limite de {_maxExpandedBytes / 1024 / 1024} MiB.");

                    var outputPath = GetContainedPath(extractionRoot, relative);
                    if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                    {
                        Directory.CreateDirectory(outputPath);
                        continue;
                    }

                    var parent = Path.GetDirectoryName(outputPath)!;
                    Directory.CreateDirectory(parent);
                    RejectReparsePath(extractionRoot, parent);
                    using var input = entry.Open();
                    using var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.SequentialScan);
                    var crc = new Crc32();
                    var buffer = new byte[64 * 1024];
                    long entryBytes = 0;
                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        entryBytes = checked(entryBytes + read);
                        actualExpandedBytes = checked(actualExpandedBytes + read);
                        if (entryBytes > _maxSingleFileBytes || actualExpandedBytes > _maxExpandedBytes || entryBytes > entry.Length)
                            throw new InvalidDataException("O ZIP expandiu além dos limites declarados.");
                        crc.Append(buffer.AsSpan(0, read));
                        output.Write(buffer, 0, read);
                    }
                    if (entryBytes != entry.Length || crc.Value != entry.Crc32)
                        throw new InvalidDataException($"CRC32 ou tamanho da entrada não confere: {Path.GetFileName(relative)}.");
                    extractedFiles.Add(outputPath);
                }
            }

            var infPaths = extractedFiles
                .Where(file => string.Equals(Path.GetExtension(file), ".inf", StringComparison.OrdinalIgnoreCase))
                .Where(IsPnpDriverInf)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (infPaths.Length == 0)
                throw new InvalidDataException("O ZIP não contém INF de driver PnP reconhecível ([Version] e [Manufacturer]). Instaladores fechados/opaque não são executados nem analisados como compatíveis.");

            var summary = $"{entryCount} entradas ZIP verificadas por tamanho e CRC32 ({actualExpandedBytes:N0} bytes descompactados). Todos os arquivos permaneceram abaixo da pasta temporária isolada.";
            var candidates = infPaths.Select(inf => AnalyzeInfCore(
                inf, extractionRoot, extractedFiles, localDevices, platform, cancellationToken)
                with { SourceSha256 = archiveDigest, SourceFileName = Path.GetFileName(archivePath), PackageRootPath = extractionRoot }).ToArray();
            return new LocalDriverPackageAnalysisResult(
                Path.GetFileName(archivePath), archiveDigest, actualExpandedBytes, entryCount,
                Array.AsReadOnly(candidates), summary, extractionRoot);
        }
        catch
        {
            TryDeleteOwnedExtraction(extractionRoot);
            throw;
        }
    }

    private LocalDriverPackageAnalysisResult AnalyzeInf(
        string infPath,
        IReadOnlyList<DriverInfo> localDevices,
        DriverPackageTargetPlatform platform,
        CancellationToken cancellationToken)
    {
        var root = Path.GetDirectoryName(infPath)!;
        var files = EnumerateDirectPackageFiles(root, cancellationToken);
        if (!files.Contains(infPath, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("O INF selecionado está fora da pasta analisada.");
        var fileInfo = new FileInfo(infPath);
        if (fileInfo.Length > _maxSingleFileBytes)
            throw new InvalidDataException("O INF excede o limite individual de tamanho.");

        var digest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(infPath)));
        var candidate = AnalyzeInfCore(infPath, root, files, localDevices, platform, cancellationToken)
            with { SourceSha256 = digest, SourceFileName = Path.GetFileName(infPath), PackageRootPath = root };
        return new LocalDriverPackageAnalysisResult(
            Path.GetFileName(infPath), digest, files.Sum(file => new FileInfo(file).Length), files.Count,
            Array.AsReadOnly([candidate]),
            $"INF local lido sem executar conteúdo; {files.Count} arquivos na pasta do pacote e dependências listadas conferidas por caminho, tamanho e hash de leitura.",
            ownedExtractionDirectory: null);
    }

    private DriverPackageInfCandidate AnalyzeInfCore(
        string infPath,
        string packageRoot,
        IReadOnlyList<string> packageFiles,
        IReadOnlyList<DriverInfo> localDevices,
        DriverPackageTargetPlatform platform,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var reasons = new List<string>();
        InfDocument document;
        try
        {
            var info = new FileInfo(infPath);
            if (info.Length > 4L * 1024 * 1024)
                return Incomplete(Path.GetFileName(infPath), infPath, "O INF excede o limite de 4 MiB do parser conservador.");
            document = InfDocument.Parse(ReadInfText(infPath));
        }
        catch (Exception exception) when (exception is IOException or DecoderFallbackException or InvalidDataException)
        {
            return Incomplete(Path.GetFileName(infPath), infPath, $"O INF não pôde ser interpretado com segurança ({exception.GetType().Name}).");
        }

        if (document.HasParseWarnings)
            reasons.AddRange(document.ParseWarnings);

        if (!document.IsPnpDriverInf)
            return Incomplete(Path.GetFileName(infPath), infPath,
                "O arquivo não contém [Version] e [Manufacturer]; não foi reconhecido como INF de driver PnP e não pode ser preparado para instalação.");

        var architecture = NormalizeArchitecture(platform.Architecture);
        if (architecture is null)
            reasons.Add("A arquitetura atual não está identificada em um formato aceito.");

        var catalog = document.GetCatalogFile(architecture, out var catalogReason);
        if (catalogReason is not null) reasons.Add(catalogReason);
        string? catalogPath = null;
        if (!string.IsNullOrWhiteSpace(catalog))
        {
            try
            {
                catalogPath = GetContainedPath(packageRoot,
                    Path.GetRelativePath(packageRoot, Path.GetFullPath(Path.Combine(Path.GetDirectoryName(infPath)!, catalog))));
                if (!File.Exists(catalogPath))
                {
                    var basenameMatches = packageFiles.Where(file => string.Equals(Path.GetFileName(file), Path.GetFileName(catalog), StringComparison.OrdinalIgnoreCase)).ToArray();
                    if (basenameMatches.Length == 1) catalogPath = basenameMatches[0];
                    else reasons.Add(basenameMatches.Length > 1
                        ? "Mais de um catálogo com o mesmo nome existe no pacote."
                        : "O catálogo declarado pelo INF não foi encontrado dentro do pacote.");
                }
            }
            catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
            {
                reasons.Add("O caminho do catálogo declarado pelo INF não é seguro ou válido.");
            }
        }

        DriverPackageSignatureResult infSignature = new(DriverPackageVerificationStatus.Unavailable, null, "Catálogo/INF ainda não verificados.");
        if (catalogPath is not null && File.Exists(catalogPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            infSignature = _signatureVerifier.VerifyInf(infPath, catalogPath);
            if (infSignature.Status != DriverPackageVerificationStatus.Valid)
                reasons.Add($"A verificação da assinatura/membership do INF não confirmou validade: {infSignature.Detail}");
        }

        var modelAnalysis = document.FindDeviceMatches(localDevices, platform, architecture);
        reasons.AddRange(modelAnalysis.Reasons);
        var dependencyAnalysis = document.GetRequiredPackageFiles(
            architecture,
            modelAnalysis.Matches,
            _systemInfDependencyResolver);
        if (dependencyAnalysis.Reason is not null) reasons.Add(dependencyAnalysis.Reason);
        var requiredPaths = new List<string>();
        var invalidPayloadMembership = false;
        var unavailablePayloadMembership = false;
        foreach (var requiredFile in dependencyAnalysis.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (requiredFile.Contains('%'))
            {
                reasons.Add("Há um nome de arquivo parametrizado no INF que o analisador não expande.");
                continue;
            }

            var sourcePath = ResolvePackageFile(infPath, packageRoot, packageFiles, requiredFile);
            if (sourcePath is null)
            {
                reasons.Add($"Arquivo exigido listado pelo INF não encontrado ou com localização ambígua: {Path.GetFileName(requiredFile)}.");
                continue;
            }

            requiredPaths.Add(sourcePath);
            if (catalogPath is null || infSignature.Status != DriverPackageVerificationStatus.Valid) continue;
            var memberResult = _signatureVerifier.VerifyCatalogMember(sourcePath, catalogPath);
            if (memberResult.Status != DriverPackageVerificationStatus.Valid)
            {
                reasons.Add($"A assinatura do catálogo não confirmou o arquivo exigido {Path.GetFileName(sourcePath)}: {memberResult.Detail}");
                invalidPayloadMembership |= memberResult.Status == DriverPackageVerificationStatus.Invalid;
                unavailablePayloadMembership |= memberResult.Status == DriverPackageVerificationStatus.Unavailable;
            }
        }

        var payloadHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var filesToHash = requiredPaths.Append(infPath);
        if (catalogPath is not null) filesToHash = filesToHash.Append(catalogPath);
        foreach (var file in filesToHash.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                payloadHashes[Path.GetRelativePath(packageRoot, file)] = HashFile(file);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                reasons.Add($"Não foi possível calcular a integridade do arquivo necessário {Path.GetFileName(file)} ({exception.GetType().Name}).");
            }
        }

        var exactMatches = modelAnalysis.Matches.Where(match => match.Strength == DriverPackageMatchStrength.ExactHardwareId).ToArray();
        var compatibleMatches = modelAnalysis.Matches.Where(match => match.Strength == DriverPackageMatchStrength.CompatibleIdOnly).ToArray();
        var provider = document.GetValue("Version", "Provider");
        var driverVersion = document.GetValue("Version", "DriverVer");

        DriverPackageAnalysisOutcome outcome;
        if (infSignature.Status == DriverPackageVerificationStatus.Invalid || invalidPayloadMembership)
            outcome = DriverPackageAnalysisOutcome.Incompatible;
        else if (modelAnalysis.Unsupported || dependencyAnalysis.Unsupported || reasons.Count > 0 && HasAnalysisBlockingReason(reasons))
            outcome = DriverPackageAnalysisOutcome.Incomplete;
        else if (modelAnalysis.OsOrArchitectureMismatch)
            outcome = DriverPackageAnalysisOutcome.Incompatible;
        else if (exactMatches.Length == 0 && compatibleMatches.Length == 0)
            outcome = DriverPackageAnalysisOutcome.Incompatible;
        else if (exactMatches.Length > 1 || exactMatches.Length == 0 && compatibleMatches.Length > 0)
        {
            outcome = DriverPackageAnalysisOutcome.Ambiguous;
            if (compatibleMatches.Length > 0 && exactMatches.Length == 0)
                reasons.Add("A correspondência depende somente de Compatible ID genérico; não será preparada para instalação.");
            if (exactMatches.Length > 1)
                reasons.Add("O mesmo INF corresponde por Hardware ID a mais de um dispositivo local; selecione/revise o alvo antes de qualquer operação.");
        }
        else if (catalogPath is null || infSignature.Status == DriverPackageVerificationStatus.Unavailable ||
                 dependencyAnalysis.Files.Count == 0 || requiredPaths.Count != dependencyAnalysis.Files.Count)
            outcome = DriverPackageAnalysisOutcome.Incomplete;
        else if (infSignature.Status == DriverPackageVerificationStatus.Unavailable || unavailablePayloadMembership)
            outcome = DriverPackageAnalysisOutcome.Incomplete;
        else if (HasAnalysisBlockingReason(reasons))
            outcome = DriverPackageAnalysisOutcome.Incomplete;
        else
            outcome = DriverPackageAnalysisOutcome.CompatibleAccordingToChecks;

        if (string.IsNullOrWhiteSpace(catalog))
            reasons.Add("O INF não declara um catálogo verificável para a arquitetura atual.");
        if (dependencyAnalysis.Files.Count == 0 && modelAnalysis.Matches.Count > 0)
            reasons.Add("O INF não contém uma seção SourceDisksFiles aplicável; os arquivos de payload necessários não podem ser enumerados com segurança.");
        if (modelAnalysis.Matches.Count == 0 && !modelAnalysis.OsOrArchitectureMismatch && !modelAnalysis.Unsupported)
            reasons.Add("Nenhum Hardware ID ou Compatible ID conhecido deste INF coincide com o inventário Plug and Play local.");

        return new DriverPackageInfCandidate(
            Path.GetFileName(infPath), infPath, provider, driverVersion,
            catalog is null ? null : Path.GetFileName(catalog), infSignature.Signer,
            infSignature.Status, outcome, Array.AsReadOnly(modelAnalysis.Matches.ToArray()),
            Array.AsReadOnly(requiredPaths.Select(file => Path.GetFileName(file)!).ToArray()),
            Array.AsReadOnly(reasons.Distinct(StringComparer.Ordinal).ToArray()),
            InfSha256: payloadHashes.GetValueOrDefault(Path.GetRelativePath(packageRoot, infPath)),
            PayloadFilesSha256: new Dictionary<string, string>(payloadHashes, StringComparer.OrdinalIgnoreCase),
            DependencyResolutionSummary: dependencyAnalysis.Detail);
    }

    private static bool HasAnalysisBlockingReason(IEnumerable<string> reasons) => reasons.Any(reason =>
        reason.Contains("não está identificada", StringComparison.OrdinalIgnoreCase) ||
        reason.Contains("não pôde ser interpretado", StringComparison.OrdinalIgnoreCase) ||
        reason.Contains("não são interpretadas", StringComparison.OrdinalIgnoreCase) ||
        reason.Contains("não expande", StringComparison.OrdinalIgnoreCase) ||
        reason.Contains("não encontrado", StringComparison.OrdinalIgnoreCase) ||
        reason.Contains("ambígua", StringComparison.OrdinalIgnoreCase) ||
        reason.Contains("não confirmou validade", StringComparison.OrdinalIgnoreCase) ||
        reason.Contains("não confirmou o arquivo", StringComparison.OrdinalIgnoreCase) ||
        reason.Contains("excede o limite", StringComparison.OrdinalIgnoreCase) ||
        reason.Contains("não há seção", StringComparison.OrdinalIgnoreCase) ||
        reason.Contains("ProductType/SuiteMask", StringComparison.OrdinalIgnoreCase));

    private static DriverPackageInfCandidate Incomplete(string infName, string infPath, string reason) => new(
        infName, infPath, null, null, null, null, DriverPackageVerificationStatus.Unavailable,
        DriverPackageAnalysisOutcome.Incomplete, Array.Empty<DriverPackageDeviceMatch>(),
        Array.Empty<string>(), Array.AsReadOnly([reason]));

    private static bool IsPnpDriverInf(string infPath)
    {
        try
        {
            var info = new FileInfo(infPath);
            if (info.Length > 4L * 1024 * 1024) return true; // Keep it visible as incomplete in the analyzer.
            return InfDocument.Parse(ReadInfText(infPath)).IsPnpDriverInf;
        }
        catch (Exception exception) when (exception is IOException or DecoderFallbackException or InvalidDataException)
        {
            return true; // A malformed candidate remains visible as incomplete instead of being silently skipped.
        }
    }

    private static string? NormalizeArchitecture(string? architecture) => architecture?.Trim().ToLowerInvariant() switch
    {
        "x64" or "amd64" => "amd64",
        "x86" or "i386" => "x86",
        "arm64" or "aarch64" => "arm64",
        "arm" => "arm",
        _ => null
    };

    private static string ReadInfText(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return new UTF8Encoding(false, true).GetString(bytes, 3, bytes.Length - 3);
        try { return new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException) { return Encoding.Latin1.GetString(bytes); }
    }

    private static string HashFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string NormalizeAndValidateEntryPath(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName) || entryName.Contains('\0'))
            throw new InvalidDataException("O ZIP contém um nome de entrada vazio ou inválido.");
        var normalized = entryName.Replace('\\', '/');
        if (normalized.StartsWith('/') || Path.IsPathRooted(normalized) || normalized.Contains(':'))
            throw new InvalidDataException("O ZIP contém um caminho absoluto ou alternativo.");
        var parts = normalized.Split('/');
        if (parts.Any(part => part is ".." or "."))
            throw new InvalidDataException("O ZIP contém um caminho de travessia de diretório.");
        return string.Join(Path.DirectorySeparatorChar, parts);
    }

    private static string GetContainedPath(string root, string relative)
    {
        var canonicalRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var canonicalTarget = Path.GetFullPath(Path.Combine(root, relative));
        if (!canonicalTarget.StartsWith(canonicalRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A entrada do pacote escaparia da pasta de análise.");
        return canonicalTarget;
    }

    private static void RejectReparsePath(string extractionRoot, string directory)
    {
        var current = Path.GetFullPath(directory);
        var root = Path.GetFullPath(extractionRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!string.Equals(current, root, StringComparison.OrdinalIgnoreCase) &&
            !current.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("O caminho de extração saiu da pasta temporária.");
        while (current.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("A extração encontrou um ponto de nova análise; foi interrompida.");
            if (string.Equals(current, root, StringComparison.OrdinalIgnoreCase)) break;
            current = Path.GetDirectoryName(current)!;
        }
    }

    private static bool IsUnixSymbolicLink(ZipArchiveEntry entry) =>
        ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000;

    private string? ResolvePackageFile(string infPath, string packageRoot, IReadOnlyList<string> packageFiles, string requiredFile)
    {
        var fileName = requiredFile.Trim().Trim('"');
        if (Path.IsPathRooted(fileName) || fileName.Contains(':') || fileName.Split(['/', '\\']).Any(part => part == ".."))
            return null;
        var direct = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(infPath)!, fileName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)));
        var rootPrefix = Path.GetFullPath(packageRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!direct.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)) return null;
        if (File.Exists(direct)) return direct;
        var byName = packageFiles.Where(file => string.Equals(Path.GetFileName(file), Path.GetFileName(fileName), StringComparison.OrdinalIgnoreCase)).ToArray();
        return byName.Length == 1 ? byName[0] : null;
    }

    private IReadOnlyList<string> EnumerateDirectPackageFiles(string root, CancellationToken cancellationToken)
    {
        var files = new List<string>();
        var pending = new Stack<string>();
        pending.Push(root);
        long totalBytes = 0;
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(entry);
                    continue;
                }
                if (files.Count >= _maxEntries) throw new InvalidDataException("A pasta do INF excede o limite de arquivos analisáveis.");
                var file = new FileInfo(entry);
                if (file.Length > _maxSingleFileBytes) throw new InvalidDataException("Um arquivo na pasta do INF excede o limite individual.");
                totalBytes = checked(totalBytes + file.Length);
                if (totalBytes > _maxExpandedBytes) throw new InvalidDataException("A pasta do INF excede o limite total de tamanho.");
                files.Add(file.FullName);
            }
        }
        return Array.AsReadOnly(files.ToArray());
    }

    private static void TryDeleteOwnedExtraction(string extractionRoot)
    {
        var ownerRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "CalixtoOptimizer", "DriverAnalysis")) + Path.DirectorySeparatorChar;
        var canonical = Path.GetFullPath(extractionRoot);
        if (!canonical.StartsWith(ownerRoot, StringComparison.OrdinalIgnoreCase)) return;
        try { Directory.Delete(canonical, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private sealed class Crc32
    {
        private static readonly uint[] Table = CreateTable();
        private uint _value = 0xFFFFFFFF;
        public uint Value => ~_value;

        public void Append(ReadOnlySpan<byte> bytes)
        {
            foreach (var item in bytes)
                _value = Table[(_value ^ item) & 0xFF] ^ (_value >> 8);
        }

        private static uint[] CreateTable()
        {
            var table = new uint[256];
            for (uint index = 0; index < table.Length; index++)
            {
                var value = index;
                for (var bit = 0; bit < 8; bit++)
                    value = (value & 1) != 0 ? 0xEDB88320 ^ (value >> 1) : value >> 1;
                table[index] = value;
            }
            return table;
        }
    }

    private sealed record InfModel(string Architecture, int? Build, int? ProductType, bool Applicable, string? InstallSection, IReadOnlyList<string> Ids);
    private sealed record ModelMatchAnalysis(
        IReadOnlyList<DriverPackageDeviceMatch> Matches,
        IReadOnlyList<string> Reasons,
        bool Unsupported,
        bool OsOrArchitectureMismatch);
    private sealed record PackageDependencyAnalysis(IReadOnlyList<string> Files, string? Reason, string Detail, bool Unsupported);

    private sealed class InfDocument
    {
        private readonly Dictionary<string, List<string>> _sections;
        private readonly List<string> _warnings;

        private InfDocument(Dictionary<string, List<string>> sections, List<string> warnings)
        {
            _sections = sections;
            _warnings = warnings;
        }

        public bool HasParseWarnings => _warnings.Count > 0;
        public IReadOnlyList<string> ParseWarnings => _warnings;
        public bool IsPnpDriverInf => _sections.ContainsKey("Version") && _sections.ContainsKey("Manufacturer");

        public static InfDocument Parse(string source)
        {
            var sections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var warnings = new List<string>();
            string? current = null;
            var pending = new StringBuilder();
            foreach (var rawLine in source.Split(["\r\n", "\n"], StringSplitOptions.None))
            {
                var line = StripComment(rawLine).Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    if (pending.Length > 0)
                    {
                        warnings.Add("O INF contém uma linha continuada que o analisador não conseguiu associar.");
                        pending.Clear();
                    }
                    current = line[1..^1].Trim();
                    if (current.Length == 0) { warnings.Add("O INF contém um nome de seção vazio."); current = null; continue; }
                    if (!sections.TryAdd(current, []))
                    {
                        warnings.Add($"O INF repete a seção {current}; sua interpretação ficou ambígua.");
                        current = null;
                    }
                    continue;
                }
                if (current is null)
                {
                    warnings.Add("Há diretivas fora de uma seção INF reconhecida.");
                    continue;
                }
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
            if (pending.Length > 0) warnings.Add("O INF termina com uma continuação incompleta.");
            return new InfDocument(sections, warnings);
        }

        public string? GetValue(string section, string key)
        {
            if (!_sections.TryGetValue(section, out var lines)) return null;
            var matches = lines.Select(ParseKeyValue).Where(pair => pair is not null &&
                string.Equals(pair.Value.Key, key, StringComparison.OrdinalIgnoreCase)).Select(pair => pair!.Value.Value).ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        public string? GetCatalogFile(string? architecture, out string? reason)
        {
            reason = null;
            if (!_sections.TryGetValue("Version", out var versionLines))
            {
                reason = "O INF não contém a seção [Version].";
                return null;
            }
            var catalogKeys = versionLines.Select(ParseKeyValue).Where(pair => pair is not null && pair.Value.Key.StartsWith("CatalogFile", StringComparison.OrdinalIgnoreCase)).Select(pair => pair!.Value).ToArray();
            if (catalogKeys.Length == 0)
            {
                reason = "O INF não declara CatalogFile; assinatura de pacote não verificável por este fluxo.";
                return null;
            }

            var preferredKey = architecture switch { "amd64" => "CatalogFile.ntamd64", "x86" => "CatalogFile.ntx86", "arm64" => "CatalogFile.ntarm64", "arm" => "CatalogFile.ntarm", _ => null };
            var preferred = preferredKey is null ? [] : catalogKeys.Where(pair => string.Equals(pair.Key, preferredKey, StringComparison.OrdinalIgnoreCase)).ToArray();
            var baseline = catalogKeys.Where(pair => string.Equals(pair.Key, "CatalogFile", StringComparison.OrdinalIgnoreCase)).ToArray();
            var selected = preferred.Length > 0 ? preferred : baseline;
            if (selected.Length != 1)
            {
                reason = "CatalogFile não tem uma única variante correspondente à arquitetura atual.";
                return null;
            }
            var value = Unquote(selected[0].Value.Trim());
            if (string.IsNullOrWhiteSpace(value) || value.Contains('%'))
            {
                reason = "CatalogFile contém um nome vazio ou não resolvido.";
                return null;
            }
            return value;
        }

        public PackageDependencyAnalysis GetRequiredPackageFiles(
            string? architecture,
            IReadOnlyList<DriverPackageDeviceMatch> matches,
            IDriverPackageSystemInfDependencyResolver systemInfResolver)
        {
            if (matches.Count == 0)
                return new(Array.Empty<string>(), null, "Nenhum dispositivo corresponde; dependências de instalação não foram avaliadas.", false);
            if (architecture is null)
                return new(Array.Empty<string>(), "A arquitetura não foi identificada; não é possível selecionar SourceDisksFiles.", "Dependências incompletas.", true);

            var errors = new List<string>();
            var unsupported = false;
            var copyFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dependencyRequirements = new List<DriverPackageInfDependencyRequirement>();
            var installSections = matches.SelectMany(match => match.ApplicableInstallSections ?? Array.Empty<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (matches.Any(match => match.ApplicableInstallSections is not { Count: 1 }))
            {
                errors.Add("A seção DDInstall aplicável ao Hardware ID não foi resolvida de forma única.");
                unsupported = true;
            }

            var fileListSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queuedSections = new Queue<string>();
            var visitedInstallSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var installSection in installSections)
            {
                var applicableSections = new[]
                    { "", ".Services", ".HW", ".CoInstallers", ".Interfaces", ".Software", ".Wdf", ".Filters", ".Components", ".Events" }
                    .Select(suffix => installSection + suffix)
                    .Where(_sections.ContainsKey)
                    .ToArray();
                foreach (var sectionName in applicableSections) queuedSections.Enqueue(sectionName);
            }

            while (queuedSections.Count > 0)
            {
                var sectionName = queuedSections.Dequeue();
                if (!visitedInstallSections.Add(sectionName)) continue;
                if (visitedInstallSections.Count > 128)
                {
                    errors.Add("As seções de instalação alcançáveis excedem o limite de análise de 128; a elegibilidade foi bloqueada.");
                    unsupported = true;
                    break;
                }
                var sectionIncludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var sectionNeeds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in _sections[sectionName])
                {
                    var pair = ParseKeyValue(line);
                    if (pair is null) continue;
                    var key = pair.Value.Key.Trim();
                    var values = SplitCsv(pair.Value.Value).Select(Unquote).Select(value => value.Trim()).ToArray();
                    if (key.Equals("CopyFiles", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var value in values.Where(value => value.Length > 0))
                        {
                            if (value.StartsWith('@'))
                                copyFileNames.Add(value[1..]);
                            else if (_sections.TryGetValue(value, out var fileLines))
                            {
                                fileListSections.Add(value);
                                foreach (var fileLine in fileLines)
                                {
                                    var fields = SplitCsv(fileLine).Select(Unquote).Select(item => item.Trim()).ToArray();
                                    var sourceName = fields.Length > 1 && fields[1].Length > 0 ? fields[1] : fields[0];
                                    if (sourceName.Length == 0 || sourceName.Contains('%'))
                                    {
                                        errors.Add($"A lista de CopyFiles [{value}] contém nome de arquivo parametrizado ou vazio.");
                                        unsupported = true;
                                        continue;
                                    }
                                    copyFileNames.Add(sourceName);
                                }
                            }
                            else
                            {
                                errors.Add($"A diretiva CopyFiles referencia uma seção ausente ou não resolvida: {value}.");
                                unsupported = true;
                            }
                        }
                    }
                    else if (key.Equals("ServiceBinary", StringComparison.OrdinalIgnoreCase))
                    {
                        var binary = values.FirstOrDefault();
                        if (string.IsNullOrWhiteSpace(binary))
                        {
                            errors.Add($"ServiceBinary em [{sectionName}] está vazio ou não pôde ser resolvido.");
                            unsupported = true;
                        }
                        else if (binary.Contains('%') && !binary.StartsWith("%12%\\", StringComparison.OrdinalIgnoreCase) &&
                                 !binary.StartsWith("%13%\\", StringComparison.OrdinalIgnoreCase))
                        {
                            errors.Add($"ServiceBinary em [{sectionName}] usa uma variável de destino não suportada.");
                            unsupported = true;
                        }
                        else
                        {
                            var binaryName = Path.GetFileName(binary.Replace("%12%\\", "", StringComparison.OrdinalIgnoreCase)
                                .Replace("%13%\\", "", StringComparison.OrdinalIgnoreCase));
                            if (binaryName.Length == 0 || binaryName.Contains('%'))
                            {
                                errors.Add($"ServiceBinary em [{sectionName}] não identifica um arquivo simples do pacote.");
                                unsupported = true;
                            }
                            else copyFileNames.Add(binaryName);
                        }
                    }
                    else if (key.Equals("Include", StringComparison.OrdinalIgnoreCase))
                    {
                        if (values.Length == 0 || values.Any(value => value.Length == 0))
                        {
                            errors.Add($"Include em [{sectionName}] está vazio ou contém uma entrada vazia; a dependência não pôde ser verificada.");
                            unsupported = true;
                        }
                        foreach (var value in values.Where(value => value.Length > 0)) sectionIncludes.Add(value);
                    }
                    else if (key.Equals("Needs", StringComparison.OrdinalIgnoreCase))
                    {
                        if (values.Length == 0 || values.Any(value => value.Length == 0))
                        {
                            errors.Add($"Needs em [{sectionName}] está vazio ou contém uma entrada vazia; a dependência não pôde ser verificada.");
                            unsupported = true;
                        }
                        foreach (var value in values.Where(value => value.Length > 0)) sectionNeeds.Add(value);
                    }
                    else if (key.Equals("CopyINF", StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add($"CopyINF em [{sectionName}] pode stagear pacotes adicionais e não é validado nesta análise.");
                        unsupported = true;
                    }
                    else if (key.Equals("AddService", StringComparison.OrdinalIgnoreCase))
                    {
                        EnqueueReferencedSection(values, 2, "AddService", sectionName, queuedSections, errors, ref unsupported);
                        EnqueueReferencedSection(values, 3, "AddService EventLog", sectionName, queuedSections, errors, ref unsupported, optional: true);
                    }
                    else if (key.Equals("AddInterface", StringComparison.OrdinalIgnoreCase) ||
                             key.Equals("AddComponent", StringComparison.OrdinalIgnoreCase) ||
                             key.Equals("AddFilter", StringComparison.OrdinalIgnoreCase))
                    {
                        EnqueueReferencedSection(values, 2, key, sectionName, queuedSections, errors, ref unsupported);
                    }
                    else if (key.Equals("AddSoftware", StringComparison.OrdinalIgnoreCase))
                    {
                        // AddSoftware queues a separate software installation after device setup;
                        // its execution/effects are outside this device-driver package analysis.
                        errors.Add($"AddSoftware em [{sectionName}] agenda uma instalação separada que não é validada por este analisador.");
                        unsupported = true;
                        EnqueueReferencedSection(values, 2, key, sectionName, queuedSections, errors, ref unsupported);
                    }
                    else if (key.Equals("AddReg", StringComparison.OrdinalIgnoreCase) &&
                             sectionName.EndsWith(".CoInstallers", StringComparison.OrdinalIgnoreCase))
                    {
                        // Co-installer registration can name DLLs that SetupAPI will load later.
                        errors.Add($"AddReg em seção CoInstallers [{sectionName}] pode registrar co-installers cujo carregamento e efeitos não são verificados.");
                        unsupported = true;
                    }
                }
                if (sectionNeeds.Count > 0 && sectionIncludes.Count == 0)
                {
                    errors.Add($"Há Needs sem Include correspondente na seção aplicável [{sectionName}]; a dependência não pôde ser resolvida.");
                    unsupported = true;
                }
                if (sectionIncludes.Count > 0)
                    dependencyRequirements.Add(new(
                        Array.AsReadOnly(sectionIncludes.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray()),
                        Array.AsReadOnly(sectionNeeds.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray())));
            }

            DriverPackageSystemDependencyResult? systemResolution = null;
            if (dependencyRequirements.Count > 0)
            {
                systemResolution = systemInfResolver.Verify(Array.AsReadOnly(dependencyRequirements.ToArray()));
                if (systemResolution.Status != DriverPackageDependencyStatus.Available)
                {
                    errors.Add($"Dependência de INF do Windows não confirmada ({systemResolution.Status}): {systemResolution.Detail}");
                    unsupported = true;
                }
            }

            var sourceDescription = dependencyRequirements.Count == 0
                ? "Nenhuma dependência Include/Needs do Windows foi declarada nas seções selecionadas."
                : $"Include/Needs selecionados: {string.Join("; ", dependencyRequirements.Select(requirement =>
                    $"Include={string.Join(",", requirement.IncludedInfNames)} Needs={string.Join(",", requirement.RequiredSections)}"))}. " +
                  (systemResolution?.Detail ?? "Dependência não verificada.");

            if (copyFileNames.Count == 0)
                return new(Array.Empty<string>(), errors.Count == 0 ? "As seções aplicáveis não declaram CopyFiles; o conjunto de arquivos do pacote não pôde ser validado por este fluxo." : string.Join(" ", errors), sourceDescription, true);

            var architectureSection = "SourceDisksFiles." + architecture;
            var sourceSectionName = _sections.ContainsKey(architectureSection) ? architectureSection : "SourceDisksFiles";
            if (!_sections.TryGetValue(sourceSectionName, out var sourceLines))
            {
                errors.Add($"A seção [{sourceSectionName}] necessária para CopyFiles não existe.");
                return new(Array.Empty<string>(), string.Join(" ", errors), sourceDescription, true);
            }

            var sourceFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in sourceLines)
            {
                var pair = ParseKeyValue(line);
                if (pair is null) { errors.Add($"Uma linha de [{sourceSectionName}] não pôde ser interpretada."); unsupported = true; continue; }
                sourceFiles.Add(Unquote(pair.Value.Key.Trim()));
            }
            foreach (var file in copyFileNames)
            {
                if (Path.IsPathRooted(file) || file.Contains(':') || file.Split(['/', '\\']).Any(part => part is ".." or ".") || file.Contains('%'))
                {
                    errors.Add("Uma diretiva CopyFiles contém um caminho fora do pacote ou parametrizado.");
                    unsupported = true;
                    continue;
                }
                if (!sourceFiles.Contains(Path.GetFileName(file)))
                {
                    errors.Add($"O arquivo {Path.GetFileName(file)} é copiado por DDInstall, mas não tem entrada em [{sourceSectionName}].");
                    unsupported = true;
                    continue;
                }
            }
            var detail = $"DDInstall aplicável: {string.Join(", ", installSections)}. Listas CopyFiles selecionadas: {string.Join(", ", fileListSections)}. Payload alcançável: {string.Join(", ", copyFileNames)}. {sourceDescription}";
            return new(Array.AsReadOnly(copyFileNames.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray()),
                errors.Count == 0 ? null : string.Join(" ", errors), detail, unsupported);
        }

        private void EnqueueReferencedSection(
            IReadOnlyList<string> values,
            int argumentIndex,
            string directive,
            string sourceSection,
            Queue<string> queue,
            List<string> errors,
            ref bool unsupported,
            bool optional = false)
        {
            if (argumentIndex >= values.Count || values[argumentIndex].Length == 0)
            {
                if (optional) return;
                errors.Add($"A diretiva {directive} em [{sourceSection}] não identifica sua seção auxiliar.");
                unsupported = true;
                return;
            }
            var referenced = values[argumentIndex];
            if (referenced.Contains('%') || !_sections.ContainsKey(referenced))
            {
                errors.Add($"A diretiva {directive} em [{sourceSection}] referencia seção parametrizada ou ausente [{referenced}].");
                unsupported = true;
                return;
            }
            queue.Enqueue(referenced);
        }

        public ModelMatchAnalysis FindDeviceMatches(
            IReadOnlyList<DriverInfo> devices,
            DriverPackageTargetPlatform platform,
            string? architecture)
        {
            var reasons = new List<string>();
            if (architecture is null)
                return new(Array.Empty<DriverPackageDeviceMatch>(), ["A arquitetura do dispositivo não pode ser comparada."], true, false);
            if (!_sections.TryGetValue("Manufacturer", out var manufacturerLines))
                return new(Array.Empty<DriverPackageDeviceMatch>(), ["O INF não tem [Manufacturer]; os IDs de dispositivo não podem ser enumerados."], true, false);

            var models = new List<InfModel>();
            var unsupported = false;
            var sawMatchingArchitecture = false;
            var sawApplicableOs = false;
            foreach (var line in manufacturerLines)
            {
                var pair = ParseKeyValue(line);
                if (pair is null)
                {
                    reasons.Add("Uma linha de [Manufacturer] não pôde ser interpretada.");
                    unsupported = true;
                    continue;
                }
                var fields = SplitCsv(pair.Value.Value);
                if (fields.Count == 0) { unsupported = true; continue; }
                var baseSection = Unquote(fields[0].Trim());
                if (baseSection.StartsWith('%') || baseSection.Length == 0)
                {
                    reasons.Add("O nome de seção Models depende de uma string ou valor que o analisador não expande.");
                    unsupported = true;
                    continue;
                }
                var decorations = fields.Skip(1).Select(Unquote).ToArray();
                if (decorations.Length == 0)
                {
                    reasons.Add("[Manufacturer] não declara decoração de arquitetura; a compatibilidade não pode ser confirmada.");
                    unsupported = true;
                    continue;
                }
                foreach (var decoration in decorations)
                {
                    var decorated = ParseTargetDecoration(decoration, platform);
                    if (!decorated.Valid)
                    {
                        reasons.Add(decorated.Reason);
                        unsupported = true;
                        continue;
                    }
                    if (!string.Equals(decorated.Architecture, architecture, StringComparison.OrdinalIgnoreCase)) continue;
                    sawMatchingArchitecture = true;
                    if (!decorated.Applicable) continue;
                    sawApplicableOs = true;
                    var sectionName = baseSection + "." + decoration;
                    if (!_sections.TryGetValue(sectionName, out var modelLines))
                    {
                        reasons.Add($"A seção Models decorada [{sectionName}] referenciada não existe.");
                        unsupported = true;
                        continue;
                    }
                    foreach (var modelLine in modelLines)
                    {
                        var modelPair = ParseKeyValue(modelLine);
                        if (modelPair is null) { unsupported = true; continue; }
                        var modelFields = SplitCsv(modelPair.Value.Value);
                        if (modelFields.Count < 2) { unsupported = true; continue; }
                        var installBase = Unquote(modelFields[0].Trim());
                        if (installBase.Length == 0 || installBase.Contains('%')) { unsupported = true; continue; }
                        var selectedInstall = ResolveInstallSection(installBase, platform, architecture, out var installReason);
                        if (selectedInstall is null && installReason is not null) reasons.Add(installReason);
                        foreach (var rawId in modelFields.Skip(1).Where(value => value.Length > 0))
                        {
                            var id = Unquote(rawId.Trim());
                            if (id.Length == 0 || id.Contains('%')) { unsupported = true; continue; }
                            models.Add(new(architecture, decorated.Build, decorated.ProductType, decorated.Applicable, selectedInstall, [id]));
                        }
                    }
                }
            }

            var matches = new List<DriverPackageDeviceMatch>();
            foreach (var device in devices)
            {
                var hwIds = device.HardwareIds ?? Array.Empty<string>();
                var compatibleIds = device.CompatibleIds ?? Array.Empty<string>();
                var ids = models.SelectMany(model => model.Ids).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                var exactId = hwIds.FirstOrDefault(id => ids.Contains(id, StringComparer.OrdinalIgnoreCase));
                var compatibleId = compatibleIds.FirstOrDefault(id => ids.Contains(id, StringComparer.OrdinalIgnoreCase));
                var matchedId = exactId ?? compatibleId;
                if (matchedId is null) continue;
                var matchedModels = models.Where(model => model.Ids.Contains(matchedId, StringComparer.OrdinalIgnoreCase)).ToArray();
                var installSections = matchedModels.Where(model => model.InstallSection is not null)
                    .Select(model => model.InstallSection!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (matchedModels.Any(model => model.InstallSection is null))
                {
                    reasons.Add("A seção DDInstall referenciada pela linha Models aplicável não existe ou não pôde ser selecionada.");
                    unsupported = true;
                }
                if (installSections.Length > 1)
                {
                    reasons.Add("O Hardware/Compatible ID seleciona mais de uma seção DDInstall; a escolha ficou ambígua.");
                    unsupported = true;
                }
                matches.Add(new(device,
                    exactId is not null ? DriverPackageMatchStrength.ExactHardwareId : DriverPackageMatchStrength.CompatibleIdOnly,
                    matchedId,
                    Array.AsReadOnly(installSections)));
            }

            var osMismatch = sawMatchingArchitecture && !sawApplicableOs;
            if (!sawMatchingArchitecture) reasons.Add("Nenhuma decoração Models declara a arquitetura atual.");
            else if (osMismatch) reasons.Add("As decorações Models x86/x64/ARM não incluem a versão ou build atual do Windows.");
            return new(Array.AsReadOnly(matches.ToArray()), Array.AsReadOnly(reasons.Distinct().ToArray()), unsupported, osMismatch);
        }

        private string? ResolveInstallSection(string baseName, DriverPackageTargetPlatform platform, string architecture, out string? reason)
        {
            reason = null;
            var candidates = new List<(string Name, int Rank)>();
            if (_sections.ContainsKey(baseName)) candidates.Add((baseName, 0));
            foreach (var sectionName in _sections.Keys)
            {
                if (!sectionName.StartsWith(baseName + ".", StringComparison.OrdinalIgnoreCase)) continue;
                var suffix = sectionName[(baseName.Length + 1)..];
                if (suffix.Equals("NT", StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add((sectionName, 100));
                    continue;
                }
                var target = ParseTargetDecoration(suffix, platform);
                if (!target.Valid || !target.Applicable || !string.Equals(target.Architecture, architecture, StringComparison.OrdinalIgnoreCase)) continue;
                var rank = 200 + (suffix.Contains('.', StringComparison.Ordinal) ? 10 : 0) +
                    (target.Build.HasValue ? 100 : 0) + (suffix.StartsWith("NT" + architecture + ".", StringComparison.OrdinalIgnoreCase) ? 20 : 0);
                candidates.Add((sectionName, rank));
            }
            if (candidates.Count == 0)
            {
                reason = $"A seção DDInstall [{baseName}] e suas variantes aplicáveis para Windows/{architecture} não foram encontradas.";
                return null;
            }
            var highestRank = candidates.Max(candidate => candidate.Rank);
            var selected = candidates.Where(candidate => candidate.Rank == highestRank).ToArray();
            if (selected.Length != 1)
            {
                reason = $"Mais de uma variante DDInstall com a mesma especificidade se aplica a [{baseName}].";
                return null;
            }
            return selected[0].Name;
        }

        private static (bool Valid, string? Architecture, int? Build, int? ProductType, bool Applicable, string Reason) ParseTargetDecoration(
            string decoration,
            DriverPackageTargetPlatform platform)
        {
            if (!decoration.StartsWith("NT", StringComparison.OrdinalIgnoreCase))
                return (false, null, null, null, false, "Uma decoração de Models não começa com NT; análise incompleta.");
            var architecture = new[] { "amd64", "arm64", "x86", "arm", "ia64" }
                .FirstOrDefault(item => decoration.StartsWith("NT" + item, StringComparison.OrdinalIgnoreCase));
            if (architecture is null)
                return (false, null, null, null, false, "A decoração Models usa arquitetura não reconhecida.");
            var remainder = decoration[(2 + architecture.Length)..];
            if (remainder.Length > 0 && !remainder.StartsWith('.'))
                return (false, null, null, null, false, "A decoração Models tem campos de arquitetura não suportados.");
            var fields = remainder.Length == 0 ? Array.Empty<string>() : remainder[1..].Split('.');
            if (fields.Length > 5)
                return (false, architecture, null, null, false, "A decoração Models contém campos OS não suportados.");
            int? major = ParseOptionalInt(fields, 0, out var majorValid);
            int? minor = ParseOptionalInt(fields, 1, out var minorValid);
            int? productType = ParseOptionalInt(fields, 2, out var productValid);
            var suite = GetField(fields, 3);
            int? build = ParseOptionalInt(fields, 4, out var buildValid);
            if (!majorValid || !minorValid || !productValid || !buildValid || suite.Length > 0)
                return (false, architecture, build, productType, false, "A decoração Models usa ProductType/SuiteMask ou versão não numérica ainda não interpretada.");

            if (major is not null && platform.OsMajorVersion is null || minor is not null && platform.OsMinorVersion is null)
                return (false, architecture, build, productType, false, "A versão base do Windows não está identificada para comparar a decoração Models.");
            if (productType is not null && platform.ProductType is null)
                return (false, architecture, build, productType, false, "A decoração Models restringe ProductType, mas essa propriedade não foi lida.");

            var applicable = true;
            if (major is not null && (platform.OsMajorVersion!.Value < major.Value ||
                platform.OsMajorVersion.Value == major.Value && minor is not null && platform.OsMinorVersion!.Value < minor.Value))
                applicable = false;
            if (build is not null)
            {
                if (platform.WindowsBuild is null)
                    return (false, architecture, build, productType, false, "A decoração Models restringe build e o build atual não está disponível.");
                if (platform.WindowsBuild.Value < build.Value) applicable = false;
            }
            if (productType is not null && platform.ProductType is not null && productType != platform.ProductType)
                applicable = false;
            return (true, architecture, build, productType, applicable, string.Empty);
        }

        private static int? ParseOptionalInt(string[] fields, int index, out bool valid)
        {
            var value = GetField(fields, index);
            valid = value.Length == 0 || int.TryParse(value, out _);
            return int.TryParse(value, out var parsed) ? parsed : null;
        }

        private static string GetField(string[] fields, int index) => fields.Length > index ? fields[index].Trim() : string.Empty;
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

    private static (string Key, string Value)? ParseKeyValue(string line)
    {
        var equals = line.IndexOf('=');
        if (equals <= 0) return null;
        var key = line[..equals].Trim();
        var value = line[(equals + 1)..].Trim();
        return key.Length == 0 ? null : (key, value);
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
                result.Add(value[start..index].Trim());
                start = index + 1;
            }
        }
        result.Add(value[start..].Trim());
        return result;
    }

    private static string Unquote(string value) => value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;
}
