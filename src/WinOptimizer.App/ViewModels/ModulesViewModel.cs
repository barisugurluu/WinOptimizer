using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinOptimizer.App.Resources;
using WinOptimizer.Core;
using WinOptimizer.Orchestration;

namespace WinOptimizer.App.ViewModels;

/// <summary>Modüller listesindeki tek satır.</summary>
public partial class ModuleToggle : ObservableObject
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string RiskBadge { get; init; }

    [ObservableProperty] private bool _isEnabled;
}

/// <summary>
/// Modüller sekmesi — tek tıkla optimizasyonun <b>kapsamını</b> kullanıcı belirler.
/// </summary>
/// <remarks>
/// Eskiden bu sekme "yakında" yer tutucusuydu ve <c>EnabledModules</c> ayarı hiçbir yerde
/// okunmuyordu; tek tıkla kayıtlı 16 modülün tamamını çalıştırıyordu. Artık liste hem
/// görünür hem düzenlenebilir.
/// </remarks>
public partial class ModulesViewModel : ObservableObject
{
    private readonly ModuleRegistry _registry;
    private readonly SettingsService _settings;

    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _hasStatus;

    public ObservableCollection<ModuleToggle> Modules { get; } = [];

    public ModulesViewModel(ModuleRegistry registry, SettingsService settings)
    {
        _registry = registry;
        _settings = settings;
        Load();
    }

    private void Load()
    {
        Modules.Clear();
        var enabled = _settings.Current.EnabledModules;
        foreach (var module in _registry.Modules)
        {
            Modules.Add(new ModuleToggle
            {
                Id = module.Id,
                DisplayName = module.DisplayName,
                RiskBadge = module.Risk.ToString(),
                IsEnabled = enabled.Contains(module.Id, StringComparer.OrdinalIgnoreCase),
            });
        }
    }

    [RelayCommand]
    private void Save()
    {
        _settings.Current.EnabledModules =
            Modules.Where(m => m.IsEnabled).Select(m => m.Id).ToList();

        StatusMessage = _settings.Save()
            ? string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                "{0} ({1})",
                Strings.SettingsSaved,
                _settings.Current.EnabledModules.Count)
            : Strings.SettingsSaveFailed;
        HasStatus = true;
    }

    /// <summary>Küratörlü güvenli varsayılana döner.</summary>
    [RelayCommand]
    private void ResetToSafeDefault()
    {
        foreach (var module in Modules)
        {
            module.IsEnabled = AppSettings.DefaultOneClickModules
                .Contains(module.Id, StringComparer.OrdinalIgnoreCase);
        }

        StatusMessage = Strings.ModulesResetHint;
        HasStatus = true;
    }

    /// <summary>Tüm modülleri seçer (ileri kullanıcı — sonuçları kullanıcının sorumluluğunda).</summary>
    [RelayCommand]
    private void SelectAll()
    {
        foreach (var module in Modules)
        {
            module.IsEnabled = true;
        }

        StatusMessage = Strings.ModulesSelectAllHint;
        HasStatus = true;
    }
}
