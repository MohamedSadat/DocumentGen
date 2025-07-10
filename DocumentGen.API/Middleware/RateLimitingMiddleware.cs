using DocumentGen.API.Services;
using System.Collections.Concurrent;

namespace DocumentGen.API.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly IApiKeyService _apiKeyService;

    // Store rate limit data in memory (use Redis in production)
    private readonly ConcurrentDictionary<string, RateLimitInfo> _rateLimitStore = new();

    // Rate limits per plan (requests per minute)
    private readonly Dictionary<string, int> _rateLimits = new()
    {
        ["free"] = 10,      // 10 requests per minute
        ["starter"] = 60,   // 60 requests per minute
        ["growth"] = 300,   // 300 requests per minute
        ["scale"] = 600     // 600 requests per minute
    };

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        IApiKeyService apiKeyService)
    {
        _next = next;
        _logger = logger;
        _apiKeyService = apiKeyService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip rate limiting for non-API endpoints
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        // Get API key from context (set by ApiKeyAuthenticationMiddleware)
        var apiKey = context.Items["ApiKey"]?.ToString() ?? "anonymous";

        // Get rate limit for this API key
        var plan = await _apiKeyService.GetPlanAsync(apiKey);
        var limit = _rateLimits.GetValueOrDefault(plan, 10);

        // Check rate limit
        if (!IsRequestAllowed(apiKey, limit))
        {
            _logger.LogWarning("Rate limit exceeded for API key: {ApiKey}", apiKey);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.Add("X-RateLimit-Limit", limit.ToString());
            context.Response.Headers.Add("X-RateLimit-Remaining", "0");
            context.Response.Headers.Add("X-RateLimit-Reset", GetResetTime().ToString());

            await context.Response.WriteAsJsonAsync(new
            {
                error = "Rate limit exceeded",
                message = $"You have exceeded the rate limit of {limit} requests per minute",
                retryAfter = 60
            });

            return;
        }

        // Add rate limit headers
        var remaining = GetRemainingRequests(apiKey, limit);
        context.Response.Headers.Add("X-RateLimit-Limit", limit.ToString());
        context.Response.Headers.Add("X-RateLimit-Remaining", remaining.ToString());
        context.Response.Headers.Add("X-RateLimit-Reset", GetResetTime().ToString());

        await _next(context);
    }

    private bool IsRequestAllowed(string apiKey, int limit)
    {
        var now = DateTime.UtcNow;

        var rateLimitInfo = _rateLimitStore.AddOrUpdate(apiKey,
            key => new RateLimitInfo
            {
                WindowStart = now,
                RequestCount = 1
            },
            (key, existing) =>
            {
                // If the window has expired, start a new one
                if (now.Subtract(existing.WindowStart).TotalMinutes >= 1)
                {
                    existing.WindowStart = now;
                    existing.RequestCount = 1;
                }
                else
                {
                    existing.RequestCount++;
                }

                return existing;
            });

        return rateLimitInfo.RequestCount <= limit;
    }

    private int GetRemainingRequests(string apiKey, int limit)
    {
        if (!_rateLimitStore.TryGetValue(apiKey, out var info))
        {
            return limit;
        }

        var now = DateTime.UtcNow;
        if (now.Subtract(info.WindowStart).TotalMinutes >= 1)
        {
            return limit;
        }

        return Math.Max(0, limit - info.RequestCount);
    }

    private long GetResetTime()
    {
        var now = DateTime.UtcNow;
        var resetTime = now.AddMinutes(1 - now.TimeOfDay.TotalMinutes % 1);
        return ((DateTimeOffset)resetTime).ToUnixTimeSeconds();
    }

    private class RateLimitInfo
    {
        public DateTime WindowStart { get; set; }
        public int RequestCount { get; set; }
    }
}