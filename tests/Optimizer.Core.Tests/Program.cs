using Optimizer.Core.Inventory;

var tests = new (string Name, Action Run)[]
{
    ("search is case-insensitive and excludes unrelated rows", SearchFiltersRows),
    ("signature filter keeps unknown separate", SignatureFilterKeepsUnknownSeparate),
    ("CSV escapes fields and omits device identifiers", CsvIsSanitized),
    ("CSV neutralizes spreadsheet formulas", CsvNeutralizesFormulaCells)
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.WriteLine($"FAIL {test.Name}: {exception.Message}");
    }
}

Console.WriteLine($"{tests.Length - failures}/{tests.Length} model checks passed.");
return failures == 0 ? 0 : 1;

static void SearchFiltersRows()
{
    var rows = SampleDrivers();
    var result = DriverCatalog.Filter(rows, "nViDiA", DriverSignatureFilter.All);
    Assert(result.Count == 1, "case-insensitive filter should return one row");
    Assert(result[0].DeviceName == "NVIDIA GeForce RTX 4060", "wrong row returned");
}

static void SignatureFilterKeepsUnknownSeparate()
{
    var rows = SampleDrivers();
    var signed = DriverCatalog.Filter(rows, null, DriverSignatureFilter.Signed);
    var unsigned = DriverCatalog.Filter(rows, null, DriverSignatureFilter.Unsigned);
    var unknown = DriverCatalog.Filter(rows, null, DriverSignatureFilter.Unknown);
    Assert(signed.Count == 1, "signed filter should return one row");
    Assert(unsigned.Count == 1, "unsigned filter should return one row");
    Assert(unknown.Count == 1 && unknown[0].DeviceName == "Unknown device", "unknown signature must remain distinct");
}

static void CsvIsSanitized()
{
    var deviceId = "PCI\\PRIVATE-HARDWARE-ID";
    var row = new DriverInfo("Device, \"special\"", deviceId, "Vendor", "1.2.3", "20250101000000.000000+000", "oem1.inf", true, "Vendor Inc.", null);
    var csv = DriverCatalog.ToSanitizedCsv([row]);
    Assert(!csv.Contains(deviceId, StringComparison.Ordinal), "CSV must omit PnP identifiers");
    Assert(csv.Contains("\"Device, \"\"special\"\"\"", StringComparison.Ordinal), "CSV field escaping is incorrect");
    Assert(csv.Contains("Dispositivo,Fornecedor,Versão", StringComparison.Ordinal), "CSV header missing");
}

static IReadOnlyList<DriverInfo> SampleDrivers() =>
[
    new("NVIDIA GeForce RTX 4060", "PCI\\VEN_10DE\\LOCAL-ID", "NVIDIA", "32.0.16.1714", null, "oem10.inf", true, "Microsoft Windows Hardware Compatibility Publisher", null),
    new("USB Sample Device", "USB\\PRIVATE-ID", "Sample Vendor", "1.0", null, "oem20.inf", false, null, 28),
    new("Unknown device", "UNKNOWN\\SERIAL-ID", null, null, null, null, null, null, null)
];

static void CsvNeutralizesFormulaCells()
{
    var row = new DriverInfo("=HYPERLINK(\"https://example.invalid\")", null, "+Vendor", null, null, null, null, null, null);
    var csv = DriverCatalog.ToSanitizedCsv([row]);
    Assert(csv.Contains("\"'=HYPERLINK", StringComparison.Ordinal), "formula-like text should be prefixed before export");
    Assert(csv.Contains("\"'+Vendor\"", StringComparison.Ordinal), "leading plus should be neutralized");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
