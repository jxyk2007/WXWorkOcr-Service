// 版本号：v1.0
using WeChatOcr.Core.Ocr;

namespace WeChatOcr.Api.Services;

public interface IOcrQueue
{
    Task<OcrRecognitionResult> EnqueueAsync(byte[] imageBytes, CancellationToken cancellationToken);
}

public sealed class OcrQueueFullException : Exception
{
    public OcrQueueFullException() : base("OCR 队列已满，请稍后重试。") { }
}
