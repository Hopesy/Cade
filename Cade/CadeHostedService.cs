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
    private readonly IAiService _aiService;

    public CadeHostedService(
        IUserInterface ui,
        MainViewModel viewModel,
        IHostApplicationLifetime appLifetime,
        IProviderConfigService configService,
        IProviderService providerService,
        IConfiguration configuration,
        IAiService aiService)
    {
        _ui = ui;
        _viewModel = viewModel;
        _appLifetime = appLifetime;
        _configService = configService;
        _providerService = providerService;
        _configuration = configuration;
        _aiService = aiService;

        // 订阅工具调用事件
        _aiService.ToolCalled += OnToolCalled;
    }

    private void OnToolCalled(object? sender, ToolCallEventArgs e)
    {
        // 在 UI 中显示工具调用日志
        var parameters = string.IsNullOrWhiteSpace(e.Parameters) || e.Parameters == "{}"
            ? ""
            : $" {e.Parameters}";

        _ui.ShowToolLog(e.ToolName, parameters, e.Output);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 初始化 Provider 和模型
        try 
        {
            await _configService.EnsureDefaultConfigExistsAsync();
            
            var currentConfigName = _configuration["AppSettings:CurrentProviderConfig"] ?? "default";
            await _configService.SetCurrentConfigFileNameAsync(currentConfigName);
            
            var config = await _configService.LoadConfigurationAsync(currentConfigName);
            
            if (config != null)
            {
                await _providerService.LoadModelsFromConfigAsync(config, currentConfigName);
            }
            else
            {
                 config = await _configService.LoadConfigurationAsync("default");
                 if (config != null)
                    await _providerService.LoadModelsFromConfigAsync(config, "default");
            }

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
                }
            }
            else
            {
                _viewModel.CurrentModelId = "No Models Available";
            }
            
            _viewModel.Theme = _configuration["AppSettings:Theme"] ?? "Dark";

        }
        catch(Exception ex)
        {
            _ui.ShowError($"初始化AI服务失败: {ex.Message}");
        }

        _ui.ShowWelcome();

        // 主交互循环
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_ui.KeyAvailable)
            {
                var key = Console.ReadKey(true);
                var input = _ui.HandleKeyPress(key);

                if (input != null)
                {
                    if (input.Trim().StartsWith("/"))
                    {
                        await HandleCommandAsync(input);
                    }
                    else
                    {
                        // 后台处理 AI 请求
                        _ = Task.Run(() => ProcessAiInputAsync(input));
                    }
                }
            }
            
            // Sync Status
            _viewModel.CurrentPath = Environment.CurrentDirectory;
            _ui.SetStatus(_viewModel.CurrentPath, _viewModel.CurrentModelId);
            
            _ui.Update();
            await Task.Delay(10, stoppingToken);
        }
    }

    private async Task ProcessAiInputAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return;

        try
        {
            // 先显示"正在回复中"提示（在历史区域）
            _ui.ShowLog("⏳ 正在处理您的请求...");

            // 底部状态栏显示处理状态
            _ui.SetProcessing(true, "正在思考...");

            _viewModel.CurrentInput = input;

            await _viewModel.SubmitCommand.ExecuteAsync(null);

            // 显示 AI 回复
            _ui.ShowResponse(_viewModel.LastResponse);
        }
        catch (Exception ex)
        {
            _ui.ShowError(ex.Message);
        }
        finally
        {
            _ui.SetProcessing(false);
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

        string selectedModel = null!;

        // 使用 SafeRender 包裹交互式 Prompt，确保输入行被正确清除和恢复
        _ui.SafeRender(() => 
        {
            selectedModel = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("请选择模型:")
                    .PageSize(10)
                    .AddChoices(models.Select(m => m.Id)));
        });

        if (selectedModel != null)
        {
            _viewModel.CurrentModelId = selectedModel;
            await UpdateAppSettingsAsync("CurrentModelId", selectedModel);
            _ui.ShowResponse($"已切换模型为: [bold green]{selectedModel}[/]");
        }
    }

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
