// 版本号：v1.1
namespace WeChatOcr.Core.Diagnostics;

public sealed class WeChatEnvironmentProbe(IWeChatEnvironmentSource source) : IWeChatEnvironmentProbe
{
    public OcrCompatibilityReport Probe(IEnumerable<string>? configuredPaths = null)
    {
        var candidates = (configuredPaths ?? [])
            .Concat(source.GetCandidateDirectories())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .Where(source.DirectoryExists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var compatibleDirectory = candidates.FirstOrDefault(HasMojoComponents);
        var localOcrExe = compatibleDirectory is null ? null : Path.Combine(compatibleDirectory, "WeChatOCR.exe");
        var ocrExe = localOcrExe is not null && source.FileExists(localOcrExe)
            ? localOcrExe
            : source.FindOcrExecutables().FirstOrDefault(source.FileExists);
        var missing = new List<string>();
        if (compatibleDirectory is null)
        {
            if (!candidates.Any(x => source.FileExists(Path.Combine(x, "mmmojo.dll")))) missing.Add("mmmojo.dll");
            if (!candidates.Any(x => source.FileExists(Path.Combine(x, "mmmojo_64.dll")))) missing.Add("mmmojo_64.dll");
        }
        if (ocrExe is null) missing.Add("WeChatOCR.exe");
        var compatible = compatibleDirectory is not null && ocrExe is not null;
        return new OcrCompatibilityReport(compatible, compatibleDirectory, ocrExe, candidates, missing,
            compatible ? "发现完整的微信 OCR 兼容组件，可进行实测。" : "未发现完整兼容组件；微信 4.x 可能不再提供旧版接口。");
    }

    private bool HasMojoComponents(string path) =>
        source.FileExists(Path.Combine(path, "mmmojo.dll")) &&
        source.FileExists(Path.Combine(path, "mmmojo_64.dll"));
}
