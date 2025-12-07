using Cade.Provider.Services.Interfaces;
using Cade.Services.Interfaces;
using Cade.ViewModels;
using Cade.Data.Entities;
using Cade.Data.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using Spectre.Console;

namespace Cade.Services;

public class CadeHostedService : BackgroundService
{
    private readonly IUserInterface _ui;
    private readonly MainViewModel _viewModel;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly IProviderConfigService _configService;
    private readonly IProviderService _providerService;
    private readonly IConfiguration _configuration;
    private readonly IAiService _aiService;
    private readonly IChatDataService _chatDataService;
    
    // 当前会话
    private ChatSession? _currentSession;
    
    // 用于取消当前 AI 任务
    private CancellationTokenSource? _currentTaskCts;
    private readonly object _ctsLock = new();

    public CadeHostedService(
        IUserInterface ui,
        MainViewModel viewModel,
        IHostApplicationLifetime appLifetime,
        IProviderConfigService configService,
        IProviderService providerService,
        IConfiguration configuration,
        IAiService aiService,
        IChatDataService chatDataService)
    {
        _ui = ui;
        _viewModel = viewModel;
        _appLifetime = appLifetime;
        _configService = configService;
        _providerService = providerService;
        _configuration = configuration;
        _aiService = aiService;
        _chatDataService = chatDataService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 初始化 Provider 和模型
        bool configMissing = false;
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
                configMissing = true;
            }

            var savedModelId = _configuration["AppSettings:CurrentModelId"];
            var availableModels = _providerService.GetAvailableModels();
            
            if (availableModels.Any())
            {
                // 提取保存的模型名称（去掉前缀）
                var savedModelName = savedModelId;
                if (!string.IsNullOrEmpty(savedModelId))
                {
                    var underscoreIndex = savedModelId.IndexOf('_');
                    if (underscoreIndex >= 0)
                        savedModelName = savedModelId.Substring(underscoreIndex + 1);
                }
                
                // 尝试匹配模型名称
                var matchedModel = availableModels.FirstOrDefault(m =>
                {
                    var modelName = m.Id;
                    var idx = m.Id.IndexOf('_');
                    if (idx >= 0)
                        modelName = m.Id.Substring(idx + 1);
                    return modelName == savedModelName;
                });
                
                _viewModel.CurrentModelId = matchedModel?.Id ?? availableModels.First().Id;
            }
            else
            {
                // 没有可用模型，提示用户配置
                configMissing = true;
                _viewModel.CurrentModelId = "⚠ 未配置";
            }
            
            _viewModel.Theme = _configuration["AppSettings:Theme"] ?? "Dark";
            
            // 加载思维链显示设置
            var showReasoningStr = _configuration["AppSettings:ShowReasoning"];
            _viewModel.ShowReasoning = bool.TryParse(showReasoningStr, out var showReasoning) && showReasoning;

        }
        catch(Exception ex)
        {
            configMissing = true;
            _ui.ShowError($"初始化AI服务失败: {ex.Message}");
        }

        // 设置工具调用回调，保存到数据库
        _aiService.SetToolCallCallback(async (toolName, formattedContent) =>
        {
            if (_currentSession != null)
            {
                await _chatDataService.AddMessageAsync(
                    _currentSession.Id, 
                    Data.Entities.MessageType.ToolCall, 
                    formattedContent, 
                    toolName: toolName);
            }
        });

        _ui.ShowWelcome();
        
        // 如果配置缺失，显示配置提示
        if (configMissing)
        {
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".cade", "settings.json");
            
            _ui.ShowResponse(
                "[yellow]⚠ 未检测到有效的模型配置[/]\n\n" +
                $"请编辑配置文件: [cyan]{configPath}[/]\n\n" +
                "配置示例:\n" +
                "[dim]{\n" +
                "  \"env\": {\n" +
                "    \"CADE_AUTH_TOKEN\": \"your-api-key\",\n" +
                "    \"CADE_BASE_URL\": \"https://api.openai.com\",\n" +
                "    \"CADE_DEFAULT_MODEL\": \"gpt-4o\",\n" +
                "    \"CADE_PROVIDE_TYPE\": \"OpenAICompatible\"\n" +
                "  }\n" +
                "}[/]\n\n" +
                "配置完成后重启程序即可使用。");
        }

        // 主交互循环
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_ui.KeyAvailable)
            {
                var key = Console.ReadKey(true);
                
                // ESC 键取消当前任务
                if (key.Key == ConsoleKey.Escape)
                {
                    CancelCurrentTask();
                    continue;
                }
                
                // Tab 键切换思考模式（仅在输入为空时）
                if (key.Key == ConsoleKey.Tab && string.IsNullOrEmpty(_ui.GetCurrentInput()))
                {
                    await HandleThinkCommand();
                    continue;
                }
                
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
                        // 后台处理 AI 请求（带取消支持）
                        _ = Task.Run(() => ProcessAiInputAsync(input));
                    }
                }
            }
            
            // Sync Status
            _viewModel.CurrentPath = Environment.CurrentDirectory;
            _ui.SetStatus(_viewModel.CurrentPath, _viewModel.CurrentModelId, _viewModel.ShowReasoning);
            
            _ui.Update();
            await Task.Delay(10, stoppingToken);
        }
    }
    
    private void CancelCurrentTask()
    {
        lock (_ctsLock)
        {
            if (_currentTaskCts != null && !_currentTaskCts.IsCancellationRequested)
            {
                _currentTaskCts.Cancel();
                _ui.SetProcessing(false);
                _ui.ShowResponse("[yellow]任务已取消[/]", "⚠️ 已取消");
            }
        }
    }

    private async Task ProcessAiInputAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return;

        // 创建新的取消令牌
        CancellationTokenSource cts;
        lock (_ctsLock)
        {
            _currentTaskCts?.Dispose();
            _currentTaskCts = new CancellationTokenSource();
            cts = _currentTaskCts;
        }

        try
        {
            // 确保有当前会话
            await EnsureSessionAsync();

            // SetProcessing 已在主循环中同步调用，这里不再重复调用
            _viewModel.CurrentInput = input;

            // 立即保存用户消息到数据库
            if (_currentSession != null)
            {
                await _chatDataService.AddMessageAsync(_currentSession.Id, Data.Entities.MessageType.User, input);
            }

            // 等待 AI 回复（此时思考动画在底部运行），传入取消令牌
            await _viewModel.SubmitCommandWithCancellation(cts.Token);

            // 检查是否被取消
            if (cts.Token.IsCancellationRequested)
                return;

            // 保存思维链内容到数据库
            if (_currentSession != null && !string.IsNullOrEmpty(_viewModel.LastReasoningContent))
            {
                await _chatDataService.AddMessageAsync(_currentSession.Id, Data.Entities.MessageType.Reasoning, _viewModel.LastReasoningContent, _viewModel.CurrentModelId);
            }

            // 保存 AI 回复到数据库
            if (_currentSession != null && !string.IsNullOrEmpty(_viewModel.LastResponse))
            {
                await _chatDataService.AddMessageAsync(_currentSession.Id, Data.Entities.MessageType.Assistant, _viewModel.LastResponse, _viewModel.CurrentModelId);
            }

            // 收到回复后，停止思考动画
            _ui.SetProcessing(false);

            // 如果开启了思维链显示且有思维链内容，先显示思维链
            if (_viewModel.ShowReasoning && !string.IsNullOrEmpty(_viewModel.LastReasoningContent))
            {
                _ui.ShowReasoning(_viewModel.LastReasoningContent);
            }

            // 提取回复的第一句话作为总结，显示完整回复（跳过第一行避免重复）
            var summary = ExtractSummary(_viewModel.LastResponse);
            var responseBody = RemoveFirstLine(_viewModel.LastResponse);
            _ui.ShowResponse(responseBody, summary);
        }
        catch (OperationCanceledException)
        {
            // 任务被取消，用户消息已保存
        }
        catch (Exception ex)
        {
            if (!cts.Token.IsCancellationRequested)
                _ui.ShowError(ex.Message);
        }
        finally
        {
            _ui.SetProcessing(false);
            lock (_ctsLock)
            {
                if (_currentTaskCts == cts)
                    _currentTaskCts = null;
            }
        }
    }

    private string ExtractSummary(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return "AI 回复";

        // 获取第一行
        var firstLine = response.Split('\n')[0].Trim();
        
        // 移除 Markdown 标记
        firstLine = System.Text.RegularExpressions.Regex.Replace(firstLine, @"^#+\s*", "");
        firstLine = System.Text.RegularExpressions.Regex.Replace(firstLine, @"\*\*([^\*]+)\*\*", "$1");
        firstLine = System.Text.RegularExpressions.Regex.Replace(firstLine, @"\*([^\*]+)\*", "$1");
        firstLine = System.Text.RegularExpressions.Regex.Replace(firstLine, @"`[^`]+`", "");

        // 限制长度
        if (firstLine.Length > 60)
        {
            firstLine = firstLine.Substring(0, 57) + "...";
        }

        return string.IsNullOrWhiteSpace(firstLine) ? "AI 回复" : firstLine;
    }

    private string RemoveFirstLine(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return content;

        var lines = content.Split('\n');
        if (lines.Length <= 1)
            return string.Empty;

        // 跳过第一行，返回剩余内容
        return string.Join('\n', lines.Skip(1)).TrimStart('\r', '\n');
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

            case "/think":
                await HandleThinkCommand();
                break;

            case "/continue":
                await HandleContinueCommand();
                break;

            case "/clear":
                await HandleClearCommand();
                break;

            case "/help":
                _ui.ShowResponse(
                    "[bold]可用命令 (Available Commands):[/]\n\n" +
                    "* [green]/model[/]: 切换 AI 模型 (Switch AI Model)\n" +
                    "* [green]/think[/]: 切换思考模式 (Toggle Think Mode) - Tab 快捷键\n" +
                    "* [green]/continue[/]: 恢复上次对话 (Continue Last Session)\n" +
                    "* [green]/clear[/]: 清空对话历史 (Clear History)\n" +
                    "* [green]/exit[/] 或 [green]/quit[/]: 退出程序 (Exit)\n" +
                    "* [green]/help[/]: 显示此帮助信息 (Show Help)");
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

        // 构建选项列表，显示模型名称
        var options = models.Select(m =>
        {
            // 模型ID格式: uuid_modelname，只显示下划线后面的部分
            var displayName = m.Id;
            var underscoreIndex = m.Id.IndexOf('_');
            if (underscoreIndex >= 0)
                displayName = m.Id.Substring(underscoreIndex + 1);
            
            var isCurrentModel = m.Id == _viewModel.CurrentModelId;
            var display = isCurrentModel ? $"{displayName} ✓" : displayName;
            return (Display: display, Value: m.Id);
        });

        var selectedModel = _ui.ShowSelectionMenu(
            "选择模型",
            "切换当前会话使用的 AI 模型",
            options);

        if (selectedModel != null)
        {
            _viewModel.CurrentModelId = selectedModel;
            await UpdateAppSettingsAsync("CurrentModelId", selectedModel);
            
            // 立即更新状态栏显示
            _ui.SetStatus(_viewModel.CurrentPath, _viewModel.CurrentModelId);
            
            // 显示模型名称（去掉 uuid 前缀）
            var displayName = selectedModel;
            var underscoreIndex = selectedModel.IndexOf('_');
            if (underscoreIndex >= 0)
                displayName = selectedModel.Substring(underscoreIndex + 1);
            
            _ui.ShowResponse($"已切换模型为: [bold green]{displayName}[/]");
        }
    }

    private async Task HandleThinkCommand()
    {
        _viewModel.ShowReasoning = !_viewModel.ShowReasoning;
        await UpdateAppSettingsAsync("ShowReasoning", _viewModel.ShowReasoning.ToString().ToLower());
        
        // 直接更新状态栏，不输出消息
        _ui.SetStatus(_viewModel.CurrentPath, _viewModel.CurrentModelId, _viewModel.ShowReasoning);
    }

    private async Task HandleContinueCommand()
    {
        try
        {
            var workDir = Environment.CurrentDirectory;
            var session = await _chatDataService.GetSessionByWorkDirectoryAsync(workDir);

            if (session == null || session.Messages.Count == 0)
            {
                _ui.ShowResponse("[yellow]当前目录没有历史对话记录[/]");
                return;
            }

            // 恢复会话到 AI 服务（只恢复 User 和 Assistant 消息）
            _currentSession = session;
            var chatMessages = session.Messages
                .Where(m => m.Type == Data.Entities.MessageType.User || m.Type == Data.Entities.MessageType.Assistant)
                .ToList();
            _aiService.RestoreHistory(chatMessages);

            // 渲染历史消息到界面
            _ui.ShowResponse($"[green]已恢复 {session.Messages.Count} 条对话记录[/]\n");
            
            foreach (var msg in session.Messages.OrderBy(m => m.SequenceNumber))
            {
                switch (msg.Type)
                {
                    case Data.Entities.MessageType.User:
                        _ui.RenderUserMessage(msg.Content);
                        break;
                    
                    case Data.Entities.MessageType.ToolCall:
                        _ui.RenderToolCall(msg.Content);
                        break;
                    
                    case Data.Entities.MessageType.Reasoning:
                        _ui.RenderReasoning(msg.Content);
                        break;
                    
                    case Data.Entities.MessageType.Assistant:
                        var summary = ExtractSummary(msg.Content);
                        var body = RemoveFirstLine(msg.Content);
                        _ui.ShowResponse(body, summary);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _ui.ShowError($"恢复对话失败: {ex.Message}");
        }
    }

    private async Task HandleClearCommand()
    {
        try
        {
            if (_currentSession != null)
            {
                await _chatDataService.ClearMessagesAsync(_currentSession.Id);
                _currentSession.Messages.Clear();
            }

            _aiService.ClearHistory();
            _ui.ShowResponse("[green]对话历史已清空[/]");
        }
        catch (Exception ex)
        {
            _ui.ShowError($"清空对话失败: {ex.Message}");
        }
    }

    private async Task EnsureSessionAsync()
    {
        if (_currentSession != null) return;

        var workDir = Environment.CurrentDirectory;
        
        // 尝试获取现有会话
        _currentSession = await _chatDataService.GetSessionByWorkDirectoryAsync(workDir);
        
        // 如果不存在，创建新会话
        if (_currentSession == null)
        {
            var modelName = _viewModel.CurrentModelId;
            var idx = modelName.IndexOf('_');
            if (idx >= 0) modelName = modelName.Substring(idx + 1);
            
            _currentSession = await _chatDataService.CreateSessionAsync(workDir, modelName);
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
