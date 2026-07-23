using Microsoft.Win32;
using WinOptimizer.Core;

namespace WinOptimizer.Modules.SystemTweaker;

/// <summary>
/// Registry tweak tanımı. Her tweak: hive + yol + değer adı + değer.
/// Uygula → önceki değeri yedekler (change journal için geri alma).
/// (Master plan Bölüm 11.4 & 3.9.)
/// </summary>
public sealed record RegistryTweak(
    string Id,
    string DisplayName,
    RegistryHive Hive,
    string Path,
    string ValueName,
    object EnabledValue,
    object? DisabledValue,
    RegistryValueKind Kind,
    RiskLevel Risk,
    string Description)
{
    /// <summary>Hive'u Registry sınıfına dönüştürür (HKLM/HKCU).</summary>
    public RegistryKey GetRootKey(bool writable) => Hive switch
    {
        RegistryHive.LocalMachine => Registry.LocalMachine,
        RegistryHive.CurrentUser => Registry.CurrentUser,
        _ => Registry.LocalMachine
    };
}

/// <summary>
/// Registry değerini okur/yazar; önceki değeri döndürür (geri alma için).
/// </summary>
public sealed class RegistryTweakApplier
{
    /// <summary>Değeri yazar, önceki değeri döndürür (null ise yoktu).</summary>
    public (bool Ok, object? Previous) SetValue(RegistryTweak tweak)
    {
        using var root = tweak.GetRootKey(writable: true);
        using var key = root.OpenSubKey(tweak.Path, writable: true)
                        ?? root.CreateSubKey(tweak.Path);
        object? previous = key.GetValue(tweak.ValueName);
        key.SetValue(tweak.ValueName, tweak.EnabledValue, tweak.Kind);
        return (true, previous);
    }

    /// <summary>Değeri önceki haline (veya DisabledValue) döndürür.</summary>
    public bool RevertValue(RegistryTweak tweak, object? previousValue)
    {
        using var root = tweak.GetRootKey(writable: true);
        using var key = root.OpenSubKey(tweak.Path, writable: true);
        if (key is null) return false;
        key.SetValue(tweak.ValueName, previousValue ?? tweak.DisabledValue ?? 0, tweak.Kind);
        return true;
    }

    /// <summary>Değerin tweak'ın EnabledValue'suna eşit olup olmadığını kontrol eder.</summary>
    public bool IsEnabled(RegistryTweak tweak)
    {
        using var root = tweak.GetRootKey(writable: false);
        using var key = root.OpenSubKey(tweak.Path, writable: false);
        if (key is null) return false;
        var current = key.GetValue(tweak.ValueName);
        return current != null && current.Equals(tweak.EnabledValue);
    }
}
