using Optimizer.Core.Adjustments;
using System.Globalization;
using System.Text.Json;

namespace Optimizer.Core.Registry;

public enum RegistryPreferenceAction
{
    Apply,
    Restore
}

public enum RegistryPreferenceHive
{
    CurrentUser
}

public enum RegistryPreferenceView
{
    Registry64
}

public enum RegistryStoredValueKind
{
    String,
    ExpandString,
    DWord,
    QWord,
    MultiString,
    Binary,
    None
}

public enum FileExtensionVisibility
{
    Show,
    Hide
}

/// <summary>Canonical, lossless payload for the registry value kinds this prototype can capture.</summary>
public sealed record RegistryTypedValue(RegistryStoredValueKind Kind, string Data)
{
    public static RegistryTypedValue DWord(uint value) =>
        new(RegistryStoredValueKind.DWord, value.ToString("X8", System.Globalization.CultureInfo.InvariantCulture));

    public bool IsWellFormed
    {
        get
        {
            if (Data is null) return false;
            return Kind switch
            {
                RegistryStoredValueKind.String or RegistryStoredValueKind.ExpandString => true,
                RegistryStoredValueKind.DWord => Data.Length == 8 && uint.TryParse(Data, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _),
                RegistryStoredValueKind.QWord => Data.Length == 16 && ulong.TryParse(Data, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _),
                RegistryStoredValueKind.MultiString => IsValidMultiString(Data),
                RegistryStoredValueKind.Binary or RegistryStoredValueKind.None => IsValidBase64(Data),
                _ => false
            };
        }
    }

    private static bool IsValidMultiString(string data)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(data) is { } items && items.All(item => item is not null);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsValidBase64(string data)
    {
        try
        {
            _ = Convert.FromBase64String(data);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

/// <summary>The only registry address allowed by this first preference prototype.</summary>
public sealed record RegistryPreferenceAddress(
    RegistryPreferenceHive Hive,
    string KeyPath,
    string ValueName,
    RegistryPreferenceView View);

/// <summary>Key existence and value existence are independent and must remain distinguishable.</summary>
public sealed record RegistryValueState(
    bool KeyExists,
    bool ValueExists,
    RegistryTypedValue? Value)
{
    public static RegistryValueState MissingKey { get; } = new(false, false, null);
    public static RegistryValueState MissingValue { get; } = new(true, false, null);

    public bool IsValid => ValueExists == (Value is not null) && (KeyExists || !ValueExists) &&
        (Value is null || Value.IsWellFormed);
}

public enum RegistryManagementStatus
{
    Unmanaged,
    Managed,
    Unknown
}

public enum RegistryManagementEvidenceKind
{
    ActiveDirectoryDomainJoin,
    MicrosoftEntraDeviceJoin,
    MicrosoftEntraWorkAccount,
    MdmRegistration,
    GroupPolicyForPreference
}

public enum RegistryManagementEvidenceStatus
{
    Detected,
    NotDetected,
    Unknown,
    Unavailable,
    AccessDenied,
    Failed
}

public sealed record RegistryManagementEvidence(
    RegistryManagementEvidenceKind Kind,
    RegistryManagementEvidenceStatus Status,
    string Source,
    string Scope,
    string Detail);

public sealed record RegistryManagementAssessment(
    RegistryManagementStatus Status,
    string Detail,
    IReadOnlyList<RegistryManagementEvidence>? Evidence = null)
{
    public IReadOnlyList<RegistryManagementEvidence> EvidenceItems => Evidence ?? Array.Empty<RegistryManagementEvidence>();
}

/// <summary>
/// Computes only what the collected evidence proves for this preference. Domain, Entra, and MDM
/// enrollment are management context, not proof that HideFileExt is policy-controlled.
/// </summary>
public static class RegistryManagementAssessmentBuilder
{
    public static bool IsExactHideFileExtPolicy(string? registryKey, string? valueName) =>
        string.Equals(registryKey?.Trim().TrimStart('\\'),
            RegistryPreferenceCatalog.FileExtensionsAddress.KeyPath, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(valueName, RegistryPreferenceCatalog.FileExtensionsAddress.ValueName,
            StringComparison.OrdinalIgnoreCase);

    public static RegistryManagementAssessment FromEvidence(IEnumerable<RegistryManagementEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var items = evidence.ToArray();
        if (items.Any(item => item.Kind == RegistryManagementEvidenceKind.GroupPolicyForPreference &&
                              item.Status == RegistryManagementEvidenceStatus.Detected))
        {
            return new(
                RegistryManagementStatus.Managed,
                "O RSoP contém uma entrada de Política de Grupo para esta preferência. A avaliação é específica ao dado exposto pelo RSoP; escrita e restauração permanecem bloqueadas.",
                items);
        }

        return new(
            RegistryManagementStatus.Unknown,
            "As consultas não encontraram uma prova conclusiva de controle nem de ausência de gerenciamento desta preferência. MDM, GPP, scripts e agentes podem não estar representados; a escrita permanece bloqueada.",
            items);
    }
}

public interface IRegistryPreferenceAdapter
{
    RegistryValueState Read(RegistryPreferenceAddress address);
    void WriteValue(RegistryPreferenceAddress address, RegistryTypedValue value);
    void DeleteValue(RegistryPreferenceAddress address);
}

public interface IRegistryManagementInspector
{
    RegistryManagementAssessment Assess(RegistryPreferenceAddress address);
}

public sealed record RegistryPreferenceReview(
    AdjustmentPlan Plan,
    FileExtensionVisibility TargetVisibility,
    RegistryTypedValue TargetValue,
    RegistryValueState CurrentState,
    RegistryManagementAssessment Management,
    DateTimeOffset ReviewedAtUtc,
    string Summary);

public sealed record RegistryPreferenceApproval(
    Guid OperationId,
    RegistryPreferenceAction Action,
    string CaptureFingerprint,
    bool ConsequenceAccepted,
    bool OrganizationManagementReviewed,
    string ConfirmationPhrase)
{
    public static string RequiredPhrase(Guid operationId, RegistryPreferenceAction action)
    {
        var verb = action == RegistryPreferenceAction.Apply ? "APPLY" : "RESTORE";
        return $"{verb} FILE EXTENSIONS {operationId.ToString("N")[..8].ToUpperInvariant()}";
    }
}

public interface IRegistryMutationPolicy
{
    bool IsAuthorized(RegistryPreferenceApproval approval, string captureFingerprint);
}

/// <summary>Production default: all registry writes and reversals are denied.</summary>
public sealed class BlockedRegistryMutationPolicy : IRegistryMutationPolicy
{
    public bool IsAuthorized(RegistryPreferenceApproval approval, string captureFingerprint) => false;
}

/// <summary>Explicit opt-in intended for isolated tests only; the application does not inject this policy.</summary>
public sealed class ConfirmedRegistryMutationPolicy(bool enabled) : IRegistryMutationPolicy
{
    public bool IsAuthorized(RegistryPreferenceApproval approval, string captureFingerprint) =>
        enabled && approval.OperationId != Guid.Empty &&
        approval.OrganizationManagementReviewed && approval.ConsequenceAccepted &&
        string.Equals(approval.CaptureFingerprint, captureFingerprint, StringComparison.Ordinal) &&
        string.Equals(approval.ConfirmationPhrase,
            RegistryPreferenceApproval.RequiredPhrase(approval.OperationId, approval.Action), StringComparison.Ordinal);
}

public static class RegistryPreferenceCatalog
{
    public const string AdjustmentId = "registry.explorer.file-extensions";
    public const string ProfileId = "registry-file-extensions";
    public const int AdjustmentVersion = 1;

    public static RegistryPreferenceAddress FileExtensionsAddress { get; } = new(
        RegistryPreferenceHive.CurrentUser,
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
        "HideFileExt",
        RegistryPreferenceView.Registry64);

    public static RegistryTypedValue ShowFileExtensionsValue { get; } = RegistryTypedValue.DWord(0);
    public static RegistryTypedValue HideFileExtensionsValue { get; } = RegistryTypedValue.DWord(1);

    public static RegistryTypedValue TargetValue(FileExtensionVisibility visibility) => visibility switch
    {
        FileExtensionVisibility.Show => ShowFileExtensionsValue,
        FileExtensionVisibility.Hide => HideFileExtensionsValue,
        _ => throw new ArgumentOutOfRangeException(nameof(visibility))
    };

    public static AdjustmentCatalog CreateAdjustmentCatalog(FileExtensionVisibility targetVisibility) => new([
        new AdjustmentDefinition(
            AdjustmentId,
            AdjustmentVersion,
            targetVisibility == FileExtensionVisibility.Show
                ? "Exibir extensões de arquivo"
                : "Ocultar extensões de arquivo",
            targetVisibility == FileExtensionVisibility.Show
                ? "Mostra extensões conhecidas nos nomes exibidos pelo Explorador de Arquivos. É uma preferência de interface, sem alegação de desempenho."
                : "Oculta extensões conhecidas nos nomes exibidos pelo Explorador de Arquivos. É uma preferência de interface, sem alegação de desempenho.",
            AdjustmentArea.Registry,
            targetVisibility == FileExtensionVisibility.Show ? AdjustmentRiskLevel.Low : AdjustmentRiskLevel.Moderate,
            targetVisibility == FileExtensionVisibility.Show
                ? "Os nomes passam a mostrar extensões como .txt e .exe. Isso altera a apresentação e pode facilitar identificar o tipo do arquivo; não renomeia nem converte arquivos. A escrita no Registro fica bloqueada por padrão."
                : "Os nomes deixam de mostrar extensões conhecidas. Isso pode dificultar identificar o tipo do arquivo e tornar nomes visualmente ambíguos; não renomeia nem converte arquivos. A escrita no Registro fica bloqueada por padrão.",
            RequiresAdministrator: false,
            SupportedArchitectures: ["x64"],
            MinimumWindowsBuild: null,
            Availability: AdjustmentAvailability.RequiresSpecificApproval,
            AvailabilityReason: "Somente revisão individual é suportada neste protótipo. Escrita permanece bloqueada por padrão e gerenciamento organizacional desconhecido impede a execução.")
    ]);
}
