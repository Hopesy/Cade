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
        // 工具调用由 ToolCallFilter 处理，不再订阅事件
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
                        // 同步设置处理状态，确保用户消息已渲染后再清除底部区域
                        _ui.SetProcessing(true, "正在思考...");
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
            // SetProcessing 已在主循环中同步调用，这里不再重复调用
            _viewModel.CurrentInput = input;

            // 等待 AI 回复（此时思考动画在底部运行）
            await _viewModel.SubmitCommand.ExecuteAsync(null);

            // 收到回复后，停止思考动画
            _ui.SetProcessing(false);

            // 提取回复的第一句话作为总结
            var summary = ExtractSummary(_viewModel.LastResponse);
            _ui.ShowResponseHeader(summary);

            // 显示完整的 AI 回复（会自动停止头部动画）
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

    private string ExtractSummary(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return "AI 回复";

        // 移除 Markdown 标记
        var text = response.Trim();

        // 移除代码块
        text = System.Text.RegularExpressions.Regex.Replace(text, @"```.*?```", "", System.Text.RegularExpressions.RegexOptions.Singleline);

        // 移除行内代码
        text = System.Text.RegularExpressions.Regex.Replace(text, @"`[^`]+`", "");

        // 移除标题标记
        text = System.Text.RegularExpressions.Regex.Replace(text, @"^#+\s*", "", System.Text.RegularExpressions.RegexOptions.Multiline);

        // 移除粗体和斜体
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\*\*([^\*]+)\*\*", "$1");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\*([^\*]+)\*", "$1");

        // 获取第一句话（以句号、问号、感叹号结尾）
        var firstSentence = System.Text.RegularExpressions.Regex.Match(text, @"^[^。！？\n]+[。！？]?");
        var summary = firstSentence.Success ? firstSentence.Value.Trim() : text.Split('\n')[0].Trim();

        // 限制长度
        if (summary.Length > 60)
        {
            summary = summary.Substring(0, 57) + "...";
        }

        return string.IsNullOrWhiteSpace(summary) ? "AI 回复" : summary;
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
