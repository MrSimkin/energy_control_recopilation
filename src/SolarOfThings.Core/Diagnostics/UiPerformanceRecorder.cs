using System.Diagnostics;

namespace SolarOfThings.Core.Diagnostics;

/// <summary>
/// Bounded, in-memory operational timing observations. No SQL text, paths,
/// device identifiers, credentials or application data are recorded.
/// Neither SQLite nor the file system is touched.
/// </summary>
public sealed class UiPerformanceRecorder
{
    public const int MaximumSamples = 256;
    private readonly object _gate = new();
    private readonly Queue<UiPerformanceSample> _samples = new(MaximumSamples);

    public IDisposable Measure(string operation)
    {
        ValidateOperation(operation);
        return new TimedOperation(this, operation);
    }

    public void Record(string operation, TimeSpan elapsed)
    {
        ValidateOperation(operation);
        if (elapsed < TimeSpan.Zero || double.IsNaN(elapsed.TotalMilliseconds) ||
            double.IsInfinity(elapsed.TotalMilliseconds))
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        var sample = new UiPerformanceSample(
            DateTimeOffset.UtcNow, operation, elapsed.TotalMilliseconds);
        lock (_gate)
        {
            if (_samples.Count == MaximumSamples) _samples.Dequeue();
            _samples.Enqueue(sample);
        }
    }

    public IReadOnlyList<UiPerformanceSample> Snapshot()
    {
        lock (_gate) return _samples.ToArray();
    }

    public IReadOnlyList<UiPerformanceSummary> Summaries()
    {
        // Calculated from at most MaximumSamples points; no long-term profile
        // or implied statistical significance when counts are small.
        return Snapshot().GroupBy(x => x.Operation, StringComparer.Ordinal)
            .Select(group =>
            {
                var ordered = group.Select(x => x.ElapsedMilliseconds)
                    .OrderBy(x => x).ToArray();
                var p95 = ordered[(int)Math.Ceiling(0.95 * ordered.Length) - 1];
                return new UiPerformanceSummary(group.Key, ordered.Length,
                    ordered.Average(), p95, ordered[^1]);
            })
            .OrderByDescending(x => x.P95Milliseconds)
            .ThenBy(x => x.Operation, StringComparer.Ordinal)
            .ToArray();
    }

    private static void ValidateOperation(string operation)
    {
        if (string.IsNullOrEmpty(operation) || operation.Length > 64 ||
            operation.Any(c => !((c >= 'A' && c <= 'Z') ||
                (c >= 'a' && c <= 'z') ||
                (c >= '0' && c <= '9') || c is '.' or '_' or '-')))
            throw new ArgumentException("Use a short fixed, non-sensitive operation name.",
                nameof(operation));
    }

    private sealed class TimedOperation : IDisposable
    {
        private readonly UiPerformanceRecorder _owner;
        private readonly string _operation;
        private readonly long _started = Stopwatch.GetTimestamp();
        private int _finished;

        public TimedOperation(UiPerformanceRecorder owner, string operation)
        {
            _owner = owner;
            _operation = operation;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _finished, 1) != 0) return;
            _owner.Record(_operation, Stopwatch.GetElapsedTime(_started));
        }
    }
}

public sealed record UiPerformanceSample(
    DateTimeOffset ObservedUtc, string Operation, double ElapsedMilliseconds);
public sealed record UiPerformanceSummary(
    string Operation, int Samples, double MeanMilliseconds,
    double P95Milliseconds, double MaxMilliseconds);
