using Optimizer.Core.Adjustments;
using Optimizer.Core.Inventory;
using Optimizer.Core.Profiles;
using Optimizer.Core.Consent;
using Optimizer.Core.Recovery;

var tests = new (string Name, Func<Task> Run)[]
{
    ("search is case-insensitive and excludes unrelated rows", () => RunSync(SearchFiltersRows)),
    ("signature filter keeps unknown separate", () => RunSync(SignatureFilterKeepsUnknownSeparate)),
    ("CSV escapes fields and omits device identifiers", () => RunSync(CsvIsSanitized)),
    ("CSV neutralizes spreadsheet formulas", () => RunSync(CsvNeutralizesFormulaCells)),
    ("diagnostic JSON omits hardware identifiers and keeps PnP codes neutral", () => RunSync(DiagnosticJsonIsSanitizedAndNeutral)),
    ("PnP codes and missing metadata are presented without diagnosis", () => RunSync(PnpStatusRemainsNeutral)),
    ("catalog rejects duplicate stable IDs", () => RunSync(CatalogRejectsDuplicateIds)),
    ("plan blocks incompatible and unknown adjustments", () => RunSync(PlanBlocksIncompatibleAndUnknown)),
    ("plan fingerprint binds consequence and risk metadata", () => RunSync(PlanFingerprintBindsConsequences)),
    ("consent requires a decision for every consequence", () => RunSync(ConsentRequiresEveryDecision)),
    ("consent records acceptances and declines separately", () => RunSync(ConsentTracksIndividualDecisions)),
    ("consent rejects stale plans and tampered receipts", () => RunSync(ConsentRejectsStaleOrTamperedReceipt)),
    ("journal persists snapshots and replaces operation state", JournalPersistsAndReplacesState),
    ("synthetic journal cannot create Windows backup snapshots", () => RunSync(SyntheticJournalCannotCreateSnapshots)),
    ("journal rejects a corrupt persisted document", JournalRejectsCorruptDocument),
    ("journal write failure preserves the last committed entry", JournalWriteFailurePreservesCommittedEntry),
    ("simulated disk-full error preserves the last committed entry", JournalDiskFullPreservesCommittedEntry),
    ("interrupted journal write preserves the last committed entry", JournalInterruptedWritePreservesCommittedEntry),
    ("journal resume assessment detects safe points and plan conflicts", () => RunSync(JournalResumeAssessmentIsConservative)),
    ("journal requires consent and complete snapshots before arming", () => RunSync(JournalRequiresConsentAndSnapshots)),
    ("recovery assessment never performs automatic rollback", () => RunSync(RecoveryAssessmentIsReadOnly)),
    ("profile catalog exposes the four requested profiles", () => RunSync(ProfileCatalogHasFourProfiles)),
    ("light and balanced profiles respect their risk limits", () => RunSync(ProfilesRespectRiskLimits)),
    ("extreme profile requires explicit selection", () => RunSync(ExtremeRequiresExplicitSelection)),
    ("custom profile uses only explicit selection", () => RunSync(CustomUsesOnlyExplicitSelection)),
    ("simulator records explicit in-memory simulation", SimulatorCompletesWithoutWindowsChanges),
    ("simulator stops after an adapter failure", SimulatorStopsAfterFailure),
    ("simulator reports cancellation and leaves remaining items unrun", SimulatorReportsInterruption),
    ("VIDEOIDLE read preview and capture are persisted while real writes stay blocked by default", VideoIdleCaptureIsPersistedAndBlockedByDefault),
    ("VIDEOIDLE test adapter persists apply intent and verifies readback", VideoIdleApplyPersistsIntentAndVerifiesReadback),
    ("VIDEOIDLE application detects a state conflict before writing", VideoIdleApplyDetectsConflict),
    ("VIDEOIDLE application detects drift after persisting intent", VideoIdleApplyDetectsConflictAfterArming),
    ("VIDEOIDLE write does not reactivate a plan changed concurrently", VideoIdlePlanChangeAfterWriteDoesNotReactivateOldPlan),
    ("VIDEOIDLE journal failure before write leaves Windows unchanged", VideoIdleApplyJournalFailureLeavesStateUntouched),
    ("VIDEOIDLE reversal restores the exact captured value after verification", VideoIdleRevertRestoresExactCapturedValue),
    ("VIDEOIDLE reversal refuses a changed setting", VideoIdleRevertIsConditional),
    ("VIDEOIDLE interruption after the fake write is left unverified and recoverable", VideoIdleInterruptionLeavesManualRecoveryRecord),
    ("VIDEOIDLE capture write failure is not reported as persisted", VideoIdleCaptureFailureIsNotReportedAsSaved),
    ("VIDEOIDLE store rejects a tampered persisted capture", VideoIdleStoreRejectsTamperedCapture),
    ("Windows power-mode GUIDs map only documented values", () => RunSync(PowerModeGuidsMapDocumentedValues)),
    ("power-mode capture persists independent AC/DC state while default writes stay blocked", PowerModeCaptureIsPersistedAndBlockedByDefault),
    ("power-mode capture can select AC only when DC is unavailable", PowerModeCaptureSupportsOneSide),
    ("power-mode application requires consequence consent for every selected side", PowerModeApplyRequiresSpecificConsent),
    ("power-mode application and restoration verify independent AC/DC values", PowerModeApplyAndRevertVerifyEachSide),
    ("power-mode application detects state conflicts before writing", PowerModeApplyDetectsConflict),
    ("power-mode interrupted partial apply is persisted and conditionally recoverable", PowerModeInterruptionCanBeConditionallyReverted),
    ("power-mode restoration refuses third-party changes", PowerModeRevertRefusesExternalChange),
    ("power-mode journal failure before first write leaves fake Windows unchanged", PowerModeJournalFailurePreventsWrite),
    ("power-mode store rejects tampered captures", PowerModeStoreRejectsTamperedCapture),
    ("power-mode store rejects an unsupported persisted state", PowerModeStoreRejectsUnsupportedState)
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        await test.Run();
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

static Task RunSync(Action action)
{
    action();
    return Task.CompletedTask;
}

static void SearchFiltersRows()
{
    var rows = SampleDrivers();
    var result = DriverCatalog.Filter(rows, "sample graphics", DriverSignatureFilter.All);
    Assert(result.Count == 1, "case-insensitive filter should return one row");
    Assert(result[0].DeviceName == "Sample Graphics Adapter", "wrong row returned");
}

static void SignatureFilterKeepsUnknownSeparate()
{
    var rows = SampleDrivers();
    var signed = DriverCatalog.Filter(rows, null, DriverSignatureFilter.Signed);
    var unsigned = DriverCatalog.Filter(rows, null, DriverSignatureFilter.Unsigned);
    var unknown = DriverCatalog.Filter(rows, null, DriverSignatureFilter.Unknown);
    Assert(signed.Count == 1, "signed filter should return one row");
    Assert(unsigned.Count == 1, "unsigned filter should return one row");
    Assert(unknown.Count == 1 && unknown[0].DeviceName == "Unknown sample device", "unknown signature must remain distinct");
}

static void CsvIsSanitized()
{
    var deviceId = "SAMPLE\\DEVICE-ID-001";
    var row = new DriverInfo("Device, \"special\"", deviceId, "Vendor", "1.2.3", "20250101000000.000000+000", "oem1.inf", true, "Vendor Inc.", null);
    var csv = DriverCatalog.ToSanitizedCsv([row]);
    Assert(!csv.Contains(deviceId, StringComparison.Ordinal), "CSV must omit PnP identifiers");
    Assert(csv.Contains("\"Device, \"\"special\"\"\"", StringComparison.Ordinal), "CSV field escaping is incorrect");
    Assert(csv.Contains("Dispositivo,Fornecedor,Versão", StringComparison.Ordinal), "CSV header missing");
}

static IReadOnlyList<DriverInfo> SampleDrivers() =>
[
    new("Sample Graphics Adapter", "SAMPLE\\DEVICE-001", "Example Vendor", "1.0.0", null, "sample.inf", true, "Example Signature Publisher", null),
    new("USB Sample Device", "USB\\SAMPLE-002", "Example Vendor", "1.0", null, "sample-usb.inf", false, null, 28),
    new("Unknown sample device", "SAMPLE\\UNKNOWN-003", null, null, null, null, null, null, null)
];


static void DiagnosticJsonIsSanitizedAndNeutral()
{
    var deviceId = "PCI\\VEN_1234&DEV_ABCD\\SERIAL-PRIVATE";
    var schemeId = Guid.Parse("2f7fa2dd-bc0a-467a-8635-c1fa2355d169");
    var computer = new ComputerInventory(
        DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
        "Windows Example",
        "Professional",
        "25H2",
        "99999.1",
        "X64",
        "Example CPU",
        ["Example GPU"],
        "16 GiB",
        "Example Board",
        ["Example Disk"],
        ["Example Network Adapter"],
        []);
    var drivers = new DriverInventoryResult(
        DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
        InventoryAvailability.Available,
        [new DriverInfo("Example Device", deviceId, "Example Vendor", "1.2.3", null, "sample.inf", true, "Example Signer", 28)],
        [],
        null);
    var power = new PowerPlanInventoryResult(
        DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
        InventoryAvailability.Available,
        [new PowerPlanInfo(schemeId, "Example Plan", true, 180, 180)],
        [],
        null);

    var json = SanitizedDiagnosticReportBuilder.BuildJson(
        computer, drivers, power, DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

    Assert(!json.Contains(deviceId, StringComparison.Ordinal), "diagnostic JSON must omit the PnP device ID");
    Assert(!json.Contains(schemeId.ToString(), StringComparison.OrdinalIgnoreCase), "diagnostic JSON must omit power scheme GUIDs");
    Assert(!json.Contains("SERIAL-PRIVATE", StringComparison.Ordinal), "diagnostic JSON must omit serial-like identifiers");
    Assert(json.Contains("\"problemCodeWindowsReported\": 28", StringComparison.Ordinal), "diagnostic must preserve the numeric code");
    Assert(json.Contains("Revise os nomes de hardware antes de compartilhar", StringComparison.Ordinal), "diagnostic must warn the user to review before sharing");
    Assert(!json.Contains("driverMissing", StringComparison.OrdinalIgnoreCase), "diagnostic must not infer a missing driver");
}

static void PnpStatusRemainsNeutral()
{
    var reported = new DriverInfo("Example Device", "PRIVATE\\DEVICE-ID", null, null, null, null, null, null, 28);
    var absentMetadata = new DriverInfo("Example PnP Device", "PRIVATE\\OTHER-ID", null, null, null, null, null, null, 0, DriverMetadataAvailable: false);

    Assert(reported.ProblemCodeLabel == "PnP 28 reportado pelo Windows", "positive code should be shown as reported data");
    Assert(!reported.ProblemCodeLabel.Contains("ausente", StringComparison.OrdinalIgnoreCase), "code label must not infer a missing driver");
    Assert(absentMetadata.DriverDetailsStatus == "Detalhes do driver não retornados", "missing query metadata should be explicit but neutral");
    Assert(absentMetadata.ProblemCodeLabel == "PnP 0 reportado pelo Windows", "zero should remain a reported code");
}
static void CsvNeutralizesFormulaCells()
{
    var row = new DriverInfo("=HYPERLINK(\"https://example.invalid\")", null, "+Vendor", null, null, null, null, null, null);
    var csv = DriverCatalog.ToSanitizedCsv([row]);
    Assert(csv.Contains("\"'=HYPERLINK", StringComparison.Ordinal), "formula-like text should be prefixed before export");
    Assert(csv.Contains("\"'+Vendor\"", StringComparison.Ordinal), "leading plus should be neutralized");
}

static void CatalogRejectsDuplicateIds()
{
    var definition = SampleAdjustment("demo.one");
    AssertThrows<ArgumentException>(() => new AdjustmentCatalog([definition, definition]));
}

static void PlanBlocksIncompatibleAndUnknown()
{
    var catalog = new AdjustmentCatalog([SampleAdjustment("demo.one")]);
    var compatibility = new AdjustmentCompatibilityEvaluator();
    var planner = new AdjustmentPlanService(catalog, compatibility);
    var platform = new PlatformDescriptor(true, "x86", 26300);

    var plan = planner.CreatePlan(["demo.one", "missing.item"], platform);
    Assert(!plan.CanSimulate, "incompatible and unknown items must block simulation");
    Assert(!plan.CanApply, "real application must remain unavailable");
    Assert(plan.Items[0].Status == PlanItemStatus.Incompatible, "wrong compatibility state");
    Assert(plan.Items[1].Status == PlanItemStatus.UnknownAdjustment, "unknown item must be explicit");
    Assert(plan.Fingerprint.Length == 64, "plan fingerprint should be SHA-256");
}

static async Task SimulatorCompletesWithoutWindowsChanges()
{
    var catalog = new AdjustmentCatalog([SampleAdjustment("demo.one")]);
    var compatibility = new AdjustmentCompatibilityEvaluator();
    var planner = new AdjustmentPlanService(catalog, compatibility);
    var adapter = new RecordingSimulatedAdapter((_, token) =>
    {
        token.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    });
    var simulator = new AdjustmentSimulationService(catalog, compatibility, adapter);
    var plan = planner.CreatePlan(["demo.one"], new PlatformDescriptor(true, "x64", 26300));

    var result = await simulator.SimulateAsync(plan);
    Assert(result.Outcome == AdjustmentSimulationOutcome.Completed, "simulation should complete");
    Assert(result.Steps.Single().Status == AdjustmentSimulationStepStatus.Simulated, "step should be marked simulated");
    Assert(!result.WindowsChanged, "simulator must report no Windows changes");
    Assert(adapter.Calls.SequenceEqual(["demo.one"]), "simulator should call the in-memory adapter");
}

static async Task SimulatorStopsAfterFailure()
{
    var catalog = new AdjustmentCatalog([
        SampleAdjustment("demo.first"),
        SampleAdjustment("demo.fail"),
        SampleAdjustment("demo.last")
    ]);
    var compatibility = new AdjustmentCompatibilityEvaluator();
    var planner = new AdjustmentPlanService(catalog, compatibility);
    var adapter = new RecordingSimulatedAdapter((definition, token) =>
    {
        token.ThrowIfCancellationRequested();
        return definition.Id == "demo.fail"
            ? Task.FromException(new InvalidOperationException("synthetic failure"))
            : Task.CompletedTask;
    });
    var simulator = new AdjustmentSimulationService(catalog, compatibility, adapter);
    var plan = planner.CreatePlan(
        ["demo.first", "demo.fail", "demo.last"],
        new PlatformDescriptor(true, "x64", 26300));

    var result = await simulator.SimulateAsync(plan);
    Assert(result.Outcome == AdjustmentSimulationOutcome.Failed, "failure must not be reported as success");
    Assert(result.Steps[0].Status == AdjustmentSimulationStepStatus.Simulated, "first step should be simulated");
    Assert(result.Steps[1].Status == AdjustmentSimulationStepStatus.Failed, "failing step should be explicit");
    Assert(result.Steps[2].Status == AdjustmentSimulationStepStatus.NotRun, "later steps must remain unrun");
    Assert(adapter.Calls.Count == 2, "adapter must stop after failure");
    Assert(!result.WindowsChanged, "failure path must report no Windows changes");
}

static async Task SimulatorReportsInterruption()
{
    using var cancellation = new CancellationTokenSource();
    var catalog = new AdjustmentCatalog([
        SampleAdjustment("demo.first"),
        SampleAdjustment("demo.last")
    ]);
    var compatibility = new AdjustmentCompatibilityEvaluator();
    var planner = new AdjustmentPlanService(catalog, compatibility);
    var adapter = new RecordingSimulatedAdapter((_, token) =>
    {
        cancellation.Cancel();
        return Task.FromCanceled(token);
    });
    var simulator = new AdjustmentSimulationService(catalog, compatibility, adapter);
    var plan = planner.CreatePlan(
        ["demo.first", "demo.last"],
        new PlatformDescriptor(true, "x64", 26300));

    var result = await simulator.SimulateAsync(plan, cancellation.Token);
    Assert(result.Outcome == AdjustmentSimulationOutcome.Interrupted, "cancellation must be explicit");
    Assert(result.Steps[0].Status == AdjustmentSimulationStepStatus.Interrupted, "active step should be interrupted");
    Assert(result.Steps[1].Status == AdjustmentSimulationStepStatus.NotRun, "remaining step must not run");
    Assert(!result.WindowsChanged, "interruption path must report no Windows changes");
}

static async Task VideoIdleCaptureIsPersistedAndBlockedByDefault()
{
    await WithTemporaryDirectory(async directory =>
    {
        var adapter = new FakeVideoIdlePowerAdapter(600);
        var store = new JsonVideoIdleOperationStore(Path.Combine(directory, "real-videoidle"));
        var service = new VideoIdleOperationService(adapter, store);
        var preview = service.ReadPreview();
        Assert(preview.CurrentAcTimeoutSeconds == 600 && preview.ProposedAcTimeoutSeconds == 900,
            "preview must show the actual source value and documented proposal");
        Assert(adapter.Writes.Count == 0, "preview must be read-only");

        var capture = await service.CaptureAsync();
        var loaded = await store.LoadAsync(capture.OperationId);
        Assert(loaded?.State == VideoIdleOperationState.Captured, "capture must be persisted");
        Assert(loaded!.PreviousAcTimeoutSeconds == 600 && loaded.ActiveSchemeId == adapter.SchemeId,
            "capture must preserve exact AC value and active scheme");
        Assert(adapter.Writes.Count == 0, "capture must not change Windows settings");

        var approval = MakeVideoIdleApproval(capture, VideoIdleAction.Apply);
        await AssertThrowsAsync<UnauthorizedAccessException>(() => service.ApplyAsync(capture.OperationId, approval));
        Assert(adapter.Writes.Count == 0, "default policy must block a real write even with an approval-shaped request");
    });
}

static async Task VideoIdleApplyPersistsIntentAndVerifiesReadback()
{
    await WithTemporaryDirectory(async directory =>
    {
        var adapter = new FakeVideoIdlePowerAdapter(600);
        var store = new JsonVideoIdleOperationStore(Path.Combine(directory, "real-videoidle"));
        var service = new VideoIdleOperationService(adapter, store, new ConfirmedVideoIdleMutationPolicy(enabled: true));
        var capture = await service.CaptureAsync();
        var applied = await service.ApplyAsync(capture.OperationId, MakeVideoIdleApproval(capture, VideoIdleAction.Apply));
        Assert(applied.State == VideoIdleOperationState.Verified, "readback must be explicitly verified");
        Assert(adapter.Writes.SequenceEqual([900u]), "only the documented target AC value may be written");
        Assert(adapter.Activations == 1, "active plan must be refreshed after writing the active plan setting");
        Assert(adapter.DcTimeoutSeconds == 300, "battery-side value must remain untouched");
        Assert((await store.LoadAsync(capture.OperationId))?.State == VideoIdleOperationState.Verified,
            "verified result must persist");
    });
}

static async Task VideoIdleApplyDetectsConflict()
{
    await WithTemporaryDirectory(async directory =>
    {
        var adapter = new FakeVideoIdlePowerAdapter(600);
        var store = new JsonVideoIdleOperationStore(Path.Combine(directory, "real-videoidle"));
        var service = new VideoIdleOperationService(adapter, store, new ConfirmedVideoIdleMutationPolicy(enabled: true));
        var capture = await service.CaptureAsync();
        adapter.AcTimeoutSeconds = 1200;
        var result = await service.ApplyAsync(capture.OperationId, MakeVideoIdleApproval(capture, VideoIdleAction.Apply));
        Assert(result.State == VideoIdleOperationState.Conflict, "changed value must be detected before mutation");
        Assert(adapter.Writes.Count == 0, "conflict must not write");
    });
}

static async Task VideoIdleApplyDetectsConflictAfterArming()
{
    await WithTemporaryDirectory(async directory =>
    {
        var adapter = new FakeVideoIdlePowerAdapter(600);
        var store = new JsonVideoIdleOperationStore(Path.Combine(directory, "real-videoidle"));
        var service = new VideoIdleOperationService(adapter, store, new ConfirmedVideoIdleMutationPolicy(enabled: true));
        var capture = await service.CaptureAsync();
        adapter.ChangeAcValueOnReadNumber = 3;
        adapter.ChangedAcValue = 1200;
        var result = await service.ApplyAsync(capture.OperationId, MakeVideoIdleApproval(capture, VideoIdleAction.Apply));
        Assert(result.State == VideoIdleOperationState.Conflict, "drift after intent persistence must be detected");
        Assert(adapter.Writes.Count == 0, "late conflict must be detected before the write");
    });
}

static async Task VideoIdlePlanChangeAfterWriteDoesNotReactivateOldPlan()
{
    await WithTemporaryDirectory(async directory =>
    {
        var adapter = new FakeVideoIdlePowerAdapter(600);
        var store = new JsonVideoIdleOperationStore(Path.Combine(directory, "real-videoidle"));
        var service = new VideoIdleOperationService(adapter, store, new ConfirmedVideoIdleMutationPolicy(enabled: true));
        var capture = await service.CaptureAsync();
        adapter.ChangeActiveSchemeOnReadNumber = 4;
        var result = await service.ApplyAsync(capture.OperationId, MakeVideoIdleApproval(capture, VideoIdleAction.Apply));
        Assert(result.State == VideoIdleOperationState.MutationUnverified,
            "an observed plan switch after the setting write must be reported as ambiguous");
        Assert(adapter.Activations == 0, "the old power plan must not be reactivated after a concurrent switch");
    });
}
static async Task VideoIdleApplyJournalFailureLeavesStateUntouched()
{
    await WithTemporaryDirectory(async directory =>
    {
        var storage = new FaultInjectingJournalStorage();
        var adapter = new FakeVideoIdlePowerAdapter(600);
        var store = new JsonVideoIdleOperationStore(Path.Combine(directory, "real-videoidle"), storage);
        var service = new VideoIdleOperationService(adapter, store, new ConfirmedVideoIdleMutationPolicy(enabled: true));
        var capture = await service.CaptureAsync();
        storage.NextWriteFailure = JournalWriteFailure.Io;
        await AssertThrowsAsync<IOException>(() => service.ApplyAsync(
            capture.OperationId,
            MakeVideoIdleApproval(capture, VideoIdleAction.Apply)));
        Assert(adapter.Writes.Count == 0, "failed recovery-boundary persistence must block the real write");
        Assert((await store.LoadAsync(capture.OperationId))?.State == VideoIdleOperationState.Captured,
            "the last durable record must remain the capture");
    });
}
static async Task VideoIdleRevertRestoresExactCapturedValue()
{
    await WithTemporaryDirectory(async directory =>
    {
        var adapter = new FakeVideoIdlePowerAdapter(627);
        var store = new JsonVideoIdleOperationStore(Path.Combine(directory, "real-videoidle"));
        var service = new VideoIdleOperationService(adapter, store, new ConfirmedVideoIdleMutationPolicy(enabled: true));
        var capture = await service.CaptureAsync();
        _ = await service.ApplyAsync(capture.OperationId, MakeVideoIdleApproval(capture, VideoIdleAction.Apply));
        var reverted = await service.RevertAsync(capture.OperationId, MakeVideoIdleApproval(capture, VideoIdleAction.Revert));
        Assert(reverted.State == VideoIdleOperationState.Reverted, "readback must verify exact previous value");
        Assert(adapter.AcTimeoutSeconds == 627 && adapter.Writes.SequenceEqual([900u, 627u]),
            "reversal must restore the captured AC index exactly");
        Assert(adapter.DcTimeoutSeconds == 300, "reversal must leave battery-side value untouched");
    });
}

static async Task VideoIdleRevertIsConditional()
{
    await WithTemporaryDirectory(async directory =>
    {
        var adapter = new FakeVideoIdlePowerAdapter(600);
        var store = new JsonVideoIdleOperationStore(Path.Combine(directory, "real-videoidle"));
        var service = new VideoIdleOperationService(adapter, store, new ConfirmedVideoIdleMutationPolicy(enabled: true));
        var capture = await service.CaptureAsync();
        _ = await service.ApplyAsync(capture.OperationId, MakeVideoIdleApproval(capture, VideoIdleAction.Apply));
        adapter.AcTimeoutSeconds = 1200;
        var result = await service.RevertAsync(capture.OperationId, MakeVideoIdleApproval(capture, VideoIdleAction.Revert));
        Assert(result.State == VideoIdleOperationState.Conflict, "reversal must refuse a value changed by another actor");
        Assert(adapter.Writes.SequenceEqual([900u]), "conflicted reversal must not write the old value");
    });
}

static async Task VideoIdleInterruptionLeavesManualRecoveryRecord()
{
    await WithTemporaryDirectory(async directory =>
    {
        var adapter = new FakeVideoIdlePowerAdapter(600) { ThrowOnNextActivation = true };
        var store = new JsonVideoIdleOperationStore(Path.Combine(directory, "real-videoidle"));
        var service = new VideoIdleOperationService(adapter, store, new ConfirmedVideoIdleMutationPolicy(enabled: true));
        var capture = await service.CaptureAsync();
        var interrupted = await service.ApplyAsync(capture.OperationId, MakeVideoIdleApproval(capture, VideoIdleAction.Apply));
        Assert(interrupted.State == VideoIdleOperationState.MutationUnverified,
            "an exception after the write must not be labeled successful");
        Assert((await store.LoadAsync(capture.OperationId))?.State == VideoIdleOperationState.MutationUnverified,
            "ambiguous result must remain persisted for manual review");

        var reverted = await service.RevertAsync(capture.OperationId, MakeVideoIdleApproval(capture, VideoIdleAction.Revert));
        Assert(reverted.State == VideoIdleOperationState.Reverted && adapter.AcTimeoutSeconds == 600,
            "a later explicitly authorized conditional reversal should verify the fake prior value");
    });
}

static async Task VideoIdleCaptureFailureIsNotReportedAsSaved()
{
    await WithTemporaryDirectory(async directory =>
    {
        var storage = new FaultInjectingJournalStorage { NextWriteFailure = JournalWriteFailure.Io };
        var adapter = new FakeVideoIdlePowerAdapter(600);
        var store = new JsonVideoIdleOperationStore(Path.Combine(directory, "real-videoidle"), storage);
        var service = new VideoIdleOperationService(adapter, store);
        await AssertThrowsAsync<IOException>(() => service.CaptureAsync());
        Assert((await store.LoadAllAsync()).Count == 0, "failed capture write must not appear as persisted history");
        Assert(adapter.Writes.Count == 0, "capture persistence failure must never reach a Windows write");
    });
}

static async Task VideoIdleStoreRejectsTamperedCapture()
{
    await WithTemporaryDirectory(async directory =>
    {
        var operationDirectory = Path.Combine(directory, "real-videoidle");
        var store = new JsonVideoIdleOperationStore(operationDirectory);
        var service = new VideoIdleOperationService(new FakeVideoIdlePowerAdapter(600), store);
        var capture = await service.CaptureAsync();
        var path = Path.Combine(operationDirectory, capture.OperationId.ToString("D") + ".json");
        var node = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        node["PreviousAcTimeoutSeconds"] = 601;
        await File.WriteAllTextAsync(path, node.ToJsonString());
        await AssertThrowsAsync<InvalidDataException>(() => store.LoadAsync(capture.OperationId));
    });
}

static void PowerModeGuidsMapDocumentedValues()
{
    Assert(UserConfiguredPowerModes.TryParse(Guid.Parse("961cc777-2547-4f9d-8174-7d86181b8a7a"), out var efficient) &&
        efficient == UserConfiguredPowerMode.BestEfficiency, "best-efficiency GUID must map to documented enum");
    Assert(UserConfiguredPowerModes.TryParse(Guid.Empty, out var balanced) && balanced == UserConfiguredPowerMode.Balanced,
        "GUID_POWER_MODE_NONE must map to Balanced");
    Assert(UserConfiguredPowerModes.TryParse(Guid.Parse("ded574b5-45a0-4f42-8737-46345c09c238"), out var performance) &&
        performance == UserConfiguredPowerMode.BestPerformance, "best-performance GUID must map to documented enum");
    Assert(!UserConfiguredPowerModes.TryParse(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), out _),
        "unknown mode GUID must never be treated as a reversible supported value");
}

static async Task PowerModeCaptureIsPersistedAndBlockedByDefault()
{
    await WithTemporaryDirectory(async directory =>
    {
        var adapter = new FakePowerModeAdapter(UserConfiguredPowerMode.Balanced, UserConfiguredPowerMode.BestEfficiency);
        var store = new JsonPowerModeOperationStore(Path.Combine(directory, "real-power-mode"));
        var service = new PowerModeOperationService(adapter, store);
        var preview = service.ReadPreview();
        Assert(preview.CanModifyAc && preview.CanModifyDc, "preview must expose both configured sides");
        Assert(adapter.Writes.Count == 0, "preview must be read-only");

        var capture = await service.CaptureAsync(UserConfiguredPowerMode.BestPerformance, UserConfiguredPowerMode.Balanced);
        var persisted = await store.LoadAsync(capture.OperationId);
        Assert(persisted?.State == PowerModeOperationState.Captured, "capture must be persisted before writes");
        Assert(persisted!.PreviousAcMode == UserConfiguredPowerMode.Balanced &&
            persisted.PreviousDcMode == UserConfiguredPowerMode.BestEfficiency, "AC/DC prior modes must be captured separately");
        Assert(adapter.Writes.Count == 0, "capture must not call a setter");

        var approval = MakePowerModeApproval(capture, PowerModeAction.Apply, true, true);
        await AssertThrowsAsync<UnauthorizedAccessException>(() => service.ApplyAsync(capture.OperationId, approval));
        Assert(adapter.Writes.Count == 0, "default mutation policy must deny real writes");
    });
}

static async Task PowerModeCaptureSupportsOneSide()
{
    await WithTemporaryDirectory(async directory =>
    {
        var adapter = new FakePowerModeAdapter(UserConfiguredPowerMode.Balanced, UserConfiguredPowerMode.Balanced)
        {
            DcModeIdOverride = null,
            DcErrorCode = 50
        };
        var store = new JsonPowerModeOperationStore(Path.Combine(directory, "real-power-mode"));
        var capture = await new PowerModeOperationService(adapter, store)
            .CaptureAsync(UserConfiguredPowerMode.BestPerformance, targetDcMode: null);
        Assert(capture.PreviousAcMode == UserConfiguredPowerMode.Balanced && capture.PreviousDcMode is null,
            "a partial API failure must not block independently supported AC capture or invent DC state");
        Assert(adapter.Writes.Count == 0, "partial-side capture must remain read-only");
    });
}

static async Task PowerModeApplyRequiresSpecificConsent()
{
    await WithTemporaryDirectory(async directory =>
    {
        var adapter = new FakePowerModeAdapter(UserConfiguredPowerMode.Balanced, UserConfiguredPowerMode.Balanced);
        var store = new JsonPowerModeOperationStore(Path.Combine(directory, "real-power-mode"));
        var service = new PowerModeOperationService(adapter, store, new ConfirmedPowerModeMutationPolicy(enabled: true));
        var capture = await service.CaptureAsync(UserConfiguredPowerMode.BestPerformance, UserConfiguredPowerMode.BestEfficiency);
        var missingDcConsent = MakePowerModeApproval(capture, PowerModeAction.Apply, ac: true, dc: false);
        await AssertThrowsAsync<UnauthorizedAccessException>(() => service.ApplyAsync(capture.OperationId, missingDcConsent));
        var wrongPhrase = missingDcConsent with { DcConsequencesAccepted = true, ConfirmationPhrase = "APPLY POWER MODE WRONG" };
        await AssertThrowsAsync<UnauthorizedAccessException>(() => service.ApplyAsync(capture.OperationId, wrongPhrase));
        Assert(adapter.Writes.Count == 0, "partial or stale consent must not reach a setter");
    });
}

static async Task PowerModeApplyAndRevertVerifyEachSide()
{
    await WithTemporaryDirectory(async directory =>
    {
        var adapter = new FakePowerModeAdapter(UserConfiguredPowerMode.Balanced, UserConfiguredPowerMode.Balanced);
        var store = new JsonPowerModeOperationStore(Path.Combine(directory, "real-power-mode"));
        var service = new PowerModeOperationService(adapter, store, new ConfirmedPowerModeMutationPolicy(enabled: true));
        var capture = await service.CaptureAsync(UserConfiguredPowerMode.BestPerformance, UserConfiguredPowerMode.BestEfficiency);
        var applied = await service.ApplyAsync(capture.OperationId,
            MakePowerModeApproval(capture, PowerModeAction.Apply, ac: true, dc: true));
        Assert(applied.State == PowerModeOperationState.Verified, "both independently selected values must verify");
        Assert(adapter.AcMode == UserConfiguredPowerMode.BestPerformance && adapter.DcMode == UserConfiguredPowerMode.BestEfficiency,
            "only the selected AC/DC targets should be written");
        Assert(adapter.Writes.Select(write => write.Side).SequenceEqual(["AC", "DC"]),
            "the adapter must call separate AC and DC setters");

        var reverted = await service.RevertAsync(capture.OperationId,
            MakePowerModeApproval(capture, PowerModeAction.Revert, recovery: true));
        Assert(reverted.State == PowerModeOperationState.Reverted, "later readback must verify restoration");
        Assert(adapter.AcMode == UserConfiguredPowerMode.Balanced && adapter.DcMode == UserConfiguredPowerMode.Balanced,
            "restoration must use both exact captured values");
    });
}

static async Task PowerModeApplyDetectsConflict()
{
    await WithTemporaryDirectory(async directory =>
    {
        var adapter = new FakePowerModeAdapter(UserConfiguredPowerMode.Balanced, UserConfiguredPowerMode.Balanced);
        var store = new JsonPowerModeOperationStore(Path.Combine(directory, "real-power-mode"));
        var service = new PowerModeOperationService(adapter, store, new ConfirmedPowerModeMutationPolicy(enabled: true));
        var capture = await service.CaptureAsync(UserConfiguredPowerMode.BestPerformance, UserConfiguredPowerMode.BestEfficiency);
        adapter.DcMode = UserConfiguredPowerMode.BestEfficiency;
        var result = await service.ApplyAsync(capture.OperationId,
            MakePowerModeApproval(capture, PowerModeAction.Apply, ac: true, dc: true));
        Assert(result.State == PowerModeOperationState.Conflict, "state changed since capture must be reported as conflict");
        Assert(adapter.Writes.Count == 0, "no side may be written after a pre-apply conflict");
    });
}

static async Task PowerModeInterruptionCanBeConditionallyReverted()
{
    await WithTemporaryDirectory(async directory =>
    {
        var adapter = new FakePowerModeAdapter(UserConfiguredPowerMode.Balanced, UserConfiguredPowerMode.Balanced)
        {
            ThrowAfterAcWrite = true
        };
        var store = new JsonPowerModeOperationStore(Path.Combine(directory, "real-power-mode"));
        var service = new PowerModeOperationService(adapter, store, new ConfirmedPowerModeMutationPolicy(enabled: true));
        var capture = await service.CaptureAsync(UserConfiguredPowerMode.BestPerformance, UserConfiguredPowerMode.BestEfficiency);
        var interrupted = await service.ApplyAsync(capture.OperationId,
            MakePowerModeApproval(capture, PowerModeAction.Apply, ac: true, dc: true));
        Assert(interrupted.State == PowerModeOperationState.MutationUnverified,
            "exception after fake AC write must not be labeled success");
        Assert((await store.LoadAsync(capture.OperationId))?.State == PowerModeOperationState.MutationUnverified,
            "partial operation state must be persisted");
        Assert(adapter.DcMode == UserConfiguredPowerMode.Balanced, "DC setter must not run after AC interruption");

        var reverted = await service.RevertAsync(capture.OperationId,
            MakePowerModeApproval(capture, PowerModeAction.Revert, recovery: true));
        Assert(reverted.State == PowerModeOperationState.Reverted &&
            adapter.AcMode == UserConfiguredPowerMode.Balanced && adapter.DcMode == UserConfiguredPowerMode.Balanced,
            "conditional recovery should restore the partial AC write and preserve unchanged DC");
    });
}

static async Task PowerModeRevertRefusesExternalChange()
{
    await WithTemporaryDirectory(async directory =>
    {
        var adapter = new FakePowerModeAdapter(UserConfiguredPowerMode.Balanced, UserConfiguredPowerMode.Balanced);
        var store = new JsonPowerModeOperationStore(Path.Combine(directory, "real-power-mode"));
        var service = new PowerModeOperationService(adapter, store, new ConfirmedPowerModeMutationPolicy(enabled: true));
        var capture = await service.CaptureAsync(UserConfiguredPowerMode.BestPerformance, UserConfiguredPowerMode.BestPerformance);
        _ = await service.ApplyAsync(capture.OperationId,
            MakePowerModeApproval(capture, PowerModeAction.Apply, ac: true, dc: true));
        var appliedWriteCount = adapter.Writes.Count;
        adapter.DcMode = UserConfiguredPowerMode.BestEfficiency;
        var reverted = await service.RevertAsync(capture.OperationId,
            MakePowerModeApproval(capture, PowerModeAction.Revert, recovery: true));
        Assert(reverted.State == PowerModeOperationState.Conflict, "third-party mode must cause a restoration conflict");
        Assert(adapter.Writes.Count == appliedWriteCount, "recovery must not overwrite an external change");
    });
}

static async Task PowerModeJournalFailurePreventsWrite()
{
    await WithTemporaryDirectory(async directory =>
    {
        var storage = new FaultInjectingJournalStorage();
        var adapter = new FakePowerModeAdapter(UserConfiguredPowerMode.Balanced, UserConfiguredPowerMode.Balanced);
        var store = new JsonPowerModeOperationStore(Path.Combine(directory, "real-power-mode"), storage);
        var service = new PowerModeOperationService(adapter, store, new ConfirmedPowerModeMutationPolicy(enabled: true));
        var capture = await service.CaptureAsync(UserConfiguredPowerMode.BestPerformance, targetDcMode: null);
        storage.NextWriteFailure = JournalWriteFailure.DiskFull;
        await AssertThrowsAsync<IOException>(() => service.ApplyAsync(capture.OperationId,
            MakePowerModeApproval(capture, PowerModeAction.Apply, ac: true, dc: false)));
        Assert(adapter.Writes.Count == 0, "failure persisting the intent must prevent the power-mode setter");
        Assert((await store.LoadAsync(capture.OperationId))?.State == PowerModeOperationState.Captured,
            "the last committed capture must survive failed intent write");
    });
}

static async Task PowerModeStoreRejectsTamperedCapture()
{
    await WithTemporaryDirectory(async directory =>
    {
        var operationDirectory = Path.Combine(directory, "real-power-mode");
        var store = new JsonPowerModeOperationStore(operationDirectory);
        var service = new PowerModeOperationService(
            new FakePowerModeAdapter(UserConfiguredPowerMode.Balanced, UserConfiguredPowerMode.Balanced), store);
        var capture = await service.CaptureAsync(UserConfiguredPowerMode.BestPerformance, targetDcMode: null);
        var path = Path.Combine(operationDirectory, capture.OperationId.ToString("D") + ".json");
        var node = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        node["TargetAcMode"] = (int)UserConfiguredPowerMode.BestEfficiency;
        await File.WriteAllTextAsync(path, node.ToJsonString());
        await AssertThrowsAsync<InvalidDataException>(() => store.LoadAsync(capture.OperationId));
    });
}

static async Task PowerModeStoreRejectsUnsupportedState()
{
    await WithTemporaryDirectory(async directory =>
    {
        var operationDirectory = Path.Combine(directory, "real-power-mode");
        var store = new JsonPowerModeOperationStore(operationDirectory);
        var service = new PowerModeOperationService(
            new FakePowerModeAdapter(UserConfiguredPowerMode.Balanced, UserConfiguredPowerMode.Balanced), store);
        var capture = await service.CaptureAsync(UserConfiguredPowerMode.BestPerformance, targetDcMode: null);
        var path = Path.Combine(operationDirectory, capture.OperationId.ToString("D") + ".json");
        var node = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        node["State"] = 999;
        await File.WriteAllTextAsync(path, node.ToJsonString());
        await AssertThrowsAsync<InvalidDataException>(() => store.LoadAsync(capture.OperationId));
    });
}

static PowerModeApproval MakePowerModeApproval(
    PowerModeOperationRecord record,
    PowerModeAction action,
    bool ac = false,
    bool dc = false,
    bool recovery = false) =>
    PowerModeApproval.Confirm(record, action, ac, dc, recovery,
        PowerModeApproval.RequiredPhrase(record.OperationId, action));
static VideoIdleApproval MakeVideoIdleApproval(VideoIdleOperationRecord record, VideoIdleAction action) =>
    VideoIdleApproval.Confirm(record, action, consequenceAccepted: true,
        VideoIdleApproval.RequiredPhrase(record.OperationId, action));
static AdjustmentDefinition SampleAdjustment(string id, AdjustmentRiskLevel risk = AdjustmentRiskLevel.Low) =>
    new(
        id,
        1,
        $"Simulated {id}",
        "Synthetic test fixture; not a Windows optimization.",
        AdjustmentArea.Other,
        risk,
        "Test-only in-memory marker. No system setting changes.",
        RequiresAdministrator: false,
        SupportedArchitectures: ["x64"],
        MinimumWindowsBuild: 19045,
        Availability: AdjustmentAvailability.SimulationOnly,
        AvailabilityReason: null);

static async Task JournalPersistsAndReplacesState()
{
    await WithTemporaryDirectory(async directory =>
    {
        var (catalog, planner, plan) = CreateTwoItemPlan();
        var consentService = new PlanConsentService();
        var coordinator = new OperationJournalCoordinator(consentService);
        var store = new JsonOperationJournalStore(directory);
        var entry = coordinator.CreatePlanned(plan, "custom");
        var receipt = consentService.Record(plan, new Dictionary<string, bool>
        {
            ["demo.first"] = true,
            ["demo.second"] = true
        });
        entry = coordinator.RecordConsent(entry, plan, receipt);
        entry = coordinator.RecordSnapshot(entry, SampleSnapshot("demo.first", "before-first"));
        entry = coordinator.RecordSnapshot(entry, SampleSnapshot("demo.second", "before-second"));

        await store.SaveAsync(entry);
        var loaded = await store.LoadAsync(entry.OperationId);
        Assert(loaded is not null, "saved history entry should load");
        Assert(loaded!.State == JournalOperationState.SnapshotsCaptured, "journal state should round-trip");
        Assert(new PlanConsentService().Validate(plan, loaded.Consent!).AllConsequencesAccepted, "consent should remain verifiable after reload");
        Assert(loaded.Snapshots.Count == 2 && loaded.Snapshots[0].Values["state"] == "before-first",
            "snapshots should round-trip");

        var armed = coordinator.ArmMutation(loaded, plan);
        var interrupted = coordinator.MarkInterrupted(armed, "synthetic interruption");
        await store.SaveAsync(interrupted);
        var history = await store.LoadAllAsync();
        Assert(history.Count == 1 && history[0].State == JournalOperationState.Interrupted,
            "history should expose the latest atomically replaced entry");
        Assert(OperationRecoveryEvaluator.Assess(history[0]).Status ==
            RecoveryAssessmentStatus.SnapshotAvailableForManualReview,
            "interrupted operation with snapshots should require manual recovery review");
    });
}

static async Task JournalRejectsCorruptDocument()
{
    await WithTemporaryDirectory(async directory =>
    {
        var (_, _, plan) = CreateTwoItemPlan();
        var store = new JsonOperationJournalStore(directory);
        var entry = new OperationJournalCoordinator(new PlanConsentService()).CreatePlanned(plan, "custom");
        await store.SaveAsync(entry);
        await File.WriteAllTextAsync(Path.Combine(directory, entry.OperationId.ToString("D") + ".json"), "{ broken-json");
        await AssertThrowsAsync<System.Text.Json.JsonException>(() => store.LoadAsync(entry.OperationId));
    });
}

static async Task JournalWriteFailurePreservesCommittedEntry()
{
    await WithTemporaryDirectory(async directory =>
    {
        var (_, _, plan) = CreateTwoItemPlan();
        var storage = new FaultInjectingJournalStorage();
        var store = new JsonOperationJournalStore(directory, storage);
        var coordinator = new OperationJournalCoordinator(new PlanConsentService());
        var entry = coordinator.CreatePlanned(plan, "custom");
        await store.SaveAsync(entry);
        storage.NextWriteFailure = JournalWriteFailure.Io;

        await AssertThrowsAsync<IOException>(() => store.SaveAsync(coordinator.MarkSimulationCompleted(entry)));
        var loaded = await store.LoadAsync(entry.OperationId);
        Assert(loaded?.State == JournalOperationState.Planned,
            "a failed atomic write must preserve the previous committed journal entry");
    });
}

static async Task JournalDiskFullPreservesCommittedEntry()
{
    await WithTemporaryDirectory(async directory =>
    {
        var (_, _, plan) = CreateTwoItemPlan();
        var storage = new FaultInjectingJournalStorage();
        var store = new JsonOperationJournalStore(directory, storage);
        var coordinator = new OperationJournalCoordinator(new PlanConsentService());
        var entry = coordinator.CreatePlanned(plan, "custom");
        await store.SaveAsync(entry);
        storage.NextWriteFailure = JournalWriteFailure.DiskFull;

        var exception = await CaptureExceptionAsync<IOException>(
            () => store.SaveAsync(coordinator.MarkSimulationCompleted(entry)));
        Assert(exception.HResult == unchecked((int)0x80070070), "injected error should represent ERROR_DISK_FULL");
        var history = await store.LoadAllAsync();
        Assert(history.Count == 1 && history[0].State == JournalOperationState.Planned,
            "simulated disk-full error must leave the old entry intact");
    });
}

static async Task JournalInterruptedWritePreservesCommittedEntry()
{
    await WithTemporaryDirectory(async directory =>
    {
        var (_, _, plan) = CreateTwoItemPlan();
        var storage = new FaultInjectingJournalStorage();
        var store = new JsonOperationJournalStore(directory, storage);
        var coordinator = new OperationJournalCoordinator(new PlanConsentService());
        var entry = coordinator.CreatePlanned(plan, "custom");
        await store.SaveAsync(entry);
        storage.NextWriteFailure = JournalWriteFailure.Interruption;

        await AssertThrowsAsync<OperationCanceledException>(
            () => store.SaveAsync(coordinator.MarkSimulationCompleted(entry)));
        Assert((await store.LoadAsync(entry.OperationId))?.State == JournalOperationState.Planned,
            "interrupted replacement must preserve the last committed entry");
    });
}

static void JournalResumeAssessmentIsConservative()
{
    var (_, planner, plan) = CreateTwoItemPlan();
    var consent = new PlanConsentService();
    var coordinator = new OperationJournalCoordinator(consent);
    var entry = coordinator.CreatePlanned(plan, "custom");
    Assert(coordinator.AssessResume(entry).Status == JournalResumeStatus.RequiresCurrentPlanReview,
        "resume without a reloaded plan requires review");
    Assert(coordinator.AssessResume(entry, plan).Status == JournalResumeStatus.CanContinueBeforeMutation,
        "same planned state may continue preparation, but not auto-apply");

    var conflictingPlan = planner.CreatePlan(["demo.first", "demo.second"], new PlatformDescriptor(true, "x64", 26300));
    Assert(coordinator.AssessResume(entry, conflictingPlan).Status == JournalResumeStatus.PlanConflict,
        "a changed plan must cause a conflict");

    var receipt = consent.Record(plan, new Dictionary<string, bool>
    {
        ["demo.first"] = true,
        ["demo.second"] = true
    });
    var consented = coordinator.RecordConsent(entry, plan, receipt);
    AssertThrows<InvalidOperationException>(() => coordinator.RecordConsent(consented, plan, receipt));
    var interrupted = coordinator.MarkInterrupted(coordinator.ArmMutation(
        coordinator.RecordSnapshot(
            coordinator.RecordSnapshot(consented, SampleSnapshot("demo.first", "before-first")),
            SampleSnapshot("demo.second", "before-second")),
        plan), "injected interruption");
    Assert(coordinator.AssessResume(interrupted, plan).Status == JournalResumeStatus.ManualReviewRequired,
        "an interrupted potentially real operation must never auto-resume");
}

static void JournalRequiresConsentAndSnapshots()
{
    var (_, _, plan) = CreateTwoItemPlan();
    var consentService = new PlanConsentService();
    var coordinator = new OperationJournalCoordinator(consentService);
    var entry = coordinator.CreatePlanned(plan, "custom");

    AssertThrows<InvalidOperationException>(() => coordinator.ArmMutation(entry, plan));
    var receipt = consentService.Record(plan, new Dictionary<string, bool>
    {
        ["demo.first"] = true,
        ["demo.second"] = true
    });
    entry = coordinator.RecordConsent(entry, plan, receipt);
    entry = coordinator.RecordSnapshot(entry, SampleSnapshot("demo.first", "before-first"));
    AssertThrows<InvalidOperationException>(() => coordinator.ArmMutation(entry, plan));

    entry = coordinator.RecordSnapshot(entry, SampleSnapshot("demo.second", "before-second"));
    var duplicateSnapshotJournal = entry with
    {
        Snapshots = [entry.Snapshots[0], entry.Snapshots[0]]
    };
    AssertThrows<InvalidOperationException>(() => coordinator.ArmMutation(duplicateSnapshotJournal, plan));
    var armed = coordinator.ArmMutation(entry, plan);
    Assert(armed.State == JournalOperationState.MutationMayHaveStarted,
        "arming requires consent and a snapshot for every plan item");
    Assert(armed.Events.Last().Message.Contains("não executa a mutação", StringComparison.Ordinal),
        "journal coordinator must not execute a real mutation");
}

static void RecoveryAssessmentIsReadOnly()
{
    var (_, _, plan) = CreateTwoItemPlan();
    var coordinator = new OperationJournalCoordinator(new PlanConsentService());
    var planned = coordinator.CreatePlanned(plan, "custom");
    var simulated = coordinator.MarkSimulationCompleted(planned);
    Assert(OperationRecoveryEvaluator.Assess(simulated).Status ==
        RecoveryAssessmentStatus.NoSystemChangeExpected,
        "simulation should not require system recovery");

    var interruptedWithoutSnapshot = planned with { State = JournalOperationState.Interrupted };
    var assessment = OperationRecoveryEvaluator.Assess(interruptedWithoutSnapshot);
    Assert(assessment.Status == RecoveryAssessmentStatus.SnapshotMissing,
        "interrupted operation without snapshots must be identified");
    Assert(!assessment.CanAutomaticallyRecover,
        "recovery assessment must never claim automatic rollback");
}

static void SyntheticJournalCannotCreateSnapshots()
{
    var (_, _, plan) = CreateTwoItemPlan();
    var coordinator = new OperationJournalCoordinator(new PlanConsentService());
    var entry = coordinator.CreatePlanned(plan, "custom", JournalDataOrigin.SyntheticDemonstration);
    AssertThrows<InvalidOperationException>(() =>
        coordinator.RecordSnapshot(entry, SampleSnapshot("demo.first", "fictional-state")));

    var completed = coordinator.MarkSimulationCompleted(entry);
    AssertThrows<InvalidOperationException>(() => coordinator.MarkSimulationCompleted(completed));
    var assessment = OperationRecoveryEvaluator.Assess(completed);
    Assert(assessment.Status == RecoveryAssessmentStatus.NoSystemChangeExpected,
        "synthetic simulation should not require recovery");
    Assert(assessment.Message.Contains("nenhuma alteração ou backup do Windows", StringComparison.Ordinal),
        "synthetic history must explicitly distinguish itself from a Windows backup");
}

static AdjustmentSnapshot SampleSnapshot(string adjustmentId, string value) =>
    new(
        adjustmentId,
        1,
        DateTimeOffset.UtcNow,
        new Dictionary<string, string> { ["state"] = value });

static async Task WithTemporaryDirectory(Func<string, Task> action)
{
    var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "CalixtoOptimizer.Core.Tests"));
    Directory.CreateDirectory(root);
    var directory = Path.Combine(root, Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);

    try
    {
        await action(directory);
    }
    finally
    {
        var fullPath = Path.GetFullPath(directory);
        var allowedPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (fullPath.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase) &&
            Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
        }
    }
}

static void ConsentRequiresEveryDecision()
{
    var (catalog, planner, plan) = CreateTwoItemPlan();
    var service = new PlanConsentService();
    AssertThrows<ArgumentException>(() => service.Record(plan, new Dictionary<string, bool>
    {
        ["demo.first"] = true
    }));
}

static void ConsentTracksIndividualDecisions()
{
    var (_, _, plan) = CreateTwoItemPlan();
    var service = new PlanConsentService();
    var receipt = service.Record(plan, new Dictionary<string, bool>
    {
        ["demo.first"] = true,
        ["demo.second"] = false
    });

    var validation = service.Validate(plan, receipt);
    Assert(validation.Status == ConsentValidationStatus.Declined, "a declined consequence must block consent");
    Assert(receipt.Decisions.Count == 2 && receipt.Decisions[0].Consequence.Contains("demo.first", StringComparison.Ordinal),
        "each consequence should be recorded separately");
    Assert(!receipt.CanAuthorizeRealApplication, "consent must not authorize a real Windows write");
}

static void ConsentRejectsStaleOrTamperedReceipt()
{
    var (_, planner, plan) = CreateTwoItemPlan();
    var service = new PlanConsentService();
    var decisions = new Dictionary<string, bool>
    {
        ["demo.first"] = true,
        ["demo.second"] = true
    };
    var receipt = service.Record(plan, decisions);
    var newPlan = planner.CreatePlan(
        ["demo.first", "demo.second"],
        new PlatformDescriptor(true, "x64", 26300));
    var tamperedReceipt = receipt with { ReceiptFingerprint = new string('0', 64) };
    var malformedReceipt = receipt with { ReceiptFingerprint = "not-hex" };

    Assert(service.Validate(newPlan, receipt).Status == ConsentValidationStatus.StalePlan,
        "a receipt from a previous plan instance must be stale");
    Assert(service.Validate(plan, malformedReceipt).Status == ConsentValidationStatus.InvalidReceipt,
        "malformed receipt fingerprints must fail safely");
    Assert(service.Validate(plan, tamperedReceipt).Status == ConsentValidationStatus.InvalidReceipt,
        "a modified receipt must fail integrity validation");
}

static (AdjustmentCatalog Catalog, AdjustmentPlanService Planner, AdjustmentPlan Plan) CreateTwoItemPlan()
{
    var catalog = new AdjustmentCatalog([
        SampleAdjustment("demo.first") with { Consequence = "Consequence for demo.first." },
        SampleAdjustment("demo.second") with { Consequence = "Consequence for demo.second." }
    ]);
    var compatibility = new AdjustmentCompatibilityEvaluator();
    var planner = new AdjustmentPlanService(catalog, compatibility);
    var plan = planner.CreatePlan(
        ["demo.first", "demo.second"],
        new PlatformDescriptor(true, "x64", 26300));
    return (catalog, planner, plan);
}

static void PlanFingerprintBindsConsequences()
{
    var firstDefinition = SampleAdjustment("demo.same", AdjustmentRiskLevel.Low) with { Consequence = "First consequence." };
    var secondDefinition = SampleAdjustment("demo.same", AdjustmentRiskLevel.Moderate) with { Consequence = "Second consequence." };
    var compatibility = new AdjustmentCompatibilityEvaluator();
    var platform = new PlatformDescriptor(true, "x64", 26300);
    var first = new AdjustmentPlanService(new AdjustmentCatalog([firstDefinition]), compatibility)
        .CreatePlan(["demo.same"], platform);
    var second = new AdjustmentPlanService(new AdjustmentCatalog([secondDefinition]), compatibility)
        .CreatePlan(["demo.same"], platform);

    Assert(first.Fingerprint != second.Fingerprint, "plan fingerprint must change when consequence or risk changes");
}

static void ProfileCatalogHasFourProfiles()
{
    var profiles = OptimizerProfileCatalog.CreateDefault().Profiles;
    Assert(profiles.Count == 4, "four built-in profiles should exist");
    Assert(profiles.Select(profile => profile.Kind).ToHashSet().SetEquals(
        [OptimizerProfileKind.Light, OptimizerProfileKind.Balanced, OptimizerProfileKind.Extreme, OptimizerProfileKind.Custom]),
        "profile kinds are incomplete");
}

static void ProfilesRespectRiskLimits()
{
    var catalog = new AdjustmentCatalog([
        SampleAdjustment("demo.low", AdjustmentRiskLevel.Low),
        SampleAdjustment("demo.moderate", AdjustmentRiskLevel.Moderate),
        SampleAdjustment("demo.high", AdjustmentRiskLevel.High)
    ]);
    var compatibility = new AdjustmentCompatibilityEvaluator();
    var plans = new AdjustmentPlanService(catalog, compatibility);
    var service = new ProfilePlanService(
        OptimizerProfileCatalog.CreateDefault(),
        catalog,
        compatibility,
        plans);
    var platform = new PlatformDescriptor(true, "x64", 26300);

    var light = service.CreatePlan("light", platform);
    var balanced = service.CreatePlan("balanced", platform);
    Assert(light.Plan.Items.Select(item => item.AdjustmentId).SequenceEqual(["demo.low"]),
        "light profile should suggest low risk only");
    Assert(balanced.Plan.Items.Select(item => item.AdjustmentId).SequenceEqual(["demo.low", "demo.moderate"]),
        "balanced profile should suggest low and moderate risk");
    Assert(light.RequiresManualReview && balanced.RequiresManualReview,
        "every profile plan must require manual review");
}

static void ExtremeRequiresExplicitSelection()
{
    var catalog = new AdjustmentCatalog([
        SampleAdjustment("demo.low", AdjustmentRiskLevel.Low),
        SampleAdjustment("demo.high", AdjustmentRiskLevel.High)
    ]);
    var compatibility = new AdjustmentCompatibilityEvaluator();
    var plans = new AdjustmentPlanService(catalog, compatibility);
    var service = new ProfilePlanService(
        OptimizerProfileCatalog.CreateDefault(),
        catalog,
        compatibility,
        plans);
    var platform = new PlatformDescriptor(true, "x64", 26300);

    var empty = service.CreatePlan("extreme", platform);
    var explicitChoice = service.CreatePlan("extreme", platform, ["demo.high"]);
    Assert(empty.Plan.Items.Count == 0, "extreme must never select all items by default");
    Assert(explicitChoice.Plan.Items.Select(item => item.AdjustmentId).SequenceEqual(["demo.high"]),
        "extreme should include only explicitly selected items");
}

static void CustomUsesOnlyExplicitSelection()
{
    var catalog = new AdjustmentCatalog([
        SampleAdjustment("demo.low", AdjustmentRiskLevel.Low),
        SampleAdjustment("demo.moderate", AdjustmentRiskLevel.Moderate)
    ]);
    var compatibility = new AdjustmentCompatibilityEvaluator();
    var plans = new AdjustmentPlanService(catalog, compatibility);
    var service = new ProfilePlanService(
        OptimizerProfileCatalog.CreateDefault(),
        catalog,
        compatibility,
        plans);

    var result = service.CreatePlan("custom", new PlatformDescriptor(true, "x64", 26300), ["demo.moderate"]);
    Assert(result.Plan.Items.Select(item => item.AdjustmentId).SequenceEqual(["demo.moderate"]),
        "custom profile must use only explicit selections");
    Assert(result.SuggestedAdjustmentIds.Count == 0, "custom profile must not add suggestions");
}
static void AssertThrows<TException>(Action action) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

static async Task AssertThrowsAsync<TException>(Func<Task> action) where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

static async Task<TException> CaptureExceptionAsync<TException>(Func<Task> action) where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException exception)
    {
        return exception;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class RecordingSimulatedAdapter(
    Func<AdjustmentDefinition, CancellationToken, Task> run) : ISimulatedAdjustmentAdapter
{
    public List<string> Calls { get; } = [];

    public Task SimulateAsync(AdjustmentDefinition adjustment, CancellationToken cancellationToken)
    {
        Calls.Add(adjustment.Id);
        return run(adjustment, cancellationToken);
    }
}

sealed class FakeVideoIdlePowerAdapter(uint initialSeconds) : IVideoIdlePowerAdapter
{
    public Guid SchemeId { get; } = Guid.Parse("11111111-2222-3333-4444-555555555555");
    public Guid ActiveSchemeId { get; set; } = Guid.Parse("11111111-2222-3333-4444-555555555555");
    public uint AcTimeoutSeconds { get; set; } = initialSeconds;
    public uint DcTimeoutSeconds { get; set; } = 300;
    public List<uint> Writes { get; } = [];
    public int Activations { get; private set; }
    public int ReadCount { get; private set; }
    public int? ChangeAcValueOnReadNumber { get; set; }
    public int? ChangeActiveSchemeOnReadNumber { get; set; }
    public uint ChangedAcValue { get; set; }
    public bool ThrowOnNextActivation { get; set; }

    public VideoIdlePowerState ReadActiveAcTimeout()
    {
        ReadCount++;
        if (ChangeAcValueOnReadNumber == ReadCount)
            AcTimeoutSeconds = ChangedAcValue;
        if (ChangeActiveSchemeOnReadNumber == ReadCount)
            ActiveSchemeId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        return new VideoIdlePowerState(ActiveSchemeId, AcTimeoutSeconds);
    }

    public void WriteAcTimeout(Guid schemeId, uint seconds)
    {
        Writes.Add(seconds);
        if (schemeId == ActiveSchemeId)
            AcTimeoutSeconds = seconds;
    }

    public void ActivateScheme(Guid schemeId)
    {
        Activations++;
        if (ThrowOnNextActivation)
        {
            ThrowOnNextActivation = false;
            throw new IOException("Injected post-write interruption.");
        }
        ActiveSchemeId = schemeId;
    }
}
sealed class FakePowerModeAdapter(UserConfiguredPowerMode acMode, UserConfiguredPowerMode dcMode) : IPowerModeAdapter
{
    public UserConfiguredPowerMode AcMode { get; set; } = acMode;
    public UserConfiguredPowerMode DcMode { get; set; } = dcMode;
    public Guid? AcModeIdOverride { get; set; }
    public Guid? DcModeIdOverride { get; set; }
    public uint? AcErrorCode { get; set; }
    public uint? DcErrorCode { get; set; }
    public bool ApiAvailable { get; set; } = true;
    public bool ThrowAfterAcWrite { get; set; }
    public Action? AfterAcWrite { get; set; }
    public List<(string Side, UserConfiguredPowerMode Mode)> Writes { get; } = [];

    public PowerModeReadResult ReadConfiguredModes() => new(
        DateTimeOffset.UtcNow,
        ApiAvailable,
        AcErrorCode is null ? AcModeIdOverride ?? UserConfiguredPowerModes.ToId(AcMode) : null,
        DcErrorCode is null ? DcModeIdOverride ?? UserConfiguredPowerModes.ToId(DcMode) : null,
        AcErrorCode,
        DcErrorCode,
        ApiAvailable ? "Simulated Windows 11 API" : "Simulated unsupported API");

    public void SetConfiguredAcMode(UserConfiguredPowerMode mode)
    {
        Writes.Add(("AC", mode));
        if (AcErrorCode is null) AcMode = mode;
        AfterAcWrite?.Invoke();
        AfterAcWrite = null;
        if (ThrowAfterAcWrite)
        {
            ThrowAfterAcWrite = false;
            throw new IOException("Injected interruption after AC write.");
        }
    }

    public void SetConfiguredDcMode(UserConfiguredPowerMode mode)
    {
        Writes.Add(("DC", mode));
        if (DcErrorCode is null) DcMode = mode;
    }
}
enum JournalWriteFailure
{
    None,
    Io,
    DiskFull,
    Interruption
}

sealed class FaultInjectingJournalStorage : IOperationJournalStorage
{
    private readonly PhysicalOperationJournalStorage _inner = new();

    public JournalWriteFailure NextWriteFailure { get; set; }
    public bool DirectoryExists(string path) => _inner.DirectoryExists(path);
    public void CreateDirectory(string path) => _inner.CreateDirectory(path);
    public bool FileExists(string path) => _inner.FileExists(path);
    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) =>
        _inner.EnumerateFiles(path, searchPattern, searchOption);

    public async Task WriteAtomicAsync(string path, ReadOnlyMemory<byte> contents, CancellationToken cancellationToken)
    {
        var failure = NextWriteFailure;
        NextWriteFailure = JournalWriteFailure.None;
        if (failure != JournalWriteFailure.None)
        {
            var partialPath = path + ".injected.tmp";
            var partialLength = Math.Max(1, contents.Length / 2);
            await File.WriteAllBytesAsync(partialPath, contents[..partialLength].ToArray(), cancellationToken);

            if (failure == JournalWriteFailure.DiskFull)
                throw new IOException("Injected disk-full condition.", unchecked((int)0x80070070));
            if (failure == JournalWriteFailure.Interruption)
                throw new OperationCanceledException("Injected interruption.");
            throw new IOException("Injected storage write failure.");
        }

        await _inner.WriteAtomicAsync(path, contents, cancellationToken);
    }

    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken) =>
        _inner.ReadAllBytesAsync(path, cancellationToken);
}
