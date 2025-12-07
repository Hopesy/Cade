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
            LastResponse = await _aiService.GetResponseAsync(CurrentInput, CurrentModelId, cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
