// 版本号：v1.0
namespace WeChatOcr.Core.Diagnostics;

public sealed record OcrCompatibilityReport(
    bool IsCompatible,
    string? WeChatDirectory,
    string? OcrExecutablePath,
    IReadOnlyList<string> CandidateDirectories,
    IReadOnlyList<string> MissingComponents,
    string Summary);
