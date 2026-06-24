using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SilkJson
{
    /// <summary>
    /// Represents a JSON object - a collection of key-value pairs.
    /// </summary>
    public class JsonObject : Json
    {
        /// <summary>
        /// Delegate for resolving merge conflicts between objects.
        /// </summary>
        /// <param name="key">The key being merged.</param>
        /// <param name="existingValue">The existing value in the target object.</param>
        /// <param name="newValue">The new value from the source object.</param>
        /// <returns>The resolved value to use.</returns>
        public delegate Json MergeConflictResolver(string key, Json existingValue, Json newValue);

        private OrderedDictionary _dictionary = new OrderedDictionary();

        /// <summary>
        /// Initializes a new instance of JsonObject.
        /// </summary>
        public JsonObject()
        {
        }

        /// <summary>
        /// Initializes a new instance of JsonObject from a dictionary.
        /// </summary>
        /// <param name="dict">The dictionary to copy from.</param>
        public JsonObject(Dictionary<string, object> dict)
        {
            foreach (var pair in dict) Set(pair.Key, pair.Value);
        }

        public JsonObject(object obj)
        {
            Json fromObject = JsonSerializer.FromObject(obj);
            if (fromObject is JsonObject jsonObj) _dictionary = jsonObj._dictionary;
        }
        
        /// <summary>
        /// Adds a key-value pair to the object and returns the object for chaining.
        /// </summary>
        /// <param name="key">The key to add.</param>
        /// <param name="value">The value to add.</param>
        /// <returns>This instance for method chaining.</returns>
        public Json Add(string key, object value) => Set(key, value);

        /// <summary>
        /// Returns an enumerator that iterates through the values.
        /// </summary>
        /// <returns>An enumerator for the values.</returns>
        public override IEnumerator<Json> GetEnumerator()
        {
            foreach (DictionaryEntry entry in _dictionary) yield return (Json)entry.Value;
        }

        public override Json Child(StringOrIntValue key)
        {
            if (key.IsString)
            {
                string sKey = (string)key;
                if (_dictionary.Contains(sKey)) return (Json)_dictionary[sKey];
            }

            if (key.IsInt)
            {
                int iKey = (int)key;
                if (iKey >= 0 && iKey < _dictionary.Count) return (Json)_dictionary[iKey];
            }

            return _missedValue;
        }

        /// <summary>
        /// Returns an enumerable collection of all direct children.
        /// </summary>
        /// <returns>A JsonEnumerable containing all children.</returns>
        public override JsonEnumerable Children() => new JsonEnumerable(_dictionary.Values.Cast<Json>());

        /// <summary>
        /// Removes all key-value pairs from the object.
        /// </summary>
        public void Clear() => _dictionary.Clear();

        public override bool Contains(StringOrIntValue key)
        {
            if (key.IsString) return _dictionary.Contains((string)key);
            if (!key.IsInt) return false;
            
            int index = (int)key;
            return index >= 0 && index < _dictionary.Count;
        }

        /// <summary>
        /// Deserializes this JsonObject into an existing object instance.
        /// </summary>
        /// <param name="obj">The object to populate.</param>
        /// <param name="bindingFlags">Binding flags for member lookup.</param>
        public void DeserializeObject(object obj, BindingFlags bindingFlags = JsonSerializer.InstanceLookup)
        {
            To(obj, obj.GetType().GetMembers(bindingFlags));
        }
        
        /// <summary>
        /// Finds all nodes that match the specified predicate recursively.
        /// </summary>
        /// <param name="match">A predicate that returns true for matching nodes, taking the value and key as parameters.</param>
        /// <returns>A JsonEnumerable containing all matching nodes.</returns>
        public override JsonEnumerable FindDeep(FindMatch match)
        {
            return new JsonEnumerable(
                _dictionary.Cast<DictionaryEntry>()
                    .SelectMany(d =>
                    {
                        var val = (Json)d.Value;
                        var key = (string)d.Key;
                        var self = match(val, key) ? new[] { val } : Enumerable.Empty<Json>();
                        return self.Concat(val.FindDeep(match));
                    })
            );
        }

        /// <summary>
        /// Gets the index of a key in the object.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <returns>The zero-based index of the key, or -1 if not found.</returns>
        public int IndexOf(string key) => _dictionary.Cast<DictionaryEntry>().Select((entry, index) => new { Key = (string)entry.Key, Index = index }).FirstOrDefault(x => x.Key == key)?.Index ?? -1;

        /// <summary>
        /// Inserts a key-value pair at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which to insert.</param>
        /// <param name="key">The key to insert.</param>
        /// <param name="value">The value to insert.</param>
        public void Insert(int index, string key, object value) => _dictionary.Insert(index, key, From(value));

        /// <summary>
        /// Merges another JsonObject into this one.
        /// </summary>
        /// <param name="other">The other JsonObject to merge.</param>
        /// <param name="conflictResolver">Optional resolver for key conflicts.</param>
        public void Merge(JsonObject other, MergeConflictResolver conflictResolver = null)
        {
            foreach (DictionaryEntry entry in other._dictionary)
            {
                var key = (string)entry.Key;
                var value = (Json)entry.Value;
                if (conflictResolver != null && _dictionary.Contains(key))
                {
                    _dictionary[key] = conflictResolver(key, (Json)_dictionary[key], value);
                }
                else
                {
                    _dictionary[key] = value;
                }
            }
        }

        /// <summary>
        /// Parses a JSON string into a JsonObject instance.
        /// </summary>
        /// <param name="jsonString">The JSON string to parse.</param>
        /// <returns>A JsonObject representing the parsed string.</returns>
        public new static JsonObject Parse(string jsonString) => JsonParser.Parse(jsonString) as JsonObject;

        /// <summary>
        /// Removes a key-value pair by key.
        /// </summary>
        /// <param name="key">The key to remove.</param>
        /// <returns>True if the key was found and removed; otherwise, false.</returns>
        public bool Remove(string key)
        {
            if (!_dictionary.Contains(key)) return false;
            _dictionary.Remove(key);
            return true;
        }

        /// <summary>
        /// Removes all items that match the specified predicate.
        /// </summary>
        /// <param name="match">A predicate that returns true for items to remove.</param>
        /// <returns>The number of items removed.</returns>
        public override int RemoveAll(RemoveMatch match)
        {
            var keysToRemove = _dictionary.Cast<DictionaryEntry>()
                .Where(p => match((string)p.Key, (Json)p.Value))
                .Select(p => (string)p.Key)
                .ToList();
            foreach (var key in keysToRemove) _dictionary.Remove(key);
            return keysToRemove.Count;
        }

        /// <summary>
        /// Removes the key-value pair at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the item to remove.</param>
        public void RemoveAt(int index) => _dictionary.RemoveAt(index);

        public override void SetChild(StringOrIntValue key, object value)
        {
            if (key.IsString) _dictionary[(string)key] = From(value);
            else if (key.IsInt)
            {
                int index = (int)key;
                if (index < 0 || index >= _dictionary.Count) return;
                _dictionary[index] = From(value);
            }
        }

        /// <summary>
        /// Sets a key-value pair and returns the object for chaining.
        /// </summary>
        /// <param name="key">The key to set.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>This instance for method chaining.</returns>
        public JsonObject Set(string key, object value)
        {
            _dictionary[key] = From(value);
            return this;
        }

        /// <summary>
        /// Serializes this JsonObject to a JSON string.
        /// </summary>
        /// <param name="builder">The StringBuilder to write to.</param>
        public override void Stringify(StringBuilder builder)
        {
            builder.Append('{');
            bool first = true;
            foreach (DictionaryEntry entry in _dictionary)
            {
                if (!first) builder.Append(',');
                first = false;
                var key = (string)entry.Key;
                var value = (Json)entry.Value;
                builder.Append('"').Append(key).Append('"').Append(':');
                value.Stringify(builder);
            }

            builder.Append('}');
        }

        /// <summary>
        /// Internal method to build a formatted JSON string.
        /// </summary>
        public override void PrettyStringify(StringBuilder builder, int depth, string indent)
        {
            if (_dictionary.Count == 0)
            {
                builder.Append("{}");
                return;
            }

            string nextIndent = JsonUtils.GetIndent(depth + 1, indent);
            builder.Append("{\n");
            bool first = true;
            foreach (DictionaryEntry entry in _dictionary)
            {
                if (!first) builder.Append(",\n");
                first = false;
                var key = (string)entry.Key;
                var value = (Json)entry.Value;
                builder.Append(nextIndent).Append('"').Append(key).Append("\": ");
                if (value is JsonObject || value is JsonArray)
                {
                    value.PrettyStringify(builder, depth + 1, indent);
                }
                else
                {
                    value.Stringify(builder);
                }
            }
            builder.Append("\n").Append(JsonUtils.GetIndent(depth, indent)).Append('}');
        }

        /// <summary>
        /// Deserializes this JsonObject into an existing object instance using the specified members.
        /// </summary>
        /// <param name="obj">The object to populate.</param>
        /// <param name="members">The members to consider for deserialization.</param>
        /// <param name="bindingFlags">Binding flags for member lookup.</param>
        public void To(object obj, IEnumerable<MemberInfo> members, BindingFlags bindingFlags = JsonSerializer.InstanceLookup)
        {
            foreach (MemberInfo member in members)
            {
                MemberTypes memberType = member.MemberType;
                if (memberType != MemberTypes.Field && memberType != MemberTypes.Property) continue;
                if (memberType == MemberTypes.Property && !((PropertyInfo)member).CanWrite) continue;
                Json item;

                object[] attributes = member.GetCustomAttributes(typeof(AliasAttribute), true);
                AliasAttribute alias = attributes.Length > 0 ? attributes[0] as AliasAttribute : null;

                if (alias == null || !alias.IgnoreFieldName)
                {
                    if (_dictionary.Contains(member.Name))
                    {
                        item = _dictionary[member.Name] as Json;
                        Type t = memberType == MemberTypes.Field ? ((FieldInfo)member).FieldType : ((PropertyInfo)member).PropertyType;
                        if (memberType == MemberTypes.Field) ((FieldInfo)member).SetValue(obj, item.To(t, bindingFlags));
                        else ((PropertyInfo)member).SetValue(obj, item.To(t, bindingFlags), null);
                        continue;
                    }
                }

                if (alias == null) continue;
                for (int j = 0; j < alias.Aliases.Length; j++)
                {
                    string key = alias.Aliases[j];
                    if (!_dictionary.Contains(key)) continue;

                    item = _dictionary[key] as Json;
                    Type t = memberType == MemberTypes.Field ? ((FieldInfo)member).FieldType : ((PropertyInfo)member).PropertyType;
                    if (memberType == MemberTypes.Field) ((FieldInfo)member).SetValue(obj, item.To(t, bindingFlags));
                    else ((PropertyInfo)member).SetValue(obj, item.To(t, bindingFlags), null);
                    break;
                }
            }
        }

        /// <summary>
        /// Converts this JsonObject to an instance of the specified type.
        /// </summary>
        /// <param name="type">The target type.</param>
        /// <param name="bindingFlags">Binding flags for member lookup.</param>
        /// <returns>An instance of the target type.</returns>
        public override object To(Type type, BindingFlags bindingFlags = JsonSerializer.InstanceLookup)
        {
            IEnumerable<MemberInfo> members = type.GetMembers(bindingFlags);
            return To(type, members, bindingFlags);
        }
        
        /// <summary>
        /// Converts this JsonObject to an instance of the specified type using the specified members.
        /// </summary>
        /// <param name="type">The target type.</param>
        /// <param name="members">The members to consider for deserialization.</param>
        /// <param name="bindingFlags">Binding flags for member lookup.</param>
        /// <returns>An instance of the target type.</returns>
        public object To(Type type, IEnumerable<MemberInfo> members, BindingFlags bindingFlags = JsonSerializer.InstanceLookup)
        {
            object v = Activator.CreateInstance(type);
            To(v, members, bindingFlags);
            return v;
        }

        /// <summary>
        /// Attempts to get a member value dynamically.
        /// </summary>
        /// <param name="binder">The binder for the get operation.</param>
        /// <param name="result">The result if found.</param>
        /// <returns>True if the member was found; otherwise, false.</returns>
        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            if (_dictionary.Contains(binder.Name))
            {
                result = _dictionary[binder.Name];
                return true;
            }

            result = null;
            return false;
        }

        /// <summary>
        /// Attempts to set a member value dynamically.
        /// </summary>
        /// <param name="binder">The binder for the set operation.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>True if the member was set; otherwise, false.</returns>
        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            Set(binder.Name, value);
            return true;
        }

        /// <summary>
        /// Returns all values of the specified type recursively.
        /// </summary>
        /// <typeparam name="T">The type of values to return.</typeparam>
        /// <returns>An enumerable of values of type T.</returns>
        public override IEnumerable<T> Values<T>()
        {
            return _dictionary.Cast<DictionaryEntry>().SelectMany(p => ((Json)p.Value).Values<T>());
        }
    }
}
