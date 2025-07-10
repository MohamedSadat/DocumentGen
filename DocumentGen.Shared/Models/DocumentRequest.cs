using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentGen.Shared.Models
{
    public class DocumentRequest
    {
        public string? TemplateId { get; set; } = "";
        public string? TemplateContent { get; set; } = "";
        public Dictionary<string, object> Data { get; set; } = new();
        public OutputFormat Format { get; set; } = OutputFormat.PDF;
        public DocumentOptions Options { get; set; } = new();
    }

    public class DocumentOptions
    {
        public string? FileName { get; set; } = "";
        public PageSize PageSize { get; set; } = PageSize.A4;
        public PageOrientation Orientation { get; set; } = PageOrientation.Portrait;
        public PageMargins Margins { get; set; } = new();
    }

    public class PageMargins
    {
        public string Top { get; set; } = "1cm";
        public string Right { get; set; } = "1cm";
        public string Bottom { get; set; } = "1cm";
        public string Left { get; set; } = "1cm";
    }

    public enum OutputFormat
    {
        PDF,
        HTML,
        DOCX
    }

    public enum PageSize
    {
        A4,
        Letter,
        Legal
    }

    public enum PageOrientation
    {
        Portrait,
        Landscape
    }

    // DocumentGen.Shared/Models/ApiResponse.cs
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? Error { get; set; }
        public string? RequestId { get; set; } = Guid.NewGuid().ToString();
    }

}
