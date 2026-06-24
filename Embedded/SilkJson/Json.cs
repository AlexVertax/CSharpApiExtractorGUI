using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;
using System.Text;

namespace SilkJson
{
    /// <summary>
    /// Abstract base class representing a JSON. Supports dynamic access and IEnumerable.
    /// </summary>
    public abstract partial class Json: DynamicObject, IEnumerable<Json>
    {
        protected static readonly JsonValue _missedValue = new JsonValue(null);
    
        /// <summary>
        /// Gets whether this JSON value is null.
        /// </summary>
        public virtual bool IsMissed => false;
        
        /// <summary>
        /// Gets or sets a JSON by string key.
        /// </summary>
        /// <param name="key">The key to access.</param>
        /// <returns>The JSON at the specified key.</returns>
        public Json this[string key]
        {
            get => Get(key);
            set => CreateAndSet(JsonUtils.PrepareKeys(new StringOrIntValue[]{key}), value);
        }

        /// <summary>
        /// Gets or sets a JSON by integer index.
        /// </summary>
        /// <param name="index">The index to access.</param>
        /// <returns>The JSON at the specified index.</returns>
        public Json this[int index]
        {
            get => Get(index);
            set => CreateAndSet(JsonUtils.PrepareKeys(new StringOrIntValue[]{index}), value);
        }
        
        /// <summary>
        /// Gets or sets a JSON using a path of keys.
        /// Creates intermediate objects if they don't exist.
        /// </summary>
        /// <param name="keys">The path of keys to traverse.</param>
        /// <returns>The JSON at the end of the path.</returns>
        public Json this[params StringOrIntValue[] keys]
        {
            get => Get(keys);
            set => CreateAndSet(keys, value);
        }

        private void CreateAndSet(StringOrIntValue[] keys, Json value)
        {
            if (keys.Length == 0) return;

            Json current = this;
            for (int i = 0; i < keys.Length - 1; i++)
            {
                StringOrIntValue key = keys[i];
                current = GetOrCreate(current, key);
            }
        
            StringOrIntValue lastKey = keys[keys.Length - 1];
            current.SetChild(lastKey, value);
        }

        /// <summary>
        /// Returns the direct child for the given key or index.
        /// </summary>
        /// <param name="key">The string key or integer index of the child to retrieve.</param>
        /// <returns>The child Json if present; otherwise a null JsonValue.</returns>
        public abstract Json Child(StringOrIntValue key);

        /// <summary>
        /// Returns an enumerable collection of all direct children.
        /// </summary>
        /// <returns>A JsonEnumerable containing all children.</returns>
        public abstract JsonEnumerable Children();
        
        /// <summary>
        /// Checks if this JSON contains a child with the specified key.
        /// </summary>
        /// <param name="key">The key to check for.</param>
        /// <returns>True if a child with the specified key exists, false otherwise.</returns>
        public virtual bool Contains(StringOrIntValue key) => false;
        
        /// <summary>
        /// Converts this Json instance to an instance of type T
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <returns>An instance of type T representing this Json.</returns>
        public T Deserialize<T>() => To<T>();

        /// <summary>
        /// Checks if Json is exists and equals to the specified value.
        /// </summary>
        /// <param name="value">The value to compare to.</param>
        /// <returns>True if this Json is not null and equals to the specified value, false otherwise.</returns>
        public bool EqualsTo(object value)
        {
            if (value == null) return IsMissed;
            if (IsMissed) return false;
        
            JsonValue jsonValue = this as JsonValue;
            return jsonValue != null && jsonValue.Value.Equals(value);
        }

        /// <summary>
        /// Finds all nodes with the specified key recursively.
        /// </summary>
        /// <param name="key">The key to search for.</param>
        /// <returns>A JsonEnumerable containing all matching nodes.</returns>
        public JsonEnumerable FindDeep(string key) => FindDeep((_, k) => k.IsString && k == key);

        /// <summary>
        /// Finds all nodes matching the provided predicate recursively.
        /// </summary>
        /// <param name="match">Predicate invoked with the current node and its key; return true to include the node.</param>
        /// <returns>A JsonEnumerable of matching nodes.</returns>
        public abstract JsonEnumerable FindDeep(FindMatch match);

        /// <summary>
        /// Performs the specified action on each direct child in this Json.
        /// </summary>
        /// <param name="action">The action to perform for each child.</param>
        public void ForEach(Action<Json> action)
        {
            foreach (Json child in this) action(child);
        }

        /// <summary>
        /// Creates a Json instance from an object.
        /// </summary>
        /// <param name="obj">The object to convert.</param>
        /// <returns>A Json representation of the object.</returns>
        public static Json From(object obj) => JsonSerializer.FromObject(obj);

        /// <summary>
        /// Gets a nested node using a path of keys.
        /// </summary>
        /// <param name="keys">The path of keys to traverse.</param>
        /// <returns>The node at the end of the path, or nullValue if not found.</returns>
        public Json Get(params StringOrIntValue[] keys) => Get(JsonUtils.PrepareKeys(keys), 0);

        private Json Get(StringOrIntValue[] keys, int index)
        {
            if (IsMissed || index >= keys.Length) return _missedValue;
            StringOrIntValue key = keys[index];
            Json child = _missedValue;
            if (key.IsString)
            {
                string sKey = key;
                if (sKey == "*") child = Children();
                else if (sKey[0] == '|') child = FindDeep(sKey.Substring(1));
                else child = Child(sKey);
            }
            else if (key.IsInt) child = Child((int)key);
            if (child.IsMissed) return _missedValue;
            return index + 1 < keys.Length ? child.Get(keys, index + 1) : child;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Returns an enumerator that iterates through the direct children of this Json.
        /// </summary>
        /// <returns>An enumerator for Json children.</returns>
        public abstract IEnumerator<Json> GetEnumerator();

        private static Json GetOrCreate(Json current, StringOrIntValue key)
        {
            Json item = current.Child(key);
            if (!item.IsMissed) return item;
            
            Json newChild = key.IsString ? (Json)new JsonObject() : new JsonArray();
            current.SetChild(key, newChild);
            item = newChild;

            return item;
        }

        /// <summary>
        /// Parses a JSON string into a Json instance.
        /// </summary>
        /// <param name="jsonString">The JSON string to parse.</param>
        /// <returns>A Json instance representing the parsed string.</returns>
        public static Json Parse(string jsonString) => JsonParser.Parse(jsonString);

        /// <summary>
        /// Returns a formatted JSON string with indentation without building a full Json tree.
        /// </summary>
        /// <param name="jsonString">The JSON string to format.</param>
        /// <param name="indent">The indentation string (default is 2 spaces).</param>
        /// <returns>A formatted JSON string.</returns>
        public static string Prettify(string jsonString, string indent = "  ") => JsonFormatter.Pretty(jsonString, indent);

        /// <summary>
        /// Returns a compact JSON string without insignificant whitespace and comments.
        /// </summary>
        /// <param name="jsonString">The JSON string to compact.</param>
        /// <returns>A compact JSON string.</returns>
        public static string Compact(string jsonString) => JsonFormatter.Compact(jsonString);
        
        /// <summary>
        /// Returns a formatted JSON string with indentation.
        /// </summary>
        /// <param name="indent">The indentation string (default is 2 spaces).</param>
        /// <returns>A formatted JSON string.</returns>
        public string Pretty(string indent = "  ")
        {
            StringBuilder builder = new StringBuilder();
            PrettyStringify(builder, 0, indent);
            return builder.ToString();
        }

        /// <summary>
        /// Internal method to build a formatted JSON string.
        /// </summary>
        public virtual void PrettyStringify(StringBuilder builder, int depth, string indent)
        {
            Stringify(builder);
        }

        /// <summary>
        /// Removes all nodes matching the specified predicate.
        /// </summary>
        /// <param name="match">A predicate that returns true for items to remove.</param>
        /// <returns>The number of items removed.</returns>
        public abstract int RemoveAll(RemoveMatch match);
        
        /// <summary>
        /// Serializes an object to a Json instance.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <returns>>A Json instance representing the serialized object.</returns>
        public static Json Serialize(object obj) => JsonSerializer.FromObject(obj);

        public abstract void SetChild(StringOrIntValue key, object value);
        public abstract void Stringify(StringBuilder builder);

        /// <summary>
        /// Converts this Json instance to the specified type.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <returns>An instance of the target type.</returns>
        public T To<T>() => (T)To(typeof(T));

        /// <summary>
        /// Converts this Json instance to the specified type.
        /// </summary>
        /// <param name="type">The target type.</param>
        /// <param name="bindingFlags">Binding flags for member lookup during conversion.</param>
        /// <returns>An instance of the target type.</returns>
        public abstract object To(Type type, BindingFlags bindingFlags = JsonSerializer.InstanceLookup);
        /// <summary>
        /// Deserializes a JSON string to an instance of type T.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="json">The JSON string to deserialize.</param>
        public static T To<T>(string json) => JsonSerializer.ToObject<T>(json);
        
        /// <summary>
        /// Deserializes a JSON string to an existing object instance.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <param name="target">The object to populate.</param>
        public static void To(string json, object target) => JsonSerializer.ToObject(json, target);
    
        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            Stringify(builder);
            return builder.ToString();
        }

        public string ToString(bool pretty)
        {
            return pretty ? Pretty() : ToString();
        }

        /// <summary>
        /// Attempts to convert this Json to the target CLR type requested by the binder.
        /// Supports conversion to string, DateTime, and primitive numeric types when the Json is a JsonValue.
        /// </summary>
        /// <param name="binder">Provides the target conversion type information.</param>
        /// <param name="result">The converted value if conversion succeeds; otherwise unspecified.</param>
        /// <returns>True if conversion succeeded; otherwise false.</returns>
        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            Type targetType = binder.Type;
            if (targetType == typeof(string))
            {
                result = ToString();
                return true;
            }
        
            JsonValue jsonValue = this as JsonValue;
            if (jsonValue == null) return base.TryConvert(binder, out result); 

            if (targetType == typeof(DateTime))
            {
                bool ret = System.DateTime.TryParse(ToString(), out DateTime dt);
                result = dt;
                return ret;
            }

            if (targetType.IsPrimitive)
            {
                result = Convert.ChangeType(jsonValue.Value, targetType);
                return true;
            }
        
            return base.TryConvert(binder, out result);
        }

        /// <summary>
        /// Updates value using the specified function.
        /// </summary>
        /// <param name="func">A function that transforms values.</param>
        public virtual void Update(Func<object, object> func) { }

        /// <summary>
        /// Returns all values of the specified type recursively.
        /// </summary>
        /// <typeparam name="T">The type of values to return.</typeparam>
        /// <returns>Enumerable of values of type T.</returns>
        public abstract IEnumerable<T> Values<T>();

        /// <summary>
        /// Delegate for matching keys and values during search.
        /// </summary>
        /// <param name="json">The current JSON node being evaluated.</param>
        /// <param name="key">The key of the current node.</param>
        /// <returns>True if the node matches the search criteria.</returns>
        public delegate bool FindMatch(Json json, StringOrIntValue key);

        /// <summary>
        /// Delegate for matching keys and values during removal.
        /// </summary>
        /// <param name="key">The key of the item.</param>
        /// <param name="value">The value of the item.</param>
        /// <returns>True if the item should be removed.</returns>
        public delegate bool RemoveMatch(object key, Json value);
    }
}
