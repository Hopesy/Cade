using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Cade;
using Cade.Interfaces;
using Cade.Services;
using Cade.ViewModels;
using Cade.Tool;

// 构建 Host
var builder = Host.CreateApplicationBuilder(args);

// 清除所有默认日志提供程序，防止日志输出到控制台
builder.Logging.ClearProviders();

// 注册服务 (依赖注入)
// 1. 核心服务
builder.Services.AddSingleton<IUserInterface, ConsoleUserInterface>();
builder.Services.AddSingleton<Cade.Provider.Services.Interfaces.IProviderConfigService, Cade.Provider.Services.ProviderConfigService>();
builder.Services.AddSingleton<Cade.Provider.Services.Interfaces.IProviderService, Cade.Provider.Services.ProviderService>();

// 2. 工具管理器
builder.Services.AddSingleton<ToolManager>(sp =>
{
    var manager = new ToolManager();
    manager.RegisterDefaultTools(); // 注册所有默认工具
    return manager;
});

// 3. AI 服务
builder.Services.AddSingleton<IAiService, ProductionAiService>(); // <-- 使用 ProductionAiService

// 4. ViewModel
builder.Services.AddSingleton<MainViewModel>();

// 5. Hosted Service (主程序入口)
builder.Services.AddHostedService<CadeHostedService>();

// 构建并运行
var host = builder.Build();
await host.RunAsync();
