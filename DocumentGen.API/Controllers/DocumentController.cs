using DocumentGen.API.Services;
using DocumentGen.Core.Services;
using DocumentGen.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DocumentGen.API.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class DocumentController : ControllerBase
    {
        private readonly ITemplateEngine _templateEngine;
        private readonly IUsageTracker _usageTracker;
        private readonly ILogger<DocumentController> _logger;

        public DocumentController(
            ITemplateEngine templateEngine,
            IUsageTracker usageTracker,
            ILogger<DocumentController> logger)
        {
            _templateEngine = templateEngine;
            _usageTracker = usageTracker;
            _logger = logger;
        }
        [HttpPost("generate")]
        public async Task<IActionResult> Generate([FromBody] DocumentRequest request)
        {
            try
            {
                // Validate request
                if (string.IsNullOrEmpty(request.TemplateContent) &&
                    string.IsNullOrEmpty(request.TemplateId))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Error = "Either TemplateContent or TemplateId must be provided"
                    });
                }

                // Get API key from context
                var apiKey = HttpContext.Items["ApiKey"]?.ToString() ?? "anonymous";

                // Check usage limits
                if (!await _usageTracker.CanGenerateAsync(apiKey))
                {
                    return StatusCode(429, new ApiResponse<object>
                    {
                        Success = false,
                        Error = "Monthly usage limit exceeded"
                    });
                }

                // Generate document
                var template = request.TemplateContent ??
                              await GetTemplateById(request.TemplateId!);

                var document = await _templateEngine.GenerateDocumentAsync(
                    template,
                    request.Data,
                    request.Format,
                    request.Options
                );

                // Track usage
                await _usageTracker.TrackUsageAsync(apiKey, 1);

                // Return file
                var contentType = request.Format switch
                {
                    OutputFormat.PDF => "application/pdf",
                    OutputFormat.HTML => "text/html",
                    OutputFormat.DOCX => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    _ => "application/octet-stream"
                };

                var fileName = request.Options.FileName ??
                              $"document.{request.Format.ToString().ToLower()}";

                return File(document, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating document");
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Error = "An error occurred while generating the document"
                });
            }
        }
        [HttpPost("preview")]
        public async Task<IActionResult> Preview([FromBody] DocumentRequest request)
        {
            // Force HTML output for preview
            request.Format = OutputFormat.HTML;
            return await Generate(request);
        }

        private async Task<string> GetTemplateById(string templateId)
        {
            // TODO: Implement template storage
            // For now, return a sample template
            return templateId switch
            {
                "invoice" => GetInvoiceTemplate(),
                "receipt" => GetReceiptTemplate(),
                _ => throw new ArgumentException($"Template {templateId} not found")
            };
        }
        private string GetInvoiceTemplate()
        {
            return @"
<div class='invoice-header'>
    <div>
        <h1>INVOICE</h1>
        <p>Invoice #: {{ invoice.number }}</p>
        <p>Date: {{ invoice.date | date.format 'MMMM dd, yyyy' }}</p>
    </div>
    <div class='text-right'>
        <h2>{{ company.name }}</h2>
        <p>{{ company.address }}</p>
        <p>{{ company.city }}, {{ company.state }} {{ company.zip }}</p>
    </div>
</div>

<div class='mb-4'>
    <h3>Bill To:</h3>
    <p>{{ customer.name }}</p>
    <p>{{ customer.address }}</p>
    <p>{{ customer.city }}, {{ customer.state }} {{ customer.zip }}</p>
</div>

<table>
    <thead>
        <tr>
            <th>Description</th>
            <th>Quantity</th>
            <th>Unit Price</th>
            <th>Total</th>
        </tr>
    </thead>
    <tbody>
        {{ for item in items }}
        <tr>
            <td>{{ item.description }}</td>
            <td>{{ item.quantity }}</td>
            <td>{{ item.unitPrice | math.format 'C' }}</td>
            <td>{{ item.quantity * item.unitPrice | math.format 'C' }}</td>
        </tr>
        {{ end }}
    </tbody>
    <tfoot>
        <tr>
            <td colspan='3' class='text-right'><strong>Subtotal:</strong></td>
            <td><strong>{{ invoice.subtotal | math.format 'C' }}</strong></td>
        </tr>
        <tr>
            <td colspan='3' class='text-right'><strong>Tax ({{ invoice.taxRate }}%):</strong></td>
            <td><strong>{{ invoice.tax | math.format 'C' }}</strong></td>
        </tr>
        <tr>
            <td colspan='3' class='text-right'><strong>Total:</strong></td>
            <td><strong>{{ invoice.total | math.format 'C' }}</strong></td>
        </tr>
    </tfoot>
</table>

<div class='mt-4'>
    <p><strong>Terms:</strong> {{ invoice.terms }}</p>
    <p><strong>Thank you for your business!</strong></p>
</div>";
        }
        private string GetReceiptTemplate()
        {
            return @"
<div style='text-align: center; margin-bottom: 30px;'>
    <h1>{{ company.name }}</h1>
    <p>{{ company.address }}</p>
    <p>Tel: {{ company.phone }}</p>
    <hr>
    <h2>RECEIPT</h2>
    <p>{{ receipt.date | date.format 'MM/dd/yyyy hh:mm tt' }}</p>
    <p>Receipt #: {{ receipt.number }}</p>
</div>

<table style='width: 100%;'>
    {{ for item in items }}
    <tr>
        <td>{{ item.name }}</td>
        <td style='text-align: right;'>{{ item.price | math.format 'C' }}</td>
    </tr>
    {{ end }}
</table>

<hr>

<table style='width: 100%;'>
    <tr>
        <td><strong>Subtotal:</strong></td>
        <td style='text-align: right;'>{{ receipt.subtotal | math.format 'C' }}</td>
    </tr>
    <tr>
        <td><strong>Tax:</strong></td>
        <td style='text-align: right;'>{{ receipt.tax | math.format 'C' }}</td>
    </tr>
    <tr>
        <td><strong>TOTAL:</strong></td>
        <td style='text-align: right;'><strong>{{ receipt.total | math.format 'C' }}</strong></td>
    </tr>
</table>

<div style='text-align: center; margin-top: 30px;'>
    <p>Thank you for your purchase!</p>
    {{ if receipt.barcode }}
    <p>{{ barcode receipt.barcode }}</p>
    {{ end }}
</div>";
        }
    


}
}
