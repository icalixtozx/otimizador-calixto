using System.Globalization;
using System.Text.Json;
using Microsoft.Win32;
using Optimizer.Core.Registry;

namespace Optimizer.Windows;

/// <summary>
/// Narrow Windows adapter for the one reviewed File Explorer preference. It never creates the key,
/// never accesses a Policies path, and writes only to HKCU in the explicit 64-bit registry view.
/// The application does not call this adapter's write methods in this milestone.
/// </summary>
public sealed class WindowsRegistryPreferenceAdapter : IRegistryPreferenceAdapter
{
    public RegistryValueState Read(RegistryPreferenceAddress address)
    {
        ValidateAddress(address);
        using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        using var key = root.OpenSubKey(address.KeyPath, writable: false);
        if (key is null)
            return RegistryValueState.MissingKey;

        var valueExists = key.GetValueNames().Contains(address.ValueName, StringComparer.OrdinalIgnoreCase);
        if (!valueExists)
            return RegistryValueState.MissingValue;

        var kind = key.GetValueKind(address.ValueName);
        var raw = key.GetValue(address.ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return new RegistryValueState(true, true, FromNative(kind, raw));
    }

    public void WriteValue(RegistryPreferenceAddress address, RegistryTypedValue value)
    {
        ValidateAddress(address);
        ArgumentNullException.ThrowIfNull(value);
        using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        using var key = root.OpenSubKey(address.KeyPath, writable: true) ??
            throw new IOException("A chave pai não existe; o adaptador não cria chaves do Windows.");
        key.SetValue(address.ValueName, ToNative(value), ToNativeKind(value.Kind));
    }

    public void DeleteValue(RegistryPreferenceAddress address)
    {
        ValidateAddress(address);
        using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        using var key = root.OpenSubKey(address.KeyPath, writable: true) ??
            throw new IOException("A chave pai não existe; nenhum valor foi removido.");
        key.DeleteValue(address.ValueName, throwOnMissingValue: true);
    }

    private static void ValidateAddress(RegistryPreferenceAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address != RegistryPreferenceCatalog.FileExtensionsAddress)
            throw new NotSupportedException("Este adaptador só atende a preferência de extensões do Explorador descrita no catálogo.");
    }

    private static RegistryTypedValue FromNative(RegistryValueKind kind, object? value) => kind switch
    {
        RegistryValueKind.String when value is string text => new(RegistryStoredValueKind.String, text),
        RegistryValueKind.ExpandString when value is string text => new(RegistryStoredValueKind.ExpandString, text),
        RegistryValueKind.DWord when value is int number =>
            new(RegistryStoredValueKind.DWord, unchecked((uint)number).ToString("X8", CultureInfo.InvariantCulture)),
        RegistryValueKind.QWord when value is long number =>
            new(RegistryStoredValueKind.QWord, unchecked((ulong)number).ToString("X16", CultureInfo.InvariantCulture)),
        RegistryValueKind.MultiString when value is string[] items =>
            new(RegistryStoredValueKind.MultiString, JsonSerializer.Serialize(items)),
        RegistryValueKind.Binary when value is byte[] bytes =>
            new(RegistryStoredValueKind.Binary, Convert.ToBase64String(bytes)),
        RegistryValueKind.None when value is byte[] bytes =>
            new(RegistryStoredValueKind.None, Convert.ToBase64String(bytes)),
        _ => throw new NotSupportedException($"O tipo de Registro {kind} não pode ser capturado com preservação exata.")
    };

    private static RegistryValueKind ToNativeKind(RegistryStoredValueKind kind) => kind switch
    {
        RegistryStoredValueKind.String => RegistryValueKind.String,
        RegistryStoredValueKind.ExpandString => RegistryValueKind.ExpandString,
        RegistryStoredValueKind.DWord => RegistryValueKind.DWord,
        RegistryStoredValueKind.QWord => RegistryValueKind.QWord,
        RegistryStoredValueKind.MultiString => RegistryValueKind.MultiString,
        RegistryStoredValueKind.Binary => RegistryValueKind.Binary,
        RegistryStoredValueKind.None => RegistryValueKind.None,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static object ToNative(RegistryTypedValue value) => value.Kind switch
    {
        RegistryStoredValueKind.String or RegistryStoredValueKind.ExpandString => value.Data,
        RegistryStoredValueKind.DWord => unchecked((int)uint.Parse(value.Data, NumberStyles.HexNumber, CultureInfo.InvariantCulture)),
        RegistryStoredValueKind.QWord => unchecked((long)ulong.Parse(value.Data, NumberStyles.HexNumber, CultureInfo.InvariantCulture)),
        RegistryStoredValueKind.MultiString => JsonSerializer.Deserialize<string[]>(value.Data) ?? [],
        RegistryStoredValueKind.Binary or RegistryStoredValueKind.None => Convert.FromBase64String(value.Data),
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
