namespace Optimizer.Core.Programs;

public enum WinGetSourcePackageRegistration
{
    RegisteredHealthy,
    RegisteredUnhealthy,
    NotRegistered,
    Unknown
}

public sealed record WinGetSourcePackageStatus(
    WinGetSourcePackageRegistration Registration,
    string? ReportedPackageStatus = null,
    string? Diagnostic = null)
{
    public static string ToUserMessage(WinGetSourcePackageStatus status) => status.Registration switch
    {
        WinGetSourcePackageRegistration.NotRegistered =>
            "WinGet indisponível neste perfil: Microsoft.Winget.Source não está registrado. A recuperação da origem continua pendente; instalação indisponível. Inventário local, navegação e demais páginas não dependem desta origem.",
        WinGetSourcePackageRegistration.RegisteredHealthy =>
            "Microsoft.Winget.Source está registrado neste perfil. Isso não confirma atualização da origem nem metadata atual; a instalação continua bloqueada nesta versão.",
        WinGetSourcePackageRegistration.RegisteredUnhealthy =>
            $"Microsoft.Winget.Source está registrado, mas o Windows reportou estado {SafeStatus(status.ReportedPackageStatus)}. A origem não foi validada e a instalação continua bloqueada.",
        _ =>
            $"Não foi possível confirmar o estado de Microsoft.Winget.Source neste perfil ({SafeDiagnostic(status.Diagnostic)}). A instalação continua bloqueada; o inventário local permanece disponível."
    };

    private static string SafeStatus(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "desconhecido" : value;

    private static string SafeDiagnostic(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "consulta inconclusiva" : value;
}

public interface IWinGetSourcePackageStatusProvider
{
    Task<WinGetSourcePackageStatus> ReadCurrentProfileStatusAsync(CancellationToken cancellationToken = default);
}
