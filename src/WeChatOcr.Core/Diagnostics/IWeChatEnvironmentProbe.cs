// 版本号：v1.0
namespace WeChatOcr.Core.Diagnostics;

public interface IWeChatEnvironmentProbe
{
    OcrCompatibilityReport Probe(IEnumerable<string>? configuredPaths = null);
}

public interface IWeChatEnvironmentSource
{
    IEnumerable<string> GetCandidateDirectories();
    IEnumerable<string> FindOcrExecutables();
    bool DirectoryExists(string path);
    bool FileExists(string path);
}
