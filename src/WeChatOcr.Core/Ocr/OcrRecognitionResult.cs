// 版本号：v1.0
namespace WeChatOcr.Core.Ocr;

public sealed record OcrRecognitionResult(string Text, IReadOnlyList<string> Lines);
