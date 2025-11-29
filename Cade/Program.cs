using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Cade;
using Cade.Interfaces;
using Cade.Services;
using Cade.ViewModels;

// 构建 Host
var builder = Host.CreateApplicationBuilder(args);

// 注册服务 (依赖注入)
// 1. 核心服务
builder.Services.AddSingleton<IUserInterface, ConsoleUserInterface>();
builder.Services.AddSingleton<Cade.Provider.Services.Interfaces.IProviderConfigService, Cade.Provider.Services.ProviderConfigService>();
builder.Services.AddSingleton<Cade.Provider.Services.Interfaces.IProviderService, Cade.Provider.Services.ProviderService>();
builder.Services.AddSingleton<IAiService, ProductionAiService>();

// 2. ViewModel
builder.Services.AddSingleton<MainViewModel>();

// 3. Hosted Service (主程序入口)
builder.Services.AddHostedService<CadeHostedService>();

// 构建并运行
var host = builder.Build();
await host.RunAsync();
