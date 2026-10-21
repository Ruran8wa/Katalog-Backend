using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Katalog_Backend.Observability;

public static class Telemetry
{
    public const string ActivitySourceName = "Katalog-Backend";
    public const string MeterName = "Katalog-Backend";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);

    public static IDisposable BeginTraceScope(ILogger logger)
    {
        var activity = Activity.Current;
        return logger.BeginScope(new Dictionary<string, object?>
        {
            ["trace_id"] = activity?.TraceId.ToHexString() ?? "unknown",
            ["span_id"] = activity?.SpanId.ToHexString() ?? "unknown"
        })!;
    }

    public static readonly Counter<long> OrdersPlaced =
        Meter.CreateCounter<long>("katalog.orders.placed", unit: "{order}", description: "Orders placed through the API.");

    public static readonly Counter<long> OrdersProcessed =
        Meter.CreateCounter<long>("katalog.orders.processed", unit: "{order}", description: "Orders processed to a final status.");

    public static readonly Histogram<double> OrderProcessingDuration =
        Meter.CreateHistogram<double>(
            "katalog.orders.processing.duration",
            unit: "ms",
            description: "Time from Pending to a final order status.");

    public static readonly ObservableGauge<long> PendingOrders =
        Meter.CreateObservableGauge(
            "katalog.orders.pending",
            ObservePendingOrders,
            unit: "{order}",
            description: "Orders currently in Pending status.");

    private static Func<long>? _pendingOrdersObserver;

    public static void SetPendingOrdersObserver(Func<long> observer)
    {
        _pendingOrdersObserver = observer ?? throw new ArgumentNullException(nameof(observer));
    }

    private static long ObservePendingOrders()
    {
        return (_pendingOrdersObserver ?? throw new InvalidOperationException(
            "The pending orders metric observer has not been configured."))();
    }

    public static readonly Counter<long> HttpRequests =
        Meter.CreateCounter<long>(
            "katalog.http.requests",
            unit: "{request}",
            description: "HTTP requests handled by the API.");

    public static readonly Counter<long> HttpErrors =
        Meter.CreateCounter<long>(
            "katalog.http.errors",
            unit: "{error}",
            description: "HTTP requests that completed with an error status.");

    public static readonly Histogram<double> HttpRequestDuration =
        Meter.CreateHistogram<double>(
            "katalog.http.request.duration",
            unit: "s",
            description: "HTTP request duration.");
}
