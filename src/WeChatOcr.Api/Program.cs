// 版本号：v1.3
using System.Text.Json;
using Microsoft.Extensions.Options;
using WeChatOcr.Api.Configuration;
using WeChatOcr.Api.Endpoints;
using WeChatOcr.Api.Services;
using WeChatOcr.Core.Diagnostics;
using WeChatOcr.Core.Ocr;

var builder = WebApplication.CreateBuilder(args);
var configurationDirectory = AppContext.BaseDirectory;
builder.Configuration.AddJsonFile(Path.Combine(configurationDirectory, "appsettings.json"), optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile(Path.Combine(configurationDirectory, "appsettings.Local.json"), optional: true, reloadOnChange: true);
builder.Services.Configure<OcrOptions>(builder.Configuration.GetSection(OcrOptions.SectionName));
builder.Services.AddSingleton<IWeChatEnvironmentSource, WindowsWeChatEnvironmentSource>();
builder.Services.AddSingleton<IWeChatEnvironmentProbe, WeChatEnvironmentProbe>();
builder.Services.AddSingleton<IOcrEngine>(services =>
{
    var probe = services.GetRequiredService<IWeChatEnvironmentProbe>();
    var options = services.GetRequiredService<IOptionsMonitor<OcrOptions>>();
    return new WeChatOcrEngine(probe, () => options.CurrentValue.CandidatePaths);
});
builder.Services.AddSingleton<OcrQueue>();
builder.Services.AddSingleton<IOcrQueue>(services => services.GetRequiredService<OcrQueue>());
builder.Services.AddHostedService(services => services.GetRequiredService<OcrQueue>());
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
if (args.Any(x => string.Equals(x, "diagnose", StringComparison.OrdinalIgnoreCase)))
{
    var probe = app.Services.GetRequiredService<IWeChatEnvironmentProbe>();
    var options = app.Services.GetRequiredService<IOptionsMonitor<OcrOptions>>().CurrentValue;
    Console.WriteLine(JsonSerializer.Serialize(probe.Probe(options.CandidatePaths), new JsonSerializerOptions { WriteIndented = true }));
    return;
}

app.MapGet("/", () => Results.Redirect("/api/diagnostics"));
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/api/diagnostics", (IWeChatEnvironmentProbe probe, IOptionsMonitor<OcrOptions> options) =>
    Results.Ok(probe.Probe(options.CurrentValue.CandidatePaths)));
app.MapOcrEndpoints();
app.UseSwagger();
app.UseSwaggerUI();

app.Run();

public partial class Program;
