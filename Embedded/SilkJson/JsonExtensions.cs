using System;
using System.Collections.Generic;
using System.Linq;

namespace SilkJson
{
    public static class JsonExtensions
    {
        public static void ForEach(this IEnumerable<Json> items, Action<Json> action)
        {
            foreach (Json item in items) action(item);
        }
    
        public static void ForEach<TKey>(this IEnumerable<IGrouping<TKey, Json>> groups, Action<TKey, IEnumerable<Json>> action)
        {
            foreach (IGrouping<TKey, Json> group in groups) action(group.Key, group);
        }
    }
}
