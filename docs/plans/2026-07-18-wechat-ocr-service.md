# 微信 OCR 检测与 API 服务 Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 创建支持微信 3.x/4.x 组件探测、配置热加载和串行 OCR 调用的 .NET 8 HTTP 服务及诊断命令。

**Architecture:** 使用 Core 类库隔离环境探测与 OCR 会话，ASP.NET Core Minimal API 暴露诊断和识别接口，并通过有界 Channel 串行化私有 OCR 进程访问。所有运行参数通过 `IOptionsMonitor` 动态读取，微信 4.x 仅在发现完整兼容组件时尝试调用。

**Tech Stack:** .NET 8、ASP.NET Core Minimal API、xUnit、Microsoft.Extensions.Options、System.Threading.Channels、WeChatOcr NuGet 1.0.5。

---

### Task 1: 创建解决方案骨架

**Files:**
- Create: `WeChatOcr.Service.sln`
- Create: `src/WeChatOcr.Core/WeChatOcr.Core.csproj`
- Create: `src/WeChatOcr.Api/WeChatOcr.Api.csproj`
- Create: `tests/WeChatOcr.Core.Tests/WeChatOcr.Core.Tests.csproj`
- Create: `tests/WeChatOcr.Api.Tests/WeChatOcr.Api.Tests.csproj`

**Steps:**
1. 创建解决方案和四个项目，并为每个源码文件预留 `版本号：v1.0` 文件头。
2. 添加项目引用与测试依赖。
3. 运行 `dotnet restore WeChatOcr.Service.sln`，预期还原成功。
4. 运行 `dotnet build WeChatOcr.Service.sln --no-restore`，预期零错误。

### Task 2: 实现微信环境探测

**Files:**
- Create: `src/WeChatOcr.Core/Diagnostics/OcrCompatibilityReport.cs`
- Create: `src/WeChatOcr.Core/Diagnostics/IWeChatEnvironmentProbe.cs`
- Create: `src/WeChatOcr.Core/Diagnostics/WeChatEnvironmentProbe.cs`
- Test: `tests/WeChatOcr.Core.Tests/WeChatEnvironmentProbeTests.cs`

**Steps:**
1. 先编写测试，覆盖显式路径、3.x 注册表安装位置、4.x 候选目录、OCR 插件目录、路径去重和组件缺失。
2. 运行 `dotnet test --filter WeChatEnvironmentProbeTests`，预期测试因类型不存在而失败。
3. 使用可替换的文件系统和注册表数据源实现探测器，完整组件必须包含 OCR EXE 与同一微信目录下的两个 mmmojo DLL。
4. 再次运行筛选测试，预期全部通过。

### Task 3: 实现热加载配置与诊断命令

**Files:**
- Create: `src/WeChatOcr.Api/Configuration/OcrOptions.cs`
- Create: `src/WeChatOcr.Api/appsettings.json`
- Create: `src/WeChatOcr.Api/Commands/DiagnoseCommand.cs`
- Test: `tests/WeChatOcr.Api.Tests/ConfigurationReloadTests.cs`

**Steps:**
1. 编写配置更新后 `IOptionsMonitor.CurrentValue` 变化的测试。
2. 配置候选路径、超时、最大图片字节数、队列容量和本机路径开关，并启用 `reloadOnChange`。
3. 实现 `diagnose` 参数，输出 JSON 兼容性报告后退出。
4. 运行配置和命令测试，预期全部通过。

### Task 4: 实现 OCR 会话与串行队列

**Files:**
- Create: `src/WeChatOcr.Core/Ocr/IOcrEngine.cs`
- Create: `src/WeChatOcr.Core/Ocr/WeChatOcrEngine.cs`
- Create: `src/WeChatOcr.Api/Services/OcrQueue.cs`
- Test: `tests/WeChatOcr.Api.Tests/OcrQueueTests.cs`

**Steps:**
1. 编写并发提交仍串行执行、队列满、超时和失败后恢复测试。
2. 引入 `WeChatOcr` 1.0.5，在兼容性报告通过后才创建 `ImageOcr`。
3. 使用有界 Channel 和单消费者执行任务；配置通过 `IOptionsMonitor` 在每项任务开始前读取。
4. 运行队列测试，预期全部通过。

### Task 5: 实现 HTTP API

**Files:**
- Create: `src/WeChatOcr.Api/Program.cs`
- Create: `src/WeChatOcr.Api/Endpoints/OcrEndpoints.cs`
- Test: `tests/WeChatOcr.Api.Tests/ApiTests.cs`

**Steps:**
1. 编写 `/health`、`/api/diagnostics`、文件、Base64 和本机路径接口测试。
2. 实现端点、OpenAPI、输入限制和 ProblemDetails 错误映射。
3. 验证本机路径接口默认返回禁用状态，修改配置后无需重启即可启用。
4. 运行 API 集成测试，预期全部通过。

### Task 6: 文档和本机验证

**Files:**
- Create: `README.md`
- Create: `start.bat`

**Steps:**
1. 编写安装、配置、热加载、接口调用和微信 4.x 兼容性说明；BAT 备注全部使用英文。
2. 运行 `dotnet test WeChatOcr.Service.sln`，预期全部测试通过。
3. 运行 `dotnet run --project src/WeChatOcr.Api -- diagnose`，记录当前机器真实报告。
4. 短暂启动服务并请求 `/health` 与 `/api/diagnostics`，预期返回有效 JSON。
