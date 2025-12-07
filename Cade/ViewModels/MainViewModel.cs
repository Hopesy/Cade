using Cade.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cade.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IAiService _aiService;

    [ObservableProperty]
    private string _currentInput = string.Empty;

    [ObservableProperty]
    private string _lastResponse = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _currentModelId = "Loading...";

    [ObservableProperty]
    private string _currentPath = Environment.CurrentDirectory;

    [ObservableProperty]
    private string _theme = "Dark";

    [ObservableProperty]
    private bool _showReasoning = false;

    [ObservableProperty]
    private string _lastReasoningContent = string.Empty;

    public MainViewModel(IAiService aiService)
    {
        _aiService = aiService;
    }

    [RelayCommand]
    public async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentInput)) return;

        IsBusy = true;
        try
        {
            LastResponse = await _aiService.GetResponseAsync(CurrentInput, CurrentModelId);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 支持取消的提交方法
    /// </summary>
    public async Task SubmitCommandWithCancellation(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(CurrentInput)) return;

        IsBusy = true;
        try
        {
            var response = await _aiService.GetResponseWithReasoningAsync(CurrentInput, CurrentModelId, cancellationToken);
            LastResponse = response.Content;
            LastReasoningContent = response.ReasoningContent ?? string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
