using System;
using System.Collections.Generic;
using SilkJson;

namespace CSharpApiExtractor
{
    public sealed class ApiMember
    {
        public string Name { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Declaration { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Access { get; set; } = "public";
        public bool IsStatic { get; set; }
        public bool IsAbstract { get; set; }
        public bool IsVirtual { get; set; }
        public bool IsGeneric { get; set; }
        public bool HasGet { get; set; }
        public bool HasSet { get; set; }
        public string? ValueType { get; set; }
        public int SortOrder { get; set; } = int.MaxValue;
        public List<ApiParameter> Parameters { get; } = new List<ApiParameter>();

        private int GetTypeCode()
        {
            return Kind switch
            {
                "field" => 1,
                "property" => 2,
                "method" => 3,
                "delegate" => 4,
                "indexer" => 5,
                "enum" => 6,
                _ => -1
            };
        }

        public JsonObject ToJson()
        {
            JsonObject json = new JsonObject()
                .Set("name", Name)
                .Set("declaration", Declaration)
                .Set("type", GetTypeCode());

            if (!string.IsNullOrWhiteSpace(Summary))
            {
                json.Set("summary", Summary);
            }

            if (!string.Equals(Access, "public", StringComparison.Ordinal))
            {
                json.Set("access", Access);
            }

            if (IsStatic)
            {
                json.Set("isStatic", 1);
            }

            if (IsAbstract)
            {
                json.Set("isAbstract", 1);
            }

            if (IsVirtual)
            {
                json.Set("isVirtual", 1);
            }

            if (IsGeneric)
            {
                json.Set("isGeneric", 1);
            }

            if (HasGet)
            {
                json.Set("hasGet", 1);
            }

            if (HasSet)
            {
                json.Set("hasSet", 1);
            }

            if (!string.IsNullOrWhiteSpace(ValueType))
            {
                json.Set("valueType", ValueType);
            }

            if (Parameters.Count > 0)
            {
                JsonArray parametersJson = new JsonArray();
                foreach (ApiParameter parameter in Parameters)
                {
                    parametersJson.Add(parameter.ToJson());
                }

                json.Set("parameters", parametersJson);
            }

            return json;
        }
    }
}
