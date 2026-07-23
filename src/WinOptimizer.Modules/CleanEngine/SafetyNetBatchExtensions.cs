using WinOptimizer.Core;
using WinOptimizer.Safety;

namespace WinOptimizer.Modules.CleanEngine;

/// <summary>SafetyNet'e eklenen yardımcı genişletme (birden çok change journal kaydını tek seferde yazar).</summary>
internal static class SafetyNetBatchExtensions
{
    public static Task RecordRangeAsync(this SafetyNet safety, IEnumerable<ChangeRecord> records, CancellationToken ct)
        => safety.Journal.WriteRangeAsync(records, ct);
}
