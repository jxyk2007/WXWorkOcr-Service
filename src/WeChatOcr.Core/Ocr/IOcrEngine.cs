// 版本号：v1.0
namespace WeChatOcr.Core.Ocr;

public interface IOcrEngine
{
    Task<OcrRecognitionResult> RecognizeAsync(byte[] imageBytes, CancellationToken cancellationToken);
}
