using System.Diagnostics;
using System.Diagnostics.Metrics;
using Npgsql;
using VmAcme.Persistence;
using VmAcme.Telemetry;

namespace VmAcme;

public sealed class TelemetryTests
{
    [Fact]
    public void Database_trace_does_not_export_sql_or_connection_credentials()
    {
        using var activity = new Activity("SELECT secret FROM accounts").Start();
        activity.SetTag("db.system.name", "postgresql");
        activity.SetTag("db.query.text", "SELECT 'sensitive'");
        activity.SetTag("db.statement", "SELECT 'sensitive'");
        activity.SetTag("db.connection_string", "Password=sensitive");
        activity.SetTag("db.user", "sensitive");
        new DatabaseTraceRedactor().OnEnd(activity);
        Assert.Equal("database operation", activity.DisplayName);
        Assert.DoesNotContain(activity.TagObjects,
            x => x.Key is "db.query.text" or "db.statement" or "db.connection_string" or "db.user");
    }

    [Fact]
    public void Database_commands_emit_duration_without_query_tags()
    {
        var measurements = new List<KeyValuePair<string, object?>[]>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "VmAcme.Database") meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, duration, tags, _) =>
        {
            Assert.True(duration >= 0);
            lock (measurements) measurements.Add(tags.ToArray());
        });
        listener.Start();
        using var connection = new NpgsqlConnection("Host=localhost;Password=sensitive");
        using var command = new NpgsqlCommand("SELECT 'sensitive'", connection);
        var interceptor = new DatabaseCommandMetrics();
        interceptor.NonQueryExecuting(command, null!, default);
        interceptor.NonQueryExecuted(command, null!, 1);
        interceptor.NonQueryExecuted(command, null!, 1);
        Assert.Single(measurements);
        Assert.All(measurements.SelectMany(x => x), tag => Assert.Contains(tag.Key,
            new[] { "db.system.name", "db.operation.name", "error.type" }));
    }
}