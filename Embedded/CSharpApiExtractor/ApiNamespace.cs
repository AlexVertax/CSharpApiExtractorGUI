using System;
using System.Collections.Generic;
using System.Linq;
using SilkJson;

namespace CSharpApiExtractor
{
    public sealed class ApiNamespace
    {
        public string Name { get; set; } = string.Empty;
        public List<ApiType> Types { get; } = new List<ApiType>();

        public JsonObject ToJson()
        {
            JsonObject json = new JsonObject();
            foreach (ApiType type in Types
                         .OrderBy(item => item.Name, StringComparer.Ordinal)
                         .ThenBy(item => item.Declaration, StringComparer.Ordinal))
            {
                json.Set(type.Name, type.ToJson());
            }

            return json;
        }
    }
}
