using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Cade.Interfaces;
using Cade.ViewModels;
using Cade.Provider.Services.Interfaces;
using Spectre.Console;
using Newtonsoft.Json.Linq;

namespace Cade;

public class CadeHostedService : BackgroundService
{
    private readonly IUserInterface _ui;
    private readonly MainViewModel _viewModel;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly IProviderConfigService _configService;
    private readonly IProviderService _providerService;
    private readonly IConfiguration _configuration;

    public CadeHostedService(
        IUserInterface ui, 
        MainViewModel viewModel,
        IHostApplicationLifetime appLifetime,
        IProviderConfigService configService,
        IProviderService providerService,
        IConfiguration configuration)
    {
        _ui = ui;
        _viewModel = viewModel;
        _appLifetime = appLifetime;
        _configService = configService;
        _providerService = providerService;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 初始化 Provider 和模型
        try 
        {
            // 1. 确保默认配置存在（同步 settings.json）
            await _configService.EnsureDefaultConfigExistsAsync();

            // 2. 获取并加载配置
            var currentConfigName = _configuration["AppSettings:CurrentProviderConfig"] ?? "default";
            // 如果 configService 之前加载过 default，这里可能需要强制重新加载以确保同步
            await _configService.SetCurrentConfigFileNameAsync(currentConfigName); // 确保 service 状态正确
            
            var config = await _configService.LoadConfigurationAsync(currentConfigName);
            
            if (config != null)
            {
                await _providerService.LoadModelsFromConfigAsync(config, currentConfigName);
            }
            else
            {
                 // Fallback
                 config = await _configService.LoadConfigurationAsync("default");
                 if (config != null)
                    await _providerService.LoadModelsFromConfigAsync(config, "default");
            }

            // 3. 设置当前模型
            var savedModelId = _configuration["AppSettings:CurrentModelId"];
            var availableModels = _providerService.GetAvailableModels();
            
            if (availableModels.Any())
            {
                if (!string.IsNullOrEmpty(savedModelId) && availableModels.Any(m => m.Id == savedModelId))
                {
                    _viewModel.CurrentModelId = savedModelId;
                }
                else
                {
                    _viewModel.CurrentModelId = availableModels.First().Id;
                    // 可选：更新回配置文件？暂时不强制，等待用户操作
                }
            }
            else
            {
                _viewModel.CurrentModelId = "No Models Available";
            }
            
            // 4. 设置主题 (暂未用到 ViewModel 中的 Theme 做逻辑，仅存储)
            _viewModel.Theme = _configuration["AppSettings:Theme"] ?? "Dark";

        }
        catch(Exception ex)
        {
            _ui.ShowError($"初始化AI服务失败: {ex.Message}");
        }

        // 1. 显示欢迎界面
        _ui.ShowWelcome();

        // 2. 进入主交互循环
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 渲染状态栏 (Path | Model)
                _viewModel.CurrentPath = Environment.CurrentDirectory; // 实时更新路径
                // _ui.RenderStatusBar(_viewModel.CurrentPath, _viewModel.CurrentModelId); // 已移除，集成到 GetInput 中

                // 从 UI 获取输入
                string input = _ui.GetInput("User", _viewModel.CurrentPath, _viewModel.CurrentModelId);

                if (string.IsNullOrWhiteSpace(input)) continue;

                // 处理命令
                if (input.Trim().StartsWith("/"))
                {
                    await HandleCommandAsync(input);
                    continue;
                }
                
                // 处理退出命令 (兼容旧习惯)
                if (input.Trim().ToLower() is "exit" or "quit")
                {
                    _ui.ShowResponse("[grey]正在关闭系统...再见。[/]");
                    _appLifetime.StopApplication();
                    break;
                }

                // 更新 ViewModel
                _viewModel.CurrentInput = input;

                // 执行提交命令，并带上 UI 加载状态
                await _ui.ShowThinkingAsync(async () => 
                {
                    await _viewModel.SubmitCommand.ExecuteAsync(null);
                });

                // 显示结果
                _ui.ShowResponse(_viewModel.LastResponse);
            }
            catch (Exception ex)
            {
                _ui.ShowError(ex.Message);
            }
        }
    }

    private async Task HandleCommandAsync(string input)
    {
        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLower();

        switch (command)
        {
            case "/exit":
            case "/quit":
                _ui.ShowResponse("[grey]正在关闭系统...再见。[/]");
                _appLifetime.StopApplication();
                break;

            case "/model":
                await HandleModelCommand();
                break;

            case "/help":
                _ui.ShowResponse("""
                [bold]可用命令 (Available Commands):[/]
                
                * [green]/model[/]: 切换 AI 模型 (Switch AI Model)
                * [green]/exit[/] 或 [green]/quit[/]: 退出程序 (Exit)
                * [green]/help[/]: 显示此帮助信息 (Show Help)
                """);
                break;

            default:
                _ui.ShowError($"未知命令: {command}");
                break;
        }
    }

    private async Task HandleModelCommand()
    {
        var models = _providerService.GetAvailableModels();
        if (!models.Any())
        {
            _ui.ShowError("当前没有可用的模型。请检查配置。");
            return;
        }

        // 使用 Spectre.Console 选择 Prompt
        var selectedModel = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("请选择模型:")
                .PageSize(10)
                .AddChoices(models.Select(m => m.Id)));

        _viewModel.CurrentModelId = selectedModel;
        
        // 更新 appsettings.json
        await UpdateAppSettingsAsync("CurrentModelId", selectedModel);
        
        _ui.ShowResponse($"已切换模型为: [bold green]{selectedModel}[/]");
    }

    // 简单的 appsettings.json 更新帮助方法 (注意：这里简单实现，生产环境建议用专门的 Service)
    private async Task UpdateAppSettingsAsync(string key, string value)
    {
        try 
        {
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (File.Exists(filePath))
            {
                var json = await File.ReadAllTextAsync(filePath);
                var jObject = JObject.Parse(json);
                
                if (jObject["AppSettings"] == null) jObject["AppSettings"] = new JObject();
                jObject["AppSettings"]![key] = value;
                
                await File.WriteAllTextAsync(filePath, jObject.ToString());
            }
        }
        catch (Exception ex)
        {
            _ui.ShowError($"保存配置失败: {ex.Message}");
        }
    }
}
