# Cade

<div align="center">

**基于 .NET 10 和 Semantic Kernel 的终端 AI 编程助手**

支持工具调用、MCP 协议和多模型切换

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![Semantic Kernel](https://img.shields.io/badge/Semantic%20Kernel-1.68.0-blue)](https://github.com/microsoft/semantic-kernel)

</div>

---

## ✨ 功能特性

- 🤖 **多模型支持** - 支持配置多个 AI Provider（OpenAI、Azure OpenAI、Anthropic Claude 等）
- 🛠️ **自动工具调用** - AI 可自动执行文件操作、Shell 命令等系统操作
- 🔌 **MCP 协议支持** - 完整支持 Model Context Protocol，轻松扩展工具能力
- 🎨 **Markdown 渲染** - 优雅的代码高亮、列表、标题等格式化输出
- ⚡ **实时取消** - 按 ESC 键可随时终止正在执行的任务
- 💭 **思考模式** - Tab 键快速切换，支持思维链模型
- 💾 **对话缓存** - 按目录缓存对话，`/continue` 一键恢复上次会话
- 🔄 **自动更新检查** - 启动时检查新版本，提示更新命令
- 📦 **NuGet 全局工具** - 一键安装，全局可用

---

## 🚀 快速开始

```bash
# 从 NuGet 安装
dotnet tool install --global Cade

# 运行
cade
```
---

## 🔧 配置

### 初始配置

首次运行时，Cade 会在 `~/.cade/settings.json` 创建默认配置文件。

**配置文件位置**：
- Windows: `C:\Users\<用户名>\.cade\settings.json`
- Linux/macOS: `~/.cade/settings.json`

### 配置示例

```json
{
  "env": {
    "CADE_AUTH_TOKEN": "sk-your-api-key",
    "CADE_BASE_URL": "https://api.openai.com/v1",
    "CADE_DEFAULT_MODEL": "gpt-4o",
    "CADE_PROVIDE_TYPE": "OpenAI"
  },
  "McpServers": {
    "filesystem": {
      "Command": "npx",
      "Args": ["-y", "@modelcontextprotocol/server-filesystem", "C:\\MyProjects"],
      "Disabled": false
    }
  }
}
```

### 配置说明

#### Provider 配置 (env)

| 环境变量 | 说明 |
|---------|------|
| `CADE_AUTH_TOKEN` | API 密钥 |
| `CADE_BASE_URL` | API 端点 |
| `CADE_DEFAULT_MODEL` | 默认模型 |
| `CADE_PROVIDE_TYPE` | 提供商类型: `OpenAI`, `OpenAICompatible`, `AzureOpenAI` |

**常用配置示例**：

```json
// OpenAI
"CADE_BASE_URL": "https://api.openai.com/v1"
"CADE_PROVIDE_TYPE": "OpenAI"

// DeepSeek
"CADE_BASE_URL": "https://api.deepseek.com/v1"
"CADE_PROVIDE_TYPE": "OpenAICompatible"

// Azure OpenAI
"CADE_BASE_URL": "https://your-resource.openai.azure.com"
"CADE_PROVIDE_TYPE": "AzureOpenAI"
```

#### MCP Server 配置

| 字段 | 说明 |
|------|------|
| `Command` | 启动命令 (如 `npx`, `uvx`, `node`) |
| `Args` | 命令行参数数组 |
| `Disabled` | 是否禁用 |

### 自定义系统提示词

在 `~/.cade/cade.md` 中编写自定义规范，Cade 启动时会自动加载并追加到系统提示词中。

**示例** (`~/.cade/cade.md`)：

```markdown
## 项目规范

- 使用 .NET 8 和 C# 12
- 遵循 Clean Architecture 架构
- 所有公共方法必须有 XML 注释
- 使用 async/await 处理异步操作
- 单元测试使用 xUnit + Moq

## 代码风格

- 命名规范：PascalCase 用于类和方法，camelCase 用于局部变量
- 每个文件只包含一个类
- 使用 nullable reference types
```

这样每次创建项目或编写代码时，AI 都会遵循你定义的规范。

---

## 📖 使用指南

### 基本使用

启动 Cade 后，直接输入你的问题或指令：

```
>> 帮我创建一个 .NET 控制台项目

>> 读取 Program.cs 文件并解释它的作用

>> 在当前目录搜索所有包含 "TODO" 的 .cs 文件

>> 执行 dotnet build 命令并分析输出结果
```

### 内置命令

| 命令 | 说明 |
|------|------|
| `/model` | 切换当前使用的 AI 模型 |
| `/think` | 切换思考模式 (Tab 快捷键) |
| `/continue` | 恢复上次对话 (基于当前目录) |
| `/clear` | 清空当前对话历史 |
| `/help` | 显示帮助信息 |
| `/exit` | 退出程序 |

### 快捷键

| 快捷键 | 说明 |
|--------|------|
| **Tab** | 切换思考模式 (输入为空时) |
| **ESC** | 取消当前正在执行的 AI 任务 |
| **Ctrl+C** | 退出程序 |

---

## 🛠️ 内置工具

Cade 提供了丰富的内置工具，AI 可以自动调用这些工具完成任务：

### 📁 文件操作

| 工具 | 说明 |
|------|------|
| `ReadFile` | 读取文件内容 |
| `WriteFile` | 写入文件（覆盖） |
| `AppendToFile` | 追加内容到文件 |
| `ReplaceInFile` | 替换文件中的内容 |
| `CreateDirectory` | 创建目录 |
| `Delete` | 删除文件或目录 |
| `Move` | 移动/重命名文件或目录 |
| `Copy` | 复制文件或目录 |
| `ListDirectory` | 列出目录内容 |
| `SearchFiles` | 搜索文件 |
| `Grep` | 在文件中搜索文本 |
| `GetInfo` | 获取文件/目录详细信息 |

### 💻 系统操作

| 工具 | 说明 |
|------|------|
| `ExecuteCommand` | 执行 Shell 命令 (如 `dotnet`, `npm`, `git` 等) |
| `GetSystemInfo` | 获取系统信息 |
| `GetTime` | 获取当前时间 |
| `GetNetworkInfo` | 获取网络信息 |

---

## 🔌 MCP 协议

Cade 完整支持 [Model Context Protocol (MCP)](https://modelcontextprotocol.io/)，可以轻松集成第三方工具服务器。

### 已测试的 MCP Servers

- **@modelcontextprotocol/server-filesystem** - 文件系统访问
- **@modelcontextprotocol/server-git** - Git 操作
- **@modelcontextprotocol/server-sqlite** - SQLite 数据库
- **@modelcontextprotocol/server-brave-search** - Brave 搜索

### 添加 MCP Server

在 `settings.json` 中添加配置即可：

```json
{
  "mcpServers": {
    "your-server-name": {
      "command": "node",
      "args": ["path/to/server.js"],
      "env": {
        "API_KEY": "your-api-key"
      }
    }
  }
}
```

---

## 🏗️ 项目结构

```
Cade/
├── Cade/              # 主程序 - 终端 UI、ViewModel、服务编排
│   ├── Services/      # 核心服务 (UI、AI 服务、更新检查等)
│   ├── ViewModels/    # MVVM ViewModel
│   ├── Filters/       # 过滤器和中间件
│   └── Program.cs     # 程序入口 (Generic Host)
│
├── Cade.Provider/     # AI Provider 和 MCP 支持
│   ├── Services/      # Provider 服务 (OpenAI、Azure、Anthropic)
│   ├── Mcp/           # MCP 协议实现
│   ├── Models/        # 配置模型
│   └── settings.json  # 默认配置文件
│
├── Cade.Data/         # 数据持久化
│   ├── Entities/      # 实体 (ChatSession、ChatMessage)
│   ├── Services/      # 数据服务 (FreeSql + SQLite)
│   └── Configuration/ # FreeSql 配置
│
├── Cade.Tool/         # 内置工具插件
│   └── Plugins/       # 文件系统、系统操作等插件
│
└── README.md          # 本文件
```

### 用户数据目录

```
~/.cade/
├── settings.json      # 配置文件 (API Key、模型等)
├── cade.md            # 自定义系统提示词
├── data/
│   └── cade.db        # SQLite 数据库 (对话缓存)
└── logs/
    └── Cade.log       # 日志文件
```

---

## 🧰 技术栈

- **运行时**: [.NET 10](https://dotnet.microsoft.com/) / C# 13
- **AI 编排**: [Microsoft Semantic Kernel 1.68.0](https://github.com/microsoft/semantic-kernel)
- **终端 UI**: [Spectre.Console](https://spectreconsole.net/)
- **MVVM 框架**: [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)
- **依赖注入**: [Microsoft.Extensions.Hosting](https://learn.microsoft.com/dotnet/core/extensions/generic-host)
- **数据持久化**: [FreeSql](https://freesql.net/) + SQLite
- **日志**: [Serilog](https://serilog.net/)
- **MCP 协议**: [ModelContextProtocol](https://modelcontextprotocol.io/)

---

## 📝 开发指南

### 前置要求

- .NET 10 SDK 或更高版本
- （可选）Node.js 18+ （用于运行 MCP Servers）

### 调试运行

```bash
# 清理构建
dotnet clean

# 还原依赖
dotnet restore

# 编译
dotnet build

# 运行（带日志输出）
dotnet run --project Cade
```

### 添加新的工具插件

1. 在 `Cade.Tool/Plugins/` 目录下创建新的插件类
2. 使用 `[KernelFunction]` 特性标记工具方法
3. 在 `Program.cs` 中注册插件

**示例**：

```csharp
public class MyCustomPlugin
{
    [KernelFunction]
    [Description("自定义工具的描述")]
    public async Task<string> MyCustomTool(
        [Description("参数说明")] string parameter)
    {
        // 工具逻辑
        return "结果";
    }
}
```

```csharp
// 在 Program.cs 中注册
builder.Services.AddSingleton<MyCustomPlugin>();
```

---

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

### 贡献流程

1. Fork 本仓库
2. 创建你的特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交你的修改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 开启一个 Pull Request

---

## 📄 许可证

本项目采用 MIT 许可证。详见 [LICENSE.txt](LICENSE.txt) 文件。

---

## 🙏 致谢

- [Microsoft Semantic Kernel](https://github.com/microsoft/semantic-kernel) - 强大的 AI 编排框架
- [Spectre.Console](https://spectreconsole.net/) - 优雅的终端 UI 库
- [Model Context Protocol](https://modelcontextprotocol.io/) - 统一的工具协议标准

---

## 📧 联系方式

- 作者: hopesy
- 项目链接: [https://github.com/hopesy/Cade](https://github.com/hopesy/Cade)
- 问题反馈: [GitHub Issues](https://github.com/hopesy/Cade/issues)

---

<div align="center">

**如果觉得这个项目有帮助，请给一个 ⭐ Star！**

Made with ❤️ by hopesy

</div>
