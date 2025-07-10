namespace DocumentGen.API.Services
{
    public interface IApiKeyService
    {
        Task<bool> IsValidAsync(string apiKey);
        Task<string> GetPlanAsync(string apiKey);
    }
    public class InMemoryApiKeyService: IApiKeyService
    {
        private readonly Dictionary<string, string> _apiKeys = new()
        {
            ["demo-key-123"] = "starter",
            ["test-key-456"] = "growth"
        };
        public Task<bool> IsValidAsync(string apiKey)
        {
            return Task.FromResult(_apiKeys.ContainsKey(apiKey));
        }

        public Task<string> GetPlanAsync(string apiKey)
        {
            if (apiKey == "anonymous") return Task.FromResult("free");
            return Task.FromResult(_apiKeys.GetValueOrDefault(apiKey, "free"));
        }
    }
}
