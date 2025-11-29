namespace MinoChat.Provider.Services.Interfaces;

public interface ITitleSummaryService
{
    Task<string> GenerateTitleAsync(string firstUserMessage, string firstAiResponse);
}