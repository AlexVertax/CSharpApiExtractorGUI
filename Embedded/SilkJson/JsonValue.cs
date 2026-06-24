using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace SilkJson
{
    /// <summary>
    /// Represents a JSON primitive value (string, number, boolean, or null).
    /// </summary>
    public class JsonValue: Json
    {
        private JsonValueType _type = JsonValueType.Null;
        private object _value;

        /// <summary>
        /// Gets whether this JSON value is null.
        /// </summary>
        public override bool IsMissed => _type == JsonValueType.Null;

        /// <summary>
        /// Gets or sets the underlying value.
        /// </summary>
        public object Value
        {
            get => _value;
            set => SetValue(value);
        }
    
        /// <summary>
        /// Initializes a new instance of JsonValue with the specified value.
        /// </summary>
        /// <param name="value">The value to store.</param>
        public JsonValue(object value) => SetValue(value);

        /// <summary>
        /// Initializes a new instance of JsonValue with the specified value and type.
        /// </summary>
        /// <param name="value">The value to store.</param>
        /// <param name="type">The type of the value.</param>
        public JsonValue(object value, JsonValueType type)
        {
            _type = type;
            _value = value;
        }
    
        /// <summary>
        /// Returns an enumerable collection of all direct children.
        /// </summary>
        /// <returns>An empty JsonEnumerable.</returns>
        public override JsonEnumerable Children() => new JsonEnumerable(Array.Empty<Json>());

        public override Json Child(StringOrIntValue key) => _missedValue;

        /// <summary>
        /// Finds all nodes that match the specified predicate recursively.
        /// </summary>
        /// <param name="match">A predicate that returns true for matching nodes, taking the value and key as parameters.</param>
        /// <returns>A JsonEnumerable containing all matching nodes.</returns>
        public override JsonEnumerable FindDeep(FindMatch match) => new JsonEnumerable(Array.Empty<Json>());

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An empty enumerator.</returns>
        public override IEnumerator<Json> GetEnumerator()
        {
            yield break;
        }

        /// <summary>
        /// Converts the stored value to the specified type.
        /// </summary>
        /// <param name="t">The target type.</param>
        /// <returns>The converted value.</returns>
        public object GetValue(Type t)
        {
            if (_type == JsonValueType.Null || _value == null) return t.IsValueType ? Activator.CreateInstance(t) : null;
            if (t == typeof(string)) return Convert.ChangeType(_value, t);

            if (_type == JsonValueType.Boolean)
            {
                if (t == typeof(bool)) return Convert.ChangeType(_value, t);
            }
            else if (_type == JsonValueType.Double)
            {
                if (t == typeof(double)) return Convert.ChangeType(_value, t, CultureInfo.InvariantCulture.NumberFormat);
                if (t == typeof(float)) return Convert.ChangeType((double) _value, t, CultureInfo.InvariantCulture.NumberFormat);
            }
            else if (_type == JsonValueType.Long)
            {
                if (t == typeof(long)) return Convert.ChangeType(_value, t);

                try
                {
                    return Convert.ChangeType((long) _value, t);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{e.Message}\n{e.StackTrace}");
                    return null;
                }
            }
            else if (_type == JsonValueType.String)
            {
                if (t.IsEnum)
                {
                    return Enum.Parse(t, Value as string);
                }

                MethodInfo method = t.GetMethod("Parse", new[] { typeof(string), typeof(IFormatProvider) });
                if (method != null) return method.Invoke(null, new object[] { Value, CultureInfo.InvariantCulture.NumberFormat });

                method = t.GetMethod("Parse", new[] { typeof(string) });
                return method.Invoke(null, new[] { Value });

            }

            StringBuilder builder = new StringBuilder();
            Stringify(builder);
            throw new InvalidCastException(t.FullName + "\n" + builder);
        }

        /// <summary>
        /// Removes all items that match the specified predicate (not supported for values).
        /// </summary>
        /// <param name="match">A predicate that returns true for items to remove.</param>
        /// <returns>Always returns 0 for JsonValue.</returns>
        public override int RemoveAll(RemoveMatch match) => 0;

        public override void SetChild(StringOrIntValue key, object value)
        {
            
        }

        private void SetValue(object value)
        {
            if (this == _missedValue) return;
            if (value == null)
            {
                _type = JsonValueType.Null;
                _value = value;
            }
            else if (value is string)
            {
                _type = JsonValueType.String;
                _value = value;
            }
            else if (value is char)
            {
                _type = JsonValueType.String;
                _value = value.ToString();
            }
            else if (value is double)
            {
                _type = JsonValueType.Double;
                _value = (double) value;
            }
            else if (value is float)
            {
                _type = JsonValueType.Double;
                _value = (double) (float) value;
            }
            else if (value is decimal)
            {
                _type = JsonValueType.Double;
                _value = Convert.ToDouble((decimal) value);
            }
            else if (value is bool)
            {
                _type = JsonValueType.Boolean;
                _value = value;
            }
            else if (value is long)
            {
                _type = JsonValueType.Long;
                _value = value;
            }
            else if (value is int || value is short || value is byte || value is uint || value is ushort || value is sbyte)
            {
                _type = JsonValueType.Long;
                _value = Convert.ChangeType(value, typeof(long));
            }
            else if (value is ulong ul)
            {
                if (ul <= long.MaxValue)
                {
                    _type = JsonValueType.Long;
                    _value = (long)ul;
                }
                else
                {
                    _type = JsonValueType.String;
                    _value = ul.ToString();
                }
            }
            else throw new Exception("Unknown type of value.");
        }

        /// <summary>
        /// Serializes this JsonValue to a JSON string.
        /// </summary>
        /// <param name="builder">The StringBuilder to write to.</param>
        public override void Stringify(StringBuilder builder)
        {
            if (_type == JsonValueType.String) WriteString(builder);
            else if (_type == JsonValueType.Null) builder.Append("null");
            else if (_type == JsonValueType.Boolean) builder.Append((bool) _value ? "true" : "false");
            else if (_type == JsonValueType.Double) builder.Append(((double) Value).ToString(CultureInfo.InvariantCulture));
            else builder.Append(Value);
        }

        /// <summary>
        /// Converts this JsonValue to the specified type.
        /// </summary>
        /// <param name="type">The target type.</param>
        /// <param name="bindingFlags">Binding flags for member lookup.</param>
        /// <returns>An instance of the target type.</returns>
        public override object To(Type type, BindingFlags bindingFlags = JsonSerializer.InstanceLookup) => GetValue(type);

        /// <summary>
        /// Returns a string representation of this JsonValue.
        /// </summary>
        /// <returns>A string representation of the value.</returns>
        public override string ToString()
        {
            if (_type == JsonValueType.Null) return null;
            if (_type == JsonValueType.Double) return ((double) _value).ToString(CultureInfo.InvariantCulture);
            if (_type == JsonValueType.Boolean) return (bool) _value ? "true" : "false";
            return _value?.ToString();
        }

        /// <summary>
        /// Updates the value using the specified function.
        /// </summary>
        /// <param name="func">A function that transforms the value.</param>
        public override void Update(Func<object, object> func) => SetValue(func.Invoke(Value));

        /// <summary>
        /// Returns all values of the specified type recursively.
        /// </summary>
        /// <typeparam name="T">The type of values to return.</typeparam>
        /// <returns>An enumerable containing the value if it matches the type.</returns>
        public override IEnumerable<T> Values<T>()
        {
            yield return (T)GetValue(typeof(T));
        }

        /// <summary>
        /// Compares this value to another value for equality.
        /// </summary>
        /// <param name="otherValue">The value to compare.</param>
        /// <returns>True if the values are equal; otherwise, false.</returns>
        public bool ValueEquals(object otherValue)
        {
            if (_value == null && otherValue == null) return true;
            if (_value == null || otherValue == null) return false;
        
            if (_value is string s1 && otherValue is string s2) return s1 == s2;
            if (_value is bool b1 && otherValue is bool b2) return b1 == b2;
        
            if (_value is long l1)
            {
                if (otherValue is int i2) return l1 == i2;
                if (otherValue is uint ui2) return l1 == ui2;
                if (otherValue is long l2) return l1 == l2;
                if (otherValue is short sh2) return l1 == sh2;
                if (otherValue is ushort us2) return l1 == us2;
                if (otherValue is byte by2) return l1 == by2;
                if (otherValue is ulong ul2) return l1 >= 0 && (ulong)l1 == ul2;
                return false;
            }
        
            if (_value is double d1)
            {
                if (otherValue is float f2) return Math.Abs(d1 - f2) < double.Epsilon;
                if (otherValue is double d2) return Math.Abs(d1 - d2) < double.Epsilon;
                return false;
            }

            return false;
        }

        private void WriteString(StringBuilder builder)
        {
            builder.Append('\"');

            string s = Value as string;

            int runIndex = -1;
            int l = s.Length;
            for (int index = 0; index < l; ++index)
            {
                char c = s[index];

                if (c >= ' ' && c < 128 && c != '\"' && c != '\\')
                {
                    if (runIndex == -1) runIndex = index;
                    continue;
                }

                if (runIndex != -1)
                {
                    builder.Append(s, runIndex, index - runIndex);
                    runIndex = -1;
                }

                switch (c)
                {
                    case '\t':
                        builder.Append("\\t");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '"':
                    case '\\':
                        builder.Append('\\');
                        builder.Append(c);
                        break;
                    default:
                        builder.Append("\\u");
                        builder.Append(((int) c).ToString("X4", NumberFormatInfo.InvariantInfo));
                        break;
                }
            }

            if (runIndex != -1) builder.Append(s, runIndex, s.Length - runIndex);
            builder.Append('\"');
        }

        /// <summary>
        /// Gets whether this value is a boolean.
        /// </summary>
        /// <returns>True if the value is a boolean; otherwise, false.</returns>
        public override bool IsBool() => _type == JsonValueType.Boolean;
        
        /// <summary>
        /// Gets whether this value is a double.
        /// </summary>
        /// <returns>True if the value is a double; otherwise, false.</returns>
        public override bool IsDouble() => _type == JsonValueType.Double;
        
        /// <summary>
        /// Gets whether this value is a long.
        /// </summary>
        /// <returns>True if the value is a long; otherwise, false.</returns>
        public override bool IsLong() => _type == JsonValueType.Long;
        
        /// <summary>
        /// Gets whether this value is a string.
        /// </summary>
        /// <returns>True if the value is a string; otherwise, false.</returns>
        public override bool IsString() => _type == JsonValueType.String;
    }
}
