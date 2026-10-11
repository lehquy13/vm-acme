using System.Collections.Concurrent;
using System.Globalization;

namespace VmAcme.Behaviors;

// Placeholder in-process metrics adapter. Replace with the project's internal metrics packages.
public sealed class RequestMetrics
{
    private readonly ConcurrentDictionary<string, RequestMetricSnapshot> _requests = new(StringComparer.Ordinal);

    public void Record(string requestName, double elapsedMilliseconds, bool failed) =>
        _requests.AddOrUpdate(requestName,
            _ => new RequestMetricSnapshot(1, elapsedMilliseconds, failed ? 1 : 0),
            (_, current) => new RequestMetricSnapshot(
                current.Count + 1,
                current.TotalMilliseconds + elapsedMilliseconds,
                current.Failures + (failed ? 1 : 0)));

    public string ToPrometheusText()
    {
        var output = new StringWriter(CultureInfo.InvariantCulture);
        output.WriteLine("# HELP vm_acme_mediatr_requests_total MediatR requests handled.");
        output.WriteLine("# TYPE vm_acme_mediatr_requests_total counter");
        output.WriteLine("# HELP vm_acme_mediatr_request_duration_milliseconds_sum Total MediatR request duration.");
        output.WriteLine("# TYPE vm_acme_mediatr_request_duration_milliseconds_sum counter");
        output.WriteLine("# HELP vm_acme_mediatr_request_failures_total Failed MediatR requests.");
        output.WriteLine("# TYPE vm_acme_mediatr_request_failures_total counter");
        foreach (var (name, metric) in _requests.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var label = name.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
            output.WriteLine($"vm_acme_mediatr_requests_total{{request=\"{label}\"}} {metric.Count}");
            output.WriteLine(
                $"vm_acme_mediatr_request_duration_milliseconds_sum{{request=\"{label}\"}} {metric.TotalMilliseconds.ToString(CultureInfo.InvariantCulture)}");
            output.WriteLine($"vm_acme_mediatr_request_failures_total{{request=\"{label}\"}} {metric.Failures}");
        }

        return output.ToString();
    }
}

public readonly record struct RequestMetricSnapshot(long Count, double TotalMilliseconds, long Failures);