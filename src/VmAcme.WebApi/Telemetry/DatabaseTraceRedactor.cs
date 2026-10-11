using System.Diagnostics;
using OpenTelemetry;

namespace VmAcme.Telemetry;

public sealed class DatabaseTraceRedactor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity activity)
    {
        if (activity.GetTagItem("db.system") is null && activity.GetTagItem("db.system.name") is null) return;
        activity.SetTag("db.query.text", null);
        activity.SetTag("db.statement", null);
        activity.SetTag("db.connection_string", null);
        activity.SetTag("db.user", null);
        activity.DisplayName = "database operation";
    }
}