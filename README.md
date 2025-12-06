# Cade

基于 .NET 10 和 Semantic Kernel 的终端 AI 编程助手，支持工具调用和 MCP 协议。

## 功能

- 多模型支持 - 配置多个 AI Provider（OpenAI、Azure、Anthropic 等）
- 自动工具调用 - AI 自动执行文件操作、Shell 命令等
- MCP 协议 - 支持 Model Context Protocol 扩展工具
- Markdown 渲染 - 代码高亮、列表、标题等
- ESC 取消 - 随时按 ESC 终止正在执行的任务

## 内置工具

**文件操作**
- ReadFile / WriteFile / AppendToFile - 读写文件
- ReplaceInFile - 替换文件内容
- CreateDirectory / Delete / Move / Copy - 目录和文件管理
- ListDirectory / SearchFiles / Grep - 搜索和浏览
- GetInfo - 获取文件详细信息

**系统操作**
- ExecuteCommand - 执行 Shell 命令（dotnet, npm, git 等）
- GetSystemInfo / GetTime / GetNetworkInfo - 系统信息

## 快速开始

```bash
# 克隆并编译
git clone <repo-url>
cd Cade
dotnet build

# 运行
dotnet run --project Cade
```

## 配置

首次运行会在 `~/.cade/settings.json` 创建配置文件：

```json
{
  "mcpServers": {},
  "providers": {
    "default": {
      "type": "openai",
      "apiKey": "your-api-key",
      "models": ["gpt-4o"]
    }
  }
}
```

## 使用

```
>> 帮我创建一个 .NET 控制台项目
>> 读取 Program.cs 文件
>> 在当前目录搜索所有 .cs 文件
```

**命令**
- `/model` - 切换模型
- `/help` - 帮助
- `/exit` - 退出

**快捷键**
- `ESC` - 取消当前任务

## 技术栈

- .NET 10 / C# 13
- Microsoft.SemanticKernel - AI 编排
- Spectre.Console - 终端 UI
- CommunityToolkit.Mvvm - MVVM
- Serilog - 日志

## 项目结构

```
Cade/           # 主程序
Cade.Tool/      # 内置插件
Cade.Provider/  # AI Provider 和 MCP 支持
```

## License

MIT
