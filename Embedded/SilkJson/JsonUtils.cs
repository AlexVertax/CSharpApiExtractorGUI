using System;
using System.Collections.Generic;
using System.Linq;

namespace SilkJson
{
    public static class JsonUtils
    {
        /// <summary>
        /// Creates an indentation string for the specified depth.
        /// </summary>
        public static string GetIndent(int depth, string indent)
        {
            return string.Concat(Enumerable.Repeat(indent, depth));
        }

        public static StringOrIntValue[] PrepareKeys(StringOrIntValue[] keys)
        {
            if (keys == null) return Array.Empty<StringOrIntValue>();
            
            List<StringOrIntValue> newKeys = new List<StringOrIntValue>(keys.Length);
            for (int i = 0; i < keys.Length; i++)
            {
                StringOrIntValue key = keys[i];
                if (key.IsString)
                {
                    string sKey = key;
                    if (sKey.Contains('/'))
                    {
                        string[] parts = sKey.Replace("//", "/|").Split('/', StringSplitOptions.RemoveEmptyEntries);
                        newKeys.AddRange(parts.Select(p => (StringOrIntValue)p));
                    }
                    else newKeys.Add(key);
                }
                else newKeys.Add(key);
            }
            return newKeys.ToArray();
        }
    }
}