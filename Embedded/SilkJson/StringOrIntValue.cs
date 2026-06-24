using System;

namespace SilkJson
{
    public class StringOrIntValue
    {
        private readonly object _value;

        public bool IsInt => !IsString;
        public bool IsString { get; }

        private StringOrIntValue(object value)
        {
            _value = value;
            IsString = value is string;
        }

        public static implicit operator StringOrIntValue(int value) => new StringOrIntValue(value);
        public static implicit operator StringOrIntValue(string value) => new StringOrIntValue(value);
        public static implicit operator int(StringOrIntValue value) => !value.IsString ? (int)value._value : throw new InvalidCastException("Value is not an int");
        public static implicit operator string(StringOrIntValue value) => value.IsString ? (string)value._value : throw new InvalidCastException("Value is not a string");
    }
}
