using Cade.Interfaces;
using Cade.Provider.Services.Interfaces;
using Microsoft.Extensions.AI;

namespace Cade.Services;

public class ProductionAiService : IAiService
{
    private readonly IProviderService _providerService;

    public ProductionAiService(IProviderService providerService)
    {
        _providerService = providerService;
    }

    public async Task<string> GetResponseAsync(string input, string modelId)
    {
        if (string.IsNullOrEmpty(modelId) || modelId == "Loading..." || modelId == "No Models Available")
        {
             // 再次尝试获取第一个可用模型作为 fallback
             var models = _providerService.GetAvailableModels();
             if (models.Any())
             {
                 modelId = models.First().Id;
             }
             else
             {
                 return "没有可用的模型，请检查配置。";
             }
        }
        
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.User, input)
        };

        return await _providerService.SendMessageAsync(modelId, messages);
    }
}
