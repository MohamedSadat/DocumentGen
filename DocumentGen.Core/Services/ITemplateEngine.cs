using DocumentGen.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentGen.Core.Services
{
    public interface ITemplateEngine
    {
        Task<byte[]> GenerateDocumentAsync(
            string template,
            object data,
            OutputFormat format,
            DocumentOptions options);
    }

}
