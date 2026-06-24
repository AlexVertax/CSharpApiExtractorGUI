using System;
using System.Collections.Generic;
using System.Linq;
using SilkJson;

namespace CSharpApiExtractor
{
    public sealed class ApiType
    {
        public string Name { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Declaration { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Access { get; set; } = "public";
        public bool IsStatic { get; set; }
        public bool IsAbstract { get; set; }
        public string? BaseType { get; set; }
        public List<string> Interfaces { get; } = new List<string>();
        public List<ApiMember> Members { get; } = new List<ApiMember>();

        private int? GetTypeCode()
        {
            return Kind switch
            {
                "interface" => 1,
                "struct" => 2,
                "enum" => 3,
                "record" => 4,
                _ => null
            };
        }

        public JsonObject ToJson()
        {
            JsonObject json = new JsonObject()
                .Set("summary", Summary)
                .Set("declaration", Declaration);

            int? typeCode = GetTypeCode();
            if (typeCode.HasValue)
            {
                json.Set("type", typeCode.Value);
            }

            if (!string.Equals(Access, "public", StringComparison.Ordinal))
            {
                json.Set("access", Access);
            }

            if (IsAbstract)
            {
                json.Set("isAbstract", 1);
            }
            else if (IsStatic)
            {
                json.Set("isStatic", 1);
            }

            if (!string.IsNullOrWhiteSpace(BaseType))
            {
                json.Set("baseClass", BaseType);
            }

            if (Interfaces.Count > 0)
            {
                JsonArray interfacesJson = new JsonArray();
                foreach (string @interface in Interfaces.OrderBy(item => item, StringComparer.Ordinal))
                {
                    interfacesJson.Add(@interface);
                }

                json.Set("interfaces", interfacesJson);
            }

            if (Members.Count > 0)
            {
                JsonArray membersJson = new JsonArray();
                foreach (ApiMember member in Members)
                {
                    membersJson.Add(member.ToJson());
                }

                json.Set("members", membersJson);
            }

            return json;
        }
    }
}
