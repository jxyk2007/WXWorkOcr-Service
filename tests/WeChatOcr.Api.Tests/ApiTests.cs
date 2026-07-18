// 版本号：v1.0
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WeChatOcr.Api.Tests;

public sealed class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    public ApiTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Health_返回健康状态()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Diagnostics_返回结构化报告()
    {
        var response = await _client.GetAsync("/api/diagnostics");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("isCompatible", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Path_默认禁止读取本机文件()
    {
        var response = await _client.PostAsJsonAsync("/api/ocr/path", new { path = @"C:\test.png" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Base64_无效内容返回四百()
    {
        var response = await _client.PostAsJsonAsync("/api/ocr/base64", new { image = "not-base64" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
