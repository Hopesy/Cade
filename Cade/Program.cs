using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Cade;
using Cade.Interfaces;
using Cade.Services;
using Cade.ViewModels;
using Cade.Tool.Plugins;
using Cade.Provider.Mcp;
using Cade.Provider.Models;
using Cade.Provider.Services;
using Cade.Provider.Services.Interfaces;

// 构建 Host
var builder = Host.CreateApplicationBuilder(args);

// 配置 Serilog 日志
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(new LoggerConfiguration()
    .WriteTo.File("Logs/Cade.log", rollingInterval: RollingInterval.Day)
    .CreateLogger());

// 加载 settings.json 配置 (MCP 配置)
var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
if (File.Exists(settingsPath))
{
    builder.Configuration.AddJsonFile(settingsPath, optional: true, reloadOnChange: true);
}

// 绑定 MCP 配置 (从 settings.json)
builder.Services.Configure<McpSettings>(builder.Configuration);

// 注册服务 (依赖注入)
// 1. 核心服务
builder.Services.AddSingleton<IUserInterface, ConsoleUserInterface>();
builder.Services.AddSingleton<IProviderConfigService, ProviderConfigService>();
builder.Services.AddSingleton<IProviderService, ProviderService>();

// 2. MCP 桥接器
builder.Services.AddSingleton<McpBridge>();

// 3. 内置插件
builder.Services.AddSingleton<FileSystemPlugin>();
builder.Services.AddSingleton<SystemPlugin>();

// 4. AI 服务
builder.Services.AddSingleton<IAiService, ProductionAiService>();

// 5. ViewModel
builder.Services.AddSingleton<MainViewModel>();

// 6. MCP 加载服务 (启动时连接 MCP Servers)
builder.Services.AddHostedService<McpLoaderService>();

// 7. Hosted Service (主程序入口)
builder.Services.AddHostedService<CadeHostedService>();

// 构建并运行
var host = builder.Build();
await host.RunAsync();
