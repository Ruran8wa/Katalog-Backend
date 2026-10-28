using System.Diagnostics;
using System.Diagnostics.Metrics;
using Katalog_Backend.Observability;
using NUnit.Framework;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;

namespace Katalog_Backend.Tests.Observability;

[TestFixture]
public sealed class TelemetryTests
{
    [Test]
    public void ServiceBusPropagation_PreservesTraceIdForConsumerSpan()
    {
        var exported = new List<Activity>();
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(Telemetry.ActivitySourceName)
            .AddInMemoryExporter(exported)
            .Build();
        using var listener = new MeterListener();
        listener.Start();

        ActivityContext consumerParent;
        using (var producer = Telemetry.ActivitySource.StartActivity(
                   "servicebus publish OrderPlaced",
                   ActivityKind.Producer))
        {
            Assert.That(producer, Is.Not.Null);
            var carrier = new Dictionary<string, object?>();
            Propagators.DefaultTextMapPropagator.Inject(
                new PropagationContext(producer!.Context, Baggage.Current),
                carrier,
                static (values, key, value) => values[key] = value);
            consumerParent = Propagators.DefaultTextMapPropagator.Extract(
                default,
                carrier,
                static (values, key) => values.TryGetValue(key, out var value)
                    ? [value?.ToString() ?? string.Empty]
                    : []).ActivityContext;
        }

        using (var consumer = Telemetry.ActivitySource.StartActivity(
                   "servicebus process OrderPlaced",
                   ActivityKind.Consumer,
                   consumerParent))
        {
            Assert.That(consumer, Is.Not.Null);
            Assert.That(consumer!.TraceId, Is.EqualTo(consumerParent.TraceId));
        }

        provider.ForceFlush();
        Assert.That(exported, Has.Count.EqualTo(2));
        Assert.That(exported.Select(activity => activity.TraceId).Distinct().Count(), Is.EqualTo(1));
    }

    [Test]
    public void OrdersProcessed_RecordsConfirmedAndRejectedReasons()
    {
        var measurements = new List<(long Value, string Outcome, string Reason)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument == Telemetry.OrdersProcessed)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            measurements.Add((
                value,
                tags.ToArray().First(tag => tag.Key == "outcome").Value?.ToString() ?? string.Empty,
                tags.ToArray().First(tag => tag.Key == "rejection_reason").Value?.ToString() ?? string.Empty));
        });
        listener.Start();
        listener.EnableMeasurementEvents(Telemetry.OrdersProcessed);

        Telemetry.OrdersProcessed.Add(1, new TagList
        {
            { "outcome", "confirmed" },
            { "rejection_reason", "none" }
        });
        Telemetry.OrdersProcessed.Add(1, new TagList
        {
            { "outcome", "rejected" },
            { "rejection_reason", "insufficient_stock" }
        });
        listener.RecordObservableInstruments();

        Assert.That(measurements, Is.EqualTo(new[]
        {
            (1L, "confirmed", "none"),
            (1L, "rejected", "insufficient_stock")
        }));
    }
}
