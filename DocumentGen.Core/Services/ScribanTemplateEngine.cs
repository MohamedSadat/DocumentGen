using DocumentGen.Shared.Models;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using Scriban;
using Scriban.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentGen.Core.Services
{
    public class ScribanTemplateEngine : ITemplateEngine
    {
        private readonly ILogger<ScribanTemplateEngine> _logger;
        private static bool _browserDownloaded = false;
        private static IBrowser? _browser;
        private static readonly SemaphoreSlim _browserLock = new(1, 1);

        public ScribanTemplateEngine(ILogger<ScribanTemplateEngine> logger)
        {
            _logger = logger;
          //  EnsureBrowserDownloaded().GetAwaiter().GetResult();
        }
        private async Task<IBrowser> GetBrowserAsync()
        {
            if (_browser == null || !_browser.IsConnected)
            {
                await _browserLock.WaitAsync();
                try
                {
                    if (_browser == null || !_browser.IsConnected)
                    {
                        _logger.LogInformation("Launching browser...");

                        // Download browser if needed
                        var browserFetcher = new BrowserFetcher();
                        var installedBrowser = await browserFetcher.DownloadAsync();
                        _logger.LogInformation($"Browser downloaded to: {installedBrowser}");

                        // Launch with optimized settings
                        _browser = await Puppeteer.LaunchAsync(new LaunchOptions
                        {
                            Headless = true,
                            Args = new[] {
                            "--no-sandbox",
                            "--disable-setuid-sandbox",
                            "--disable-dev-shm-usage",
                            "--disable-gpu",
                            "--no-first-run",
                            "--no-zygote",
                            "--single-process",
                            "--disable-extensions"
                        },
                            Timeout = 30000 // 30 seconds timeout for launch
                        });

                        _logger.LogInformation("Browser launched successfully");
                    }
                }
                finally
                {
                    _browserLock.Release();
                }
            }

            return _browser!;
        }

        private async Task EnsureBrowserDownloaded()
        {
            if (!_browserDownloaded)
            {
                await new BrowserFetcher().DownloadAsync();
                _browserDownloaded = true;
            }
        }

        public async Task<byte[]> GenerateDocumentAsync(
            string template,
            object data,
            OutputFormat format,
            DocumentOptions options)
        {
            try
            {
                // Parse and render template
                var parsedTemplate = Template.Parse(template);
                var context = new TemplateContext();
                var scriptObject = new ScriptObject();

                // Import data into template context
                scriptObject.Import(data, renamer: member => member.Name);
                context.PushGlobal(scriptObject);

                // Add useful functions
                context.PushGlobal(new CustomFunctions());

                var html = await parsedTemplate.RenderAsync(context);

                // Wrap in HTML document if needed
                if (!html.TrimStart().StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase))
                {
                    html = WrapInHtmlDocument(html, options);
                }

                // Convert to requested format
                return format switch
                {
                    OutputFormat.HTML => Encoding.UTF8.GetBytes(html),
                    OutputFormat.PDF => await ConvertToPdfAsync(html, options),
                    OutputFormat.DOCX => await ConvertToDocxAsync(html, options),
                    _ => throw new NotSupportedException($"Format {format} not supported")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating document");
                throw;
            }
        }

        private string WrapInHtmlDocument(string content, DocumentOptions options)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        @page {{
            size: {options.PageSize} {options.Orientation.ToString().ToLower()};
            margin: {options.Margins.Top} {options.Margins.Right} {options.Margins.Bottom} {options.Margins.Left};
        }}
        body {{
            font-family: Arial, sans-serif;
            line-height: 1.6;
            color: #333;
        }}
        table {{
            width: 100%;
            border-collapse: collapse;
            margin: 20px 0;
        }}
        th, td {{
            border: 1px solid #ddd;
            padding: 8px;
            text-align: left;
        }}
        th {{
            background-color: #f4f4f4;
            font-weight: bold;
        }}
        .invoice-header {{
            display: flex;
            justify-content: space-between;
            margin-bottom: 30px;
        }}
        .text-right {{ text-align: right; }}
        .mt-4 {{ margin-top: 2rem; }}
        .mb-4 {{ margin-bottom: 2rem; }}
    </style>
</head>
<body>
    {content}
</body>
</html>";
        }

        private async Task<byte[]> ConvertToPdfAsync(string html, DocumentOptions options)
        {
            var browser = await GetBrowserAsync();
            var page = await browser.NewPageAsync();

            try
            {
                await page.SetContentAsync(html, new NavigationOptions
                {
                    WaitUntil = new[] { WaitUntilNavigation.Networkidle0 },
                    Timeout = 30000 // 30 seconds
                });

                var pdfOptions = new PdfOptions
                {
                    Format = options.PageSize == PageSize.A4 ? PaperFormat.A4 : PaperFormat.Letter,
                    Landscape = options.Orientation == PageOrientation.Landscape,
                    PrintBackground = true,
                    MarginOptions = new MarginOptions
                    {
                        Top = options.Margins.Top,
                        Right = options.Margins.Right,
                        Bottom = options.Margins.Bottom,
                        Left = options.Margins.Left
                    }
                };

                return await page.PdfDataAsync(pdfOptions);
            }
            finally
            {
                await page.CloseAsync();
            }
        }
        private async Task<byte[]> ConvertToDocxAsync(string html, DocumentOptions options)
        {
            // For now, return HTML as DOCX conversion requires more complex library
            // TODO: Implement with OpenXML SDK or similar
            throw new NotImplementedException("DOCX conversion coming soon");
        }
        // Cleanup on disposal
        public void Dispose()
        {
            _browser?.Dispose();
        }
    }

}
