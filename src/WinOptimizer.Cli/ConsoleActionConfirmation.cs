using System.Globalization;
using WinOptimizer.Core;
using WinOptimizer.Orchestration.Confirmation;

namespace WinOptimizer.Cli;

/// <summary>
/// CLI onay uygulaması — bayrak tabanlı, etkileşimsiz.
/// </summary>
/// <remarks>
/// <para><b>Neden iki ayrı bayrak?</b> <c>--yes</c> "sorma, uygula" demektir ama
/// <c>SchedulerService</c> haftalık görevi <c>optimize --yes</c> olarak 03:00'te
/// <b>gözetimsiz</b> çalıştırır. Tek bayrak olsaydı geri dönüşüm kutusunu boşaltmak veya
/// HAGS çevirmek gibi geri alınamaz/etkili işlemler gecenin bir yarısı kimseye sorulmadan
/// yapılırdı. Bu yüzden ek onay isteyen eylemler ayrıca <c>--allow-risky</c> ister.</para>
/// <para>Reddedilen eylemler <b>yazdırılır</b>: kullanıcı neyin atlandığını görmeden
/// "çalıştı" sanmamalı.</para>
/// </remarks>
internal sealed class ConsoleActionConfirmation : IActionConfirmation
{
    private readonly bool _yes;
    private readonly bool _allowRisky;

    public ConsoleActionConfirmation(bool yes, bool allowRisky)
    {
        _yes = yes;
        _allowRisky = allowRisky;
    }

    public Task<IReadOnlyList<PreviewAction>> ConfirmAsync(
        ConfirmationRequest request, CancellationToken ct = default)
    {
        if (!_yes)
        {
            Console.Error.WriteLine(
                $"'{request.ModuleDisplayName}' onay gerektiriyor; uygulanmadı. " +
                "Uygulamak için --yes ekleyin.");
            return Task.FromResult<IReadOnlyList<PreviewAction>>([]);
        }

        var approved = new List<PreviewAction>();
        var refused = new List<PreviewAction>();

        foreach (var action in request.Actions)
        {
            if (ConfirmationGate.NeedsExplicitOptIn(action) && !_allowRisky)
            {
                refused.Add(action);
            }
            else
            {
                approved.Add(action);
            }
        }

        foreach (var action in refused)
        {
            Console.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "  ATLANDI [{0}] {1}: {2}  (uygulamak için --allow-risky)",
                action.Risk, request.ModuleId, action.Description));
        }

        return Task.FromResult<IReadOnlyList<PreviewAction>>(approved);
    }
}
