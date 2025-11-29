namespace Cade.Interfaces;

public interface IAiService
{
    Task<string> GetResponseAsync(string input, string modelId);
}
