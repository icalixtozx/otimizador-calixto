using Microsoft.Win32;
using Optimizer.Core.Programs;

namespace Optimizer.Windows;

public sealed class WindowsInstalledProgramInventoryProvider : IInstalledProgramInventoryProvider
{
    private const string UninstallSubKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    public InstalledProgramInventory ReadInstalledPrograms()
    {
        var entries = new List<InstalledProgramEntry>();
        var warnings = new List<string>();

        ReadHive(RegistryHive.LocalMachine, RegistryView.Registry64, "computador", entries, warnings);
        ReadHive(RegistryHive.LocalMachine, RegistryView.Registry32, "computador", entries, warnings);
        ReadHive(RegistryHive.CurrentUser, RegistryView.Registry64, "usuário", entries, warnings);
        ReadHive(RegistryHive.CurrentUser, RegistryView.Registry32, "usuário", entries, warnings);

        var unique = entries
            .DistinctBy(entry => string.Join('\0', entry.DisplayName, entry.Publisher, entry.Version, entry.Scope), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new(Array.AsReadOnly(unique), Array.AsReadOnly(warnings.Distinct(StringComparer.Ordinal).ToArray()));
    }

    private static void ReadHive(
        RegistryHive hive,
        RegistryView view,
        string scope,
        ICollection<InstalledProgramEntry> entries,
        ICollection<string> warnings)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var uninstallKey = baseKey.OpenSubKey(UninstallSubKey, writable: false);
            if (uninstallKey is null) return;

            foreach (var childName in uninstallKey.GetSubKeyNames())
            {
                try
                {
                    using var entryKey = uninstallKey.OpenSubKey(childName, writable: false);
                    if (entryKey is null) continue;
                    var displayName = entryKey.GetValue("DisplayName") as string;
                    var publisher = entryKey.GetValue("Publisher") as string;
                    if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(publisher)) continue;

                    entries.Add(new(
                        displayName.Trim(),
                        publisher.Trim(),
                        (entryKey.GetValue("DisplayVersion") as string)?.Trim(),
                        scope));
                }
                catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException)
                {
                    warnings.Add($"Uma entrada da lista local de programas não pôde ser lida ({exception.GetType().Name}).");
                }
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException or ArgumentException)
        {
            warnings.Add($"Uma visão da lista local de programas não pôde ser lida ({exception.GetType().Name}).");
        }
    }
}
