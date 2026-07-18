// 版本号：v1.1
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using WeChatOcr.Api.Configuration;
using WeChatOcr.Core.Ocr;

namespace WeChatOcr.Api.Services;

public sealed class OcrQueue : BackgroundService, IOcrQueue
{
    private readonly Channel<WorkItem> _channel;
    private readonly IOcrEngine _engine;
    private readonly IOptionsMonitor<OcrOptions> _options;
    private int _queuedCount;

    public OcrQueue(IOcrEngine engine, IOptionsMonitor<OcrOptions> options)
    {
        _engine = engine;
        _options = options;
        _channel = Channel.CreateUnbounded<WorkItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public Task<OcrRecognitionResult> EnqueueAsync(byte[] imageBytes, CancellationToken cancellationToken)
    {
        var capacity = Math.Max(1, _options.CurrentValue.QueueCapacity);
        if (Interlocked.Increment(ref _queuedCount) > capacity)
        {
            Interlocked.Decrement(ref _queuedCount);
            throw new OcrQueueFullException();
        }
        var completion = new TaskCompletionSource<OcrRecognitionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_channel.Writer.TryWrite(new WorkItem(imageBytes, cancellationToken, completion)))
        {
            Interlocked.Decrement(ref _queuedCount);
            throw new OcrQueueFullException();
        }
        return completion.Task;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            if (item.CancellationToken.IsCancellationRequested)
            {
                item.Completion.TrySetCanceled(item.CancellationToken);
                Interlocked.Decrement(ref _queuedCount);
                continue;
            }
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, item.CancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.CurrentValue.TimeoutSeconds)));
                item.Completion.TrySetResult(await _engine.RecognizeAsync(item.ImageBytes, timeout.Token));
            }
            catch (OperationCanceledException) when (!item.CancellationToken.IsCancellationRequested)
            {
                item.Completion.TrySetException(new TimeoutException("OCR 识别超时。"));
            }
            catch (Exception exception)
            {
                item.Completion.TrySetException(exception);
            }
            Interlocked.Decrement(ref _queuedCount);
        }
    }

    private sealed record WorkItem(byte[] ImageBytes, CancellationToken CancellationToken, TaskCompletionSource<OcrRecognitionResult> Completion);
}
