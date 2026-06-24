using System;
using System.Linq;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace CSharpApiExtractor
{
    internal static class DocumentationCommentParser
    {
        public static string GetSummary(ISymbol symbol)
        {
            return GetSummary(symbol.GetDocumentationCommentXml());
        }

        public static string GetSummary(string? xml)
        {
            if (string.IsNullOrWhiteSpace(xml))
            {
                return string.Empty;
            }

            try
            {
                XDocument document = XDocument.Parse(xml);
                XElement? summaryElement = document.Descendants("summary").FirstOrDefault();
                return GetSummary(summaryElement);
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string GetSummary(XElement? element)
        {
            if (element == null)
            {
                return string.Empty;
            }

            string[] lines = element.Value.Split('\n');
            return string.Join(
                '\n',
                lines
                    .Select(line => line.TrimStart('/', '*', ' ', '\n', '\r', '\t'))
                    .Where(line => !string.IsNullOrWhiteSpace(line)));
        }

        public static void PopulateMemberSummary(ApiMember member, string? xml)
        {
            if (string.IsNullOrWhiteSpace(xml))
            {
                return;
            }

            try
            {
                XDocument document = XDocument.Parse(xml);
                member.Summary = GetSummary(document.Descendants("summary").FirstOrDefault());

                foreach (ApiParameter parameter in member.Parameters)
                {
                    XElement? parameterElement = document
                        .Descendants("param")
                        .FirstOrDefault(element => string.Equals(element.Attribute("name")?.Value, parameter.Name, StringComparison.Ordinal));

                    if (parameterElement != null)
                    {
                        parameter.Summary = GetSummary(parameterElement);
                    }
                }
            }
            catch
            {
            }
        }
    }
}
