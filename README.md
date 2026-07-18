# 企业微信 OCR 检测与 API 服务

版本号：v1.5

项目发布版本：v1.0.1

已验证组件版本：

- 企业微信：5.0.7.8011
- WeChatOCR：1.0.1.28
- mmmojo / mmmojo_64：109.0.5414.75

这是一个面向 Windows 的 .NET 8 企业微信 OCR 服务，用于检测企业微信 OCR 私有组件，并在组件完整且兼容时提供串行 OCR HTTP API。代码仍保留旧版个人微信组件探测能力，但当前发布版以企业微信为主要运行方式。

## 运行条件

- Windows 10/11 x64
- .NET 8 Runtime 或 SDK
- 已安装包含 `WeChatOCR.exe`、`mmmojo.dll` 和 `mmmojo_64.dll` 的兼容微信版本
- 仅用于学习和内部验证，不建议商用

## 快速开始

```powershell
dotnet restore WeChatOcr.Service.sln
dotnet run --project src/WeChatOcr.Api
```

打开 `http://localhost:5000/swagger`。实际端口以控制台输出为准。

只执行环境诊断：

```powershell
dotnet run --project src/WeChatOcr.Api -- diagnose
```

## 接口

- `GET /health`：服务存活检查。
- `GET /api/diagnostics`：返回微信候选目录、OCR 程序路径、缺失组件和兼容结论。
- `POST /api/ocr/file`：使用 multipart/form-data 上传图片，字段名为 `file`。
- `POST /api/ocr/base64`：JSON 请求，例如 `{ "image": "..." }`。
- `POST /api/ocr/path`：JSON 请求，例如 `{ "path": "D:\\images\\a.png" }`；默认禁用。

## 配置热加载

配置文件为 `src/WeChatOcr.Api/appsettings.json`。程序运行期间修改以下配置会自动加载，无需重启：

- `CandidatePaths`：额外的微信完整版本目录候选项。
- `TimeoutSeconds`：每个 OCR 请求的超时时间。
- `MaxImageBytes`：图片最大字节数。
- `QueueCapacity`：允许等待和执行的请求总数，修改后从下一次入队立即生效。
- `AllowLocalPath`：是否允许 API 读取服务器本机图片路径。

每次请求都会通过 `IOptionsMonitor` 使用最新配置。

## 微信 4.x 兼容性

服务不会把旧版本 `_tmp` 目录中的 DLL 与当前 OCR 插件混用。只有同一正式微信版本目录同时存在 `mmmojo.dll` 和 `mmmojo_64.dll`，并找到 `WeChatOCR.exe` 时，诊断结果才会标记为兼容。微信 4.x 如果取消这些旧接口，服务会返回 503 和明确的缺失组件说明。

当前机器的个人微信目录只有旧版 `_tmp` DLL，不能用于个人微信模式；服务现使用下面的完整企业微信组件。

## 企业微信模式

项目已使用本地上游源码替代固定路径的 NuGet 调用，允许 `WeChatOCR.exe`、`mmmojo.dll`、`mmmojo_64.dll` 和模型全部从同一个企业微信目录加载。请在不会提交到 Git 的 `src/WeChatOcr.Api/appsettings.Local.json` 中配置本机目录，例如：

```text
D:\WXWork\5.0.7.8011\WeChatOCR
```

本项目已使用企业微信 OCR `1.0.1.28` 和版本均为 `109.0.5414.75` 的两个 mmmojo DLL 完成验证。包含“企业微信 OCR 测试 12345”的图片端到端测试返回 200 且文本识别正确。

也可以使用环境变量配置首个候选目录：

```powershell
$env:Ocr__CandidatePaths__0 = 'D:\WXWork\5.0.7.8011\WeChatOCR'
```

## 使用限制

本项目基于 ZGGSONG/WeChatOcr 修改，上游明确标注仅供学习、不得商用。仓库公开不代表获得微信或企业微信私有组件的商业授权；本仓库不会分发腾讯的 DLL、OCR 程序或模型文件。

## 测试

```powershell
dotnet test WeChatOcr.Service.sln
```
