using System;

namespace SilkJson
{
    public abstract partial class Json
    {
        public static bool ThrowOnInvalidCast = false;
        
        public T Cast<T>(T defaultValue = default, bool? throwOnInvalidCast = null) where T : struct
        {
            JsonValue jsonValue = this as JsonValue;
            bool useThrow = throwOnInvalidCast.HasValue ? throwOnInvalidCast.Value : ThrowOnInvalidCast;
            
            if (useThrow)
            {
                if (jsonValue == null) throw new InvalidCastException($"Cannot cast Json to type {typeof(T).Name}");
                if (jsonValue.IsMissed) throw new InvalidCastException($"Cannot cast null Json to type {typeof(T).Name}");
            }
            if (jsonValue == null || jsonValue.IsMissed) return defaultValue;
        
            try
            {
                object value = jsonValue.Value;
                return (T) Convert.ChangeType(value, typeof(T));
            }
            catch (Exception e)
            {
                if (useThrow) throw new InvalidCastException($"Cannot cast Json value '{jsonValue.Value}' of type {jsonValue.Value.GetType().Name} to type {typeof(T).Name}", e);
                return defaultValue;
            }
        }

        #region Type Check Methods

        public virtual bool IsBool() => false;
        public virtual bool IsDouble() => false;
        public virtual bool IsLong() => false;
        public virtual bool IsString() => false;

        #endregion

        #region Type Cast Methods

        public JsonObject Obj() => this as JsonObject;
        public JsonArray Arr() => this as JsonArray;
        public bool Bool(bool defaultValue = false) => Cast(defaultValue, false);
        public byte Byte(byte defaultValue = 0) => Cast(defaultValue, false);
        public char Char(char defaultValue = '\0') => Cast(defaultValue, false);
        public short Short(short defaultValue = 0) => Cast(defaultValue, false);
        public ushort UShort(ushort defaultValue = 0) => Cast(defaultValue, false);
        public int Int(int defaultValue = 0) => Cast(defaultValue, false);
        public uint UInt(uint defaultValue = 0) => Cast(defaultValue, false);
        public long Long(long defaultValue = 0) => Cast(defaultValue, false);
        public ulong ULong(ulong defaultValue = 0) => Cast(defaultValue, false);
        public float Float(float defaultValue = 0) => Cast(defaultValue, false);
        public double Double(double defaultValue = 0) => Cast(defaultValue, false);
        public decimal Decimal(decimal defaultValue = 0) => Cast(defaultValue, false);
        public DateTime DateTime(DateTime defaultValue = default) => Cast(defaultValue, false);

        public string String(string defaultValue = null)
        {
            JsonValue jsonValue = this as JsonValue;
            if (jsonValue == null || jsonValue.IsMissed) return defaultValue;
            return jsonValue.ToString();
        }

        #endregion

        #region Implicit and Explicit Operators

        public static implicit operator Json(bool value) => From(value);
        public static implicit operator Json(byte value) => From(value);
        public static implicit operator Json(char value) => From(value);
        public static implicit operator Json(short value) => From(value);
        public static implicit operator Json(ushort value) => From(value);
        public static implicit operator Json(int value) => From(value);
        public static implicit operator Json(uint value) => From(value);
        public static implicit operator Json(long value) => From(value);
        public static implicit operator Json(ulong value) => From(value);
        public static implicit operator Json(float value) => From(value);
        public static implicit operator Json(double value) => From(value);
        public static implicit operator Json(decimal value) => From(value);
        public static implicit operator Json(string value) => From(value);
        public static implicit operator Json(DateTime value) => From(value);
        public static implicit operator bool (Json json) => json.Cast<bool>();
        public static implicit operator byte (Json json) => json.Cast<byte>();
        public static implicit operator char (Json json) => json.Cast<char>();
        public static implicit operator short (Json json) => json.Cast<short>();
        public static implicit operator ushort (Json json) => json.Cast<ushort>();
        public static implicit operator int (Json json) => json.Cast<int>();
        public static implicit operator uint (Json json) => json.Cast<uint>();
        public static implicit operator long (Json json) => json.Cast<long>();
        public static implicit operator ulong (Json json) => json.Cast<ulong>();
        public static implicit operator float (Json json) => json.Cast<float>();
        public static implicit operator double (Json json) => json.Cast<double>();
        public static implicit operator decimal (Json json) => json.Cast<decimal>();
        public static implicit operator DateTime (Json json) => System.DateTime.Parse((string) json);
        public static implicit operator string (Json json) => json.ToString();

        #endregion
    }
}