namespace Palpitao.Api.Monitoring;

public class SentrySettings
{
    public string Dsn { get; set; } = string.Empty;
    public string Environment { get; set; } = "Development";
    public string Release { get; set; } = "palpitao-england-backend@1.0.0";
    public double TracesSampleRate { get; set; }
    public bool Debug { get; set; }
    public bool SendDefaultPii { get; set; }
    public string MinimumBreadcrumbLevel { get; set; } = "Information";
    public string MinimumEventLevel { get; set; } = "Error";
}
