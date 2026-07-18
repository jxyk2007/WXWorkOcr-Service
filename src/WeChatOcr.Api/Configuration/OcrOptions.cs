// 版本号：v1.0
namespace WeChatOcr.Api.Configuration;

public sealed class OcrOptions
{
    public const string SectionName = "Ocr";
    public string[] CandidatePaths { get; set; } = [];
    public int TimeoutSeconds { get; set; } = 30;
    public long MaxImageBytes { get; set; } = 10 * 1024 * 1024;
    public int QueueCapacity { get; set; } = 20;
    public bool AllowLocalPath { get; set; }
}
