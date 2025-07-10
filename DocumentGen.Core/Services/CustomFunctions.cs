using Scriban.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentGen.Core.Services
{
    public class CustomFunctions : ScriptObject
    {
        public static string FormatCurrency(decimal amount, string currency = "USD")
        {
            return currency switch
            {
                "USD" => $"${amount:N2}",
                "EUR" => $"€{amount:N2}",
                "GBP" => $"£{amount:N2}",
                _ => $"{currency} {amount:N2}"
            };
        }

        public static string FormatDate(DateTime date, string format = "yyyy-MM-dd")
        {
            return date.ToString(format);
        }

        public static string Barcode(string text)
        {
            // Simple placeholder - implement actual barcode generation
            return $"[BARCODE: {text}]";
        }
    }

}
