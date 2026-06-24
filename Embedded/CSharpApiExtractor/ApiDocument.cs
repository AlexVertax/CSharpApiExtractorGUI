using System;
using System.Collections.Generic;
using System.Linq;
using SilkJson;

namespace CSharpApiExtractor
{
    public sealed class ApiDocument
    {
        public List<ApiNamespace> Namespaces { get; } = new List<ApiNamespace>();
        public List<string> MissedItems { get; } = new List<string>();

        public JsonObject ToJson()
        {
            JsonObject json = new JsonObject();
            foreach (ApiNamespace apiNamespace in Namespaces.OrderBy(i => i.Name, StringComparer.Ordinal))
            {
                json.Set(apiNamespace.Name, apiNamespace.ToJson());
            }

            return json;
        }

        public string ToJsonString(bool pretty = false)
        {
            JsonObject json = ToJson();
            return pretty ? json.Pretty() : json.ToString();
        }
    }
}
