using Optimizer.Core.Adjustments;
using Optimizer.Core.Inventory;
using Optimizer.Core.Profiles;
using Optimizer.Core.Consent;
using Optimizer.Core.Recovery;
using Optimizer.Core.Network;
using Optimizer.Core.Processes;
using Optimizer.Core.Registry;
using Optimizer.Core.Programs;
using Optimizer.App.ViewModels;
using Optimizer.Windows;
using System.IO;
using System.Net;
using System.IO.Compression;
using System.Xml.Linq;

if (args.Length == 1 && args[0] == "--windows-registry-integration")
{
    if (!OperatingSystem.IsWindows())
    {
        Console.Error.WriteLine("SKIP: a integração de Registro exige Windows.");
        return 2;
    }
    return WindowsRegistryIntegrationProbe.Run();
}

if (args.Length == 1 && args[0] == "--registry-management-readonly")
{
    if (!OperatingSystem.IsWindows())
    {
        Console.Error.WriteLine("SKIP: a consulta de gerenciamento exige Windows.");
        return 2;
    }
    return RegistryManagementReadOnlyProbe.Run();
}

if (args.Length == 1 && args[0] == "--power-mode-history-readonly")
{
    var historyDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CalixtoOptimizer",
        "History");
    var viewModel = new PowerModePanelViewModel(
        new FakePowerModeAdapter(UserConfiguredPowerMode.Balanced, UserConfiguredPowerMode.Balanced),
        historyDirectory);
    await viewModel.LoadHistoryAsync();
    Console.WriteLine(viewModel.HistoryLoadStatusMessage);
    foreach (var item in viewModel.History)
        Console.WriteLine($"{item.StateLabel} · {item.Date}");
    return viewModel.HistoryLoadSucceeded ? 0 : 1;
}

if (args.Length == 1 && args[0] == "--diagnostic-export-save-readonly")
    return await DiagnosticExportReadOnlyProbe.RunAsync();

var tests = new (string Name, Func<Task> Run)[]
{
    ("shared WPF theme pins readable contrast for controls, selection, disabled states, and dialogs", () => RunSync(SharedThemePinsReadableControlStates)),
    ("Windows 11 identity prefers matching CIM and build over stale ProductName", () => RunSync(Windows11IdentityUsesCimAndBuild)),
    ("Windows 10 identity uses caption, NT version, and build", () => RunSync(Windows10IdentityUsesBuild)),
    ("Windows identity marks conflicting caption and build as ambiguous", () => RunSync(WindowsIdentityMarksConflictingSources)),
    ("Windows identity marks incompatible client version or product type as ambiguous", () => RunSync(WindowsIdentityMarksConflictingPlatformFacts)),
    ("Windows identity reports inference and does not misclassify a server", () => RunSync(WindowsIdentityReportsInferenceAndPreservesServer)),
    ("Windows identity prefers live CIM build when registry build differs", () => RunSync(WindowsIdentityPrefersCimBuild)),
    ("search is case-insensitive and excludes unrelated rows", () => RunSync(SearchFiltersRows)),
    ("signature filter keeps unknown separate", () => RunSync(SignatureFilterKeepsUnknownSeparate)),
    ("CSV escapes fields and omits device identifiers", () => RunSync(CsvIsSanitized)),
    ("CSV neutralizes spreadsheet formulas", () => RunSync(CsvNeutralizesFormulaCells)),
    ("official driver candidates match exact hardware IDs and show installed metadata", () => RunSync(DriverCandidatesMatchExactHardwareIds)),
    ("official driver candidate ambiguity is explicit and blocks installation", () => RunSync(DriverCandidateAmbiguityBlocksInstallation)),
    ("local package recognition uses the reviewed archive SHA-256 only", () => RunSync(LocalDriverPackageRecognitionUsesHash)),
    ("generic ZIP analysis verifies exact INF matches and disposes only its owned extraction", GenericDriverPackageZipAnalysisIsBoundedAndScoped),
    ("generic ZIP analysis blocks traversal and archive size-limit violations", GenericDriverPackageZipRejectsUnsafeAndOversizedInputs),
    ("generic package analysis separates compatible, incompatible, ambiguous, and incomplete candidates", GenericDriverPackageOutcomesAreConservative),
    ("INF payload checks follow only applicable DDInstall CopyFiles sections", GenericInfDependenciesFollowApplicableInstallSections),
    ("INF analysis blocks unverified AddSoftware and co-installer registration effects", GenericInfAnalysisBlocksUnverifiedInstallEffects),
    ("INF analysis blocks unresolved required Windows Include and Needs dependencies", GenericInfAnalysisBlocksMissingSystemDependencies),
    ("system INF dependencies resolve recursively with bounded cycle detection", SystemInfDependenciesResolveRecursivelyAndDetectCycles),
    ("system INF dependencies retain explicitly decorated Needs sections", SystemInfDependenciesResolveDecoratedSections),
    ("system INF dependency resolver enforces depth, file, and byte limits", SystemInfDependencyResolverEnforcesLimits),
    ("directed driver selection refuses lower-ranked and tied candidates", () => RunSync(DriverStoreSelectionUsesUniqueBestRank)),
    ("INF model entries distinguish install section from Hardware and Compatible IDs", GenericInfModelMatchingUsesOnlyDeviceIds),
    ("local INF analysis reads package files without extraction", LocalInfAnalysisIsReadOnly),
    ("auxiliary INF files are visible as incomplete and cannot become driver previews", AuxiliaryInfIsNotDriverCandidate),
    ("driver install preview reuses consent and history while default mutation policy blocks the backend", DriverInstallPreviewAndDefaultPolicyAreSafe),
    ("directed driver association revalidates target and separates stage failure, refusal, and restart", DirectedDriverAssociationFailsClosedAndTracksRestart),
    ("diagnostic JSON omits hardware identifiers and keeps PnP codes neutral", () => RunSync(DiagnosticJsonIsSanitizedAndNeutral)),
    ("diagnostic JSON keeps provenance, partial state, divergence, and explicit unknown fields while redacting identifiers", () => RunSync(DiagnosticJsonPreservesProvenanceAndUnknowns)),
    ("diagnostic report save handles cancellation and overwrite confirmation", () => RunSync(DiagnosticReportSaveCancellationAndOverwrite)),
    ("diagnostic report save verifies exact content and fails closed on write errors", () => RunSync(DiagnosticReportSaveVerifiesContentAndFailures)),
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
    ("journal state numeric values remain compatible with existing history", () => RunSync(JournalEnumValuesRemainStable)),
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
    ("power-mode capture refuses incomplete AC/DC/plan baselines", PowerModeCaptureRequiresFullBaseline),
    ("power-mode application requires consequence consent for every selected side", PowerModeApplyRequiresSpecificConsent),
    ("power-mode application and restoration verify independent AC/DC values", PowerModeApplyAndRevertVerifyEachSide),
    ("power-mode application detects state conflicts before writing", PowerModeApplyDetectsConflict),
    ("power-mode application rereads and refuses a changed active plan", PowerModeApplyDetectsActivePlanConflict),
    ("power-mode application verifies an unselected DC preference is preserved", PowerModeApplyVerifiesUnselectedDc),
    ("power-mode application verifies the active plan remains unchanged", PowerModeApplyVerifiesActivePlan),
    ("power-mode interrupted partial apply is persisted and conditionally recoverable", PowerModeInterruptionCanBeConditionallyReverted),
    ("power-mode restoration refuses third-party changes", PowerModeRevertRefusesExternalChange),
    ("power-mode restoration refuses a changed active plan", PowerModeRevertRefusesChangedActivePlan),
    ("power-mode journal failure before first write leaves fake Windows unchanged", PowerModeJournalFailurePreventsWrite),
    ("power-mode store rejects tampered captures", PowerModeStoreRejectsTamperedCapture),
    ("power-mode store rejects an unsupported persisted state", PowerModeStoreRejectsUnsupportedState),
    ("power-mode history view model surfaces persisted apply and restoration states", PowerModeHistoryViewModelShowsVerifiedCycle),
    ("power-mode history load failure is visible and never presented as confirmed empty", PowerModeHistoryViewModelSurfacesLoadFailure),
    ("network diagnostic classifies successful probes and reports bounded measurements", NetworkSuccessReportsMeasurements),
    ("network diagnostic separates DNS failure from probe failure", NetworkDnsFailureIsDistinct),
    ("network diagnostic reports ICMP timeouts without claiming internet loss", NetworkTimeoutIsInconclusive),
    ("network diagnostic distinguishes unreachable errors from timeouts", NetworkProbeFailureIsDistinct),
    ("network diagnostic identifies missing local link before active traffic", NetworkNoConnectionSkipsProbe),
    ("network diagnostic distinguishes local inventory failure from no connection", NetworkAdapterReadFailureIsNotOffline),
    ("network diagnostic preserves partial responses and loss counts", NetworkPartialResultsArePreserved),
    ("network diagnostic cancellation returns completed partial measurements", NetworkCancellationPreservesPartialResults),
    ("network diagnostic rejects URLs and unbounded probe settings", NetworkInputIsBounded),
    ("process priority review is individual, consequence-bound, and excludes realtime", () => RunSync(ProcessReviewIsIndividualAndBounded)),
    ("process priority capture uses shared consent and history while default writes stay blocked", ProcessCaptureUsesSharedJournalAndDefaultBlocksWrites),
    ("process priority access denied and ended processes cannot be prepared", ProcessAccessDeniedAndEndedAreRefused),
    ("process priority conflict and PID reuse refuse writes", ProcessConflictAndPidReuseRefuseWrites),
    ("process priority journal failure before write leaves the adapter untouched", ProcessJournalFailurePreventsWrite),
    ("process priority interruption after write remains unverified without auto-restore", ProcessPostWriteJournalFailureDoesNotRollback),
    ("process priority restoration is conditional and verified for the same process", ProcessRestoreIsConditionalAndVerified),
    ("process priority restoration rejects external changes and ended targets", ProcessRestoreRejectsConflictAndExit),
    ("registry preference plan is specific, consequence-bound, and x64-only", () => RunSync(RegistryPreferencePlanIsSpecificAndBounded)),
    ("registry capture distinguishes missing value and reuses shared consent and history", RegistryCapturePreservesAbsence),
    ("registry capture refuses state drift since the reviewed preview", RegistryCaptureRefusesStaleReview),
    ("registry preference restores exact previous registry data types", RegistryRestorePreservesTypedValues),
    ("registry restore removes only a value created from an absent snapshot", RegistryRestoreDeletesOnlyCreatedValue),
    ("registry access denial is recorded without attempting a write", RegistryAccessDeniedPreventsWrite),
    ("registry state drift blocks apply and external restore conflicts", RegistryConflictsRefuseWrites),
    ("registry rechecks state after persisting intent before either setter", RegistryCheckpointDriftRefusesSetter),
    ("registry management unknown blocks apply", RegistryUnknownManagementBlocksApply),
    ("registry management keeps domain, Entra, and MDM context separate from preference policy", () => RunSync(RegistryManagementContextDoesNotProvePreferencePolicy)),
    ("registry management identifies only exact RSoP evidence as policy management", () => RunSync(RegistryManagementExactPolicyEvidenceIsSpecific)),
    ("registry management preserves unavailable, unknown, and access-denied outcomes", () => RunSync(RegistryManagementUncertainEvidenceIsConservative)),
    ("registry policy evidence must match the exact reviewed key and value", () => RunSync(RegistryPolicyEvidenceMatchesOnlyExactPreference)),
    ("registry production policy blocks writes by default", RegistryDefaultPolicyBlocksWrites),
    ("registry journal persistence failure happens before the setter", RegistryJournalFailurePreventsWrite),
    ("registry interruption is persisted without automatic rollback", RegistryInterruptionRemainsManual),
    ("registry missing parent key is never created", RegistryMissingParentIsNotCreated),
    ("post-format catalog uses only reviewed exact WinGet IDs and dated manifests", () => RunSync(ProgramCatalogIsBoundedAndDated)),
    ("WinGet source registration status is clear and does not imply install readiness", () => RunSync(ProgramSourceStatusIsConservative)),
    ("installed program detection requires exact name and publisher and preserves uncertainty", () => RunSync(ProgramInstalledDetectionIsConservative)),
    ("WinGet command factory rejects unknown packages and emits only fixed reviewed arguments", () => RunSync(ProgramCommandFactoryIsBounded)),
    ("program install execution is blocked by the production policy without starting WinGet", ProgramProductionPolicyBlocksRunner),
    ("program install requires package-bound consequence and agreement consent", ProgramInstallConsentIsPackageBound),
    ("missing WinGet manager creates an individual unavailable result for selected packages", ProgramInstallReportsManagerUnavailable),
    ("WinGet exact package-not-found code is kept distinct", ProgramInstallMapsMissingPackage),
    ("WinGet no-network result is reported without claiming internet failure", ProgramInstallMapsNetworkUnavailable),
    ("WinGet exit code zero is reported but never treated as verified installation", ProgramInstallZeroExitCodeRemainsUnverified),
    ("program installs preserve individual results after a partial package failure", ProgramInstallContinuesAfterPackageFailure),
    ("cancelled WinGet process records uncertain current state and leaves remainder unrun", ProgramInstallCancellationLeavesUnknownPartialState),
    ("restart-required WinGet result pauses the remaining package selection", ProgramInstallRestartRequiredPausesRemainder),
    ("program install request rejects arbitrary package IDs", ProgramInstallRejectsUnknownId)
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

static void SharedThemePinsReadableControlStates()
{
    var repositoryRoot = FindRepositoryRoot();
    XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    var appXaml = XDocument.Load(Path.Combine(repositoryRoot, "src", "Optimizer.App", "App.xaml"));
    var resources = appXaml.Root?.Element(presentation + "Application.Resources")
        ?? throw new InvalidOperationException("Application.Resources was not found in App.xaml.");
    var styles = resources.Elements(presentation + "Style").ToArray();

    XElement? FindStyle(string targetType, string? key = null) => styles.FirstOrDefault(style =>
        string.Equals((string?)style.Attribute("TargetType"), targetType, StringComparison.Ordinal)
        && string.Equals((string?)style.Attribute(xaml + "Key"), key, StringComparison.Ordinal));

    var textBlockStyle = FindStyle("TextBlock")
        ?? throw new InvalidOperationException("Implicit TextBlock style is missing.");
    Assert(!textBlockStyle.Elements(presentation + "Setter").Any(setter =>
            string.Equals((string?)setter.Attribute("Property"), "Foreground", StringComparison.Ordinal)),
        "TextBlock default style must inherit foreground from dark/light parent surfaces.");

    foreach (var targetType in new[]
             {
                 "Button", "TextBox", "ComboBox", "ComboBoxItem", "ListBox", "ListBoxItem",
                 "CheckBox", "RadioButton", "TabControl", "TabItem", "DataGrid", "DataGridRow",
                 "DataGridCell", "ToolTip", "ContextMenu", "MenuItem"
             })
        Assert(FindStyle(targetType) is not null, $"implicit theme style missing for {targetType}");

    var brushes = resources.Elements(presentation + "SolidColorBrush")
        .Where(brush => (string?)brush.Attribute(xaml + "Key") is { } key && !key.StartsWith("{", StringComparison.Ordinal))
        .ToDictionary(brush => (string)brush.Attribute(xaml + "Key")!, brush => (string)brush.Attribute("Color")!, StringComparer.Ordinal);
    Assert(ContrastRatio(brushes["Ink"], brushes["Paper"]) >= 4.5, "primary text must contrast with the paper surface");
    Assert(ContrastRatio(brushes["Muted"], brushes["Paper"]) >= 4.5, "muted text must remain readable on paper");
    Assert(ContrastRatio(brushes["DisabledInk"], brushes["DisabledSurface"]) >= 4.5, "disabled text must remain readable");
    Assert(ContrastRatio(brushes["SelectedText"], brushes["SelectedSurface"]) >= 4.5, "selected text must contrast with selection fill");

    var systemColorKeys = resources.Elements(presentation + "SolidColorBrush")
        .Select(brush => (string?)brush.Attribute(xaml + "Key") ?? string.Empty)
        .ToArray();
    foreach (var key in new[] { "WindowBrushKey", "WindowTextBrushKey", "ControlBrushKey", "ControlTextBrushKey", "HighlightBrushKey", "HighlightTextBrushKey", "GrayTextBrushKey" })
        Assert(systemColorKeys.Any(value => value.Contains(key, StringComparison.Ordinal)), $"WPF system color {key} should be pinned by the app palette");

    foreach (var key in new[] { "QuietButton", "PrimaryButton" })
    {
        var style = FindStyle("Button", key) ?? throw new InvalidOperationException($"{key} style is missing.");
        var disabled = style.Descendants(presentation + "Trigger").FirstOrDefault(trigger =>
            string.Equals((string?)trigger.Attribute("Property"), "IsEnabled", StringComparison.Ordinal)
            && string.Equals((string?)trigger.Attribute("Value"), "False", StringComparison.Ordinal));
        Assert(disabled is not null, $"{key} needs an explicit disabled state");
        Assert(disabled!.Elements(presentation + "Setter").Any(setter => string.Equals((string?)setter.Attribute("Property"), "Foreground", StringComparison.Ordinal)),
            $"{key} disabled state needs an explicit foreground");
        Assert(disabled.Elements(presentation + "Setter").Any(setter => string.Equals((string?)setter.Attribute("Property"), "Background", StringComparison.Ordinal)),
            $"{key} disabled state needs an explicit background");
        Assert(!disabled.Elements(presentation + "Setter").Any(setter => string.Equals((string?)setter.Attribute("Property"), "Opacity", StringComparison.Ordinal)),
            $"{key} must not make disabled labels unreadable by fading the whole button");
    }

    foreach (var targetType in new[] { "ComboBoxItem", "ListBoxItem", "TabItem", "DataGridRow", "DataGridCell" })
    {
        var style = FindStyle(targetType) ?? throw new InvalidOperationException($"{targetType} style is missing.");
        var selected = style.Descendants(presentation + "Trigger").FirstOrDefault(trigger =>
            string.Equals((string?)trigger.Attribute("Property"), "IsSelected", StringComparison.Ordinal)
            && string.Equals((string?)trigger.Attribute("Value"), "True", StringComparison.Ordinal));
        Assert(selected is not null, $"{targetType} needs a deterministic selected state");
        Assert(selected!.Elements(presentation + "Setter").Any(setter => string.Equals((string?)setter.Attribute("Property"), "Foreground", StringComparison.Ordinal)),
            $"{targetType} selected state needs an explicit foreground");
        Assert(selected.Elements(presentation + "Setter").Any(setter => string.Equals((string?)setter.Attribute("Property"), "Background", StringComparison.Ordinal)),
            $"{targetType} selected state needs an explicit background");
    }

    var previewXaml = XDocument.Load(Path.Combine(repositoryRoot, "src", "Optimizer.App", "Views", "DiagnosticExportPreviewWindow.xaml"));
    var previewWindow = previewXaml.Root ?? throw new InvalidOperationException("Diagnostic preview Window was not found.");
    Assert((string?)previewWindow.Attribute("Foreground") == "{StaticResource Ink}", "diagnostic preview dialog must pin its foreground");
    Assert(previewWindow.Descendants(presentation + "TextBox").Any(input =>
            (string?)input.Attribute("Foreground") == "{StaticResource Ink}"
            && (string?)input.Attribute("Background") == "{StaticResource Paper}"),
        "diagnostic preview text field must have explicit contrasting colors");

    var messageDialogXaml = XDocument.Load(Path.Combine(repositoryRoot, "src", "Optimizer.App", "Views", "AppMessageDialog.xaml"));
    var messageDialog = messageDialogXaml.Root ?? throw new InvalidOperationException("Themed message dialog Window was not found.");
    Assert((string?)messageDialog.Attribute("Foreground") == "{StaticResource Ink}"
           && (string?)messageDialog.Attribute("Background") == "{StaticResource Canvas}",
        "in-app information and overwrite dialogs must use the shared palette");
    Assert(messageDialog.Descendants(presentation + "Button").Any(button => (string?)button.Attribute("Style") == "{StaticResource SecondaryButton}")
           && messageDialog.Descendants(presentation + "Button").Any(button => (string?)button.Attribute("Style") == "{StaticResource PrimaryButton}"),
        "themed dialog must retain explicit readable confirm and cancel actions");

    static double ContrastRatio(string foreground, string background)
    {
        static double Luminance(string color)
        {
            var red = Convert.ToInt32(color.Substring(1, 2), 16) / 255d;
            var green = Convert.ToInt32(color.Substring(3, 2), 16) / 255d;
            var blue = Convert.ToInt32(color.Substring(5, 2), 16) / 255d;
            static double Linearize(double channel) => channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
            return 0.2126 * Linearize(red) + 0.7152 * Linearize(green) + 0.0722 * Linearize(blue);
        }

        var first = Luminance(foreground);
        var second = Luminance(background);
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }
}

static string FindRepositoryRoot()
{
    foreach (var startingPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startingPath));
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CalixtoOptimizer.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
    }

    throw new DirectoryNotFoundException("Could not locate CalixtoOptimizer.sln for shared-theme regression checks.");
}

static void Windows11IdentityUsesCimAndBuild()
{
    var resolved = WindowsReleaseIdentityResolver.Resolve(new WindowsIdentitySources(
        "Microsoft Windows 11 Pro", "10.0.26300", 26300, 1, 48,
        "Windows 10 Pro", "Professional", "26H2", 26300, 9711));

    Assert(resolved.Name == "Windows 11", "CIM caption and build should identify Windows 11");
    Assert(resolved.Edition == "Professional" && resolved.Version == "26H2", "edition and display version should be retained");
    Assert(resolved.Build == "26300.9711", "matching registry UBR should complete the CIM build");
    Assert(resolved.Warnings.Any(warning => warning.Contains("ProductName no Registro informa Windows 10", StringComparison.Ordinal)),
        "stale registry product label should be shown as a source discrepancy");
    Assert(resolved.Warnings.Any(warning => warning.Contains("não confirma a inscrição", StringComparison.OrdinalIgnoreCase)),
        "Insider enrollment/channel must remain explicitly unknown");

    var skuOnlyEdition = WindowsReleaseIdentityResolver.Resolve(new WindowsIdentitySources(
        "Microsoft Windows 11 Pro", "10.0.26300", 26300, 1, 48,
        "Windows 10 Pro", null, "26H2", 26300, 9711));
    Assert(skuOnlyEdition.Edition == "Professional", "documented CIM SKU 48 should identify Professional when EditionID is absent");

    var editionConflict = WindowsReleaseIdentityResolver.Resolve(new WindowsIdentitySources(
        "Microsoft Windows 11 Pro", "10.0.26300", 26300, 1, 48,
        "Windows 11 Pro", "Enterprise", "26H2", 26300, 9711));
    Assert(editionConflict.Edition is null && editionConflict.Warnings.Any(warning => warning.Contains("A edição diverge", StringComparison.Ordinal)),
        "conflicting CIM caption, SKU, and registry edition should remain unconfirmed");
}

static void Windows10IdentityUsesBuild()
{
    var resolved = WindowsReleaseIdentityResolver.Resolve(new WindowsIdentitySources(
        "Microsoft Windows 10 Pro", "10.0.19045", 19045, 1, 48,
        "Windows 10 Pro", "Professional", "22H2", 19045, 6466));

    Assert(resolved.Name == "Windows 10", "matching caption and pre-22000 workstation build should identify Windows 10");
    Assert(!resolved.Warnings.Any(warning => warning.Contains("diverg", StringComparison.OrdinalIgnoreCase)),
        "matching Windows 10 sources should not warn about identity divergence");
}

static void WindowsIdentityMarksConflictingSources()
{
    var resolved = WindowsReleaseIdentityResolver.Resolve(new WindowsIdentitySources(
        "Microsoft Windows 11 Pro", "10.0.19045", 19045, 1, 48,
        "Windows 11 Pro", "Professional", "22H2", 19045, 6466));

    Assert(resolved.Name == "Windows (identificação divergente)", "incompatible caption and build must not produce a confident Windows 10/11 label");
    Assert(resolved.Warnings.Any(warning => warning.Contains("Caption CIM identifica Windows 11", StringComparison.Ordinal)),
        "the conflict warning should identify the source values");
}

static void WindowsIdentityMarksConflictingPlatformFacts()
{
    var incompatibleVersion = WindowsReleaseIdentityResolver.Resolve(new WindowsIdentitySources(
        "Microsoft Windows 11 Pro", "6.3.9600", 19045, 1, 48,
        "Windows 11 Pro", "Professional", "26H2", 19045, 6466));
    Assert(incompatibleVersion.Name == "Windows (identificação divergente)",
        "a Windows 11 caption should not override an incompatible NT version");

    var incompatibleType = WindowsReleaseIdentityResolver.Resolve(new WindowsIdentitySources(
        "Microsoft Windows 11 Pro", "10.0.26300", 26300, 3, 48,
        "Windows 11 Pro", "Professional", "26H2", 26300, 9711));
    Assert(incompatibleType.Name == "Windows (identificação divergente)",
        "a client Windows caption should not override a known server product type");
}

static void WindowsIdentityReportsInferenceAndPreservesServer()
{
    var inferred = WindowsReleaseIdentityResolver.Resolve(new WindowsIdentitySources(
        null, "10.0.26300", 26300, 1, 48, null, "Professional", "26H2", 26300, 9711));
    Assert(inferred.Name == "Windows 11", "a recognized client build should permit a labeled inference when CIM caption is absent");
    Assert(inferred.Warnings.Any(warning => warning.Contains("inferido", StringComparison.OrdinalIgnoreCase)),
        "build-only identification should disclose that it is inferred");

    var server = WindowsReleaseIdentityResolver.Resolve(new WindowsIdentitySources(
        "Microsoft Windows Server 2022 Standard", "10.0.20348", 20348, 3, 7,
        "Windows Server 2022 Standard", "ServerStandard", "21H2", 20348, 3207));
    Assert(server.Name == "Windows Server 2022 Standard", "server caption should not be converted to a client Windows 10 label");

    var registryOnly = WindowsReleaseIdentityResolver.Resolve(new WindowsIdentitySources(
        null, null, null, null, null, "Windows 10 Pro", "Professional", "26H2", 26300, 9711));
    Assert(registryOnly.Name == "Windows (identificação não confirmada)",
        "registry ProductName alone must not be displayed as a confirmed release");
}

static void WindowsIdentityPrefersCimBuild()
{
    var resolved = WindowsReleaseIdentityResolver.Resolve(new WindowsIdentitySources(
        "Microsoft Windows 11 Pro", "10.0.26300", 26300, 1, 48,
        "Windows 11 Pro", "Professional", "26H2", 26200, 9711));

    Assert(resolved.Name == "Windows 11" && resolved.Build == "26300",
        "live CIM build should win and stale registry UBR should not be appended");
    Assert(resolved.Warnings.Any(warning => warning.Contains("CIM (26300)", StringComparison.Ordinal)),
        "the CIM versus registry build mismatch should be explicit");
}

static void SearchFiltersRows()
{
    var rows = SampleDrivers();
    var result = DriverCatalog.Filter(rows, "sample graphics", DriverSignatureFilter.All);
    Assert(result.Count == 1, "case-insensitive filter should return one row");
    Assert(result[0].DeviceName == "Sample Graphics Adapter", "wrong row returned");
}

static async Task NetworkSuccessReportsMeasurements()
{
    var fake = FakeNetworkDiagnosticsAdapter.Connected();
    fake.Probes.Enqueue(new(NetworkProbeStatus.Success, 8));
    fake.Probes.Enqueue(new(NetworkProbeStatus.Success, 10));
    var service = new NetworkDiagnosticsService(fake);
    var progressValues = new List<NetworkProbeProgress>();

    var result = await service.TestConnectivityAsync("example.test", 2, 700,
        new InlineProgress<NetworkProbeProgress>(progressValues.Add));

    Assert(result.Outcome == NetworkConnectivityOutcome.Success, "all replies should be classified as success");
    Assert(result.ResolvedAddress?.ToString() == "198.51.100.20", "result should show the selected resolved IP");
    Assert(result.RequestedSamples == 2 && result.CompletedSamples == 2, "requested and completed samples should be explicit");
    Assert(result.TimeoutMilliseconds == 700 && result.DurationMilliseconds >= 0, "timeout and total duration should be recorded");
    Assert(result.MinimumRoundtripMilliseconds == 8 && result.MaximumRoundtripMilliseconds == 10, "RTT extrema should match fake replies");
    Assert(result.LossPercentage == 0 && progressValues.Count == 2, "zero loss and per-sample progress should be reported");
}

static async Task NetworkDnsFailureIsDistinct()
{
    var fake = FakeNetworkDiagnosticsAdapter.Connected();
    fake.ResolutionError = new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.HostNotFound);
    var result = await new NetworkDiagnosticsService(fake).TestConnectivityAsync("missing.invalid");

    Assert(result.Outcome == NetworkConnectivityOutcome.DnsResolutionFailed, "resolver failure should have its own outcome");
    Assert(fake.ProbeCount == 0 && result.CompletedSamples == 0, "no ICMP probe should be sent after DNS failure");
}

static async Task NetworkTimeoutIsInconclusive()
{
    var fake = FakeNetworkDiagnosticsAdapter.Connected();
    for (var index = 0; index < 3; index++) fake.Probes.Enqueue(new(NetworkProbeStatus.TimedOut));
    var result = await new NetworkDiagnosticsService(fake).TestConnectivityAsync("example.test", 3, 250);

    Assert(result.Outcome == NetworkConnectivityOutcome.NoIcmpResponses, "all timed-out probes should be classified distinctly");
    Assert(result.TimedOutSamples == 3 && result.LostResponses == 3 && result.LossPercentage == 100, "timeouts should count as unanswered samples");
    Assert(result.Summary.Contains("não comprova falta de internet", StringComparison.OrdinalIgnoreCase), "timeout must not be described as no internet");
}

static async Task NetworkProbeFailureIsDistinct()
{
    var fake = FakeNetworkDiagnosticsAdapter.Connected();
    fake.Probes.Enqueue(new(NetworkProbeStatus.DestinationUnreachable, StatusDetail: "DestinationNetworkUnreachable"));
    fake.Probes.Enqueue(new(NetworkProbeStatus.DestinationUnreachable, StatusDetail: "DestinationNetworkUnreachable"));
    var result = await new NetworkDiagnosticsService(fake).TestConnectivityAsync("example.test", 2);

    Assert(result.Outcome == NetworkConnectivityOutcome.ProbeFailed, "unreachable errors should not be classified as timeouts");
    Assert(result.TimedOutSamples == 0 && result.FailedSamples == 2, "native ICMP error counts should remain distinct");
}

static async Task NetworkNoConnectionSkipsProbe()
{
    var fake = FakeNetworkDiagnosticsAdapter.Connected(isOperational: false);
    var result = await new NetworkDiagnosticsService(fake).TestConnectivityAsync("example.test");

    Assert(result.Outcome == NetworkConnectivityOutcome.NoActiveConnection, "inactive local adapter should be identified before resolving");
    Assert(fake.ResolveCount == 0 && fake.ProbeCount == 0, "offline preflight should send neither DNS nor ICMP traffic");
}

static async Task NetworkAdapterReadFailureIsNotOffline()
{
    var fake = new FakeNetworkDiagnosticsAdapter(new NetworkInventoryResult(DateTimeOffset.UtcNow, [], ["synthetic read error"]));
    var result = await new NetworkDiagnosticsService(fake).TestConnectivityAsync("example.test");

    Assert(result.Outcome == NetworkConnectivityOutcome.AdapterReadFailed, "failed local inventory must not be described as disconnected");
    Assert(fake.ResolveCount == 0 && fake.ProbeCount == 0, "an incomplete local preflight must stop active traffic");
}

static async Task NetworkPartialResultsArePreserved()
{
    var fake = FakeNetworkDiagnosticsAdapter.Connected();
    fake.Probes.Enqueue(new(NetworkProbeStatus.Success, 12));
    fake.Probes.Enqueue(new(NetworkProbeStatus.TimedOut));
    fake.Probes.Enqueue(new(NetworkProbeStatus.DestinationUnreachable, StatusDetail: "DestinationHostUnreachable"));
    var result = await new NetworkDiagnosticsService(fake).TestConnectivityAsync("example.test", 3, 800);

    Assert(result.Outcome == NetworkConnectivityOutcome.PartialResponses, "one response and failures should be partial");
    Assert(result.CompletedSamples == 3 && result.RepliesReceived == 1 && result.LostResponses == 2, "partial counters should be internally consistent");
    Assert(result.TimedOutSamples == 1 && result.FailedSamples == 1, "timeout and other ICMP errors should remain separate");
    Assert(result.ProbeDetails.Contains("DestinationHostUnreachable"), "native status detail should be retained");
}

static async Task NetworkCancellationPreservesPartialResults()
{
    using var cancellation = new CancellationTokenSource();
    var fake = FakeNetworkDiagnosticsAdapter.Connected();
    fake.Probes.Enqueue(new(NetworkProbeStatus.Success, 6));
    fake.BeforeProbe = index =>
    {
        if (index == 2) cancellation.Cancel();
    };
    var result = await new NetworkDiagnosticsService(fake).TestConnectivityAsync(
        "example.test", 4, 1000, cancellationToken: cancellation.Token);

    Assert(result.Outcome == NetworkConnectivityOutcome.Cancelled, "user cancellation should be visible");
    Assert(result.CompletedSamples == 1 && result.RepliesReceived == 1, "completed sample must survive cancellation");
    Assert(fake.ProbeCount == 2, "no further probe should run after cancellation");
}

static async Task NetworkInputIsBounded()
{
    var service = new NetworkDiagnosticsService(FakeNetworkDiagnosticsAdapter.Connected());
    await AssertThrowsAsync<ArgumentException>(() => service.TestConnectivityAsync("https://example.test/path"));
    await AssertThrowsAsync<ArgumentOutOfRangeException>(() => service.TestConnectivityAsync("example.test", 11));
    await AssertThrowsAsync<ArgumentOutOfRangeException>(() => service.TestConnectivityAsync("example.test", 1, 6000));
}

static void ProcessReviewIsIndividualAndBounded()
{
    var (adapter, _, store, identity) = CreateProcessPriorityFixture(Path.Combine(Path.GetTempPath(), "unused-process-priority-review"));
    var service = CreateProcessPriorityService(adapter, store);
    var review = service.CreateReview(identity, ProcessPriorityLevel.AboveNormal,
        new PlatformDescriptor(true, "x64", 26100));
    var item = review.Plan.Items.Single();

    Assert(review.Plan.CanAcceptConsent && !review.Plan.CanSimulate && !review.Plan.CanApply,
        "review consent must not enable simulation or real application");
    Assert(item.Status == PlanItemStatus.ReadyForExplicitApproval && item.AdjustmentId == ProcessPriorityOperationService.AdjustmentId,
        "the engine must receive one reviewable individual process item");
    Assert(!ProcessPriorityTargets.Allowed.Contains(ProcessPriorityLevel.Realtime),
        "Realtime must not be an offered target");
    Assert(item.Consequence.Contains("não garante FPS", StringComparison.OrdinalIgnoreCase) &&
        item.Consequence.Contains("outros aplicativos", StringComparison.OrdinalIgnoreCase),
        "consequences must describe limits and impact on other applications");
    Assert(review.Identity.ProcessId == identity.ProcessId && review.Identity.CreatedAtUtc == identity.CreatedAtUtc,
        "review must bind PID and creation time");
    AssertThrows<ArgumentOutOfRangeException>(() => service.CreateReview(identity,
        ProcessPriorityLevel.Realtime, new PlatformDescriptor(true, "x64", 26100)));

    var unsupportedAdapter = new FakeProcessPriorityAdapter();
    unsupportedAdapter.SetObservation(ProcessObservationFor(identity, ProcessPriorityLevel.Unknown));
    var unsupportedService = CreateProcessPriorityService(unsupportedAdapter, store);
    AssertThrows<InvalidOperationException>(() => unsupportedService.CreateReview(identity,
        ProcessPriorityLevel.Normal, new PlatformDescriptor(true, "x64", 26100)));

    unsupportedAdapter.SetObservation(ProcessObservationFor(identity, ProcessPriorityLevel.Realtime));
    AssertThrows<InvalidOperationException>(() => unsupportedService.CreateReview(identity,
        ProcessPriorityLevel.Normal, new PlatformDescriptor(true, "x64", 26100)));
}

static async Task ProcessCaptureUsesSharedJournalAndDefaultBlocksWrites()
{
    await WithTemporaryDirectory(async directory =>
    {
        var (adapter, storage, store, identity) = CreateProcessPriorityFixture(directory);
        var service = CreateProcessPriorityService(adapter, store);
        var review = service.CreateReview(identity, ProcessPriorityLevel.AboveNormal,
            new PlatformDescriptor(true, "x64", 26100));
        var entry = await service.CaptureAsync(review, consequencesAccepted: true);

        Assert(entry.State == JournalOperationState.SnapshotsCaptured && entry.Consent is not null,
            "capturing must persist consent and the prior process state to the common journal");
        Assert(!entry.Consent!.CanAuthorizeRealApplication,
            "reviewed consequences alone cannot authorize a write");
        Assert(OperationRecoveryEvaluator.Assess(entry).Status == RecoveryAssessmentStatus.NoMutationRecorded,
            "the common recovery evaluator must confirm that capture preceded every mutation");
        var persisted = await store.LoadAsync(entry.OperationId);
        Assert(persisted?.Snapshots.Single().Values["PreviousPriority"] == nameof(ProcessPriorityLevel.Normal),
            "the persisted capture should contain the actual prior priority from the adapter");

        var approval = ProcessApproval(entry, ProcessPriorityAction.Apply);
        await AssertThrowsAsync<UnauthorizedAccessException>(() => service.ApplyAsync(entry.OperationId, approval));
        Assert(adapter.WriteCalls == 0, "the production default must deny all real setters");
        Assert(storage.NextWriteFailure == JournalWriteFailure.None, "normal capture must complete without storage faults");
    });
}

static async Task ProcessAccessDeniedAndEndedAreRefused()
{
    await WithTemporaryDirectory(async directory =>
    {
        var identity = SampleProcessIdentity();
        var deniedAdapter = new FakeProcessPriorityAdapter();
        deniedAdapter.SetObservation(new ProcessObservation(identity.ProcessId, "protected-sample", null, null,
            ProcessInspectionStatus.AccessDenied, "synthetic access denied"));
        var deniedService = CreateProcessPriorityService(deniedAdapter,
            new JsonOperationJournalStore(Path.Combine(directory, "denied")));
        Assert(deniedService.ListProcesses().Processes.Single().Status == ProcessInspectionStatus.AccessDenied,
            "listing should preserve access-denied state");
        AssertThrows<InvalidOperationException>(() => deniedService.CreateReview(identity,
            ProcessPriorityLevel.High, new PlatformDescriptor(true, "x64", 26100)));
        Assert(deniedAdapter.WriteCalls == 0, "access denied must never reach a setter");

        var accessChanges = new FakeProcessPriorityAdapter();
        accessChanges.SetObservation(ProcessObservationFor(identity, ProcessPriorityLevel.Normal));
        var accessService = CreateProcessPriorityService(accessChanges,
            new JsonOperationJournalStore(Path.Combine(directory, "access-changes")), allowWrites: true);
        var accessReview = accessService.CreateReview(identity, ProcessPriorityLevel.High,
            new PlatformDescriptor(true, "x64", 26100));
        var accessCapture = await accessService.CaptureAsync(accessReview, true);
        accessChanges.SetObservation(new ProcessObservation(identity.ProcessId, "protected-sample", null, null,
            ProcessInspectionStatus.AccessDenied, "synthetic access denied after capture"));
        var accessFailure = await accessService.ApplyAsync(accessCapture.OperationId,
            ProcessApproval(accessCapture, ProcessPriorityAction.Apply));
        Assert(accessFailure.State == JournalOperationState.SnapshotsCaptured &&
            accessFailure.Events.Last().EventType == "WriteNotAttempted" && accessChanges.WriteCalls == 0,
            "access denial after capture must be journaled as a pre-write failure, not misreported as a state conflict");

        var ended = new FakeProcessPriorityAdapter();
        ended.SetObservation(ProcessObservationFor(identity, ProcessPriorityLevel.Normal));
        var endedStore = new JsonOperationJournalStore(Path.Combine(directory, "ended"));
        var service = CreateProcessPriorityService(ended, endedStore, allowWrites: true);
        var review = service.CreateReview(identity, ProcessPriorityLevel.High,
            new PlatformDescriptor(true, "x64", 26100));
        var capture = await service.CaptureAsync(review, true);
        ended.SetObservation(new ProcessObservation(identity.ProcessId, "probe", identity, null,
            ProcessInspectionStatus.Exited, "synthetic process ended"));
        var result = await service.ApplyAsync(capture.OperationId, ProcessApproval(capture, ProcessPriorityAction.Apply));
        Assert(result.State == JournalOperationState.Conflict && ended.WriteCalls == 0,
            "an ended process should be recorded without any write attempt");
        Assert(result.Events.Last().Message.Contains("terminou", StringComparison.OrdinalIgnoreCase),
            "the journal should explicitly record process termination");
    });
}

static async Task ProcessConflictAndPidReuseRefuseWrites()
{
    await WithTemporaryDirectory(async directory =>
    {
        var (adapter, _, store, identity) = CreateProcessPriorityFixture(directory);
        var service = CreateProcessPriorityService(adapter, store, allowWrites: true);
        var review = service.CreateReview(identity, ProcessPriorityLevel.AboveNormal,
            new PlatformDescriptor(true, "x64", 26100));
        var capture = await service.CaptureAsync(review, true);
        adapter.SetObservation(ProcessObservationFor(identity, ProcessPriorityLevel.BelowNormal));
        var conflict = await service.ApplyAsync(capture.OperationId, ProcessApproval(capture, ProcessPriorityAction.Apply));
        Assert(conflict.State == JournalOperationState.Conflict && adapter.WriteCalls == 0,
            "a priority changed since capture must be refused as a conflict");
    });

    await WithTemporaryDirectory(async directory =>
    {
        var (adapter, _, store, identity) = CreateProcessPriorityFixture(directory);
        var service = CreateProcessPriorityService(adapter, store, allowWrites: true);
        var review = service.CreateReview(identity, ProcessPriorityLevel.AboveNormal,
            new PlatformDescriptor(true, "x64", 26100));
        var capture = await service.CaptureAsync(review, true);
        adapter.BeforeSet = fake => fake.SetObservation(ProcessObservationFor(
            identity with { CreatedAtUtc = identity.CreatedAtUtc.AddSeconds(5) }, ProcessPriorityLevel.Normal));
        var reused = await service.ApplyAsync(capture.OperationId, ProcessApproval(capture, ProcessPriorityAction.Apply));
        Assert(reused.State == JournalOperationState.MutationMayHaveStarted &&
            reused.Events.Last().EventType == "Conflict" && adapter.WriteCalls == 0,
            "a PID reused between preflight and adapter write must be rejected and kept recoverable after the intent was armed");
    });
}

static async Task ProcessJournalFailurePreventsWrite()
{
    await WithTemporaryDirectory(async directory =>
    {
        var (adapter, storage, store, identity) = CreateProcessPriorityFixture(directory);
        var service = CreateProcessPriorityService(adapter, store, allowWrites: true);
        var review = service.CreateReview(identity, ProcessPriorityLevel.AboveNormal,
            new PlatformDescriptor(true, "x64", 26100));
        var capture = await service.CaptureAsync(review, true);
        storage.NextWriteFailure = JournalWriteFailure.Io;
        await AssertThrowsAsync<IOException>(() => service.ApplyAsync(
            capture.OperationId, ProcessApproval(capture, ProcessPriorityAction.Apply)));
        Assert(adapter.WriteCalls == 0, "a failure to persist mutation intent must prevent the setter call");
        Assert((await store.LoadAsync(capture.OperationId))?.State == JournalOperationState.SnapshotsCaptured,
            "the last durable operation state must remain the captured snapshot");
    });
}

static async Task ProcessPostWriteJournalFailureDoesNotRollback()
{
    await WithTemporaryDirectory(async directory =>
    {
        var (adapter, storage, store, identity) = CreateProcessPriorityFixture(directory);
        var service = CreateProcessPriorityService(adapter, store, allowWrites: true);
        var review = service.CreateReview(identity, ProcessPriorityLevel.AboveNormal,
            new PlatformDescriptor(true, "x64", 26100));
        var capture = await service.CaptureAsync(review, true);
        adapter.AfterWrite = () => storage.NextWriteFailure = JournalWriteFailure.Io;

        await AssertThrowsAsync<IOException>(() => service.ApplyAsync(
            capture.OperationId, ProcessApproval(capture, ProcessPriorityAction.Apply)));
        Assert(adapter.WriteCalls == 1 && adapter.Current(identity.ProcessId)?.CurrentPriority == ProcessPriorityLevel.AboveNormal,
            "the fake operation should apply once before injected journal persistence failure");
        Assert((await store.LoadAsync(capture.OperationId))?.State == JournalOperationState.MutationMayHaveStarted,
            "the durable armed entry must remain for manual review after the final journal write fails");
        Assert(adapter.WriteCalls == 1, "no automatic rollback may be attempted after a persistence failure");
    });
}

static async Task ProcessRestoreIsConditionalAndVerified()
{
    await WithTemporaryDirectory(async directory =>
    {
        var (adapter, _, store, identity) = CreateProcessPriorityFixture(directory);
        var service = CreateProcessPriorityService(adapter, store, allowWrites: true);
        var review = service.CreateReview(identity, ProcessPriorityLevel.AboveNormal,
            new PlatformDescriptor(true, "x64", 26100));
        var capture = await service.CaptureAsync(review, true);
        var applied = await service.ApplyAsync(capture.OperationId, ProcessApproval(capture, ProcessPriorityAction.Apply));
        Assert(applied.State == JournalOperationState.MutationVerified, "fake readback should verify the selected process");

        var restored = await service.RestoreAsync(applied.OperationId, ProcessApproval(applied, ProcessPriorityAction.Restore));
        Assert(restored.State == JournalOperationState.RestoredVerified,
            "conditional restoration should be verified through the shared journal");
        Assert(adapter.Current(identity.ProcessId)?.CurrentPriority == ProcessPriorityLevel.Normal && adapter.WriteCalls == 2,
            "restoration should restore only the captured process to its previous value");
    });
}

static async Task ProcessRestoreRejectsConflictAndExit()
{
    await WithTemporaryDirectory(async directory =>
    {
        var (adapter, _, store, identity) = CreateProcessPriorityFixture(directory);
        var service = CreateProcessPriorityService(adapter, store, allowWrites: true);
        var review = service.CreateReview(identity, ProcessPriorityLevel.AboveNormal,
            new PlatformDescriptor(true, "x64", 26100));
        var capture = await service.CaptureAsync(review, true);
        var applied = await service.ApplyAsync(capture.OperationId, ProcessApproval(capture, ProcessPriorityAction.Apply));
        adapter.SetObservation(ProcessObservationFor(identity, ProcessPriorityLevel.BelowNormal));
        var conflict = await service.RestoreAsync(applied.OperationId, ProcessApproval(applied, ProcessPriorityAction.Restore));
        Assert(conflict.State == JournalOperationState.MutationVerified && conflict.Events.Last().EventType == "Conflict" && adapter.WriteCalls == 1,
            "external priority changes must prevent conditional restoration");
    });

    await WithTemporaryDirectory(async directory =>
    {
        var (adapter, _, store, identity) = CreateProcessPriorityFixture(directory);
        var service = CreateProcessPriorityService(adapter, store, allowWrites: true);
        var review = service.CreateReview(identity, ProcessPriorityLevel.AboveNormal,
            new PlatformDescriptor(true, "x64", 26100));
        var capture = await service.CaptureAsync(review, true);
        var applied = await service.ApplyAsync(capture.OperationId, ProcessApproval(capture, ProcessPriorityAction.Apply));
        adapter.SetObservation(new ProcessObservation(identity.ProcessId, "probe", identity, null,
            ProcessInspectionStatus.Exited, "synthetic exit before restore"));
        var ended = await service.RestoreAsync(applied.OperationId, ProcessApproval(applied, ProcessPriorityAction.Restore));
        Assert(ended.State == JournalOperationState.MutationVerified && ended.Events.Last().EventType == "Conflict" && adapter.WriteCalls == 1,
            "an ended process must not be restored through a PID that may be reused");
    });

    await WithTemporaryDirectory(async directory =>
    {
        var (adapter, _, store, identity) = CreateProcessPriorityFixture(directory);
        var service = CreateProcessPriorityService(adapter, store, allowWrites: true);
        var review = service.CreateReview(identity, ProcessPriorityLevel.AboveNormal,
            new PlatformDescriptor(true, "x64", 26100));
        var capture = await service.CaptureAsync(review, true);
        var applied = await service.ApplyAsync(capture.OperationId, ProcessApproval(capture, ProcessPriorityAction.Apply));
        var reusedIdentity = identity with { CreatedAtUtc = identity.CreatedAtUtc.AddSeconds(9) };
        adapter.SetObservation(ProcessObservationFor(reusedIdentity, ProcessPriorityLevel.AboveNormal));
        var reused = await service.RestoreAsync(applied.OperationId, ProcessApproval(applied, ProcessPriorityAction.Restore));
        Assert(reused.State == JournalOperationState.MutationVerified && reused.Events.Last().EventType == "Conflict" && adapter.WriteCalls == 1,
            "restoration must refuse a different process that reused the captured PID");
    });
}

static ProcessIdentity SampleProcessIdentity(int processId = 54321) => new(
    processId,
    DateTimeOffset.Parse("2026-02-03T04:05:06.7890000+00:00", System.Globalization.CultureInfo.InvariantCulture),
    @"C:\Samples\CalixtoOptimizer.ProcessProbe.exe");

static ProcessObservation ProcessObservationFor(ProcessIdentity identity, ProcessPriorityLevel priority) =>
    new(identity.ProcessId, "CalixtoOptimizer.ProcessProbe", identity, priority,
        ProcessInspectionStatus.Available, "synthetic identity and priority");

static (FakeProcessPriorityAdapter Adapter, FaultInjectingJournalStorage Storage,
    JsonOperationJournalStore Store, ProcessIdentity Identity) CreateProcessPriorityFixture(string directory)
{
    var adapter = new FakeProcessPriorityAdapter();
    var identity = SampleProcessIdentity();
    adapter.SetObservation(ProcessObservationFor(identity, ProcessPriorityLevel.Normal));
    var storage = new FaultInjectingJournalStorage();
    var store = new JsonOperationJournalStore(directory, storage);
    return (adapter, storage, store, identity);
}

static ProcessPriorityOperationService CreateProcessPriorityService(
    FakeProcessPriorityAdapter adapter,
    JsonOperationJournalStore store,
    bool allowWrites = false)
{
    var consent = new PlanConsentService();
    return new ProcessPriorityOperationService(adapter, store, consent,
        new OperationJournalCoordinator(consent), new ConfirmedProcessPriorityMutationPolicy(allowWrites));
}

static ProcessPriorityApproval ProcessApproval(OperationJournalEntry entry, ProcessPriorityAction action)
{
    var fingerprint = entry.Snapshots.Single().Values["CaptureFingerprint"];
    return new ProcessPriorityApproval(entry.OperationId, action, fingerprint,
        ConsequencesAccepted: true,
        RecoveryConsequencesAccepted: action == ProcessPriorityAction.Restore,
        ProcessPriorityOperationService.RequiredConfirmationPhrase(entry.OperationId, action));
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
    var hardwareId = "PCI\\VEN_1234&DEV_ABCD&SUBSYS_11223344";
    var compatibleId = "PCI\\VEN_1234&DEV_ABCD";
    var row = new DriverInfo("Device, \"special\"", deviceId, "Vendor", "1.2.3", "20250101000000.000000+000", "oem1.inf", true, "Vendor Inc.", null,
        HardwareIds: [hardwareId], CompatibleIds: [compatibleId]);
    var csv = DriverCatalog.ToSanitizedCsv([row]);
    Assert(!csv.Contains(deviceId, StringComparison.Ordinal), "CSV must omit PnP identifiers");
    Assert(!csv.Contains(hardwareId, StringComparison.Ordinal) && !csv.Contains(compatibleId, StringComparison.Ordinal),
        "CSV must omit hardware and compatible ID arrays");
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
    var hardwareId = "PCI\\VEN_1234&DEV_ABCD&SUBSYS_11223344";
    var compatibleId = "PCI\\VEN_1234&DEV_ABCD";
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
        [new DriverInfo("Example Device", deviceId, "Example Vendor", "1.2.3", null, "sample.inf", true, "Example Signer", 28,
            HardwareIds: [hardwareId], CompatibleIds: [compatibleId])],
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
    Assert(!json.Contains(hardwareId, StringComparison.Ordinal) && !json.Contains(compatibleId, StringComparison.Ordinal),
        "diagnostic JSON must omit hardware and compatible ID arrays");
    Assert(!json.Contains(schemeId.ToString(), StringComparison.OrdinalIgnoreCase), "diagnostic JSON must omit power scheme GUIDs");
    Assert(!json.Contains("SERIAL-PRIVATE", StringComparison.Ordinal), "diagnostic JSON must omit serial-like identifiers");
    Assert(json.Contains("\"problemCodeWindowsReported\": 28", StringComparison.Ordinal), "diagnostic must preserve the numeric code");
    Assert(json.Contains("Nomes e modelos de hardware permanecem", StringComparison.Ordinal), "diagnostic must warn that hardware models can identify a device");
    Assert(!json.Contains("driverMissing", StringComparison.OrdinalIgnoreCase), "diagnostic must not infer a missing driver");
}

static void DiagnosticJsonPreservesProvenanceAndUnknowns()
{
    var context = new DiagnosticReportPrivacyContext(
        "PRIVATE-PC",
        "PRIVATE-USER",
        "PRIVATE-DOMAIN",
        ["PRIVATE-ALIAS"]);
    var computer = new ComputerInventory(
        DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
        "Windows 11 Pro",
        null,
        "25H2",
        "26100.1",
        "X64",
        "CPU PRIVATE-PC private@example.invalid",
        ["GPU Example"],
        null,
        "Board Example",
        [],
        ["Adapter Example fe80::1"],
        ["A divergência entre CIM build 26100 e ProductName foi preservada; C:\\Users\\PRIVATE-USER\\trace.txt 192.168.1.44 serial number: SERIAL-PRIVATE"]);
    var drivers = new DriverInventoryResult(
        DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
        InventoryAvailability.Partial,
        [new DriverInfo("PCI\\VEN_1234&DEV_ABCD serial number: SERIAL-DEVICE", "PCI\\VEN_1234&DEV_ABCD\\PRIVATE", "Example Provider", "1.2.3", null, "oem123.inf", true, "PRIVATE-ALIAS", 28,
            HardwareIds: ["PCI\\VEN_1234&DEV_ABCD"], CompatibleIds: ["PCI\\VEN_1234"])],
        ["Uma leitura parcial para PRIVATE-DOMAIN"],
        null);
    var power = new PowerPlanInventoryResult(
        DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
        InventoryAvailability.Partial,
        [new PowerPlanInfo(Guid.Parse("2f7fa2dd-bc0a-467a-8635-c1fa2355d169"), "Plano PRIVATE-USER", true, null, 180)],
        [],
        null);

    var json = SanitizedDiagnosticReportBuilder.BuildJson(
        computer, drivers, power,
        DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
        context,
        computerErrorMessage: null);

    foreach (var secret in new[]
             {
                 "PRIVATE-PC", "PRIVATE-USER", "PRIVATE-DOMAIN", "PRIVATE-ALIAS", "private@example.invalid", "SERIAL-PRIVATE", "SERIAL-DEVICE",
                 "192.168.1.44", "fe80::1", "oem123.inf", "PCI\\\\VEN_1234", "2f7fa2dd-bc0a-467a-8635-c1fa2355d169", "C:\\Users"
             })
        Assert(!json.Contains(secret, StringComparison.OrdinalIgnoreCase), $"diagnostic leaked sensitive marker {secret}");

    Assert(json.Contains("Win32_OperatingSystem", StringComparison.Ordinal), "system source should be documented");
    Assert(json.Contains("Win32_PnPSignedDriver", StringComparison.Ordinal), "driver source should be documented");
    Assert(json.Contains("PowrProf.dll", StringComparison.Ordinal), "power source should be documented");
    using var document = System.Text.Json.JsonDocument.Parse(json);
    var sanitizedObservation = document.RootElement.GetProperty("system").GetProperty("observations")[0].GetString();
    Assert(sanitizedObservation?.Contains("A divergência entre CIM build 26100 e ProductName foi preservada", StringComparison.Ordinal) == true,
        "sanitization should preserve source divergence text");
    Assert(json.Contains("\"availability\": \"Partial\"", StringComparison.Ordinal), "partial collection state should remain explicit");
    Assert(json.Contains("\"windowsEdition\": null", StringComparison.Ordinal) &&
           json.Contains("\"memory\": null", StringComparison.Ordinal), "unknown fields should remain explicit nulls");
    Assert(json.Contains("\"unknownFields\"", StringComparison.Ordinal), "unknown field names should be listed");
    Assert(json.Contains("\"problemCodeWindowsReported\": 28", StringComparison.Ordinal), "Windows-reported PnP code should remain data, not a diagnosis");
}

static void DiagnosticReportSaveCancellationAndOverwrite()
{
    var operations = new FakeDiagnosticReportFileOperations();
    var saver = new DiagnosticReportFileSaver(operations);
    var cancelled = saver.Save(null, "reviewed json", overwriteConfirmed: false);
    Assert(cancelled.State == DiagnosticReportSaveState.Cancelled, "cancelled destination should be reported as cancelled");
    Assert(!cancelled.Message.Contains("salvo", StringComparison.OrdinalIgnoreCase), "cancel must not show a success message");
    Assert(operations.OperationCount == 0, "cancel must not touch the file system");

    var destination = Path.Combine(Path.GetTempPath(), "diagnostic-report-fake", "report.json");
    operations.AddDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
    operations.AddExistingFile(destination, "previous file");
    var confirmationRequired = saver.Save(destination, "reviewed json", overwriteConfirmed: false);
    Assert(confirmationRequired.State == DiagnosticReportSaveState.OverwriteConfirmationRequired,
        "an existing file must require explicit overwrite confirmation");
    Assert(operations.ReadText(destination) == "previous file", "unconfirmed existing content must remain unchanged");

    var overwritten = saver.Save(destination, "reviewed json", overwriteConfirmed: true);
    Assert(overwritten.State == DiagnosticReportSaveState.SavedAndVerified, "confirmed overwrite should succeed after verification");
    Assert(operations.ReadText(destination) == "reviewed json", "the final file must exactly match the reviewed text");
    Assert(overwritten.Message.Contains("corresponde exatamente", StringComparison.Ordinal), "success message must state exact verification");

    var raceDestination = Path.Combine(Path.GetTempPath(), "diagnostic-report-fake", "appeared-after-review.json");
    operations.BeforeMoveNew = path => operations.AddExistingFile(path, "created by another process");
    var race = saver.Save(raceDestination, "reviewed json", overwriteConfirmed: false);
    Assert(race.State == DiagnosticReportSaveState.OverwriteConfirmationRequired,
        "a destination appearing after preflight must request confirmation instead of overwriting");
    Assert(operations.ReadText(raceDestination) == "created by another process", "a race-created destination must remain unchanged");
}

static void DiagnosticReportSaveVerifiesContentAndFailures()
{
    var destination = Path.Combine(Path.GetTempPath(), "diagnostic-report-fake", "new-report.json");
    var operations = new FakeDiagnosticReportFileOperations();
    operations.AddDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
    var saver = new DiagnosticReportFileSaver(operations);

    operations.NextWriteFailure = new UnauthorizedAccessException();
    var denied = saver.Save(destination, "reviewed", overwriteConfirmed: false);
    Assert(denied.State == DiagnosticReportSaveState.Failed && denied.FailureType == nameof(UnauthorizedAccessException),
        "access denied must be reported as failure");
    Assert(!denied.Message.Contains("Arquivo salvo", StringComparison.Ordinal), "access denied must not show success");

    operations.NextWriteFailure = new IOException();
    var failedWrite = saver.Save(destination, "reviewed", overwriteConfirmed: false);
    Assert(failedWrite.State == DiagnosticReportSaveState.Failed && failedWrite.FailureType == nameof(IOException),
        "I/O failure must be reported as failure");
    Assert(!failedWrite.Message.Contains("Arquivo salvo", StringComparison.Ordinal), "write failure must not show success");

    operations.CorruptNextRead = true;
    var mismatch = saver.Save(destination, "reviewed", overwriteConfirmed: false);
    Assert(mismatch.State == DiagnosticReportSaveState.Failed && mismatch.FailureType == "ContentMismatch",
        "temporary-file mismatch must fail before finalizing the destination");
    Assert(!operations.FileExists(destination), "a failed preflight verification must not create the destination");
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

static void DriverCandidatesMatchExactHardwareIds()
{
    var lan = DriverPackageCandidateCatalog.All.Single(candidate => candidate.Id == "asus-rtl8111h-lan-1168.27.50.919");
    var wifi = DriverPackageCandidateCatalog.All.Single(candidate => candidate.Id == "asus-rtl8821ce-wifi-2024.10.138.3");
    var rows = new[]
    {
        new DriverInfo("Ethernet sample", "PCI\\INSTANCE-1", "Microsoft", "1.0.0.14", null, "rtcx21x64.inf", true, "Microsoft", 0,
            HardwareIds: [lan.MatchHardwareIds[0]]),
        new DriverInfo("Wi-Fi sample", "PCI\\INSTANCE-2", "Microsoft", "2024.0.8.108", null, "netrtwlane.inf", true, "Microsoft", 0,
            HardwareIds: [wifi.MatchHardwareIds[0]])
    };

    var assessments = DriverPackageCandidateCatalog.Assess(rows);
    var lanAssessment = assessments.Single(item => item.Candidate.Id == lan.Id);
    var wifiAssessment = assessments.Single(item => item.Candidate.Id == wifi.Id);
    Assert(lanAssessment.MatchKind == DriverPackageMatchKind.ExactHardwareId && lanAssessment.IsCandidateFound,
        "LAN package must match only the reviewed exact hardware ID");
    Assert(wifiAssessment.MatchKind == DriverPackageMatchKind.ExactHardwareId && wifiAssessment.IsCandidateFound,
        "Wi-Fi package must match the reviewed exact hardware ID");
    Assert(lanAssessment.InstalledPackageSummary.Contains("1.0.0.14", StringComparison.Ordinal),
        "installed Windows-reported package version should be shown separately");
    Assert(lanAssessment.Candidate.ReviewedArchiveSha256 == lanAssessment.Candidate.PublishedArchiveSha256,
        "LAN archive digest should match the published ASUS SHA-256");
    Assert(wifiAssessment.Candidate.PublishedArchiveSha256 is null,
        "Wi-Fi catalog must preserve that ASUS did not publish an archive SHA-256");
    Assert(assessments.All(item => item.Candidate.PendingItems.Contains("não houve instalação", StringComparison.OrdinalIgnoreCase)),
        "candidate review must state that no real driver installation occurred");
}

static void DriverCandidateAmbiguityBlocksInstallation()
{
    var candidate = DriverPackageCandidateCatalog.All.Single(item => item.Id == "asus-rtl8111h-lan-1168.27.50.919");
    var repeatedId = candidate.MatchHardwareIds[0];
    var rows = new[]
    {
        new DriverInfo("Ethernet sample A", "PCI\\INSTANCE-A", null, null, null, null, null, null, 0,
            HardwareIds: [repeatedId]),
        new DriverInfo("Ethernet sample B", "PCI\\INSTANCE-B", null, null, null, null, null, null, 0,
            HardwareIds: [repeatedId])
    };

    var assessment = DriverPackageCandidateCatalog.Assess(rows).Single(item => item.Candidate.Id == candidate.Id);
    Assert(assessment.MatchKind == DriverPackageMatchKind.Ambiguous, "duplicate exact matches should be ambiguous");
    Assert(assessment.InstalledDriver is null && !assessment.IsCandidateFound,
        "ambiguous matching must not choose a device");
    Assert(assessment.CandidateStatus.Contains("ambígua", StringComparison.OrdinalIgnoreCase),
        "ambiguity must be visible in the review status");

    var compatibleOnly = new DriverInfo("Ethernet generic", "PCI\\INSTANCE-C", null, null, null, null, null, null, 0,
        CompatibleIds: [candidate.MatchCompatibleIds[0]]);
    var genericAssessment = DriverPackageCandidateCatalog.Assess([compatibleOnly]).Single(item => item.Candidate.Id == candidate.Id);
    Assert(genericAssessment.MatchKind == DriverPackageMatchKind.CompatibleIdOnly,
        "a generic Compatible ID should be reported distinctly from an exact hardware match");
}

static void LocalDriverPackageRecognitionUsesHash()
{
    const string lanHash = "3B6946C79C745B6E5B2F9E08A9CF6F291EE5C9951F85860753F820F41D76F96D";
    const string wifiHash = "E061EBD3E77F54DA07ECED9876DB54DCAA46E871DB4015FEE6EC1F2987535647";
    var lan = DriverPackageCandidateCatalog.IdentifyArchive("offline-lan.zip", lanHash.ToLowerInvariant());
    var wifi = DriverPackageCandidateCatalog.IdentifyArchive("offline-wifi.zip", wifiHash);
    var unknown = DriverPackageCandidateCatalog.IdentifyArchive("other.zip", new string('A', 64));

    Assert(lan.RecognizedCandidate?.Id == "asus-rtl8111h-lan-1168.27.50.919",
        "known LAN file should be recognized independent of its local filename");
    Assert(wifi.RecognizedCandidate?.Id == "asus-rtl8821ce-wifi-2024.10.138.3",
        "known Wi-Fi file should be recognized by reviewed digest");
    Assert(unknown.RecognizedCandidate is null && unknown.Status.Contains("continuam sem validação", StringComparison.Ordinal),
        "unrecognized archive must not be considered validated");
    AssertThrows<ArgumentException>(() => DriverPackageCandidateCatalog.IdentifyArchive("bad.zip", "not-a-digest"));
}

static async Task GenericDriverPackageZipAnalysisIsBoundedAndScoped()
{
    await WithTemporaryDirectory(async directory =>
    {
        var zipPath = Path.Combine(directory, "local-driver.zip");
        CreateSampleDriverZip(zipPath);
        var targetId = "PCI\\VEN_1234&DEV_5678&SUBSYS_ABCD0001";
        var device = new DriverInfo("Sample Ethernet", "PCI\\INSTANCE-1", "Current vendor", "1.2.3.4", null,
            "old.inf", true, "Current signer", 0, HardwareIds: [targetId]);
        var analyzer = new LocalDriverPackageAnalyzer(new FakeDriverPackageSignatureVerifier());
        var result = await analyzer.AnalyzeAsync(zipPath, [device], new("x64", 10, 0, 26300));
        var infPath = result.Candidates.Single().InfPath;
        Assert(result.SourceSha256.Length == 64 && result.ArchiveEntryCount == 5,
            "ZIP hash and bounded entry count should be recorded");
        Assert(result.Candidates.Count == 1,
            "non-driver auxiliary INF files such as Autorun.inf should not be presented as PnP driver candidates");
        Assert(result.Candidates.Single().Outcome == DriverPackageAnalysisOutcome.CompatibleAccordingToChecks,
            "an exact Hardware ID, applicable x64/Windows decoration, valid mock catalog and complete source file should pass only documented checks");
        Assert(result.Candidates.Single().CanPrepareInstallation,
            "only the unique exact match should be eligible for an installation preview");
        Assert(File.Exists(infPath) && Path.GetFullPath(infPath).StartsWith(
            Path.GetFullPath(Path.Combine(Path.GetTempPath(), "CalixtoOptimizer", "DriverAnalysis")), StringComparison.OrdinalIgnoreCase),
            "ZIP content should be extracted under the owned temporary analysis root");
        Assert(result.Candidates.Single().RequiredPackageFiles.SequenceEqual(["sample.sys"]),
            "SourceDisksFiles entries should be enumerated for package integrity checks");
        Assert(result.Candidates.Single().PayloadFilesSha256?.Count == 3,
            "INF, catalog and required payload file hashes should be retained for tamper rechecks");
        result.Dispose();
        Assert(!Directory.Exists(Path.GetDirectoryName(infPath)),
            "disposing a ZIP analysis should remove only its owned temporary extraction");
        Assert(File.Exists(zipPath), "disposing analysis must preserve the source ZIP");
    });
}

static async Task GenericDriverPackageZipRejectsUnsafeAndOversizedInputs()
{
    await WithTemporaryDirectory(async directory =>
    {
        var zipPath = Path.Combine(directory, "traversal.zip");
        var escapeToken = Guid.NewGuid().ToString("N");
        var escapePath = Path.Combine(Path.GetTempPath(), "CalixtoOptimizer", "DriverAnalysis", $"escaped-{escapeToken}.txt");
        using (var stream = File.Create(zipPath))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            var escapeName = $"../escaped-{escapeToken}.txt";
            var entry = archive.CreateEntry(escapeName);
            using (var writer = new StreamWriter(entry.Open())) await writer.WriteAsync("must not be written");
            var inf = archive.CreateEntry("sample.inf");
            using (var writer = new StreamWriter(inf.Open())) await writer.WriteAsync(SampleDriverInf("PCI\\VEN_1234&DEV_5678"));
        }

        var analyzer = new LocalDriverPackageAnalyzer(new FakeDriverPackageSignatureVerifier());
        await AssertThrowsAsync<InvalidDataException>(() => analyzer.AnalyzeAsync(
            zipPath, Array.Empty<DriverInfo>(), new("x64", 10, 0, 26300)));
        Assert(!File.Exists(escapePath), "ZIP traversal must be rejected before writing outside its temporary directory");

        var normalZip = Path.Combine(directory, "normal.zip");
        CreateSampleDriverZip(normalZip);
        var tinyLimitAnalyzer = new LocalDriverPackageAnalyzer(
            new FakeDriverPackageSignatureVerifier(), maxArchiveBytes: 8, maxExpandedBytes: 1024, maxSingleFileBytes: 1024, maxEntries: 16);
        await AssertThrowsAsync<InvalidDataException>(() => tinyLimitAnalyzer.AnalyzeAsync(
            normalZip, Array.Empty<DriverInfo>(), new("x64", 10, 0, 26300)));
    });
}

static async Task GenericDriverPackageOutcomesAreConservative()
{
    await WithTemporaryDirectory(async directory =>
    {
        var zipPath = Path.Combine(directory, "outcomes.zip");
        CreateSampleDriverZip(zipPath);
        var targetId = "PCI\\VEN_1234&DEV_5678&SUBSYS_ABCD0001";
        var analyzer = new LocalDriverPackageAnalyzer(new FakeDriverPackageSignatureVerifier());
        var platform = new DriverPackageTargetPlatform("amd64", 10, 0, 26300);

        using var compatible = await analyzer.AnalyzeAsync(zipPath,
            [new DriverInfo("Target", "PCI\\ONE", null, "1", null, "old.inf", true, null, 0, HardwareIds: [targetId])], platform);
        Assert(compatible.Candidates.Single().Outcome == DriverPackageAnalysisOutcome.CompatibleAccordingToChecks,
            "one exact match should pass when required files and signatures are all confirmed");

        using var incompatible = await analyzer.AnalyzeAsync(zipPath,
            [new DriverInfo("Other", "PCI\\TWO", null, "1", null, "old.inf", true, null, 0, HardwareIds: ["PCI\\VEN_9999&DEV_0001"])], platform);
        Assert(incompatible.Candidates.Single().Outcome == DriverPackageAnalysisOutcome.Incompatible,
            "a fully parsed INF with no matching ID should be incompatible rather than incomplete");

        using var ambiguous = await analyzer.AnalyzeAsync(zipPath,
            [new DriverInfo("Generic match", "PCI\\THREE", null, "1", null, "old.inf", true, null, 0,
                CompatibleIds: [targetId])], platform);
        Assert(ambiguous.Candidates.Single().Outcome == DriverPackageAnalysisOutcome.Ambiguous &&
               !ambiguous.Candidates.Single().CanPrepareInstallation,
            "Compatible ID only must remain ambiguous and block preview");

        using var duplicate = await analyzer.AnalyzeAsync(zipPath,
            [new DriverInfo("Target A", "PCI\\A", null, "1", null, "old.inf", true, null, 0, HardwareIds: [targetId]),
             new DriverInfo("Target B", "PCI\\B", null, "1", null, "old.inf", true, null, 0, HardwareIds: [targetId])], platform);
        Assert(duplicate.Candidates.Single().Outcome == DriverPackageAnalysisOutcome.Ambiguous &&
               duplicate.Candidates.Single().TargetSummary.Contains("PCI\\A", StringComparison.Ordinal) &&
               duplicate.Candidates.Single().TargetSummary.Contains("PCI\\B", StringComparison.Ordinal),
            "more than one exact match should block preview and enumerate every discovered device in the reach summary");

        var missingPayloadZip = Path.Combine(directory, "missing-payload.zip");
        CreateSampleDriverZip(missingPayloadZip, includeDriverBinary: false);
        using var incomplete = await analyzer.AnalyzeAsync(missingPayloadZip,
            [new DriverInfo("Target", "PCI\\ONE", null, "1", null, "old.inf", true, null, 0, HardwareIds: [targetId])], platform);
        Assert(incomplete.Candidates.Single().Outcome == DriverPackageAnalysisOutcome.Incomplete &&
               incomplete.Candidates.Single().Reasons.Any(reason => reason.Contains("não encontrado", StringComparison.OrdinalIgnoreCase)),
            "a required payload file missing from the package should make analysis incomplete and block use");

        using var unavailableSignature = await new LocalDriverPackageAnalyzer(
            new FakeDriverPackageSignatureVerifier(DriverPackageVerificationStatus.Unavailable)).AnalyzeAsync(zipPath,
                [new DriverInfo("Target", "PCI\\ONE", null, "1", null, "old.inf", true, null, 0, HardwareIds: [targetId])], platform);
        Assert(unavailableSignature.Candidates.Single().Outcome == DriverPackageAnalysisOutcome.Incomplete,
            "an unavailable signature-verification mechanism must be reported as incomplete rather than incompatible");

        using var invalidSignature = await new LocalDriverPackageAnalyzer(
            new FakeDriverPackageSignatureVerifier(DriverPackageVerificationStatus.Invalid)).AnalyzeAsync(zipPath,
                [new DriverInfo("Target", "PCI\\ONE", null, "1", null, "old.inf", true, null, 0, HardwareIds: [targetId])], platform);
        Assert(invalidSignature.Candidates.Single().Outcome == DriverPackageAnalysisOutcome.Incompatible,
            "an explicitly rejected package signature must be classified as incompatible, not as an unknown incomplete check");
    });
}

static async Task GenericInfDependenciesFollowApplicableInstallSections()
{
    await WithTemporaryDirectory(async directory =>
    {
        var zipPath = Path.Combine(directory, "applicable-sections.zip");
        using (var stream = File.Create(zipPath))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            Add("selected.inf", """
                [Version]
                Signature="$Windows NT$"
                Class=Net
                Provider=Sample
                DriverVer=01/01/2025,1.0.0.0
                CatalogFile=selected.cat
                [Manufacturer]
                %Maker%=Models,NTamd64.10.0...22000
                [Models.NTamd64.10.0...22000]
                %Device%=InstallOld,PCI\VEN_1234&DEV_5678
                [InstallOld.NT]
                CopyFiles=Selected.Copy
                [Selected.Copy]
                selected.sys,,,2
                [InstallOld.NTamd64.10.0...99999]
                Include=missing-system.inf
                Needs=Missing.System.Section
                CopyFiles=Future.Copy
                [Future.Copy]
                future.sys
                [Unreferenced.Copy]
                missing-coinstaller.dll
                [SourceDisksFiles]
                selected.sys=1
                future.sys=1
                missing-coinstaller.dll=1
                """);
            Add("selected.cat", "catalog placeholder");
            Add("selected.sys", "payload");
            void Add(string name, string content)
            {
                using var writer = new StreamWriter(archive.CreateEntry(name).Open());
                writer.Write(content);
            }
        }

        var device = new DriverInfo("Target", "PCI\\TARGET", null, "1", null, "old.inf", true, null, 0,
            HardwareIds: ["PCI\\VEN_1234&DEV_5678"]);
        var unusedSectionDependency = new FakeSystemInfDependencyResolver(
            new(DriverPackageDependencyStatus.Missing, "must not be checked"));
        using var result = await new LocalDriverPackageAnalyzer(new FakeDriverPackageSignatureVerifier(), unusedSectionDependency)
            .AnalyzeAsync(zipPath, [device], new("amd64", 10, 0, 26300));
        var candidate = result.Candidates.Single();
        Assert(candidate.RequiredPackageFiles.SequenceEqual(["selected.sys"], StringComparer.OrdinalIgnoreCase),
            "Only the selected model's applicable DDInstall CopyFiles payload should be required; unused and future section files must be ignored");
        Assert(candidate.Outcome == DriverPackageAnalysisOutcome.CompatibleAccordingToChecks && candidate.CanPrepareInstallation,
            "files mentioned only in unreferenced or non-applicable install sections must not block the selected model");
        Assert(unusedSectionDependency.LastRequirements.Count == 0,
            "Include/Needs from a DDInstall variant requiring a future Windows build must not be resolved for this host");
        Assert(candidate.ApplicableInstallSectionsSummary == "InstallOld.NT",
            "The selected model should retain the applicable DDInstall section used to derive payload dependencies");
    });
}

static async Task GenericInfAnalysisBlocksMissingSystemDependencies()
{
    await WithTemporaryDirectory(async directory =>
    {
        var zipPath = Path.Combine(directory, "missing-system-dependency.zip");
        using (var stream = File.Create(zipPath))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            Add("dependent.inf", """
                [Version]
                Signature="$Windows NT$"
                Class=Net
                Provider=Sample
                DriverVer=01/01/2025,1.0.0.0
                CatalogFile=dependent.cat
                [Manufacturer]
                %Maker%=Models,NTamd64.10.0
                [Models.NTamd64.10.0]
                %Device%=Install,PCI\VEN_1234&DEV_5678
                [Install.NT]
                Include=systembus.inf
                Needs=SystemBus.Install
                CopyFiles=Driver.Copy
                [Driver.Copy]
                driver.sys
                [SourceDisksFiles]
                driver.sys=1
                """);
            Add("dependent.cat", "catalog placeholder");
            Add("driver.sys", "payload");
            void Add(string name, string content)
            {
                using var writer = new StreamWriter(archive.CreateEntry(name).Open());
                writer.Write(content);
            }
        }

        var device = new DriverInfo("Target", "PCI\\TARGET", null, "1", null, "old.inf", true, null, 0,
            HardwareIds: ["PCI\\VEN_1234&DEV_5678"]);
        var resolver = new FakeSystemInfDependencyResolver(new(DriverPackageDependencyStatus.Missing, "systembus.inf ou SystemBus.Install ausente"));
        using var result = await new LocalDriverPackageAnalyzer(new FakeDriverPackageSignatureVerifier(), resolver)
            .AnalyzeAsync(zipPath, [device], new("amd64", 10, 0, 26300));
        var candidate = result.Candidates.Single();
        Assert(candidate.Outcome == DriverPackageAnalysisOutcome.Incomplete && !candidate.CanPrepareInstallation,
            "An applicable Include/Needs dependency absent from the current Windows image must keep the candidate blocked");
        Assert(candidate.Reasons.Any(reason => reason.Contains("systembus.inf", StringComparison.OrdinalIgnoreCase)),
            "The failure should identify the unresolved system dependency rather than an unrelated package DLL");
        Assert(resolver.LastRequirements.Count == 1 &&
               resolver.LastRequirements[0].IncludedInfNames.SequenceEqual(["systembus.inf"], StringComparer.OrdinalIgnoreCase) &&
               resolver.LastRequirements[0].RequiredSections.SequenceEqual(["SystemBus.Install"], StringComparer.OrdinalIgnoreCase),
            "Include and Needs from each selected applicable install section should remain paired for recursive resolution");
    });
}

static async Task GenericInfAnalysisBlocksUnverifiedInstallEffects()
{
    await WithTemporaryDirectory(async directory =>
    {
        var device = new DriverInfo("Target", "PCI\\TARGET", null, "1", null, "old.inf", true, null, 0,
            HardwareIds: ["PCI\\VEN_1234&DEV_5678"]);
        var platform = new DriverPackageTargetPlatform("amd64", 10, 0, 26300);

        foreach (var (name, section, directive, expectedReason) in new[]
        {
            ("software", "Install.NT.Software", "AddSoftware=SampleSoftware,0,Software.Install", "AddSoftware"),
            ("coinstaller", "Install.NT.CoInstallers", "AddReg=CoInstaller.Register", "CoInstallers")
        })
        {
            var path = Path.Combine(directory, name + ".zip");
            using (var stream = File.Create(path))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                var inf = SampleDriverInf("PCI\\VEN_1234&DEV_5678") + $"\r\n[{section}]\r\n{directive}\r\n" +
                          (name == "software" ? "[Software.Install]\r\nCopyFiles=Driver.Copy\r\n" : "[CoInstaller.Register]\r\nHKR,,CoInstallers32,0x00010000,coinstaller.dll\r\n");
                Add("sample.inf", inf);
                Add("sample.cat", "catalog placeholder; test verifier is injected");
                Add("sample.sys", "test payload bytes");
                if (name == "coinstaller") Add("coinstaller.dll", "test co-installer bytes");

                void Add(string entryName, string content)
                {
                    using var writer = new StreamWriter(archive.CreateEntry(entryName).Open());
                    writer.Write(content);
                }
            }

            using var result = await new LocalDriverPackageAnalyzer(new FakeDriverPackageSignatureVerifier())
                .AnalyzeAsync(path, [device], platform);
            var candidate = result.Candidates.Single();
            Assert(candidate.Outcome == DriverPackageAnalysisOutcome.Incomplete && !candidate.CanPrepareInstallation,
                $"The reachable {name} behavior is not fully modeled, so it must block installation eligibility");
            Assert(candidate.Reasons.Any(reason => reason.Contains(expectedReason, StringComparison.OrdinalIgnoreCase)),
                $"The incomplete result should explain the unverified {name} behavior");
        }

        var emptyDependencyPath = Path.Combine(directory, "empty-dependency.zip");
        using (var stream = File.Create(emptyDependencyPath))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            var inf = SampleDriverInf("PCI\\VEN_1234&DEV_5678").Replace(
                "CopyFiles=Driver.Copy", "Include=\r\nNeeds=\r\nCopyFiles=Driver.Copy", StringComparison.Ordinal);
            foreach (var (entryName, content) in new[]
            {
                ("sample.inf", inf), ("sample.cat", "catalog placeholder"), ("sample.sys", "test payload bytes")
            })
            {
                using var writer = new StreamWriter(archive.CreateEntry(entryName).Open());
                writer.Write(content);
            }
        }
        using var emptyResult = await new LocalDriverPackageAnalyzer(new FakeDriverPackageSignatureVerifier())
            .AnalyzeAsync(emptyDependencyPath, [device], platform);
        Assert(emptyResult.Candidates.Single().Outcome == DriverPackageAnalysisOutcome.Incomplete &&
               !emptyResult.Candidates.Single().CanPrepareInstallation &&
               emptyResult.Candidates.Single().Reasons.Any(reason => reason.Contains("vazio", StringComparison.OrdinalIgnoreCase)),
            "Empty Include/Needs entries must not be silently discarded as if no required dependency existed");
    });
}

static async Task SystemInfDependenciesResolveRecursivelyAndDetectCycles()
{
    await WithTemporaryDirectory(async directory =>
    {
        var successRoot = Path.Combine(directory, "success");
        Directory.CreateDirectory(successRoot);
        await File.WriteAllTextAsync(Path.Combine(successRoot, "bus.inf"), """
            [Bus.Root]
            Include=base.inf
            Needs=Base.Root
            """);
        await File.WriteAllTextAsync(Path.Combine(successRoot, "base.inf"), """
            [Base.Root]
            """);
        var resolved = new WindowsSystemInfDependencyResolver(successRoot).Verify(
            [new(["bus.inf"], ["Bus.Root"])]);
        Assert(resolved.Status == DriverPackageDependencyStatus.Available &&
               resolved.Detail.Contains("2 INF", StringComparison.Ordinal) &&
               resolved.Detail.Contains("2 seção", StringComparison.OrdinalIgnoreCase),
            "the resolver should follow a system INF Needs section into its own Include/Needs graph");

        var cycleRoot = Path.Combine(directory, "cycle");
        Directory.CreateDirectory(cycleRoot);
        await File.WriteAllTextAsync(Path.Combine(cycleRoot, "first.inf"), """
            [First.Root]
            Include=second.inf
            Needs=Second.Root
            """);
        await File.WriteAllTextAsync(Path.Combine(cycleRoot, "second.inf"), """
            [Second.Root]
            Include=first.inf
            Needs=First.Root
            """);
        var cycle = new WindowsSystemInfDependencyResolver(cycleRoot).Verify(
            [new(["first.inf"], ["First.Root"])]);
        Assert(cycle.Status == DriverPackageDependencyStatus.CycleDetected &&
               cycle.Detail.Contains("Ciclo", StringComparison.OrdinalIgnoreCase),
            "a recursive Needs graph must detect the active section cycle instead of accepting a visited node as complete");

        var copyInfRoot = Path.Combine(directory, "copyinf");
        Directory.CreateDirectory(copyInfRoot);
        await File.WriteAllTextAsync(Path.Combine(copyInfRoot, "bus.inf"), "[Bus.Root]\r\nCopyINF=extra.inf\r\n");
        var copyInf = new WindowsSystemInfDependencyResolver(copyInfRoot).Verify(
            [new(["bus.inf"], ["Bus.Root"])]);
        Assert(copyInf.Status == DriverPackageDependencyStatus.Unavailable &&
               copyInf.Detail.Contains("CopyINF", StringComparison.OrdinalIgnoreCase),
            "a reachable system INF CopyINF directive may stage packages outside the analyzed dependency graph and must block eligibility");
    });
}

static async Task SystemInfDependenciesResolveDecoratedSections()
{
    await WithTemporaryDirectory(async directory =>
    {
        await File.WriteAllTextAsync(Path.Combine(directory, "bus.inf"), """
            [Bus.Install.NTamd64.10.0]
            Include=base.inf
            Needs=Base.Install.NTamd64.10.0
            """);
        await File.WriteAllTextAsync(Path.Combine(directory, "base.inf"), """
            [Base.Install.NTamd64.10.0]
            [Base.Install.NTamd64.10.0...99999]
            """);

        var result = new WindowsSystemInfDependencyResolver(directory).Verify(
            [new(["bus.inf"], ["Bus.Install.NTamd64.10.0"])]);
        Assert(result.Status == DriverPackageDependencyStatus.Available &&
               result.Detail.Contains("2 INF", StringComparison.Ordinal) &&
               result.Detail.Contains("2 seção", StringComparison.OrdinalIgnoreCase),
            "an explicitly decorated Needs section must resolve by its literal selected name without selecting unrelated build decorations");
    });
}

static async Task SystemInfDependencyResolverEnforcesLimits()
{
    await WithTemporaryDirectory(async directory =>
    {
        var root = Path.Combine(directory, "infs");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "bus.inf"), "[Bus.Root]\nInclude=base.inf\nNeeds=Base.Root\n");
        await File.WriteAllTextAsync(Path.Combine(root, "base.inf"), "[Base.Root]\n");
        var requirement = new DriverPackageInfDependencyRequirement(["bus.inf"], ["Bus.Root"]);

        var depth = new WindowsSystemInfDependencyResolver(root, maxDepth: 1).Verify([requirement]);
        var files = new WindowsSystemInfDependencyResolver(root, maxFiles: 1).Verify([requirement]);
        var bytes = new WindowsSystemInfDependencyResolver(root, maxSingleFileBytes: 16).Verify([requirement]);
        Assert(depth.Status == DriverPackageDependencyStatus.LimitExceeded &&
               files.Status == DriverPackageDependencyStatus.LimitExceeded &&
               bytes.Status == DriverPackageDependencyStatus.LimitExceeded,
            "depth, file-count, and byte limits must return incomplete/blocked results rather than imply compatibility");
    });
}

static void DriverStoreSelectionUsesUniqueBestRank()
{
    var requested = Path.Combine(Path.GetTempPath(), "oem42.inf");
    var other = Path.Combine(Path.GetTempPath(), "oem43.inf");
    var best = DriverStoreCandidateSelector.SelectUniqueBest(
        [new(requested, 0), new(other, 0x1000)], requested);
    var lower = DriverStoreCandidateSelector.SelectUniqueBest(
        [new(requested, 0x1000), new(other, 0)], requested);
    var tied = DriverStoreCandidateSelector.SelectUniqueBest(
        [new(requested, 0), new(other, 0)], requested);
    var absent = DriverStoreCandidateSelector.SelectUniqueBest([new(other, 0)], requested);
    Assert(best.Status == DriverStoreCandidateSelectionStatus.Selected && best.Candidate?.InfPath == requested,
        "the exact staged INF may be selected only when it is the unique best-ranked compatible package");
    Assert(lower.Status == DriverStoreCandidateSelectionStatus.CandidateNotBestRank &&
           tied.Status == DriverStoreCandidateSelectionStatus.BestRankAmbiguous &&
           absent.Status == DriverStoreCandidateSelectionStatus.CandidateNotFound,
        "lower rank, equal-best ambiguity, and missing staged INF must all refuse association");
}

static async Task DirectedDriverAssociationFailsClosedAndTracksRestart()
{
    await WithTemporaryDirectory(async directory =>
    {
        var zipPath = Path.Combine(directory, "directed-driver.zip");
        CreateSampleDriverZip(zipPath);
        const string targetId = "PCI\\VEN_1234&DEV_5678&SUBSYS_ABCD0001";
        var device = new DriverInfo("Sample Ethernet", "PCI\\INSTANCE-1", "Current vendor", "1.2.3.4", null,
            "old.inf", true, "Current signer", 0, HardwareIds: [targetId]);
        using var analysis = await new LocalDriverPackageAnalyzer(new FakeDriverPackageSignatureVerifier())
            .AnalyzeAsync(zipPath, [device], new("amd64", 10, 0, 26300));
        var candidate = analysis.Candidates.Single();

        async Task<(DriverPackageInstallationService Service, DriverPackageInstallReview Review, Guid OperationId, FakeDriverInventoryService Inventory)> Prepare(
            string name, FakeDriverPackageInstaller installer)
        {
            var inventory = new FakeDriverInventoryService([device]);
            var consent = new PlanConsentService();
            var journal = new OperationJournalCoordinator(consent);
            var store = new JsonOperationJournalStore(Path.Combine(directory, name));
            var service = new DriverPackageInstallationService(inventory, installer,
                new FakeDriverPackageSignatureVerifier(), store, consent, journal,
                new TestEnabledDriverMutationPolicy());
            var review = service.CreateReview(candidate, device, new(true, "x64", 26300));
            var capture = await service.CaptureAsync(review, consequencesAccepted: true);
            return (service, review, capture.OperationId, inventory);
        }

        var stageFailureAdapter = new FakeDriverPackageInstaller
        {
            StageResult = new(false, 5, false, "simulated staging access denied")
        };
        var stageFailure = await Prepare("stage-failure", stageFailureAdapter);
        var failedStage = await stageFailure.Service.StageToDriverStoreAsync(stageFailure.Review, stageFailure.OperationId);
        var afterFailedStage = await stageFailure.Service.AssociateWithTargetDeviceAsync(stageFailure.Review, stageFailure.OperationId);
        Assert(failedStage.Status == DriverPackageInstallStepStatus.AccessDenied &&
               afterFailedStage.Status == DriverPackageInstallStepStatus.RefusedByStateConflict &&
               stageFailureAdapter.AssociationCalls == 0,
            "failed staging must leave no receipt and make directed association impossible");

        var removedAdapter = new FakeDriverPackageInstaller();
        var removed = await Prepare("device-removed", removedAdapter);
        await removed.Service.StageToDriverStoreAsync(removed.Review, removed.OperationId);
        removed.Inventory.Drivers = Array.Empty<DriverInfo>();
        var removedResult = await removed.Service.AssociateWithTargetDeviceAsync(removed.Review, removed.OperationId);
        Assert(removedResult.Status == DriverPackageInstallStepStatus.RefusedByStateConflict && removedAdapter.AssociationCalls == 0,
            "a removed target must be caught by a fresh inventory read before the native adapter is called");

        var changedAdapter = new FakeDriverPackageInstaller();
        var changed = await Prepare("identity-changed", changedAdapter);
        await changed.Service.StageToDriverStoreAsync(changed.Review, changed.OperationId);
        changed.Inventory.Drivers = [device with { HardwareIds = ["PCI\\VEN_9999&DEV_0001"] }];
        var changedResult = await changed.Service.AssociateWithTargetDeviceAsync(changed.Review, changed.OperationId);
        Assert(changedResult.Status == DriverPackageInstallStepStatus.RefusedByStateConflict && changedAdapter.AssociationCalls == 0,
            "changed Hardware IDs must block before association; the native adapter adds a final SetupAPI identity reread");

        var refusedAdapter = new FakeDriverPackageInstaller
        {
            AssociationResult = new(false, null, false, "simulated candidate rank conflict", Refusal: DriverPackageCommandRefusal.CandidateNotBestRank)
        };
        var refused = await Prepare("rank-refused", refusedAdapter);
        await refused.Service.StageToDriverStoreAsync(refused.Review, refused.OperationId);
        var refusedResult = await refused.Service.AssociateWithTargetDeviceAsync(refused.Review, refused.OperationId);
        var associationRequest = refusedAdapter.LastAssociationRequest;
        Assert(refusedResult.Status == DriverPackageInstallStepStatus.RefusedByStateConflict &&
               associationRequest?.DeviceInstanceId == device.DeviceId &&
               associationRequest?.ExpectedHardwareId == targetId &&
               associationRequest?.StagedInfPath == refusedAdapter.StagedInfPath,
            "association must pass the exact instance, expected Hardware ID, current INF, and persisted staged INF; rank refusal is recorded as conflict");

        var rebootAdapter = new FakeDriverPackageInstaller
        {
            AssociationResult = new(true, 0, true, "simulated DiInstallDevice needs reboot")
        };
        var reboot = await Prepare("reboot-pending", rebootAdapter);
        await reboot.Service.StageToDriverStoreAsync(reboot.Review, reboot.OperationId);
        var rebootResult = await reboot.Service.AssociateWithTargetDeviceAsync(reboot.Review, reboot.OperationId);
        Assert(rebootResult.Status == DriverPackageInstallStepStatus.DeviceAssociationPendingRestart &&
               rebootResult.RebootMayBeRequired == true && reboot.Inventory.Drivers.Single().InfName == "old.inf",
            "restart-required result must remain pending, avoid auto reboot, and not claim the candidate is active");
    });
}

static async Task GenericInfModelMatchingUsesOnlyDeviceIds()
{
    await WithTemporaryDirectory(async directory =>
    {
        var zipPath = Path.Combine(directory, "model-id.zip");
        CreateSampleDriverZip(zipPath);
        var device = new DriverInfo("Target", "PCI\\TARGET", null, "1", null, "old.inf", true, null, 0,
            HardwareIds: ["PCI\\VEN_1234&DEV_5678&SUBSYS_ABCD0001"]);
        using var result = await new LocalDriverPackageAnalyzer(new FakeDriverPackageSignatureVerifier())
            .AnalyzeAsync(zipPath, [device], new("amd64", 10, 0, 26300));
        var candidate = result.Candidates.Single();
        Assert(candidate.DeviceMatches.Single().MatchedId == "PCI\\VEN_1234&DEV_5678&SUBSYS_ABCD0001",
            "The install section name from the first Models field must never be misread as a Hardware ID");
        Assert(candidate.CanPrepareInstallation, "A unique exact device match and one resolved install section should remain eligible for preview");
    });
}

static async Task LocalInfAnalysisIsReadOnly()
{
    await WithTemporaryDirectory(async directory =>
    {
        var infPath = Path.Combine(directory, "sample.inf");
        await File.WriteAllTextAsync(infPath, SampleDriverInf("PCI\\VEN_1234&DEV_5678&SUBSYS_ABCD0001"));
        await File.WriteAllTextAsync(Path.Combine(directory, "sample.cat"), "catalog");
        await File.WriteAllTextAsync(Path.Combine(directory, "sample.sys"), "driver");
        var device = new DriverInfo("Sample device", "PCI\\INSTANCE", "Old vendor", "1", null, "old.inf", true, null, 0,
            HardwareIds: ["PCI\\VEN_1234&DEV_5678&SUBSYS_ABCD0001"]);
        using var result = await new LocalDriverPackageAnalyzer(new FakeDriverPackageSignatureVerifier())
            .AnalyzeAsync(infPath, [device], new("amd64", 10, 0, 26300));
        Assert(result.Candidates.Single().Outcome == DriverPackageAnalysisOutcome.CompatibleAccordingToChecks,
            "a direct local INF should use the same metadata checks without extracting or changing files");
        Assert(File.Exists(infPath) && File.Exists(Path.Combine(directory, "sample.sys")),
            "disposing a direct INF analysis must never remove or modify the user's package folder");
    });
}

static async Task AuxiliaryInfIsNotDriverCandidate()
{
    await WithTemporaryDirectory(async directory =>
    {
        var infPath = Path.Combine(directory, "Autorun.inf");
        await File.WriteAllTextAsync(infPath, "[autorun]\r\nopen=setup.exe\r\n");
        using var result = await new LocalDriverPackageAnalyzer(new FakeDriverPackageSignatureVerifier())
            .AnalyzeAsync(infPath, Array.Empty<DriverInfo>(), new("amd64", 10, 0, 26300));
        Assert(result.Candidates.Single().Outcome == DriverPackageAnalysisOutcome.Incomplete &&
               !result.Candidates.Single().CanPrepareInstallation &&
               result.Candidates.Single().Reasons.Any(reason => reason.Contains("INF de driver PnP", StringComparison.OrdinalIgnoreCase)),
            "an auxiliary INF selected directly must be reported as incomplete and must not be treated as a driver package");
    });
}

static async Task DriverInstallPreviewAndDefaultPolicyAreSafe()
{
    await WithTemporaryDirectory(async directory =>
    {
        var zipPath = Path.Combine(directory, "local-driver.zip");
        CreateSampleDriverZip(zipPath);
        const string targetId = "PCI\\VEN_1234&DEV_5678&SUBSYS_ABCD0001";
        var device = new DriverInfo("Sample Ethernet", "PCI\\INSTANCE-1", "Current vendor", "1.2.3.4", null,
            "old.inf", true, "Current signer", 0, HardwareIds: [targetId]);
        using var analysis = await new LocalDriverPackageAnalyzer(new FakeDriverPackageSignatureVerifier())
            .AnalyzeAsync(zipPath, [device], new("x64", 10, 0, 26300));
        var candidate = analysis.Candidates.Single();
        var inventory = new FakeDriverInventoryService([device]);
        var signatureVerifier = new FakeDriverPackageSignatureVerifier();
        var installer = new FakeDriverPackageInstaller();
        var consent = new PlanConsentService();
        var journal = new OperationJournalCoordinator(consent);
        var store = new JsonOperationJournalStore(Path.Combine(directory, "history"));
        var blockedService = new DriverPackageInstallationService(
            inventory, installer, signatureVerifier, store, consent, journal,
            new BlockedDriverPackageMutationPolicy("blocked for tests"));
        var review = blockedService.CreateReview(candidate, device, new(true, "x64", 26300));
        Assert(review.RequiredPermissions.Contains("Administrador", StringComparison.Ordinal) &&
               review.PossibleImpact.Contains("pode cair", StringComparison.OrdinalIgnoreCase) &&
               review.PossibleImpact.Contains("reinicialização", StringComparison.OrdinalIgnoreCase) &&
               review.RecoveryLimits.Contains("não garante recuperação", StringComparison.OrdinalIgnoreCase),
            "preview should communicate privilege, likely network impact and recovery limits");
        Assert(review.InstallationReach.Contains("DiInstallDevice", StringComparison.Ordinal) &&
               review.InstallationReach.Contains("Driver Store da máquina", StringComparison.Ordinal) &&
               review.InstallationReach.Contains("CopyINF", StringComparison.Ordinal),
            "preview must distinguish one-devnode association from machine-wide staging and INF side effects");
        await AssertThrowsAsync<InvalidOperationException>(() => blockedService.CaptureAsync(review, consequencesAccepted: false));
        Assert((await store.LoadAllAsync()).Count == 0, "declined consent should not create a history capture");

        var capture = await blockedService.CaptureAsync(review, consequencesAccepted: true);
        Assert(capture.State == JournalOperationState.SnapshotsCaptured && capture.Consent?.CanAuthorizeRealApplication == false,
            "accepted consent and read-only baseline should reuse the shared journal without authorizing mutation");
        var blocked = await blockedService.StageToDriverStoreAsync(review, capture.OperationId);
        Assert(blocked.Status == DriverPackageInstallStepStatus.Blocked && installer.StageCalls == 0,
            "default production mutation policy should block staging before calling the backend");

        var enabledService = new DriverPackageInstallationService(
            inventory, installer, signatureVerifier, store, consent, journal, new TestEnabledDriverMutationPolicy());
        var secondReview = enabledService.CreateReview(candidate, device, new(true, "x64", 26300));
        var secondCapture = await enabledService.CaptureAsync(secondReview, consequencesAccepted: true);
        var staged = await enabledService.StageToDriverStoreAsync(secondReview, secondCapture.OperationId);
        Assert(staged.Status == DriverPackageInstallStepStatus.DriverStoreStagedNotAssociated,
            "a successful simulated stage must be reported separately from any device association");
        var journalAfterStage = await store.LoadAsync(secondCapture.OperationId);
        Assert(journalAfterStage?.Events.Any(item => item.EventType == "DriverPackageStagedToDriverStore") == true,
            "stage-only outcome should be recorded in the existing operation history");
        var associated = await enabledService.AssociateWithTargetDeviceAsync(secondReview, secondCapture.OperationId);
        Assert(associated.Status == DriverPackageInstallStepStatus.DeviceAssociationNotVerified && installer.AssociationCalls == 1,
            "an unchanged simulated inventory must leave device association explicitly unverified after backend success");
    });
}

static void CreateSampleDriverZip(string path, bool includeDriverBinary = true)
{
    using var stream = File.Create(path);
    using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
    Add("sample.inf", SampleDriverInf("PCI\\VEN_1234&DEV_5678&SUBSYS_ABCD0001"));
    Add("sample.cat", "catalog placeholder; test verifier is injected");
    if (includeDriverBinary) Add("sample.sys", "test payload bytes");
    Add("setup.exe", "dummy bytes; must never be executed");
    Add("Autorun.inf", "[autorun]\r\nopen=setup.exe\r\n");

    void Add(string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }
}

static string SampleDriverInf(string hardwareId) => $"""
    [Version]
    Signature="$Windows NT$"
    Class=Net
    Provider=Sample Provider
    DriverVer=01/01/2025,1.2.3.4
    CatalogFile=sample.cat
    [Manufacturer]
    %Maker%=Models,NTamd64.10.0...22000
    [Models.NTamd64.10.0...22000]
    %Device%=Install,{hardwareId}
    [Install]
    CopyFiles=Driver.Copy
    [Install.NT]
    CopyFiles=Driver.Copy
    [Driver.Copy]
    sample.sys
    [SourceDisksFiles]
    sample.sys=1
    [Strings]
    Maker="Sample"
    Device="Sample Device"
    """;

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

static async Task PowerModeCaptureRequiresFullBaseline()
{
    await WithTemporaryDirectory(async directory =>
    {
        var adapter = new FakePowerModeAdapter(UserConfiguredPowerMode.Balanced, UserConfiguredPowerMode.Balanced)
        {
            DcModeIdOverride = null,
            DcErrorCode = 50
        };
        var store = new JsonPowerModeOperationStore(Path.Combine(directory, "real-power-mode"));
        await AssertThrowsAsync<PlatformNotSupportedException>(() => new PowerModeOperationService(adapter, store)
            .CaptureAsync(UserConfiguredPowerMode.BestPerformance, targetDcMode: null));
        Assert(adapter.Writes.Count == 0, "an incomplete baseline must be refused without a setter");
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

static async Task PowerModeApplyDetectsActivePlanConflict()
{
    await WithTemporaryDirectory(async directory =>
    {
        var adapter = new FakePowerModeAdapter(UserConfiguredPowerMode.Balanced, UserConfiguredPowerMode.Balanced);
        var store = new JsonPowerModeOperationStore(Path.Combine(directory, "real-power-mode"));
        var service = new PowerModeOperationService(adapter, store, new ConfirmedPowerModeMutationPolicy(enabled: true));
        var capture = await service.CaptureAsync(UserConfiguredPowerMode.BestPerformance, targetDcMode: null);
        adapter.ActivePlanId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var result = await service.ApplyAsync(capture.OperationId,
            MakePowerModeApproval(capture, PowerModeAction.Apply, ac: true));
        Assert(result.State == PowerModeOperationState.Conflict, "a changed active plan must make the approved capture stale");
        Assert(adapter.Writes.Count == 0, "plan drift must be detected before any setter");
    });
}

static async Task PowerModeApplyVerifiesUnselectedDc()
{
    await WithTemporaryDirectory(async directory =>
    {
        var adapter = new FakePowerModeAdapter(UserConfiguredPowerMode.Balanced, UserConfiguredPowerMode.Balanced);
        var store = new JsonPowerModeOperationStore(Path.Combine(directory, "real-power-mode"));
        var service = new PowerModeOperationService(adapter, store, new ConfirmedPowerModeMutationPolicy(enabled: true));
        var capture = await service.CaptureAsync(UserConfiguredPowerMode.BestPerformance, targetDcMode: null);
        adapter.AfterAcWrite = () => adapter.DcMode = UserConfiguredPowerMode.BestEfficiency;
        var result = await service.ApplyAsync(capture.OperationId,
            MakePowerModeApproval(capture, PowerModeAction.Apply, ac: true));
        Assert(result.State == PowerModeOperationState.MutationUnverified,
            "a change to unselected DC during AC write must never be reported as verified");
        Assert(adapter.Writes.Select(write => write.Side).SequenceEqual(["AC"]), "DC must not be written as a side effect");
    });
}

static async Task PowerModeApplyVerifiesActivePlan()
{
    await WithTemporaryDirectory(async directory =>
    {
        var adapter = new FakePowerModeAdapter(UserConfiguredPowerMode.Balanced, UserConfiguredPowerMode.Balanced);
        var store = new JsonPowerModeOperationStore(Path.Combine(directory, "real-power-mode"));
        var service = new PowerModeOperationService(adapter, store, new ConfirmedPowerModeMutationPolicy(enabled: true));
        var capture = await service.CaptureAsync(UserConfiguredPowerMode.BestPerformance, targetDcMode: null);
        adapter.AfterAcWrite = () => adapter.ActivePlanId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var result = await service.ApplyAsync(capture.OperationId,
            MakePowerModeApproval(capture, PowerModeAction.Apply, ac: true));
        Assert(result.State == PowerModeOperationState.MutationUnverified,
            "a changed active plan after write must never be reported as verified");
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

static async Task PowerModeRevertRefusesChangedActivePlan()
{
    await WithTemporaryDirectory(async directory =>
    {
        var adapter = new FakePowerModeAdapter(UserConfiguredPowerMode.Balanced, UserConfiguredPowerMode.Balanced);
        var store = new JsonPowerModeOperationStore(Path.Combine(directory, "real-power-mode"));
        var service = new PowerModeOperationService(adapter, store, new ConfirmedPowerModeMutationPolicy(enabled: true));
        var capture = await service.CaptureAsync(UserConfiguredPowerMode.BestPerformance, targetDcMode: null);
        _ = await service.ApplyAsync(capture.OperationId,
            MakePowerModeApproval(capture, PowerModeAction.Apply, ac: true));
        var writeCount = adapter.Writes.Count;
        adapter.ActivePlanId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var result = await service.RevertAsync(capture.OperationId,
            MakePowerModeApproval(capture, PowerModeAction.Revert, recovery: true));
        Assert(result.State == PowerModeOperationState.Conflict, "a changed active plan must refuse restoration");
        Assert(adapter.Writes.Count == writeCount, "no restoration setter may run after active-plan drift");
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

static async Task PowerModeHistoryViewModelShowsVerifiedCycle()
{
    await WithTemporaryDirectory(async directory =>
    {
        var adapter = new FakePowerModeAdapter(UserConfiguredPowerMode.Balanced, UserConfiguredPowerMode.Balanced);
        var store = new JsonPowerModeOperationStore(Path.Combine(directory, "RealPowerMode"));
        var service = new PowerModeOperationService(adapter, store, new ConfirmedPowerModeMutationPolicy(enabled: true));
        var capture = await service.CaptureAsync(UserConfiguredPowerMode.BestPerformance, targetDcMode: null);
        var viewModel = CreateProfilesPageViewModel(directory, adapter);

        await viewModel.LoadHistoryAsync();
        Assert(viewModel.PowerMode.History.Count == 1 &&
            viewModel.PowerMode.History[0].StateLabel == "CAPTURA REAL PERSISTIDA · NÃO APLICADA",
            "the visual history model must distinguish a persisted capture from an applied change");
        Assert(viewModel.PowerMode.History[0].Summary.StartsWith("Proposta capturada:", StringComparison.Ordinal),
            "a stored target must be presented as a proposal, not as the current setting");

        var applied = await service.ApplyAsync(capture.OperationId,
            MakePowerModeApproval(capture, PowerModeAction.Apply, ac: true));
        Assert(applied.State == PowerModeOperationState.Verified, "fake adapter must reach the application-verified state");
        await viewModel.LoadHistoryAsync();
        Assert(viewModel.PowerMode.History.Single().StateLabel == "PREFERÊNCIA CONFIGURADA CONFIRMADA POR LEITURA POSTERIOR",
            "the view model must label an application confirmed by readback");

        var reverted = await service.RevertAsync(capture.OperationId,
            MakePowerModeApproval(capture, PowerModeAction.Revert, recovery: true));
        Assert(reverted.State == PowerModeOperationState.Reverted, "fake adapter must reach the restoration-verified state");
        await viewModel.LoadHistoryAsync();
        Assert(viewModel.PowerMode.History.Single().StateLabel == "PREFERÊNCIAS CAPTURADAS RESTAURADAS POR LEITURA",
            "the view model must show the final verified restoration state");
        Assert(viewModel.PowerMode.HistoryLoadSucceeded &&
            viewModel.PowerMode.HistoryLoadStatusMessage.Contains("1 operação", StringComparison.Ordinal),
            "the visual flow must confirm that the persisted real-operation record was loaded");
        Assert(viewModel.History.Count == 0,
            "real power-mode records remain separate from synthetic/local demonstration history");

        viewModel.IncludeSyntheticDemo = true;
        viewModel.ConsentDecisions.Single().Accepted = true;
        Assert(viewModel.SimulateCommand.CanExecute(null), "synthetic simulation requires the visible explicit consent");
        viewModel.SimulateCommand.Execute(null);
        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (viewModel.IsRunning && DateTime.UtcNow < timeout) await Task.Delay(5);
        Assert(!viewModel.IsRunning && viewModel.History.Single().DataLabel == "DADOS SINTÉTICOS",
            "the local demonstration must remain visibly classified as synthetic in a separate history list");
    });
}

static async Task PowerModeHistoryViewModelSurfacesLoadFailure()
{
    await WithTemporaryDirectory(async directory =>
    {
        var operationDirectory = Path.Combine(directory, "RealPowerMode");
        Directory.CreateDirectory(operationDirectory);
        var invalidId = Guid.NewGuid();
        await File.WriteAllTextAsync(Path.Combine(operationDirectory, invalidId.ToString("D") + ".json"), "{ broken");
        var viewModel = CreateProfilesPageViewModel(directory,
            new FakePowerModeAdapter(UserConfiguredPowerMode.Balanced, UserConfiguredPowerMode.Balanced));

        await viewModel.LoadHistoryAsync();

        Assert(!viewModel.PowerMode.HistoryLoadSucceeded,
            "an unreadable diary must not be reported as a successful empty load");
        Assert(viewModel.PowerMode.History.Count == 0 &&
            viewModel.PowerMode.HistoryLoadStatusMessage.Contains("lista vazia não confirma", StringComparison.Ordinal),
            "the visible status must explain that an empty collection after failure is inconclusive");
    });
}

static ProfilesPageViewModel CreateProfilesPageViewModel(string historyDirectory, IPowerModeAdapter adapter) => new(
    OptimizerProfileCatalog.CreateDefault(),
    new AdjustmentCatalog([]),
    new FakeVideoIdlePowerAdapter(600),
    adapter,
    historyDirectory);

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
        var entry = coordinator.CreatePlanned(plan, "custom", JournalDataOrigin.SyntheticDemonstration);
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
        var entry = coordinator.CreatePlanned(plan, "custom", JournalDataOrigin.SyntheticDemonstration);
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
        var entry = coordinator.CreatePlanned(plan, "custom", JournalDataOrigin.SyntheticDemonstration);
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
    var planned = coordinator.CreatePlanned(plan, "custom", JournalDataOrigin.SyntheticDemonstration);
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

static void JournalEnumValuesRemainStable()
{
    Assert((int)JournalOperationState.Planned == 0 &&
        (int)JournalOperationState.ConsentRecorded == 1 &&
        (int)JournalOperationState.SnapshotsCaptured == 2 &&
        (int)JournalOperationState.MutationMayHaveStarted == 3 &&
        (int)JournalOperationState.Failed == 4 &&
        (int)JournalOperationState.Interrupted == 5 &&
        (int)JournalOperationState.SimulationCompleted == 6,
        "existing JSON numeric states must retain their original values");
    var (_, _, plan) = CreateTwoItemPlan();
    var coordinator = new OperationJournalCoordinator(new PlanConsentService());
    var realPlanEntry = coordinator.CreatePlanned(plan, "custom");
    AssertThrows<InvalidOperationException>(() => coordinator.MarkSimulationCompleted(realPlanEntry));
    var syntheticEntry = coordinator.CreatePlanned(plan, "custom", JournalDataOrigin.SyntheticDemonstration);
    AssertThrows<InvalidOperationException>(() => coordinator
        .MarkSimulationCompleted(syntheticEntry with { State = JournalOperationState.MutationVerified }));
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

static void RegistryPreferencePlanIsSpecificAndBounded()
{
    var adapter = new FakeRegistryPreferenceAdapter(RegistryValueState.MissingValue);
    var management = new FakeRegistryManagementInspector(RegistryManagementStatus.Unmanaged);
    var consent = new PlanConsentService();
    var service = new RegistryPreferenceOperationService(
        adapter,
        management,
        new JsonOperationJournalStore(Path.Combine(Path.GetTempPath(), "unused-registry-review")),
        consent,
        new OperationJournalCoordinator(consent));
    var review = service.CreateReview(new PlatformDescriptor(true, "x64", 26300));

    Assert(review.Plan.Items.Single().AdjustmentId == RegistryPreferenceCatalog.AdjustmentId,
        "review should contain only the stable registry adjustment");
    Assert(review.Plan.Items.Single().Area == AdjustmentArea.Registry &&
        review.Plan.Items.Single().Risk == AdjustmentRiskLevel.Low,
        "showing extensions should be classified as a low-risk interface preference");
    Assert(RegistryPreferenceCatalog.FileExtensionsAddress.Hive == RegistryPreferenceHive.CurrentUser &&
        RegistryPreferenceCatalog.FileExtensionsAddress.KeyPath == @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" &&
        RegistryPreferenceCatalog.FileExtensionsAddress.ValueName == "HideFileExt" &&
        RegistryPreferenceCatalog.FileExtensionsAddress.View == RegistryPreferenceView.Registry64 &&
        RegistryPreferenceCatalog.ShowFileExtensionsValue == RegistryTypedValue.DWord(0),
        "the exact HKCU path, value, view, type, and show target should be fixed");
    Assert(review.Summary.Contains("sem ganho de desempenho", StringComparison.Ordinal),
        "the review must not imply performance benefit");

    var hide = service.CreateReview(new PlatformDescriptor(true, "x64", 26300), FileExtensionVisibility.Hide);
    Assert(hide.TargetValue == RegistryTypedValue.DWord(1) &&
        hide.Plan.Items.Single().Risk == AdjustmentRiskLevel.Moderate &&
        hide.Plan.Items.Single().Consequence.Contains("ambíguos", StringComparison.Ordinal),
        "hiding extensions must have its own consequence and higher risk");
    AssertThrows<PlatformNotSupportedException>(() =>
        service.CreateReview(new PlatformDescriptor(true, "x86", 26300)));
    AssertThrows<PlatformNotSupportedException>(() =>
        service.CreateReview(new PlatformDescriptor(false, "x64", null)));
    Assert(RegistryValueState.MissingKey.IsValid && RegistryValueState.MissingValue.IsValid,
        "missing key and existing key with missing value must both be representable");
    Assert(!new RegistryValueState(true, true, new RegistryTypedValue(RegistryStoredValueKind.DWord, "1")).IsValid &&
        !new RegistryValueState(true, true, new RegistryTypedValue(RegistryStoredValueKind.Binary, "not-base64")).IsValid,
        "malformed captured type data must be rejected before a future restore");
}

static async Task RegistryCapturePreservesAbsence()
{
    await WithTemporaryDirectory(async directory =>
    {
        var initial = RegistryValueState.MissingValue;
        var (service, adapter, _, entry) = await CreateRegistryCaptureAsync(directory, initial);
        var store = new JsonOperationJournalStore(Path.Combine(directory, "registry-history"));
        var loaded = await store.LoadAsync(entry.OperationId);
        var snapshot = loaded!.Snapshots.Single();
        var captured = System.Text.Json.JsonSerializer.Deserialize<RegistryValueState>(snapshot.Values["RegistryStateJson"]);
        Assert(loaded.State == JournalOperationState.SnapshotsCaptured && loaded.Consent is not null,
            "the existing shared journal and consent receipt should be used");
        Assert(captured == initial && captured is { KeyExists: true, ValueExists: false, Value: null },
            "an absent value must remain distinct from a present REG_DWORD 0");
        Assert(adapter.WriteCalls == 0 && adapter.DeleteCalls == 0,
            "capture must never write to the Registry");
        Assert(loaded.Snapshots.Single().Values.ContainsKey("PlanJson") &&
            loaded.Snapshots.Single().Values.ContainsKey("CaptureFingerprint"),
            "shared history should bind the reviewed plan and captured state");
    });
}

static async Task RegistryCaptureRefusesStaleReview()
{
    await WithTemporaryDirectory(async directory =>
    {
        var original = new RegistryValueState(true, true, RegistryTypedValue.DWord(0));
        var adapter = new FakeRegistryPreferenceAdapter(original);
        var management = new FakeRegistryManagementInspector(RegistryManagementStatus.Unmanaged);
        var consent = new PlanConsentService();
        var store = new JsonOperationJournalStore(Path.Combine(directory, "registry-history"));
        var service = new RegistryPreferenceOperationService(
            adapter, management, store, consent, new OperationJournalCoordinator(consent));
        var review = service.CreateReview(new PlatformDescriptor(true, "x64", 26300), FileExtensionVisibility.Hide);
        adapter.State = new RegistryValueState(true, true, RegistryTypedValue.DWord(2));

        var entry = await service.CaptureAsync(review, consequenceAccepted: true);
        Assert(entry.State == JournalOperationState.Conflict && entry.Snapshots.Count == 0 &&
            entry.Events.Last().EventType == "Conflict",
            "a changed value since preview must require a fresh review instead of silently recapturing it");
        Assert(adapter.WriteCalls == 0 && adapter.DeleteCalls == 0,
            "stale review detection must remain read-only");
    });
}

static async Task RegistryRestorePreservesTypedValues()
{
    RegistryTypedValue[] values =
    [
        new(RegistryStoredValueKind.String, "legacy string"),
        new(RegistryStoredValueKind.ExpandString, "%TEMP%\\legacy"),
        RegistryTypedValue.DWord(0),
        new(RegistryStoredValueKind.QWord, "0000000000000012"),
        new(RegistryStoredValueKind.MultiString, System.Text.Json.JsonSerializer.Serialize(new[] { "first", "second" })),
        new(RegistryStoredValueKind.Binary, "AP8="),
        new(RegistryStoredValueKind.None, "AQI=")
    ];

    await WithTemporaryDirectory(async directory =>
    {
        foreach (var value in values)
        {
            var operationDirectory = Path.Combine(directory, value.Kind.ToString());
            var original = new RegistryValueState(true, true, value);
            var (service, adapter, _, entry) = await CreateRegistryCaptureAsync(
                operationDirectory, original, FileExtensionVisibility.Hide);
            var applied = await service.ApplyAsync(entry.OperationId,
                MakeRegistryApproval(entry, RegistryPreferenceAction.Apply));
            Assert(applied.State == JournalOperationState.MutationVerified &&
                adapter.State == new RegistryValueState(true, true, RegistryPreferenceCatalog.HideFileExtensionsValue),
                $"{value.Kind} prior data should be replaced by the exact reviewed DWORD target");

            var restored = await service.RestoreAsync(entry.OperationId,
                MakeRegistryApproval(entry, RegistryPreferenceAction.Restore));
            Assert(restored.State == JournalOperationState.RestoredVerified && adapter.State == original,
                $"{value.Kind} kind and data should be restored byte-for-byte/canonically");
        }
    });
}

static async Task RegistryRestoreDeletesOnlyCreatedValue()
{
    await WithTemporaryDirectory(async directory =>
    {
        var initial = RegistryValueState.MissingValue;
        var (service, adapter, _, entry) = await CreateRegistryCaptureAsync(
            directory, initial, FileExtensionVisibility.Hide);
        adapter.OtherValueCount = 3;
        var applied = await service.ApplyAsync(entry.OperationId,
            MakeRegistryApproval(entry, RegistryPreferenceAction.Apply));
        Assert(applied.State == JournalOperationState.MutationVerified && adapter.WriteCalls == 1,
            "hiding extensions from an absent-value default should create only the named value");

        var restored = await service.RestoreAsync(entry.OperationId,
            MakeRegistryApproval(entry, RegistryPreferenceAction.Restore));
        Assert(restored.State == JournalOperationState.RestoredVerified && adapter.State == initial,
            "restoration should return the named value to absent");
        Assert(adapter.DeleteCalls == 1 && adapter.OtherValueCount == 3,
            "restoration must delete only HideFileExt and preserve the parent key and other values");
    });
}

static async Task RegistryAccessDeniedPreventsWrite()
{
    await WithTemporaryDirectory(async directory =>
    {
        var (service, adapter, _, entry) = await CreateRegistryCaptureAsync(
            directory,
            new RegistryValueState(true, true, RegistryTypedValue.DWord(0)),
            FileExtensionVisibility.Hide);
        adapter.NextReadFailure = new UnauthorizedAccessException("Injected access denied.");
        var result = await service.ApplyAsync(entry.OperationId,
            MakeRegistryApproval(entry, RegistryPreferenceAction.Apply));
        Assert(result.Events.Last().EventType == "WriteNotAttempted" &&
            result.Events.Last().Message.Contains("UnauthorizedAccessException", StringComparison.Ordinal),
            "access denial should be recorded as a pre-write failure");
        Assert(adapter.WriteCalls == 0 && adapter.DeleteCalls == 0,
            "access denial must not reach a setter");
    });
}

static async Task RegistryConflictsRefuseWrites()
{
    await WithTemporaryDirectory(async directory =>
    {
        var (service, adapter, _, entry) = await CreateRegistryCaptureAsync(
            Path.Combine(directory, "apply"), new RegistryValueState(true, true, RegistryTypedValue.DWord(0)),
            FileExtensionVisibility.Hide);
        adapter.State = new RegistryValueState(true, true, RegistryTypedValue.DWord(2));
        var conflict = await service.ApplyAsync(entry.OperationId,
            MakeRegistryApproval(entry, RegistryPreferenceAction.Apply));
        Assert(conflict.Events.Last().EventType == "Conflict" && adapter.WriteCalls == 0,
            "external drift after capture must block the apply setter");
    });

    await WithTemporaryDirectory(async directory =>
    {
        var original = new RegistryValueState(true, true, RegistryTypedValue.DWord(0));
        var (service, adapter, _, entry) = await CreateRegistryCaptureAsync(
            Path.Combine(directory, "restore"), original, FileExtensionVisibility.Hide);
        await service.ApplyAsync(entry.OperationId,
            MakeRegistryApproval(entry, RegistryPreferenceAction.Apply));
        adapter.State = new RegistryValueState(true, true, RegistryTypedValue.DWord(2));
        var conflict = await service.RestoreAsync(entry.OperationId,
            MakeRegistryApproval(entry, RegistryPreferenceAction.Restore));
        Assert(conflict.Events.Last().EventType == "Conflict" && adapter.WriteCalls == 1 && adapter.DeleteCalls == 0,
            "an external value must never be overwritten during restoration");
    });
}

static async Task RegistryCheckpointDriftRefusesSetter()
{
    await WithTemporaryDirectory(async directory =>
    {
        var (service, adapter, _, entry) = await CreateRegistryCaptureAsync(
            Path.Combine(directory, "apply"),
            new RegistryValueState(true, true, RegistryTypedValue.DWord(0)),
            FileExtensionVisibility.Hide);
        adapter.AfterRead = () => adapter.State = new RegistryValueState(true, true, RegistryTypedValue.DWord(2));
        var conflict = await service.ApplyAsync(entry.OperationId,
            MakeRegistryApproval(entry, RegistryPreferenceAction.Apply));
        Assert(conflict.Events.Last().EventType == "Conflict" && adapter.WriteCalls == 0,
            "a change while persisting apply intent must be caught by the final pre-write read");
    });

    await WithTemporaryDirectory(async directory =>
    {
        var original = new RegistryValueState(true, true, RegistryTypedValue.DWord(0));
        var (service, adapter, _, entry) = await CreateRegistryCaptureAsync(
            Path.Combine(directory, "restore"), original, FileExtensionVisibility.Hide);
        await service.ApplyAsync(entry.OperationId,
            MakeRegistryApproval(entry, RegistryPreferenceAction.Apply));
        adapter.AfterRead = () => adapter.State = new RegistryValueState(true, true, RegistryTypedValue.DWord(2));
        var conflict = await service.RestoreAsync(entry.OperationId,
            MakeRegistryApproval(entry, RegistryPreferenceAction.Restore));
        Assert(conflict.Events.Last().EventType == "Conflict" && adapter.WriteCalls == 1 &&
            adapter.State.Value == RegistryTypedValue.DWord(2),
            "a change while persisting restore intent must not be overwritten");
    });
}

static async Task RegistryUnknownManagementBlocksApply()
{
    await WithTemporaryDirectory(async directory =>
    {
        var (service, adapter, _, entry) = await CreateRegistryCaptureAsync(
            directory,
            new RegistryValueState(true, true, RegistryTypedValue.DWord(0)),
            FileExtensionVisibility.Hide,
            RegistryManagementStatus.Unknown);
        var result = await service.ApplyAsync(entry.OperationId,
            MakeRegistryApproval(entry, RegistryPreferenceAction.Apply));
        Assert(result.Events.Last().EventType == "Conflict" && adapter.WriteCalls == 0,
            "unknown organization-management state must block writes even with an enabled test policy");
    });
}

static void RegistryManagementContextDoesNotProvePreferencePolicy()
{
    var assessment = RegistryManagementAssessmentBuilder.FromEvidence([
        new(RegistryManagementEvidenceKind.ActiveDirectoryDomainJoin, RegistryManagementEvidenceStatus.Detected,
            "fake NetGetJoinInformation", "local computer", "domain membership found"),
        new(RegistryManagementEvidenceKind.MicrosoftEntraDeviceJoin, RegistryManagementEvidenceStatus.Detected,
            "fake NetGetAadJoinInformation", "device", "device join found"),
        new(RegistryManagementEvidenceKind.MicrosoftEntraWorkAccount, RegistryManagementEvidenceStatus.Unknown,
            "fake NetGetAadJoinInformation", "current user", "not enumerated"),
        new(RegistryManagementEvidenceKind.MdmRegistration, RegistryManagementEvidenceStatus.Detected,
            "fake IsDeviceRegisteredWithManagement", "device", "MDM registered"),
        new(RegistryManagementEvidenceKind.GroupPolicyForPreference, RegistryManagementEvidenceStatus.NotDetected,
            "fake RSoP query", "user scope", "no exact setting in exposed data")
    ]);

    Assert(assessment.Status == RegistryManagementStatus.Unknown,
        "domain join, Entra join, and MDM registration alone must not be promoted to preference-managed or unmanaged");
    Assert(assessment.EvidenceItems.Count == 5 &&
        assessment.EvidenceItems.Single(item => item.Kind == RegistryManagementEvidenceKind.ActiveDirectoryDomainJoin).Status == RegistryManagementEvidenceStatus.Detected &&
        assessment.EvidenceItems.Single(item => item.Kind == RegistryManagementEvidenceKind.MdmRegistration).Status == RegistryManagementEvidenceStatus.Detected,
        "independent sources and their outcomes must remain visible");
}

static void RegistryManagementExactPolicyEvidenceIsSpecific()
{
    var assessment = RegistryManagementAssessmentBuilder.FromEvidence([
        new(RegistryManagementEvidenceKind.ActiveDirectoryDomainJoin, RegistryManagementEvidenceStatus.NotDetected,
            "fake NetGetJoinInformation", "local computer", "workgroup"),
        new(RegistryManagementEvidenceKind.MdmRegistration, RegistryManagementEvidenceStatus.NotDetected,
            "fake IsDeviceRegisteredWithManagement", "device", "not registered via this API"),
        new(RegistryManagementEvidenceKind.GroupPolicyForPreference, RegistryManagementEvidenceStatus.Detected,
            "fake RSOP_RegistryPolicySetting", "user scope", "exact HideFileExt policy entry")
    ]);

    Assert(assessment.Status == RegistryManagementStatus.Managed,
        "an exact RSoP policy entry for the reviewed preference must be identified as managed");
    Assert(assessment.Detail.Contains("escrita e restauração permanecem bloqueadas", StringComparison.OrdinalIgnoreCase),
        "detected policy evidence must explicitly keep mutations blocked");
}

static void RegistryManagementUncertainEvidenceIsConservative()
{
    var unknown = RegistryManagementAssessmentBuilder.FromEvidence([
        new(RegistryManagementEvidenceKind.ActiveDirectoryDomainJoin, RegistryManagementEvidenceStatus.AccessDenied,
            "fake NetGetJoinInformation", "local computer", "access denied"),
        new(RegistryManagementEvidenceKind.MicrosoftEntraDeviceJoin, RegistryManagementEvidenceStatus.Unavailable,
            "fake NetGetAadJoinInformation", "device", "entry point unavailable"),
        new(RegistryManagementEvidenceKind.MdmRegistration, RegistryManagementEvidenceStatus.Failed,
            "fake IsDeviceRegisteredWithManagement", "device", "failed"),
        new(RegistryManagementEvidenceKind.GroupPolicyForPreference, RegistryManagementEvidenceStatus.AccessDenied,
            "fake RSOP_RegistryPolicySetting", "computer scope", "access denied")
    ]);
    var empty = RegistryManagementAssessmentBuilder.FromEvidence([]);

    Assert(unknown.Status == RegistryManagementStatus.Unknown &&
        unknown.EvidenceItems.Any(item => item.Status == RegistryManagementEvidenceStatus.AccessDenied) &&
        unknown.EvidenceItems.Any(item => item.Status == RegistryManagementEvidenceStatus.Unavailable),
        "access denied and unavailable sources must remain distinct and must not imply unmanaged");
    Assert(empty.Status == RegistryManagementStatus.Unknown,
        "no collected evidence must be unknown, never unmanaged");
}

static void RegistryPolicyEvidenceMatchesOnlyExactPreference()
{
    var address = RegistryPreferenceCatalog.FileExtensionsAddress;
    Assert(RegistryManagementAssessmentBuilder.IsExactHideFileExtPolicy(
            "\\" + address.KeyPath.ToUpperInvariant(), "hidefileext"),
        "the exact key and value should match case-insensitively even with a leading slash");
    Assert(!RegistryManagementAssessmentBuilder.IsExactHideFileExtPolicy(
            address.KeyPath + "\\Subkey", address.ValueName) &&
        !RegistryManagementAssessmentBuilder.IsExactHideFileExtPolicy(
            address.KeyPath, "OtherValue"),
        "nearby policy paths or a different value must not be presented as policy for HideFileExt");
}

static async Task RegistryDefaultPolicyBlocksWrites()
{
    await WithTemporaryDirectory(async directory =>
    {
        var (service, adapter, _, entry) = await CreateRegistryCaptureAsync(
            directory,
            new RegistryValueState(true, true, RegistryTypedValue.DWord(0)),
            FileExtensionVisibility.Hide,
            RegistryManagementStatus.Unmanaged,
            useProductionDefaultPolicy: true);
        await AssertThrowsAsync<UnauthorizedAccessException>(() => service.ApplyAsync(entry.OperationId,
            MakeRegistryApproval(entry, RegistryPreferenceAction.Apply)));
        Assert(adapter.WriteCalls == 0,
            "the default production policy must refuse actual setters");
    });
}

static async Task RegistryJournalFailurePreventsWrite()
{
    await WithTemporaryDirectory(async directory =>
    {
        var storage = new FaultInjectingJournalStorage();
        var (service, adapter, _, entry) = await CreateRegistryCaptureAsync(
            directory,
            new RegistryValueState(true, true, RegistryTypedValue.DWord(0)),
            FileExtensionVisibility.Hide,
            RegistryManagementStatus.Unmanaged,
            storage);
        storage.NextWriteFailure = JournalWriteFailure.DiskFull;
        await AssertThrowsAsync<IOException>(() => service.ApplyAsync(entry.OperationId,
            MakeRegistryApproval(entry, RegistryPreferenceAction.Apply)));
        Assert(adapter.WriteCalls == 0 &&
            (await service.LoadHistoryAsync()).Single().State == JournalOperationState.SnapshotsCaptured,
            "a failed persisted recovery point must keep the last committed capture and prevent the setter");
    });
}

static async Task RegistryInterruptionRemainsManual()
{
    await WithTemporaryDirectory(async directory =>
    {
        var (service, adapter, _, entry) = await CreateRegistryCaptureAsync(
            directory,
            new RegistryValueState(true, true, RegistryTypedValue.DWord(0)),
            FileExtensionVisibility.Hide);
        adapter.AfterWrite = () => throw new OperationCanceledException("Injected interruption after the fake write.");
        var interrupted = await service.ApplyAsync(entry.OperationId,
            MakeRegistryApproval(entry, RegistryPreferenceAction.Apply));
        Assert(interrupted.State == JournalOperationState.Interrupted && adapter.WriteCalls == 1 && adapter.DeleteCalls == 0,
            "an interruption after the fake setter must persist uncertainty and avoid automatic rollback");
        Assert(adapter.State == new RegistryValueState(true, true, RegistryPreferenceCatalog.HideFileExtensionsValue),
            "the simulated post-write state should be left for conditional manual recovery");

        var restored = await service.RestoreAsync(entry.OperationId,
            MakeRegistryApproval(entry, RegistryPreferenceAction.Restore));
        Assert(restored.State == JournalOperationState.RestoredVerified &&
            adapter.State == new RegistryValueState(true, true, RegistryTypedValue.DWord(0)),
            "a separate explicit restoration call should be conditional and verified");
    });
}

static async Task RegistryMissingParentIsNotCreated()
{
    await WithTemporaryDirectory(async directory =>
    {
        var (service, adapter, _, entry) = await CreateRegistryCaptureAsync(
            directory, RegistryValueState.MissingKey, FileExtensionVisibility.Hide);
        var result = await service.ApplyAsync(entry.OperationId,
            MakeRegistryApproval(entry, RegistryPreferenceAction.Apply));
        Assert(result.Events.Last().Message.Contains("não cria chaves", StringComparison.Ordinal) &&
            adapter.WriteCalls == 0 && !adapter.State.KeyExists,
            "an absent parent key must be refused without creation");
    });
}

static async Task<(RegistryPreferenceOperationService Service, FakeRegistryPreferenceAdapter Adapter,
    FakeRegistryManagementInspector Management, OperationJournalEntry Entry)> CreateRegistryCaptureAsync(
    string directory,
    RegistryValueState initialState,
    FileExtensionVisibility targetVisibility = FileExtensionVisibility.Show,
    RegistryManagementStatus managementStatus = RegistryManagementStatus.Unmanaged,
    IOperationJournalStorage? storage = null,
    bool useProductionDefaultPolicy = false)
{
    var adapter = new FakeRegistryPreferenceAdapter(initialState);
    var management = new FakeRegistryManagementInspector(managementStatus);
    var consent = new PlanConsentService();
    var journalStore = new JsonOperationJournalStore(Path.Combine(directory, "registry-history"), storage);
    var service = new RegistryPreferenceOperationService(
        adapter,
        management,
        journalStore,
        consent,
        new OperationJournalCoordinator(consent),
        useProductionDefaultPolicy ? null : new ConfirmedRegistryMutationPolicy(enabled: true));
    var review = service.CreateReview(new PlatformDescriptor(true, "x64", 26300), targetVisibility);
    var entry = await service.CaptureAsync(review, consequenceAccepted: true);
    return (service, adapter, management, entry);
}

static RegistryPreferenceApproval MakeRegistryApproval(
    OperationJournalEntry entry,
    RegistryPreferenceAction action)
{
    var snapshot = entry.Snapshots.Single(item => item.AdjustmentId == RegistryPreferenceCatalog.AdjustmentId);
    var fingerprint = snapshot.Values["CaptureFingerprint"];
    return new RegistryPreferenceApproval(
        entry.OperationId,
        action,
        fingerprint,
        ConsequenceAccepted: true,
        OrganizationManagementReviewed: true,
        RegistryPreferenceOperationService.RequiredConfirmationPhrase(entry.OperationId, action));
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

static void ProgramCatalogIsBoundedAndDated()
{
    var catalog = ProgramCatalog.CreateDefault();
    Assert(catalog.Packages.Select(package => package.PackageId).ToHashSet(StringComparer.OrdinalIgnoreCase)
        .SetEquals(["7zip.7zip", "VideoLAN.VLC"]), "the initial program catalog must contain only the two reviewed exact IDs");
    Assert(catalog.Packages.All(package => package.SourceName == "winget"
        && package.ManifestReviewedOn == new DateOnly(2026, 10, 20)
        && package.ManifestUrl.StartsWith("https://github.com/microsoft/winget-pkgs/", StringComparison.Ordinal)),
        "each package should cite the dated Microsoft-hosted manifest reviewed for its exact ID");
    AssertThrows<ArgumentException>(() => new ProgramCatalog([
        catalog.Packages[0], catalog.Packages[0] with { DisplayName = "Duplicate" }]));
    AssertThrows<ArgumentException>(() => new ProgramCatalog([
        catalog.Packages[0] with { SourceName = "arbitrary-source" }]));
}

static void ProgramSourceStatusIsConservative()
{
    var missing = WinGetSourcePackageStatus.ToUserMessage(
        new WinGetSourcePackageStatus(WinGetSourcePackageRegistration.NotRegistered));
    Assert(missing.Contains("indisponível neste perfil", StringComparison.OrdinalIgnoreCase)
        && missing.Contains("recuperação", StringComparison.OrdinalIgnoreCase)
        && missing.Contains("Inventário local, navegação e demais páginas", StringComparison.Ordinal),
        "an absent source package must explain the profile-local outage and preserve unrelated app capabilities");

    var registered = WinGetSourcePackageStatus.ToUserMessage(
        new WinGetSourcePackageStatus(WinGetSourcePackageRegistration.RegisteredHealthy, "Ok"));
    Assert(registered.Contains("não confirma atualização", StringComparison.OrdinalIgnoreCase)
        && registered.Contains("bloqueada", StringComparison.OrdinalIgnoreCase),
        "AppX registration must not be represented as a functioning/current WinGet catalog");

    var unhealthy = WinGetSourcePackageStatus.ToUserMessage(
        new WinGetSourcePackageStatus(WinGetSourcePackageRegistration.RegisteredUnhealthy, "NeedsRemediation"));
    Assert(unhealthy.Contains("NeedsRemediation", StringComparison.Ordinal)
        && unhealthy.Contains("não foi validada", StringComparison.OrdinalIgnoreCase),
        "an unhealthy package status must be surfaced without declaring source availability");

    var unknown = WinGetSourcePackageStatus.ToUserMessage(
        new WinGetSourcePackageStatus(WinGetSourcePackageRegistration.Unknown, Diagnostic: "consulta AppX falhou"));
    Assert(unknown.Contains("não foi possível confirmar", StringComparison.OrdinalIgnoreCase)
        && unknown.Contains("inventário local permanece disponível", StringComparison.OrdinalIgnoreCase),
        "a failed status check must remain unknown and must not disable local inventory");
}

static void ProgramInstalledDetectionIsConservative()
{
    var package = ProgramCatalog.CreateDefault().Packages.Single(item => item.PackageId == "7zip.7zip");
    var exact = InstalledProgramMatcher.Match(package,
        new InstalledProgramInventory([new("7-Zip", "Igor Pavlov", "26.04", "computador")], []));
    Assert(exact.Status == InstalledProgramStatus.IdentifiedInstalled && exact.Version == "26.04",
        "exact display name and publisher should identify an uninstall entry");
    Assert(exact.Reasons.Single().Contains("não comprova origem WinGet", StringComparison.Ordinal),
        "registry detection must not claim the package was installed by WinGet");

    var installedX64 = InstalledProgramMatcher.Match(package,
        new InstalledProgramInventory([new("7-Zip 26.04 (x64)", "Igor Pavlov", "26.04", "computador")], []));
    Assert(installedX64.Status == InstalledProgramStatus.IdentifiedInstalled && installedX64.Version == "26.04",
        "the exact x64 DisplayName observed in Windows Programs and Features should be recognized conservatively");

    var partialExact = InstalledProgramMatcher.Match(package,
        new InstalledProgramInventory([new("7-Zip", "Igor Pavlov", "26.04", "computador")], ["partial read failure"]));
    Assert(partialExact.Status == InstalledProgramStatus.CheckIncomplete,
        "a partial inventory read must not turn one match into a unique result");

    var wrongPublisher = InstalledProgramMatcher.Match(package,
        new InstalledProgramInventory([new("7-Zip", "Someone Else", "26.04", "computador")], []));
    Assert(wrongPublisher.Status == InstalledProgramStatus.NotIdentified,
        "a matching name with an unrelated publisher must not be called installed");

    var ambiguous = InstalledProgramMatcher.Match(package,
        new InstalledProgramInventory([
            new("7-Zip", "Igor Pavlov", "26.04", "computador"),
            new("7-Zip", "Igor Pavlov", "25.01", "usuário")], []));
    Assert(ambiguous.Status == InstalledProgramStatus.Ambiguous,
        "multiple exact uninstall records must remain ambiguous");

    var incomplete = InstalledProgramMatcher.Match(package,
        new InstalledProgramInventory([], ["partial read failure"]));
    Assert(incomplete.Status == InstalledProgramStatus.CheckIncomplete,
        "partial registry access must not be represented as an absent program");
}

static void ProgramCommandFactoryIsBounded()
{
    var catalog = ProgramCatalog.CreateDefault();
    var consent = MakeProgramConsent("7zip.7zip");
    var arguments = ProgramInstallCommandFactory.BuildArguments(catalog, consent);
    Assert(arguments.SequenceEqual([
        "install", "--id", "7zip.7zip", "--exact", "--source", "winget", "--interactive",
        "--accept-source-agreements", "--accept-package-agreements"]),
        "WinGet call must be built as fixed ArgumentList values for one exact reviewed package");
    Assert(!arguments.Any(value => value is "--force" or "--silent" or "--scope" or "--override"
        or "--ignore-security-hash" or "--allow-reboot"),
        "command must not force, suppress installer UI, force scope, bypass hash checks or request reboot");
    AssertThrows<InvalidOperationException>(() => ProgramInstallCommandFactory.BuildArguments(catalog,
        MakeProgramConsent("arbitrary.command")));
    AssertThrows<InvalidOperationException>(() => ProgramInstallCommandFactory.BuildArguments(catalog,
        MakeProgramConsent("7zip.7zip") with { PackageAgreementAccepted = false }));
}

static async Task ProgramProductionPolicyBlocksRunner()
{
    var catalog = ProgramCatalog.CreateDefault();
    var runner = new FakeProgramInstallRunner();
    var service = new ProgramInstallService(catalog, runner,
        new BlockedProgramInstallExecutionPolicy("test production default block"));
    var results = await service.ExecuteAsync([
        new ProgramInstallRequest("7zip.7zip", MakeProgramConsent("7zip.7zip")),
        new ProgramInstallRequest("VideoLAN.VLC", MakeProgramConsent("VideoLAN.VLC"))]);
    Assert(results.Count == 2 && results.All(result => result.Outcome == ProgramInstallOutcome.ExecutionBlocked),
        "every selected package should receive an individual production-blocked outcome");
    Assert(runner.Calls.Count == 0, "blocked production policy must prevent any WinGet runner invocation");
}

static async Task ProgramInstallConsentIsPackageBound()
{
    var catalog = ProgramCatalog.CreateDefault();
    var runner = new FakeProgramInstallRunner();
    runner.Results.Enqueue(new(ProgramRunnerOutcome.Completed, 0));
    var service = new ProgramInstallService(catalog, runner, new TestProgramInstallExecutionPolicy(true));
    var wrongPackage = await service.ExecuteAsync([
        new ProgramInstallRequest("7zip.7zip", MakeProgramConsent("VideoLAN.VLC"))]);
    var missingAgreement = await service.ExecuteAsync([
        new ProgramInstallRequest("7zip.7zip", MakeProgramConsent("7zip.7zip") with { SourceAgreementAccepted = false })]);
    Assert(wrongPackage.Single().Outcome == ProgramInstallOutcome.ConsentRequired
        && missingAgreement.Single().Outcome == ProgramInstallOutcome.ConsentRequired,
        "consent must match package/source and explicitly include both agreements");
    Assert(runner.Calls.Count == 0, "invalid package-bound consent must never reach WinGet");
}

static async Task ProgramInstallReportsManagerUnavailable()
{
    var catalog = ProgramCatalog.CreateDefault();
    var runner = new FakeProgramInstallRunner();
    runner.Results.Enqueue(new(ProgramRunnerOutcome.ManagerUnavailable));
    var service = new ProgramInstallService(catalog, runner, new TestProgramInstallExecutionPolicy(true));
    var results = await service.ExecuteAsync([
        new ProgramInstallRequest("7zip.7zip", MakeProgramConsent("7zip.7zip")),
        new ProgramInstallRequest("VideoLAN.VLC", MakeProgramConsent("VideoLAN.VLC"))]);
    Assert(results.Select(result => result.Outcome).SequenceEqual([
        ProgramInstallOutcome.ManagerUnavailable, ProgramInstallOutcome.ManagerUnavailable]),
        "manager absence should be represented per selected package and stop further attempts");
    Assert(runner.Calls.Count == 1, "no additional process start should be attempted after manager absence");
}

static async Task ProgramInstallMapsMissingPackage()
{
    var runner = new FakeProgramInstallRunner();
    runner.Results.Enqueue(new(ProgramRunnerOutcome.Completed, unchecked((int)0x8A150014)));
    var service = new ProgramInstallService(ProgramCatalog.CreateDefault(), runner, new TestProgramInstallExecutionPolicy(true));
    var result = await service.ExecuteAsync([new ProgramInstallRequest("7zip.7zip", MakeProgramConsent("7zip.7zip"))]);
    Assert(result.Single().Outcome == ProgramInstallOutcome.PackageNotFound,
        "documented WinGet no-packages-found HRESULT should not trigger a fallback by localized name");
}

static async Task ProgramInstallMapsNetworkUnavailable()
{
    var runner = new FakeProgramInstallRunner();
    runner.Results.Enqueue(new(ProgramRunnerOutcome.Completed, unchecked((int)0x8A150107)));
    var service = new ProgramInstallService(ProgramCatalog.CreateDefault(), runner, new TestProgramInstallExecutionPolicy(true));
    var result = await service.ExecuteAsync([new ProgramInstallRequest("7zip.7zip", MakeProgramConsent("7zip.7zip"))]);
    Assert(result.Single().Outcome == ProgramInstallOutcome.NetworkUnavailable
        && result.Single().Summary.Contains("não foi inferido", StringComparison.OrdinalIgnoreCase),
        "documented WinGet no-network result should not be generalized to total internet failure");
}

static async Task ProgramInstallZeroExitCodeRemainsUnverified()
{
    var runner = new FakeProgramInstallRunner();
    runner.Results.Enqueue(new(ProgramRunnerOutcome.Completed, 0));
    var service = new ProgramInstallService(ProgramCatalog.CreateDefault(), runner, new TestProgramInstallExecutionPolicy(true));
    var result = await service.ExecuteAsync([
        new ProgramInstallRequest("7zip.7zip", MakeProgramConsent("7zip.7zip"))]);
    var item = result.Single();

    Assert(item.ExitCode == 0, "the zero exit code should remain available for review");
    Assert(item.Outcome == ProgramInstallOutcome.ReportedSuccessUnverified,
        "a process exit code of zero must not become verified installation success");
    Assert(item.Summary.Contains("não foram verificados", StringComparison.OrdinalIgnoreCase),
        "the result must state that installed presence and function were not verified");
}

static async Task ProgramInstallContinuesAfterPackageFailure()
{
    var runner = new FakeProgramInstallRunner();
    runner.Results.Enqueue(new(ProgramRunnerOutcome.Completed, unchecked((int)0x8A150108)));
    runner.Results.Enqueue(new(ProgramRunnerOutcome.Completed, 0));
    var service = new ProgramInstallService(ProgramCatalog.CreateDefault(), runner, new TestProgramInstallExecutionPolicy(true));
    var results = await service.ExecuteAsync([
        new ProgramInstallRequest("7zip.7zip", MakeProgramConsent("7zip.7zip")),
        new ProgramInstallRequest("VideoLAN.VLC", MakeProgramConsent("VideoLAN.VLC"))]);
    Assert(results.Select(result => result.Outcome).SequenceEqual([
        ProgramInstallOutcome.Failed, ProgramInstallOutcome.ReportedSuccessUnverified]),
        "partial success/failure must remain attached to the package that returned it");
    Assert(runner.Calls.SequenceEqual(["7zip.7zip", "VideoLAN.VLC"]),
        "each selected package must be invoked independently in order");
    Assert(results[1].Summary.Contains("não foram verificados", StringComparison.Ordinal),
        "WinGet success must not be described as post-install verification");
}

static async Task ProgramInstallCancellationLeavesUnknownPartialState()
{
    using var cancellation = new CancellationTokenSource();
    var runner = new FakeProgramInstallRunner
    {
        BeforeNextResult = () => cancellation.Cancel()
    };
    runner.Results.Enqueue(new(ProgramRunnerOutcome.Cancelled, unchecked((int)0x8A150005)));
    var service = new ProgramInstallService(ProgramCatalog.CreateDefault(), runner, new TestProgramInstallExecutionPolicy(true));
    var results = await service.ExecuteAsync([
        new ProgramInstallRequest("7zip.7zip", MakeProgramConsent("7zip.7zip")),
        new ProgramInstallRequest("VideoLAN.VLC", MakeProgramConsent("VideoLAN.VLC"))], cancellation.Token);
    Assert(results.Select(result => result.Outcome).SequenceEqual([
        ProgramInstallOutcome.CancelledOutcomeUnknown, ProgramInstallOutcome.NotRun]),
        "cancellation after the process starts must record an uncertain partial state and not start later packages");
    Assert(results[0].Summary.Contains("parcial", StringComparison.OrdinalIgnoreCase),
        "cancelled install must explain that partial side effects are possible");
    Assert(runner.Calls.Count == 1, "cancellation must stop sequential package execution");
}

static async Task ProgramInstallRestartRequiredPausesRemainder()
{
    var runner = new FakeProgramInstallRunner();
    runner.Results.Enqueue(new(ProgramRunnerOutcome.Completed, unchecked((int)0x8A150109)));
    var service = new ProgramInstallService(ProgramCatalog.CreateDefault(), runner, new TestProgramInstallExecutionPolicy(true));
    var results = await service.ExecuteAsync([
        new ProgramInstallRequest("7zip.7zip", MakeProgramConsent("7zip.7zip")),
        new ProgramInstallRequest("VideoLAN.VLC", MakeProgramConsent("VideoLAN.VLC"))]);
    Assert(results.Select(result => result.Outcome).SequenceEqual([
        ProgramInstallOutcome.RebootRequired, ProgramInstallOutcome.NotRunRestartPending]),
        "reboot-required status must pause later package installs without restarting Windows");
    Assert(runner.Calls.Count == 1, "packages after a pending restart must not start");
}

static async Task ProgramInstallRejectsUnknownId()
{
    var runner = new FakeProgramInstallRunner();
    var service = new ProgramInstallService(ProgramCatalog.CreateDefault(), runner, new TestProgramInstallExecutionPolicy(true));
    var result = await service.ExecuteAsync([new ProgramInstallRequest("arbitrary.command", null)]);
    Assert(result.Single().Outcome == ProgramInstallOutcome.UnknownPackageBlocked && runner.Calls.Count == 0,
        "request input cannot become an arbitrary WinGet package or command");
}

static ProgramInstallConsent MakeProgramConsent(string packageId) => new(
    packageId, "winget", ConsequencesReviewed: true, SourceAgreementAccepted: true, PackageAgreementAccepted: true);

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
    public static readonly Guid DefaultActivePlanId = Guid.Parse("22222222-3333-4444-5555-666666666666");
    public UserConfiguredPowerMode AcMode { get; set; } = acMode;
    public UserConfiguredPowerMode DcMode { get; set; } = dcMode;
    public Guid? ActivePlanId { get; set; } = DefaultActivePlanId;
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
        ApiAvailable ? "Simulated Windows 11 API" : "Simulated unsupported API",
        ActivePlanId);

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

sealed class FakeNetworkDiagnosticsAdapter(NetworkInventoryResult inventory) : INetworkDiagnosticsAdapter
{
    public Queue<NetworkProbeReply> Probes { get; } = new();
    public Exception? ResolutionError { get; set; }
    public Action<int>? BeforeProbe { get; set; }
    public int ResolveCount { get; private set; }
    public int ProbeCount { get; private set; }

    public static FakeNetworkDiagnosticsAdapter Connected(bool isOperational = true) => new(
        new NetworkInventoryResult(DateTimeOffset.UtcNow,
            [new NetworkAdapterInfo("simulated", "Synthetic adapter", "Ethernet", isOperational ? "Up" : "Down",
                isOperational, 1_000_000_000, ["192.0.2.10"], ["192.0.2.1"], ["192.0.2.53"])],
            []));

    public NetworkInventoryResult ReadAdapters() => inventory;

    public Task<IReadOnlyList<IPAddress>> ResolveHostAsync(string hostName, CancellationToken cancellationToken)
    {
        ResolveCount++;
        cancellationToken.ThrowIfCancellationRequested();
        if (ResolutionError is not null) throw ResolutionError;
        IReadOnlyList<IPAddress> addresses = [IPAddress.Parse("198.51.100.20")];
        return Task.FromResult(addresses);
    }

    public Task<NetworkProbeReply> ProbeAsync(IPAddress destination, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ProbeCount++;
        BeforeProbe?.Invoke(ProbeCount);
        cancellationToken.ThrowIfCancellationRequested();
        var reply = Probes.Count > 0 ? Probes.Dequeue() : new NetworkProbeReply(NetworkProbeStatus.TimedOut);
        return Task.FromResult(reply);
    }
}

sealed class FakeProcessPriorityAdapter : IProcessPriorityAdapter
{
    private readonly Dictionary<int, ProcessObservation> _observations = [];
    public int WriteCalls { get; private set; }
    public List<ProcessPriorityLevel> Writes { get; } = [];
    public Action<FakeProcessPriorityAdapter>? BeforeSet { get; set; }
    public Action? AfterWrite { get; set; }

    public ProcessInventoryResult ListProcesses() => new(DateTimeOffset.UtcNow,
        Array.AsReadOnly(_observations.Values.OrderBy(item => item.ProcessId).ToArray()), []);

    public ProcessObservation Inspect(int processId) => _observations.TryGetValue(processId, out var observation)
        ? observation
        : new(processId, null, null, null, ProcessInspectionStatus.NotFound, "synthetic PID missing");

    public ProcessPriorityWriteResult SetPriority(ProcessIdentity expectedIdentity,
        ProcessPriorityLevel expectedCurrentPriority, ProcessPriorityLevel targetPriority)
    {
        BeforeSet?.Invoke(this);
        BeforeSet = null;
        var current = Inspect(expectedIdentity.ProcessId);
        var identity = expectedIdentity.CompareTo(current.Identity);
        if (current.Status is ProcessInspectionStatus.Exited or ProcessInspectionStatus.NotFound)
            return new(ProcessPriorityWriteStatus.ProcessExited, false, "synthetic process exited before setter");
        if (identity == ProcessIdentityComparison.DifferentProcess)
            return new(ProcessPriorityWriteStatus.IdentityMismatch, false, "synthetic PID was reused");
        if (identity == ProcessIdentityComparison.Unverifiable || current.Identity is null)
            return new(ProcessPriorityWriteStatus.IdentityUnverifiable, false, "synthetic identity unavailable");
        if (!current.CanReviewPriority)
            return new(ProcessPriorityWriteStatus.AccessDenied, false, "synthetic access denied");
        if (current.CurrentPriority != expectedCurrentPriority)
            return new(ProcessPriorityWriteStatus.PriorityConflict, false, "synthetic priority conflict");

        WriteCalls++;
        Writes.Add(targetPriority);
        _observations[current.ProcessId] = current with { CurrentPriority = targetPriority };
        AfterWrite?.Invoke();
        AfterWrite = null;
        return new(ProcessPriorityWriteStatus.Applied, true, "synthetic setter completed");
    }

    public void SetObservation(ProcessObservation observation) => _observations[observation.ProcessId] = observation;
    public ProcessObservation? Current(int processId) => _observations.TryGetValue(processId, out var observation) ? observation : null;
}

sealed class FakeRegistryPreferenceAdapter(RegistryValueState initialState) : IRegistryPreferenceAdapter
{
    public RegistryValueState State { get; set; } = initialState;
    public int WriteCalls { get; private set; }
    public int DeleteCalls { get; private set; }
    public int OtherValueCount { get; set; }
    public Exception? NextReadFailure { get; set; }
    public Exception? NextWriteFailure { get; set; }
    public Exception? NextDeleteFailure { get; set; }
    public Action? AfterWrite { get; set; }
    public Action? AfterRead { get; set; }

    public RegistryValueState Read(RegistryPreferenceAddress address)
    {
        ValidateAddress(address);
        if (NextReadFailure is not null)
        {
            var failure = NextReadFailure;
            NextReadFailure = null;
            throw failure;
        }
        var read = State;
        var callback = AfterRead;
        AfterRead = null;
        callback?.Invoke();
        return read;
    }

    public void WriteValue(RegistryPreferenceAddress address, RegistryTypedValue value)
    {
        ValidateAddress(address);
        if (NextWriteFailure is not null)
        {
            var failure = NextWriteFailure;
            NextWriteFailure = null;
            throw failure;
        }
        if (!State.KeyExists)
            throw new IOException("Fake adapter refuses to create the parent key.");
        WriteCalls++;
        State = new RegistryValueState(true, true, value);
        var callback = AfterWrite;
        AfterWrite = null;
        callback?.Invoke();
    }

    public void DeleteValue(RegistryPreferenceAddress address)
    {
        ValidateAddress(address);
        if (NextDeleteFailure is not null)
        {
            var failure = NextDeleteFailure;
            NextDeleteFailure = null;
            throw failure;
        }
        if (!State.KeyExists || !State.ValueExists)
            throw new IOException("Fake adapter has no value to delete.");
        DeleteCalls++;
        State = RegistryValueState.MissingValue;
    }

    private static void ValidateAddress(RegistryPreferenceAddress address)
    {
        if (address != RegistryPreferenceCatalog.FileExtensionsAddress)
            throw new NotSupportedException("Fake adapter only supports the catalogued preference.");
    }
}

sealed class FakeRegistryManagementInspector(RegistryManagementStatus status) : IRegistryManagementInspector
{
    public RegistryManagementStatus Status { get; set; } = status;
    public RegistryManagementAssessment Assess(RegistryPreferenceAddress address)
    {
        if (address != RegistryPreferenceCatalog.FileExtensionsAddress)
            throw new NotSupportedException("Fake inspector only supports the catalogued preference.");
        return new(Status, "synthetic organization-management assessment");
    }
}

sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
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

sealed class FakeDriverPackageSignatureVerifier(DriverPackageVerificationStatus status = DriverPackageVerificationStatus.Valid)
    : IDriverPackageSignatureVerifier
{
    public DriverPackageSignatureResult VerifyInf(string infPath, string catalogPath) =>
        new(status, status == DriverPackageVerificationStatus.Valid ? "Synthetic test signer" : null,
            status == DriverPackageVerificationStatus.Valid ? "simulated INF membership" : "simulated signature failure");

    public DriverPackageSignatureResult VerifyCatalogMember(string filePath, string catalogPath) =>
        new(status, null, status == DriverPackageVerificationStatus.Valid ? "simulated catalog member" : "simulated member failure");
}

sealed class FakeSystemInfDependencyResolver(DriverPackageSystemDependencyResult result)
    : IDriverPackageSystemInfDependencyResolver
{
    public IReadOnlyList<DriverPackageInfDependencyRequirement> LastRequirements { get; private set; } = Array.Empty<DriverPackageInfDependencyRequirement>();

    public DriverPackageSystemDependencyResult Verify(
        IReadOnlyList<DriverPackageInfDependencyRequirement> requirements)
    {
        LastRequirements = requirements;
        return result;
    }
}

sealed class FakeDriverInventoryService(IReadOnlyList<DriverInfo> drivers) : IDriverInventoryService
{
    public IReadOnlyList<DriverInfo> Drivers { get; set; } = drivers;
    public InventoryAvailability Availability { get; set; } = InventoryAvailability.Available;

    public Task<DriverInventoryResult> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new DriverInventoryResult(DateTimeOffset.UtcNow, Availability, Drivers, Array.Empty<string>(), null));
    }
}

sealed class FakeDriverPackageInstaller : IDriverPackageInstaller
{
    public int StageCalls { get; private set; }
    public int AssociationCalls { get; private set; }
    public string StagedInfPath { get; set; } = Path.Combine(Path.GetTempPath(), "oem42.inf");
    public DriverPackageCommandResult? StageResult { get; set; }
    public DriverPackageCommandResult? AssociationResult { get; set; }
    public DriverPackageAssociationRequest? LastAssociationRequest { get; private set; }

    public Task<DriverPackageCommandResult> StageToDriverStoreAsync(string infPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StageCalls++;
        return Task.FromResult(StageResult ?? new DriverPackageCommandResult(true, 0, false,
            "simulated SetupCopyOEMInf staging success", StagedInfPath));
    }

    public Task<DriverPackageCommandResult> AssociateWithDeviceAsync(DriverPackageAssociationRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AssociationCalls++;
        LastAssociationRequest = request;
        return Task.FromResult(AssociationResult ?? new DriverPackageCommandResult(true, 0, false,
            "simulated DiInstallDevice success"));
    }
}

sealed class TestEnabledDriverMutationPolicy : IDriverPackageMutationPolicy
{
    public bool IsEnabled => true;
    public string BlockReason => string.Empty;
}

sealed class TestProgramInstallExecutionPolicy(bool isAllowed) : IProgramInstallExecutionPolicy
{
    public bool IsAllowed => isAllowed;
    public string BlockReason => isAllowed ? string.Empty : "simulated blocked policy";
}

sealed class FakeProgramInstallRunner : IProgramInstallRunner
{
    public Queue<ProgramRunnerResult> Results { get; } = new();
    public List<string> Calls { get; } = [];
    public Action? BeforeNextResult { get; set; }

    public Task<ProgramRunnerResult> InstallAsync(
        ProgramPackageDefinition package,
        ProgramInstallConsent consent,
        CancellationToken cancellationToken)
    {
        Calls.Add(package.PackageId);
        cancellationToken.ThrowIfCancellationRequested();
        BeforeNextResult?.Invoke();
        BeforeNextResult = null;
        return Task.FromResult(Results.Count > 0
            ? Results.Dequeue()
            : new ProgramRunnerResult(ProgramRunnerOutcome.Completed, 0));
    }
}

sealed class FakeDiagnosticReportFileOperations : IDiagnosticReportFileOperations
{
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

    public int OperationCount { get; private set; }
    public Exception? NextWriteFailure { get; set; }
    public bool CorruptNextRead { get; set; }
    public Action<string>? BeforeMoveNew { get; set; }

    public bool DirectoryExists(string path)
    {
        OperationCount++;
        return _directories.Contains(Path.GetFullPath(path));
    }

    public bool FileExists(string path)
    {
        OperationCount++;
        return _files.ContainsKey(Path.GetFullPath(path));
    }

    public void WriteNew(string path, ReadOnlyMemory<byte> contents)
    {
        OperationCount++;
        if (NextWriteFailure is not null)
        {
            var failure = NextWriteFailure;
            NextWriteFailure = null;
            throw failure;
        }
        var fullPath = Path.GetFullPath(path);
        if (_files.ContainsKey(fullPath)) throw new IOException("Fake destination already exists.");
        _files[fullPath] = contents.ToArray();
    }

    public byte[] ReadAllBytes(string path)
    {
        OperationCount++;
        var bytes = _files[Path.GetFullPath(path)].ToArray();
        if (!CorruptNextRead) return bytes;
        CorruptNextRead = false;
        return bytes.Append((byte)0x20).ToArray();
    }

    public void MoveNew(string sourcePath, string destinationPath)
    {
        OperationCount++;
        var source = Path.GetFullPath(sourcePath);
        var destination = Path.GetFullPath(destinationPath);
        BeforeMoveNew?.Invoke(destination);
        BeforeMoveNew = null;
        if (_files.ContainsKey(destination)) throw new IOException("Fake destination already exists.");
        _files.Add(destination, _files[source]);
        _files.Remove(source);
    }

    public void ReplaceExisting(string sourcePath, string destinationPath)
    {
        OperationCount++;
        var source = Path.GetFullPath(sourcePath);
        var destination = Path.GetFullPath(destinationPath);
        if (!_files.ContainsKey(destination)) throw new FileNotFoundException();
        _files[destination] = _files[source];
        _files.Remove(source);
    }

    public void Delete(string path)
    {
        OperationCount++;
        _files.Remove(Path.GetFullPath(path));
    }

    public void AddDirectory(string path) => _directories.Add(Path.GetFullPath(path));
    public void AddExistingFile(string path, string contents) => _files[Path.GetFullPath(path)] = System.Text.Encoding.UTF8.GetBytes(contents);
    public string ReadText(string path) => System.Text.Encoding.UTF8.GetString(_files[Path.GetFullPath(path)]);
}

static class DiagnosticExportReadOnlyProbe
{
    public static async Task<int> RunAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("SKIP: o relatório real exige as APIs de inventário do Windows.");
            return 2;
        }

        var temporaryDirectory = Path.Combine(Path.GetTempPath(), "CalixtoOptimizer-DiagnosticExport-Test", Guid.NewGuid().ToString("N"));
        var destination = Path.Combine(temporaryDirectory, "diagnostic-report.json");
        try
        {
            Directory.CreateDirectory(temporaryDirectory);
            var computerResult = await new WmiComputerInventoryService().CollectAsync();
            var drivers = await new WmiDriverInventoryService().CollectAsync();
            var power = new WindowsPowerPlanInventoryService().Collect();
            var privacy = new DiagnosticReportPrivacyContext(Environment.MachineName, Environment.UserName, Environment.UserDomainName);
            var json = SanitizedDiagnosticReportBuilder.BuildJson(
                computerResult.Inventory,
                drivers,
                power,
                privacyContext: privacy,
                computerErrorMessage: computerResult.ErrorMessage);

            var deviceIdentifiers = drivers.Drivers
                .SelectMany(driver => new[] { driver.DeviceId }.Concat(driver.HardwareIds ?? Array.Empty<string>()).Concat(driver.CompatibleIds ?? Array.Empty<string>()))
                .Where(identifier => !string.IsNullOrWhiteSpace(identifier));
            var leakedDeviceIdentifier = deviceIdentifiers.Any(identifier => json.Contains(identifier!, StringComparison.OrdinalIgnoreCase));
            var leakedSchemeGuid = power.Plans.Any(plan => json.Contains(plan.SchemeId.ToString(), StringComparison.OrdinalIgnoreCase));
            var leakedAddress = System.Text.RegularExpressions.Regex.IsMatch(
                json,
                @"(?<![\d.])(?:\d{1,3}\.){3}\d{1,3}(?![\d.])",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100));
            var leakedPath = System.Text.RegularExpressions.Regex.IsMatch(
                json,
                @"(?i)[A-Z]:\\[^\r\n\t\""<>|]*",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100));
            if (leakedDeviceIdentifier || leakedSchemeGuid || leakedAddress || leakedPath)
            {
                Console.Error.WriteLine("FAIL: uma classe de identificador proibida apareceu no relatório sanitizado; os valores não serão impressos.");
                return 1;
            }

            var result = new DiagnosticReportFileSaver().Save(destination, json, overwriteConfirmed: false);
            if (result.State != DiagnosticReportSaveState.SavedAndVerified)
            {
                Console.Error.WriteLine($"FAIL: salvamento real não foi confirmado ({result.FailureType ?? result.State.ToString()}).");
                return 1;
            }

            var savedBytes = await File.ReadAllBytesAsync(destination);
            var expectedBytes = new System.Text.UTF8Encoding(false).GetBytes(json);
            var exactMatch = savedBytes.AsSpan().SequenceEqual(expectedBytes);
            System.Text.Json.JsonDocument.Parse(savedBytes).Dispose();

            Console.WriteLine("Consultas locais somente leitura: nenhuma rede foi acessada e nenhum setter foi chamado.");
            Console.WriteLine($"Sistema: {(computerResult.Inventory is null ? (computerResult.ErrorMessage is null ? "NotCollected" : "Failed") : computerResult.Inventory.Warnings.Count == 0 ? "Available" : "Partial")}; avisos={computerResult.Inventory?.Warnings.Count ?? 0}; CPU={(!string.IsNullOrWhiteSpace(computerResult.Inventory?.Processor) ? 1 : 0)}; GPUs={computerResult.Inventory?.GraphicsAdapters.Count ?? 0}; armazenamento={computerResult.Inventory?.StorageDevices.Count ?? 0}; adaptadores físicos={computerResult.Inventory?.NetworkAdapters.Count ?? 0}.");
            Console.WriteLine($"Drivers/dispositivos WMI: {drivers.Availability}; registros={drivers.Drivers.Count}; avisos={drivers.Warnings.Count}.");
            Console.WriteLine($"Planos PowrProf: {power.Availability}; registros={power.Plans.Count}; avisos={power.Warnings.Count}.");
            Console.WriteLine($"Arquivo temporário salvo e relido; JSON válido; conteúdo exato={exactMatch}; bytes={savedBytes.Length}; SHA-256={Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(savedBytes))}.");
            Console.WriteLine("Campos sensíveis não foram impressos no console. O arquivo temporário será removido ao final.");
            return exactMatch ? 0 : 1;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException or System.Text.Json.JsonException)
        {
            Console.Error.WriteLine($"FAIL: consulta ou validação do salvamento real falhou ({exception.GetType().Name}).");
            return 1;
        }
        finally
        {
            try
            {
                if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, recursive: true);
                var parent = Path.GetDirectoryName(temporaryDirectory);
                if (parent is not null && Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any()) Directory.Delete(parent);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                Console.Error.WriteLine($"WARN: limpeza da pasta de teste incompleta ({exception.GetType().Name}); local: pasta temporária CalixtoOptimizer-DiagnosticExport-Test.");
            }
        }
    }
}
