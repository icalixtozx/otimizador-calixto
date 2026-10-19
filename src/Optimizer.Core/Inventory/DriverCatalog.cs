using System.Text;

namespace Optimizer.Core.Inventory;

public static class DriverCatalog
{
    public static IReadOnlyList<DriverInfo> Filter(
        IEnumerable<DriverInfo> drivers,
        string? searchText,
        DriverSignatureFilter signatureFilter)
    {
        ArgumentNullException.ThrowIfNull(drivers);
        var term = searchText?.Trim();

        return drivers
            .Where(driver => MatchesSignature(driver, signatureFilter))
            .Where(driver => string.IsNullOrEmpty(term) || MatchesText(driver, term))
            .OrderBy(driver => driver.DeviceName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    /// <summary>Creates a reviewable CSV without PnP IDs or other hardware identifiers.</summary>
    public static string ToSanitizedCsv(IEnumerable<DriverInfo> drivers)
    {
        ArgumentNullException.ThrowIfNull(drivers);
        var builder = new StringBuilder();
        builder.AppendLine("Dispositivo,Fornecedor,Versão,Data informada,INF,Assinado,Assinante,Código de problema");

        foreach (var driver in drivers)
        {
            builder.Append(Escape(driver.DeviceName)).Append(',')
                .Append(Escape(driver.Provider)).Append(',')
                .Append(Escape(driver.Version)).Append(',')
                .Append(Escape(driver.DriverDateRaw)).Append(',')
                .Append(Escape(driver.InfName)).Append(',')
                .Append(Escape(driver.IsSigned switch { true => "Sim", false => "Não", null => "Desconhecido" })).Append(',')
                .Append(Escape(driver.Signer)).Append(',')
                .AppendLine(Escape(driver.ProblemCode?.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        return builder.ToString();
    }

    private static bool MatchesSignature(DriverInfo driver, DriverSignatureFilter filter) => filter switch
    {
        DriverSignatureFilter.All => true,
        DriverSignatureFilter.Signed => driver.IsSigned is true,
        DriverSignatureFilter.Unsigned => driver.IsSigned is false,
        DriverSignatureFilter.Unknown => driver.IsSigned is null,
        _ => false
    };

    private static bool MatchesText(DriverInfo driver, string term) =>
        Contains(driver.DeviceName, term) || Contains(driver.Provider, term) ||
        Contains(driver.Version, term) || Contains(driver.InfName, term) ||
        Contains(driver.Signer, term);

    private static bool Contains(string? source, string value) =>
        source?.Contains(value, StringComparison.CurrentCultureIgnoreCase) == true;

    private static string Escape(string? value)
    {
        var safe = value ?? string.Empty;
        var firstVisibleCharacter = safe.TrimStart();
        if (firstVisibleCharacter.Length > 0 && "=+-@".IndexOf(firstVisibleCharacter[0]) >= 0)
        {
            safe = $"'{safe}";
        }

        return $"\"{safe.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
