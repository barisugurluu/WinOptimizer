using Microsoft.Extensions.Logging;
using WinOptimizer.Core;
using WinOptimizer.Safety;

namespace WinOptimizer.Orchestration;

/// <summary>
/// Modül kayıt defteri — uygulama başlangıcında tüm modüller burada toplanır.
/// (Master plan Bölüm 2.1 — modülerlik; her modül bağımsız bir IOptimizationModule.)
/// </summary>
public sealed class ModuleRegistry
{
    private readonly List<IOptimizationModule> _modules = new();
    private readonly ILogger<ModuleRegistry> _logger;

    public ModuleRegistry(ILogger<ModuleRegistry> logger) => _logger = logger;

    public IReadOnlyList<IOptimizationModule> Modules => _modules;

    public ModuleRegistry Register(IOptimizationModule module)
    {
        _modules.Add(module);
        _logger.LogInformation("Modül kaydedildi: {Id} ({Name}, risk={Risk})",
            module.Id, module.DisplayName, module.Risk);
        return this;
    }

    public IOptimizationModule? Find(string id) =>
        _modules.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
}
