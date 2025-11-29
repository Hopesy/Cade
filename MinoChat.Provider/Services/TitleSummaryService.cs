using Microsoft.Extensions.Logging;
using MinoChat.Provider.Services.Interfaces;
using Microsoft.Extensions.AI;

namespace MinoChat.Provider.Services;

public class TitleSummaryService : ITitleSummaryService
{
    private readonly ILogger<TitleSummaryService> _logger;
    private readonly IProviderService _providerService;

    public TitleSummaryService(ILogger<TitleSummaryService> logger, IProviderService providerService)
    {
        _logger = logger;
        _providerService = providerService;
    }

    public async Task<string> GenerateTitleAsync(string firstUserMessage, string firstAiResponse)
    {
        try
        {
            var models = _providerService.GetAvailableModels();
            if (models.Count == 0) return "新对话";

            var prompt = $@"请为这段对话生成一个精准的标题。

要求：
1. 标题长度：8-12个字（严格控制，不要超过12个字）
2. 聚焦核心：抓住用户的核心问题或主题
3. 简洁明了：直接概括对话内容，避免模糊描述
4. 格式规范：不要使用引号、书名号等标点符号
5. 直接输出：只输出标题文本，不要任何解释或前缀

示例：
- 用户问Python列表操作 → ""Python列表操作""
- 用户问如何学习编程 → ""编程学习方法""
- 用户问数据库优化建议 → ""数据库性能优化""

对话内容：
用户：{firstUserMessage}
AI：{firstAiResponse}

标题：";

            var messages = new List<ChatMessage> { new(ChatRole.User, prompt) };

            var title = await _providerService.SendMessageAsync(models[0].Id, messages, CancellationToken.None);

            if (string.IsNullOrWhiteSpace(title)) return "新对话";

            // 清理标题：去除前后空白、引号和常见前缀
            title = title.Trim()
                .Trim('"').Trim('\'').Trim('`')
                .Trim('「').Trim('」').Trim('『').Trim('』')
                .Trim('《').Trim('》')
                .Replace("标题：", "")
                .Replace("标题:", "")
                .Trim();

            // 限制长度为12个字符（中文字符）
            if (title.Length > 12)
            {
                title = title.Substring(0, 12);
            }

            // 确保至少有2个字符
            if (title.Length < 2)
            {
                return "新对话";
            }

            return title;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成标题失败");
            return "新对话";
        }
    }
}