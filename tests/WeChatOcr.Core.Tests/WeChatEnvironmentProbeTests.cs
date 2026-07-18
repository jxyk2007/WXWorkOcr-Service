// 版本号：v1.1
using WeChatOcr.Core.Diagnostics;

namespace WeChatOcr.Core.Tests;

public sealed class WeChatEnvironmentProbeTests
{
    [Fact]
    public void Probe_完整组件存在时返回兼容()
    {
        var source = new FakeEnvironmentSource(
            [@"D:\WeChat", @"d:\wechat\"],
            [@"D:\WeChat\mmmojo.dll", @"D:\WeChat\mmmojo_64.dll", @"C:\Ocr\WeChatOCR.exe"]);
        var probe = new WeChatEnvironmentProbe(source);

        var report = probe.Probe([@"D:\WeChat"]);

        Assert.True(report.IsCompatible);
        Assert.Equal(@"D:\WeChat", report.WeChatDirectory);
        Assert.Equal(@"C:\Ocr\WeChatOCR.exe", report.OcrExecutablePath);
        Assert.Single(report.CandidateDirectories);
    }

    [Fact]
    public void Probe_组件缺失时列出缺失项()
    {
        var source = new FakeEnvironmentSource([@"D:\Weixin"], [@"D:\Weixin\mmmojo.dll"]);
        var report = new WeChatEnvironmentProbe(source).Probe();

        Assert.False(report.IsCompatible);
        Assert.Contains("mmmojo_64.dll", report.MissingComponents);
        Assert.Contains("WeChatOCR.exe", report.MissingComponents);
    }

    [Fact]
    public void Probe_优先使用组件目录内的企业微信Ocr程序()
    {
        var source = new FakeEnvironmentSource(
            [@"G:\WXWork\WeChatOCR"],
            [@"G:\WXWork\WeChatOCR\mmmojo.dll", @"G:\WXWork\WeChatOCR\mmmojo_64.dll",
             @"G:\WXWork\WeChatOCR\WeChatOCR.exe", @"C:\Personal\WeChatOCR.exe"]);

        var report = new WeChatEnvironmentProbe(source).Probe();

        Assert.True(report.IsCompatible);
        Assert.Equal(@"G:\WXWork\WeChatOCR\WeChatOCR.exe", report.OcrExecutablePath);
    }

    private sealed class FakeEnvironmentSource(string[] candidates, string[] files) : IWeChatEnvironmentSource
    {
        public IEnumerable<string> GetCandidateDirectories() => candidates;
        public IEnumerable<string> FindOcrExecutables() => files.Where(x => x.EndsWith("WeChatOCR.exe", StringComparison.OrdinalIgnoreCase));
        public bool DirectoryExists(string path) => candidates.Any(x => string.Equals(x.TrimEnd('\\'), path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        public bool FileExists(string path) => files.Contains(path, StringComparer.OrdinalIgnoreCase);
    }
}
