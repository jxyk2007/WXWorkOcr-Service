// 版本号：v1.1
using Microsoft.Extensions.Options;
using WeChatOcr.Api.Configuration;
using WeChatOcr.Api.Services;
using WeChatOcr.Core.Ocr;

namespace WeChatOcr.Api.Tests;

public sealed class OcrQueueTests
{
    [Fact]
    public async Task Queue_并发请求保持串行执行()
    {
        var engine = new TrackingEngine();
        var queue = new OcrQueue(engine, new StaticOptionsMonitor(new OcrOptions { QueueCapacity = 5, TimeoutSeconds = 5 }));
        await queue.StartAsync(CancellationToken.None);
        try
        {
            await Task.WhenAll(queue.EnqueueAsync([1], default), queue.EnqueueAsync([2], default), queue.EnqueueAsync([3], default));
            Assert.Equal(1, engine.MaximumConcurrency);
        }
        finally { await queue.StopAsync(CancellationToken.None); }
    }

    [Fact]
    public async Task Queue_容量配置更新后立即生效()
    {
        var monitor = new MutableOptionsMonitor(new OcrOptions { QueueCapacity = 1, TimeoutSeconds = 5 });
        var queue = new OcrQueue(new TrackingEngine(), monitor);
        _ = queue.EnqueueAsync([1], default);
        await Assert.ThrowsAsync<OcrQueueFullException>(() => queue.EnqueueAsync([2], default));
        monitor.Value = new OcrOptions { QueueCapacity = 2, TimeoutSeconds = 5 };
        _ = queue.EnqueueAsync([3], default);
    }

    private sealed class TrackingEngine : IOcrEngine
    {
        private int _current;
        public int MaximumConcurrency { get; private set; }
        public async Task<OcrRecognitionResult> RecognizeAsync(byte[] imageBytes, CancellationToken cancellationToken)
        {
            var current = Interlocked.Increment(ref _current);
            MaximumConcurrency = Math.Max(MaximumConcurrency, current);
            await Task.Delay(20, cancellationToken);
            Interlocked.Decrement(ref _current);
            return new OcrRecognitionResult("ok", ["ok"]);
        }
    }

    private sealed class StaticOptionsMonitor(OcrOptions value) : IOptionsMonitor<OcrOptions>
    {
        public OcrOptions CurrentValue => value;
        public OcrOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<OcrOptions, string?> listener) => null;
    }

    private sealed class MutableOptionsMonitor(OcrOptions value) : IOptionsMonitor<OcrOptions>
    {
        public OcrOptions Value { get; set; } = value;
        public OcrOptions CurrentValue => Value;
        public OcrOptions Get(string? name) => Value;
        public IDisposable? OnChange(Action<OcrOptions, string?> listener) => null;
    }
}
