// 版本号：v1.0
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WeChatOcr.Api.Configuration;

namespace WeChatOcr.Api.Tests;

public sealed class ConfigurationReloadTests
{
    [Fact]
    public void OptionsMonitor_配置重新加载后读取最新值()
    {
        var values = new Dictionary<string, string?> { ["Ocr:TimeoutSeconds"] = "10" };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        using var provider = new ServiceCollection().AddOptions().Configure<OcrOptions>(configuration.GetSection("Ocr")).BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<OcrOptions>>();

        configuration["Ocr:TimeoutSeconds"] = "25";
        configuration.Reload();

        Assert.Equal(25, monitor.CurrentValue.TimeoutSeconds);
    }
}
