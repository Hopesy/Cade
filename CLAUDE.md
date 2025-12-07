# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## 项目概述

Cade 是一个基于 .NET 10 和 Semantic Kernel 的终端 AI 编程助手，支持多模型切换、自动工具调用和 MCP (Model Context Protocol) 协议。

---

## 常用命令

### 构建与运行
```bash
# 清理构建
dotnet clean

# 还原依赖
dotnet restore

# 编译项目
dotnet build

# 运行主程序（从源码）
dotnet run --project Cade

# 作为全局工具安装（用于测试打包）
dotnet pack Cade/Cade.csproj
dotnet tool install --global --add-source ./Cade/bin/Release Cade

# 运行已安装的工具
cade
```

### 日志查看
```bash
# 日志位置: Cade/bin/Debug/net10.0/Logs/Cade.log
# Windows 查看日志
type Cade\bin\Debug\net10.0\Logs\Cade*.log

# 实时监控日志
Get-Content Cade\bin\Debug\net10.0\Logs\Cade*.log -Wait
```

---

## 架构设计

### 三层项目结构

#### 1. **Cade** (主程序 - 终端 UI 和服务编排)
- **入口**: `Program.cs` 使用 Generic Host (`Host.CreateApplicationBuilder`) 管理应用生命周期
- **核心服务**:
  - `CadeHostedService`: 主程序入口，负责交互循环、命令处理、任务取消（ESC键）
  - `ConsoleUserInterface`: 基于 Spectre.Console 的终端 UI，包含实时状态栏、Markdown 渲染、思考动画
  - `ProductionAiService`: AI 服务实现，集成 Semantic Kernel 进行对话管理
  - `MainViewModel`: MVVM 模式的 ViewModel（使用 CommunityToolkit.Mvvm）
- **过滤器** (`Filters/`):
  - `ToolCallFilter`: 拦截工具调用，在 UI 中显示工具执行过程
  - `AutoFunctionFilter`: 拦截自动函数调用，显示 AI 思考过程

**关键架构点**:
- 所有服务通过 DI 容器注册，严禁手动 `new` 实例
- UI 更新通过事件和回调机制，避免直接依赖
- 支持任务取消（ESC 键）通过 `CancellationTokenSource` 实现

#### 2. **Cade.Provider** (AI Provider 和 MCP 支持)
- **Provider 服务**:
  - `ProviderConfigService`: 管理 `~/.cade/settings.json` 配置文件的读写
  - `ProviderService`: 负责加载和管理多个 AI Provider（OpenAI、Azure、Anthropic 等）
- **MCP 支持** (`Mcp/`):
  - `McpBridge`: 连接 MCP Server 并将其工具自动注册为 Semantic Kernel 插件
  - `McpLoaderService`: 启动时自动加载 `settings.json` 中配置的 MCP Servers

**配置文件位置**:
- 默认配置: `Cade.Provider/settings.json` (编译时复制到输出目录)
- 用户配置: `~/.cade/settings.json` (首次运行时自动创建)
- 临时配置: `Cade/appsettings.json` (运行时配置，如当前模型)

#### 3. **Cade.Tool** (内置工具插件)
- **插件**:
  - `FileSystemPlugin`: 文件操作工具（ReadFile, WriteFile, SearchFiles, Grep 等）
  - `SystemPlugin`: 系统操作工具（ExecuteCommand, GetSystemInfo, GetTime 等）

**插件开发规范**:
- 使用 `[KernelFunction]` 特性标记工具方法
- 使用 `[Description]` 特性为工具和参数添加描述（AI 会读取这些描述）
- 在 `Program.cs` 中注册插件为单例服务

---

## 依赖注入架构

所有组件通过 `Program.cs` 注册到 DI 容器，启动流程如下：

```
Host 启动
  ↓
加载配置 (settings.json, appsettings.json)
  ↓
注册服务 (IUserInterface, IProviderService, IAiService 等)
  ↓
注册插件 (FileSystemPlugin, SystemPlugin)
  ↓
启动 McpLoaderService (连接 MCP Servers)
  ↓
启动 CadeHostedService (主交互循环)
```

**MVVM 模式**:
- ViewModel 使用 `CommunityToolkit.Mvvm` 的 `[ObservableProperty]` 和 `[RelayCommand]`
- 不使用传统的 `OnPropertyChanged`，而是依赖源生成器
- ViewModel 必须从 DI 容器获取，不可直接在 XAML 中实例化（虽然这是控制台应用）

---

## 核心工作流程

### 1. AI 对话流程
```
用户输入
  ↓
CadeHostedService.ProcessAiInputAsync()
  ↓
MainViewModel.SubmitCommandWithCancellation()
  ↓
ProductionAiService.GetResponseAsync()
  ↓
Semantic Kernel 处理 (ChatCompletionService)
  ├─ AutoFunctionFilter (显示思考过程)
  ├─ 自动调用工具 (ToolCallBehavior.AutoInvokeKernelFunctions)
  └─ ToolCallFilter (显示工具调用)
  ↓
返回 AI 回复
  ↓
ConsoleUserInterface 渲染 Markdown
```

### 2. MCP 工具集成流程
```
settings.json 配置 MCP Server
  ↓
McpLoaderService 启动时读取配置
  ↓
McpBridge.ConnectAsync() 连接 MCP Server
  ↓
获取 MCP Server 提供的工具列表
  ↓
转换为 Semantic Kernel KernelFunction
  ↓
注册到 Kernel.Plugins
  ↓
AI 可自动调用这些工具
```

---

## 关键实现细节

### 配置文件结构 (settings.json)
```json
{
  "env": {
    "CADE_AUTH_TOKEN": "your-api-key",
    "CADE_BASE_URL": "https://api.example.com/v1",
    "CADE_DEFAULT_MODEL": "model-name",
    "CADE_PROVIDE_TYPE": "OpenAICompatible"
  },
  "McpServers": {
    "server-name": {
      "Command": "npx",
      "Args": ["-y", "@modelcontextprotocol/server-filesystem"],
      "Disabled": false
    }
  }
}
```

### Semantic Kernel 插件注册
```csharp
// 在 ProductionAiService.RegisterPlugins() 中
kernel.Plugins.AddFromObject(_fileSystemPlugin);
kernel.Plugins.AddFromObject(_systemPlugin);

// MCP 工具在 McpLoaderService 中注册
kernel.Plugins.Add(await _mcpBridge.ConnectAsync(...));
```

### UI 组件交互
- **ConsoleUserInterface**: 使用 Spectre.Console 提供 3 个区域的终端 UI:
  - 顶部：对话历史（滚动）
  - 中间：当前用户输入行
  - 底部：状态栏（路径、模型、思考动画）
- **SafeRender**: 所有 Spectre.Console 交互式组件（如 SelectionPrompt）必须包裹在 `SafeRender()` 中

---

## 调试与开发

### 调试运行时问题
1. **检查控制台输出**: 直接查看运行时错误
2. **检查日志文件**: `Logs/Cade.log` (使用 Serilog 记录)
3. **启用详细日志**: 在 `Program.cs` 中调整 Serilog 配置

### 常见问题

**编译错误: 找不到生成的属性/方法**
- 原因: CommunityToolkit.Mvvm 源生成器未刷新
- 解决: `dotnet clean && dotnet build`

**AI 不调用工具**
- 检查工具是否注册到 Kernel (`RegisterPlugins()`)
- 检查 `ToolCallBehavior` 是否设置为 `AutoInvokeKernelFunctions`
- 检查工具的 `[Description]` 是否清晰

**MCP Server 连接失败**
- 检查 `settings.json` 中的 Command 和 Args 是否正确
- 检查 MCP Server 是否安装（如 `npx -y @modelcontextprotocol/server-filesystem`）
- 查看日志文件中的 MCP 连接错误

---

## 添加新功能

### 添加新的内置工具
1. 在 `Cade.Tool/Plugins/` 中创建新插件类
2. 使用 `[KernelFunction]` 和 `[Description]` 标记方法
3. 在 `Program.cs` 中注册为单例服务
4. 在 `ProductionAiService.RegisterPlugins()` 中添加到 Kernel

### 添加新的 AI Provider
1. 在 `Cade.Provider/Services/` 中实现 Provider 逻辑
2. 在 `ProviderService` 中添加 Provider 加载逻辑
3. 在 `settings.json` 的 `env` 中添加配置支持

### 扩展 UI 功能
- 修改 `ConsoleUserInterface.cs` 中的渲染逻辑
- 使用 Spectre.Console 的 Panel, Table, Tree 等组件
- 所有交互式组件必须使用 `SafeRender()` 包裹

---

## 技术栈特性

- **.NET 10 / C# 13**: 使用最新语言特性（如 `required` 关键字、文件作用域命名空间）
- **Semantic Kernel 1.68.0**: AI 编排框架，支持自动工具调用和流式响应
- **CommunityToolkit.Mvvm 8.4.0**: 源生成器驱动的 MVVM 框架
- **Spectre.Console 0.54.0**: 跨平台终端 UI 库
- **Serilog**: 结构化日志，支持文件滚动
- **ModelContextProtocol 0.4.1**: MCP 协议客户端库

---

## NuGet 打包

项目配置为全局工具：
- `<PackAsTool>true</PackAsTool>`
- `<ToolCommandName>cade</ToolCommandName>`
- 编译后自动复制 `settings.json` 到 `~/.cade/` 目录

发布流程：
```bash
dotnet pack Cade/Cade.csproj -c Release
dotnet nuget push Cade/bin/Release/Cade.*.nupkg --source https://api.nuget.org/v3/index.json
```
