using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SilkJson
{
    /// <summary>
    /// Represents a JSON array - an ordered collection of JSON values.
    /// </summary>
    public class JsonArray : Json, IEnumerable<Json>
    {
        /// <summary>
        /// Gets the number of elements in the array.
        /// </summary>
        public int Count => _items.Count;
        
        /// <summary>
        /// Gets whether the array is read-only.
        /// </summary>
        public bool IsReadOnly => false;
        
        private List<Json> _items { get; } = new List<Json>();

        /// <summary>
        /// Initializes a new instance of JsonArray.
        /// </summary>
        public JsonArray()
        {
        }

        /// <summary>
        /// Initializes a new instance of JsonArray with the specified items.
        /// </summary>
        /// <param name="items">The items to add to the array.</param>
        public JsonArray(params object[] items)
        {
            foreach (var item in items) this._items.Add(From(item));
        }

        /// <summary>
        /// Adds a JSON item to the array and returns the array for chaining.
        /// </summary>
        /// <param name="item">The item to add.</param>
        /// <returns>This instance for method chaining.</returns>
        public JsonArray Add(Json item)
        {
            _items.Add(item);
            return this;
        }

        /// <summary>
        /// Adds an item to the array, converting it to a JSON value if necessary, and returns the array for chaining.
        /// </summary>
        /// <param name="item">The item to add.</param>
        /// <returns>This instance for method chaining.</returns>
        public JsonArray Add(object item)
        {
            _items.Add(From(item));
            return this;
        }

        /// <summary>
        /// Adds multiple items to the array and returns the array for chaining.
        /// </summary>
        /// <param name="values">The values to add.</param>
        /// <returns>This instance for method chaining.</returns>
        public JsonArray AddRange(IEnumerable values)
        {
            /*IEnumerable<Json> select = values.Select(v => From(v));
            items.AddRange(select);*/

            foreach (object value in values)
            {
                _items.Add(From(value));
            }
            
            return this;
        }
        
        /// <summary>
        /// Adds multiple items to the array and returns the array for chaining.
        /// </summary>
        /// <param name="values">The values to add.</param>
        /// <returns>This instance for method chaining.</returns>
        public JsonArray AddRange(params object[] values) => AddRange((IEnumerable)values);

        /// <summary>
        /// Returns an enumerable collection of all direct children.
        /// </summary>
        /// <returns>A JsonEnumerable containing all children.</returns>
        public override JsonEnumerable Children() => new JsonEnumerable(_items);

        /// <summary>
        /// Returns the child at the specified integer index.
        /// </summary>
        /// <param name="key">The integer index wrapped in StringOrIntValue.</param>
        /// <returns>The child Json at the specified index, or a null JsonValue if the index is out of range or the key is not an integer.</returns>
        public override Json Child(StringOrIntValue key)
        {
            if (!key.IsInt) return _missedValue;
            
            int i = key;
            return i >= 0 && i < _items.Count ? _items[i] : _missedValue;
        }

        /// <summary>
        /// Removes all elements from the array.
        /// </summary>
        public void Clear() => _items.Clear();
        
        /// <summary>
        /// Determines whether the array contains a specific item.
        /// </summary>
        /// <param name="item">The item to locate.</param>
        /// <returns>True if the item is found; otherwise, false.</returns>
        public bool Contains(Json item) => _items.Contains(item);

        public override bool Contains(StringOrIntValue key)
        {
            if (!key.IsInt) return false;
            
            int i = key;
            return i >= 0 && i < _items.Count;

        }

        /// <summary>
        /// Copies the elements of the array to the specified destination array.
        /// </summary>
        /// <param name="array">The destination array.</param>
        /// <param name="arrayIndex">The zero-based index in the destination array.</param>
        public void CopyTo(Json[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
        
        /// <summary>
        /// Finds all nodes that match the specified predicate recursively.
        /// </summary>
        /// <param name="match">A predicate that returns true for matching nodes, taking the value and key as parameters.</param>
        /// <returns>A JsonEnumerable containing all matching nodes.</returns>
        public override JsonEnumerable FindDeep(FindMatch match)
        {
            return new JsonEnumerable(_items.SelectMany((c, index) =>
            {
                var self = match(c, index) ? new[] { c } : Enumerable.Empty<Json>();
                return self.Concat(c.FindDeep(match));
            }));
        }

        /// <summary>
        /// Returns an enumerator that iterates through the array.
        /// </summary>
        /// <returns>An enumerator for the array.</returns>
        public override IEnumerator<Json> GetEnumerator() => _items.GetEnumerator();
        
        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An enumerator for the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Searches for the specified item and returns the zero-based index.
        /// </summary>
        /// <param name="item">The item to locate.</param>
        /// <returns>The zero-based index of the item, or -1 if not found.</returns>
        public int IndexOf(Json item)
        {
            if (item is JsonValue jItem)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    JsonValue json = _items[i] as JsonValue;
                    if (json != null && json.ValueEquals(jItem.Value)) return i;
                }

                return -1;
            }

            return _items.IndexOf(item);
        }

        /// <summary>
        /// Inserts an item at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which the item should be inserted.</param>
        /// <param name="item">The item to insert.</param>
        public void Insert(int index, Json item) => _items.Insert(index, item);
        
        /// <summary>
        /// Parses a JSON string into a JsonArray instance.
        /// </summary>
        /// <param name="jsonString">The JSON string to parse.</param>
        /// <returns>A JsonArray representing the parsed string.</returns>
        public new static JsonArray Parse(string jsonString) => JsonParser.Parse(jsonString) as JsonArray;

        /// <summary>
        /// Removes the first occurrence of a specific item.
        /// </summary>
        /// <param name="item">The item to remove.</param>
        /// <returns>True if the item was successfully removed; otherwise, false.</returns>
        public bool Remove(Json item)
        {
            if (item is JsonValue jItem)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    JsonValue json = _items[i] as JsonValue;
                    if (json != null && json.ValueEquals(jItem.Value))
                    {
                        _items.RemoveAt(i);
                        return true;
                    }
                }

                return false;
            }

            return _items.Remove(item);
        }

        /// <summary>
        /// Removes all items that match the specified predicate.
        /// </summary>
        /// <param name="match">A predicate that returns true for items to remove.</param>
        /// <returns>The number of items removed.</returns>
        public override int RemoveAll(RemoveMatch match)
        {
            int count = 0;
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                if (match(i, _items[i]))
                {
                    _items.RemoveAt(i);
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Sets the child value at the given index if the index is valid.
        /// </summary>
        /// <param name="key">The integer index wrapped in StringOrIntValue.</param>
        /// <param name="value">The new value to set (will be converted to Json).</param>
        public override void SetChild(StringOrIntValue key, object value)
        {
            if (!key.IsInt) return;
            
            int i = key;
            if (i >= 0 && i < _items.Count)
            {
                _items[i] = From(value);
            }
        }

        /// <summary>
        /// Removes the item at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the item to remove.</param>
        public void RemoveAt(int index) => _items.RemoveAt(index);

        /// <summary>
        /// Serializes this JsonArray to a JSON string.
        /// </summary>
        /// <param name="builder">The StringBuilder to write to.</param>
        public override void Stringify(StringBuilder builder)
        {
            builder.Append('[');
            for (int i = 0; i < _items.Count; i++)
            {
                if (i > 0) builder.Append(',');
                _items[i].Stringify(builder);
            }

            builder.Append(']');
        }

        /// <summary>
        /// Internal method to build a formatted JSON string.
        /// </summary>
        public override void PrettyStringify(StringBuilder builder, int depth, string indent)
        {
            if (_items.Count == 0)
            {
                builder.Append("[]");
                return;
            }

            string nextIndent = JsonUtils.GetIndent(depth + 1, indent);
            builder.Append("[\n");
            for (int i = 0; i < _items.Count; i++)
            {
                builder.Append(nextIndent);
                if (_items[i] is JsonObject || _items[i] is JsonArray)
                {
                    _items[i].PrettyStringify(builder, depth + 1, indent);
                }
                else
                {
                    _items[i].Stringify(builder);
                }
                if (i < _items.Count - 1) builder.Append(',');
                builder.Append('\n');
            }
            builder.Append(JsonUtils.GetIndent(depth, indent)).Append(']');
        }

        /// <summary>
        /// Converts this JsonArray to an instance of the specified type.
        /// </summary>
        /// <param name="type">The target type.</param>
        /// <param name="bindingFlags">Binding flags for member lookup.</param>
        /// <returns>An instance of the target type.</returns>
        public override object To(Type type, BindingFlags bindingFlags = JsonSerializer.InstanceLookup)
        {
            int count = Count;
            if (count == 0) return null;

            if (type.IsArray)
            {
                Type elementType = type.GetElementType();
                Array arr = Array.CreateInstance(elementType, count);
                if (_items[0] is JsonObject)
                {
                    IEnumerable<MemberInfo> members = elementType.GetMembers(bindingFlags);
                    for (int i = 0; i < count; i++)
                    {
                        Json child = _items[i];
                        object item = (child as JsonObject).To(elementType, members, bindingFlags);
                        arr.SetValue(item, i);
                    }
                }
                else
                {
                    for (int i = 0; i < count; i++)
                    {
                        Json child = _items[i];
                        object item = child.To(elementType, bindingFlags);
                        arr.SetValue(item, i);
                    }
                }

                return arr;
            }

            if (!type.IsGenericType) return null;
            
            Type listType = type.GetGenericArguments()[0];
            object v = Activator.CreateInstance(type);

            if (_items[0] is JsonObject)
            {
                IEnumerable<MemberInfo> members = listType.GetMembers(bindingFlags);
                for (int i = 0; i < count; i++)
                {
                    Json child = _items[i];
                    object item = (child as JsonObject).To(listType, members);
                    try
                    {
                        MethodInfo methodInfo = type.GetMethod("Add");
                        if (methodInfo != null) methodInfo.Invoke(v, new[] { item });
                    }
                    catch
                    {
                    }
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    Json child = _items[i];
                    object item = child.To(listType, bindingFlags);
                    try
                    {
                        MethodInfo methodInfo = type.GetMethod("Add");
                        if (methodInfo != null) methodInfo.Invoke(v, new[] { item });
                    }
                    catch
                    {
                    }
                }
            }

            return v;
        }

        /// <summary>
        /// Returns all values of the specified type recursively.
        /// </summary>
        /// <typeparam name="T">The type of values to return.</typeparam>
        /// <returns>An enumerable of values of type T.</returns>
        public override IEnumerable<T> Values<T>() => _items.SelectMany(i => i.Values<T>());

        /// <summary>
        /// Adds an item to the array using the + operator.
        /// </summary>
        /// <param name="array">The array to add to.</param>
        /// <param name="item">The item to add.</param>
        /// <returns>The modified array.</returns>
        public static JsonArray operator +(JsonArray array, Json item)
        {
            array.Add(item);
            return array;
        }
    }
}
