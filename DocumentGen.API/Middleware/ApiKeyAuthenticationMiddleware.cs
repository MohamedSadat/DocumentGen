using DocumentGen.API.Services;

namespace DocumentGen.API.Middleware
{
    public class ApiKeyAuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IApiKeyService _apiKeyService;

        public ApiKeyAuthenticationMiddleware(
            RequestDelegate next,
            IApiKeyService apiKeyService)
        {
            _next = next;
            _apiKeyService = apiKeyService;
        }
        public async Task InvokeAsync(HttpContext context)
        {
            // Skip auth for Swagger
            if (context.Request.Path.StartsWithSegments("/swagger"))
            {
                await _next(context);
                return;
            }

            // Extract API key
            string? apiKey = null;

            // Check header first
            if (context.Request.Headers.TryGetValue("X-API-Key", out var headerKey))
            {
                apiKey = headerKey.FirstOrDefault();
            }
            // Check query string
            else if (context.Request.Query.TryGetValue("apiKey", out var queryKey))
            {
                apiKey = queryKey.FirstOrDefault();
            }

            // Validate API key
            if (string.IsNullOrEmpty(apiKey) || !await _apiKeyService.IsValidAsync(apiKey))
            {
                // Allow anonymous access with limits
                apiKey = "anonymous";
            }

            // Store in context
            context.Items["ApiKey"] = apiKey;

            await _next(context);
        }

    }
}
