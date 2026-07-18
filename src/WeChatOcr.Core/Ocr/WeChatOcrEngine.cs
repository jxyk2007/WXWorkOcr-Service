// 版本号：v1.2
using WeChatOcr.Core.Diagnostics;

namespace WeChatOcr.Core.Ocr;

public sealed class WeChatOcrEngine(IWeChatEnvironmentProbe probe, Func<IEnumerable<string>> configuredPaths) : IOcrEngine
{
    public async Task<OcrRecognitionResult> RecognizeAsync(byte[] imageBytes, CancellationToken cancellationToken)
    {
        var report = probe.Probe(configuredPaths());
        if (!report.IsCompatible || report.WeChatDirectory is null || report.OcrExecutablePath is null)
            throw new OcrUnavailableException(report.Summary + " 缺失：" + string.Join("、", report.MissingComponents));

        var completion = new TaskCompletionSource<OcrRecognitionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        global::WeChatOcr.DataLocation.SetBaseDirectory(AppDomain.CurrentDomain.BaseDirectory);
        using var ocr = new global::WeChatOcr.ImageOcr(report.WeChatDirectory, report.OcrExecutablePath);
        ocr.Run(imageBytes, (_, result) =>
        {
            var lines = result?.OcrResult?.SingleResult?
                .Select(x => x.SingleStrUtf8)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>()
                .ToArray() ?? [];
            completion.TrySetResult(new OcrRecognitionResult(string.Join(Environment.NewLine, lines), lines));
        });
        return await completion.Task.ConfigureAwait(false);
    }
}
