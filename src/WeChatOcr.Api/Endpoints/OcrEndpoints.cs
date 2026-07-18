// 版本号：v1.0
using Microsoft.Extensions.Options;
using WeChatOcr.Api.Configuration;
using WeChatOcr.Api.Services;
using WeChatOcr.Core.Ocr;

namespace WeChatOcr.Api.Endpoints;

public static class OcrEndpoints
{
    public static IEndpointRouteBuilder MapOcrEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/ocr/file", RecognizeFileAsync).DisableAntiforgery();
        endpoints.MapPost("/api/ocr/base64", RecognizeBase64Async);
        endpoints.MapPost("/api/ocr/path", RecognizePathAsync);
        return endpoints;
    }

    private static async Task<IResult> RecognizeFileAsync(IFormFile file, IOcrQueue queue, IOptionsMonitor<OcrOptions> options, CancellationToken token)
    {
        if (file.Length <= 0 || file.Length > options.CurrentValue.MaxImageBytes)
            return Results.Problem("图片为空或超过大小限制。", statusCode: StatusCodes.Status400BadRequest);
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, token);
        return await ExecuteAsync(queue, stream.ToArray(), token);
    }

    private static async Task<IResult> RecognizeBase64Async(Base64OcrRequest request, IOcrQueue queue, IOptionsMonitor<OcrOptions> options, CancellationToken token)
    {
        byte[] bytes;
        try { bytes = Convert.FromBase64String(request.Image ?? string.Empty); }
        catch (FormatException) { return Results.Problem("Base64 图片格式无效。", statusCode: StatusCodes.Status400BadRequest); }
        if (bytes.Length == 0 || bytes.LongLength > options.CurrentValue.MaxImageBytes)
            return Results.Problem("图片为空或超过大小限制。", statusCode: StatusCodes.Status400BadRequest);
        return await ExecuteAsync(queue, bytes, token);
    }

    private static async Task<IResult> RecognizePathAsync(PathOcrRequest request, IOcrQueue queue, IOptionsMonitor<OcrOptions> options, CancellationToken token)
    {
        var current = options.CurrentValue;
        if (!current.AllowLocalPath) return Results.Problem("本机路径识别已禁用。", statusCode: StatusCodes.Status403Forbidden);
        if (string.IsNullOrWhiteSpace(request.Path) || !File.Exists(request.Path))
            return Results.Problem("图片路径不存在。", statusCode: StatusCodes.Status400BadRequest);
        var info = new FileInfo(request.Path);
        if (info.Length <= 0 || info.Length > current.MaxImageBytes)
            return Results.Problem("图片为空或超过大小限制。", statusCode: StatusCodes.Status400BadRequest);
        return await ExecuteAsync(queue, await File.ReadAllBytesAsync(request.Path, token), token);
    }

    private static async Task<IResult> ExecuteAsync(IOcrQueue queue, byte[] bytes, CancellationToken token)
    {
        try { return Results.Ok(await queue.EnqueueAsync(bytes, token)); }
        catch (OcrQueueFullException exception) { return Results.Problem(exception.Message, statusCode: StatusCodes.Status429TooManyRequests); }
        catch (OcrUnavailableException exception) { return Results.Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable); }
        catch (TimeoutException exception) { return Results.Problem(exception.Message, statusCode: StatusCodes.Status504GatewayTimeout); }
    }

    public sealed record Base64OcrRequest(string? Image);
    public sealed record PathOcrRequest(string? Path);
}
