
using DocumentGen.API.Middleware;
using DocumentGen.API.Services;
using DocumentGen.Core.Services;

namespace DocumentGen.API
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            // Add services
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new() { Title = "DocumentGen API", Version = "v1" });
            });

            // Add CORS for Blazor app
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowBlazorApp", policy =>
                {
                    policy.WithOrigins("https://localhost:7058", "http://localhost:7265")
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });
            builder.Services.AddCors(policy => policy.AddPolicy("open", opt => opt.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

            // Register services
            builder.Services.AddSingleton<ITemplateEngine, ScribanTemplateEngine>();
            builder.Services.AddSingleton<IApiKeyService, InMemoryApiKeyService>();
            builder.Services.AddSingleton<IUsageTracker, InMemoryUsageTracker>();
            // Add memory cache
            builder.Services.AddMemoryCache();

            // Add HttpContextAccessor
            builder.Services.AddHttpContextAccessor();

            var app = builder.Build();

            // Pre-download browser
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Pre-downloading browser for PDF generation...");

            try
            {
                var browserFetcher = new PuppeteerSharp.BrowserFetcher();
                await browserFetcher.DownloadAsync();
                logger.LogInformation("Browser downloaded successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to download browser");
            }

            // Configure pipeline
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseCors("open");
            app.UseHttpsRedirection();
            app.UseCors("AllowBlazorApp");

            app.UseAuthorization();

            // Add custom middleware
            app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
           // app.UseMiddleware<RateLimitingMiddleware>();

            app.MapControllers();

            app.Run();
        }
    }
}
