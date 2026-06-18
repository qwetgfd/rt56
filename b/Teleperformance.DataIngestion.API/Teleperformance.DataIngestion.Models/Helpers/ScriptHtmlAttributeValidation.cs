using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp.Html.Parser;
using AngleSharp;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace Teleperformance.DataIngestion.Models.Helpers
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ScriptHtmlAttributeValidation : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value != null)
            {
                string input = value.ToString();
                if (ContainsHtmlOrScript(input))
                {
                    return new ValidationResult(ErrorMessage);
                }
            }

            return ValidationResult.Success;
        }

        private bool ContainsHtmlOrScript(string input)
        {

            string html = WebUtility.HtmlDecode(input);
            // Setup AngleSharp configuration and context
            var config = Configuration.Default.WithDefaultLoader();
            var context = BrowsingContext.New(config);

            // Parse the HTML string into an AngleSharp document
            var parser = new HtmlParser();
            var document = parser.ParseDocument(html);
            // Select all img elements
            var imgElements = document.QuerySelectorAll("img");
            var scriptElements = document.QuerySelectorAll("script");

            if (ContainsHtmlTags(html))
            {
                return true;
            }
            if (ContainsOnerrorAttribute(html))
            {
                return true;
            }

            // Loop through the img elements and get their 'src' attribute
            foreach (var imgElement in imgElements)
            {
                return true;
            }

            foreach (var imgElement in scriptElements)
            {
                return true;
            }

            // Check if the HTML contains any script tags using the Regex IsMatch method
            return false;
        }


        public static bool ContainsOnerrorAttribute(string htmlContent)
        {
            var context = BrowsingContext.New(Configuration.Default);
            var document = context.OpenAsync(req => req.Content(htmlContent)).Result;

            var allElements = document.All;
            foreach (var element in allElements)
            {
                var onerrorAttribute = element.GetAttribute("onerror");
                if (!string.IsNullOrEmpty(onerrorAttribute))
                {
                    // Found an element with onerror attribute
                    // Handle this case as needed, e.g., log, prevent execution, etc.
                    return true;
                }
            }

            return false;
        }

        private bool ContainsHtmlTags(string input)
        {
            // Decode HTML entities
            string decodedInput = WebUtility.HtmlDecode(input);

            // Check for script and img tags using regular expressions
            if (Regex.IsMatch(decodedInput, @"<[^>]+>", RegexOptions.IgnoreCase))
            {
                return true;
            }

            // You can add more checks for other HTML tags as needed

            return false;
        }
    }
}
