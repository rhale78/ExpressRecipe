using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ExpressRecipe.Messaging.Core.Messages;
using ExpressRecipe.Messaging.Core.Tracing;

namespace ExpressRecipe.Messaging.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for the overhead of injecting and extracting W3C trace context into message envelopes.
/// </summary>
[SimpleJob(RuntimeMoniker.Net90)]
[MemoryDiagnoser]
public class TraceContextBenchmarks
{
    private Activity? _activity;
    private MessageEnvelope _envelopeWithTrace = new();
    private MessageEnvelope _envelopeWithoutTrace = new();

    [GlobalSetup]
    public void Setup()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == MessagingActivitySource.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        _activity = MessagingActivitySource.Source.StartActivity("benchmark-publish", ActivityKind.Producer);

        _envelopeWithTrace = new MessageEnvelope();
        MessagingActivitySource.InjectTraceContext(_envelopeWithTrace, _activity);

        _envelopeWithoutTrace = new MessageEnvelope();
    }

    [GlobalCleanup]
    public void Cleanup() => _activity?.Dispose();

    [Benchmark]
    public void InjectTraceContext()
    {
        var envelope = new MessageEnvelope();
        MessagingActivitySource.InjectTraceContext(envelope, _activity);
    }

    [Benchmark]
    public ActivityContext? ExtractTraceContext()
        => MessagingActivitySource.ExtractTraceContext(_envelopeWithTrace);

    [Benchmark]
    public ActivityContext? ExtractTraceContext_NoTrace()
        => MessagingActivitySource.ExtractTraceContext(_envelopeWithoutTrace);
}
