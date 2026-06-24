using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SilkJson
{
    /// <summary>
    /// Represents an enumerable collection of JSON nodes, typically from querying a structure.
    /// </summary>
    public class JsonEnumerable : Json
    {
        private List<Json> _items;

        /// <summary>
        /// Gets whether this enumerable is empty (null).
        /// </summary>
        public override bool IsMissed => !_items.Any();

        /// <summary>
        /// Initializes a new instance of JsonEnumerable with the specified items.
        /// </summary>
        /// <param name="items">The items to wrap.</param>
        public JsonEnumerable(IEnumerable<Json> items) => _items = items.ToList();

        public override Json Child(StringOrIntValue key) => new JsonEnumerable(_items.Select(i => i.Child(key)));

        /// <summary>
        /// Returns an enumerable collection of all direct children.
        /// </summary>
        /// <returns>A JsonEnumerable containing all children.</returns>
        public override JsonEnumerable Children() => new JsonEnumerable (_items.SelectMany(i => i.Children()));

        public override bool Contains(StringOrIntValue key) => _items.Any(i => i.Contains(key));

        /// <summary>
        /// Finds all nodes that match the specified predicate recursively.
        /// </summary>
        /// <param name="match">A predicate that returns true for matching nodes, taking the value and key as parameters.</param>
        /// <returns>A JsonEnumerable containing all matching nodes.</returns>
        public override JsonEnumerable FindDeep(FindMatch match) => new JsonEnumerable (_items.SelectMany(i => i.FindDeep(match)));

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator for the collection.</returns>
        public override IEnumerator<Json> GetEnumerator() => _items.GetEnumerator();
        
        /// <summary>
        /// Removes all items that match the specified predicate (not supported for enumerable).
        /// </summary>
        /// <param name="match">A predicate that returns true for items to remove.</param>
        /// <returns>Always returns 0.</returns>
        public override int RemoveAll(RemoveMatch match) => 0;

        public override void SetChild(StringOrIntValue key, object value)
        {
            foreach (Json item in _items)
            {
                item.SetChild(key, value);
            }
        }

        /// <summary>
        /// Serializes this JsonEnumerable to a JSON string.
        /// </summary>
        /// <param name="builder">The StringBuilder to write to.</param>
        public override void Stringify(StringBuilder builder)
        {
            Json[] items = _items.ToArray();
            if (items.Length == 0)
            {
                builder.Append("null");
                return;
            }
            if (items.Length == 1)
            {
                items[0].Stringify(builder);
                return;
            }
            builder.Append('[');
            bool first = true;
            foreach (var item in items)
            {
                if (!first) builder.Append(',');
                item.Stringify(builder);
                first = false;
            }
            builder.Append(']');
        }

        /// <summary>
        /// Converts this JsonEnumerable to an instance of the specified type.
        /// </summary>
        /// <param name="type">The target type.</param>
        /// <param name="bindingFlags">Binding flags for member lookup.</param>
        /// <returns>An instance of the target type.</returns>
        public override object To(Type type, BindingFlags bindingFlags = JsonSerializer.InstanceLookup)
        {
            if (type.IsArray)
            {
                Type elementType = type.GetElementType();
                Array array = Array.CreateInstance(elementType, _items.Count());
                int i = 0;
                foreach (Json item in _items)
                {
                    array.SetValue(item.To(elementType, bindingFlags), i++);
                }
                return array;
            }
            if (typeof(IEnumerable<>).IsAssignableFrom(type))
            {
                Type elementType = type.GetGenericArguments()[0];
                Type listType = typeof(List<>).MakeGenericType(elementType);
                IList list = (IList)Activator.CreateInstance(listType);
                foreach (Json item in _items)
                {
                    list.Add(item.To(elementType, bindingFlags));
                }
                return list;
            }
            throw new InvalidOperationException($"Cannot convert JsonEnumerable to {type}");
        }

        /// <summary>
        /// Returns all values of the specified type recursively.
        /// </summary>
        /// <typeparam name="T">The type of values to return.</typeparam>
        /// <returns>An enumerable of values of type T.</returns>
        public override IEnumerable<T> Values<T>()
        {
            return Children().SelectMany(c => c.Values<T>());
        }
    }
}
