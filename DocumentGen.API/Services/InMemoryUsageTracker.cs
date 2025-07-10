namespace DocumentGen.API.Services
{
    public interface IUsageTracker
    {
        Task<bool> CanGenerateAsync(string apiKey);
        Task TrackUsageAsync(string apiKey, int count);
        Task<int> GetUsageAsync(string apiKey);
    }
    public class InMemoryUsageTracker : IUsageTracker
    {
        private readonly Dictionary<string, List<DateTime>> _usage = new();
        private readonly IApiKeyService _apiKeyService;

        private readonly Dictionary<string, int> _planLimits = new()
        {
            ["free"] = 100,
            ["starter"] = 1000,
            ["growth"] = 10000,
            ["scale"] = 50000
        };

        public InMemoryUsageTracker(IApiKeyService apiKeyService)
        {
            _apiKeyService = apiKeyService;
        }

        public async Task<bool> CanGenerateAsync(string apiKey)
        {
            var usage = await GetUsageAsync(apiKey);
            var plan = await _apiKeyService.GetPlanAsync(apiKey);
            var limit = _planLimits.GetValueOrDefault(plan, 100);

            return usage < limit;
        }

        public Task TrackUsageAsync(string apiKey, int count)
        {
            if (!_usage.ContainsKey(apiKey))
            {
                _usage[apiKey] = new List<DateTime>();
            }

            for (int i = 0; i < count; i++)
            {
                _usage[apiKey].Add(DateTime.UtcNow);
            }

            return Task.CompletedTask;
        }

        public Task<int> GetUsageAsync(string apiKey)
        {
            if (!_usage.ContainsKey(apiKey))
            {
                return Task.FromResult(0);
            }

            // Count usage in current month
            var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var count = _usage[apiKey].Count(d => d >= startOfMonth);

            return Task.FromResult(count);
        }
    }
}
