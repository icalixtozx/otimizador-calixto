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
    ("simulator reports cancellation and leaves remaining items unrun", SimulatorReportsInterruption)
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
