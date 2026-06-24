using System;
using SilkJson;

namespace CSharpApiExtractor
{
    public sealed class ApiParameter
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsOptional { get; set; }
        public string? DefaultValue { get; set; }
        public string? RefKind { get; set; }
        public string? Summary { get; set; }

        public JsonObject ToJson()
        {
            JsonObject json = new JsonObject()
                .Set("name", Name)
                .Set("type", Type);

            if (IsOptional)
            {
                json.Set("isOptional", 1);
            }

            if (DefaultValue != null)
            {
                json.Set("defaultValue", DefaultValue);
            }

            if (RefKind != null)
            {
                json.Set("refKind", RefKind);
            }

            if (!string.IsNullOrWhiteSpace(Summary))
            {
                json.Set("summary", Summary);
            }

            return json;
        }
    }
}
