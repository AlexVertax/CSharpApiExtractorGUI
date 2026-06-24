using System;
using System.Collections.Generic;
using System.Text;

namespace SilkJson
{
    /// <summary>
    /// Provides functionality for parsing JSON strings into Json objects.
    /// </summary>
    public class JsonParser
    {
        private readonly StringBuilder _builder;
        private int _index = 0;
        private readonly string _json;
        private readonly int _length;
        private Token _lookAheadToken = Token.None;
        private char c;
        
        /// <summary>
        /// Initializes a new instance of JsonParser with the specified JSON string.
        /// </summary>
        /// <param name="json">The JSON string to parse.</param>
        protected JsonParser(string json)
        {
            _builder = new StringBuilder(Math.Min(1 << 10, json.Length));
            _json = json;
            _length = json.Length;
        }

        private Token LookAhead()
        {
            if (_lookAheadToken != Token.None) return _lookAheadToken;
            return _lookAheadToken = NextTokenCore();
        }

        private Token NextToken()
        {
            Token result = _lookAheadToken != Token.None ? _lookAheadToken : NextTokenCore();
            _lookAheadToken = Token.None;
            return result;
        }

        private Token NextTokenCore()
        {
            do
            {
                c = _json[_index];

                if (c == '/' && _json[_index + 1] == '/')
                {
                    _index += 2;
                    do
                    {
                        c = _json[_index];
                        if (c == '\r' || c == '\n') break;
                    } while (++_index < _length);
                }

                if (c > ' ') break;
                if (c != ' ' && c != '\t' && c != '\n' && c != '\r') break;
            } while (++_index < _length);

            if (_index == _length) throw new Exception("Reached end of string unexpectedly");

            c = _json[_index++];

            switch (c)
            {
                case '{':
                    return Token.Curly_Open;

                case '}':
                    return Token.Curly_Close;

                case '[':
                    return Token.Squared_Open;

                case ']':
                    return Token.Squared_Close;

                case ',':
                    return Token.Comma;

                case '"':
                    return Token.String;

                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                case '-':
                case '+':
                case '.':
                    return Token.Number;

                case ':':
                    return Token.Colon;

                case 'f':
                    if (_length - _index >= 4 &&
                        _json[_index + 0] == 'a' &&
                        _json[_index + 1] == 'l' &&
                        _json[_index + 2] == 's' &&
                        _json[_index + 3] == 'e')
                    {
                        _index += 4;
                        return Token.False;
                    }

                    break;

                case 't':
                    if (_length - _index >= 3 &&
                        _json[_index + 0] == 'r' &&
                        _json[_index + 1] == 'u' &&
                        _json[_index + 2] == 'e')
                    {
                        _index += 3;
                        return Token.True;
                    }

                    break;

                case 'n':
                    if (_length - _index >= 3 &&
                        _json[_index + 0] == 'u' &&
                        _json[_index + 1] == 'l' &&
                        _json[_index + 2] == 'l')
                    {
                        _index += 3;
                        return Token.Null;
                    }

                    break;
            }

            throw new Exception("Could not find token at index " + --_index);
        }
    
        /// <summary>
        /// Parses a JSON string into a Json instance.
        /// </summary>
        /// <param name="jsonString">The JSON string to parse.</param>
        /// <returns>A Json instance representing the parsed string.</returns>
        public static Json Parse(string jsonString)
        {
            if (string.IsNullOrEmpty(jsonString)) return new JsonValue(null, JsonValueType.Null);

            JsonParser parser = new JsonParser(jsonString);
            return parser.ParseValue();
        }

        /// <summary>
        /// Parses a JSON array.
        /// </summary>
        /// <returns>A JsonArray containing the parsed elements.</returns>
        private JsonArray ParseArray()
        {
            JsonArray array = new JsonArray();
            _lookAheadToken = Token.None;

            while (true)
            {
                switch (LookAhead())
                {
                    case Token.Comma:
                        _lookAheadToken = Token.None;
                        break;

                    case Token.Squared_Close:
                        _lookAheadToken = Token.None;
                        return array;

                    default:
                        array.Add(ParseValue());
                        break;
                }
            }
        }

        /// <summary>
        /// Parses a JSON array as raw objects.
        /// </summary>
        /// <returns>A list of parsed objects.</returns>
        private List<object> ParseArrayDirect()
        {
            List<object> array = new List<object>();
            _lookAheadToken = Token.None;

            while (true)
            {
                switch (LookAhead())
                {
                    case Token.Comma:
                        _lookAheadToken = Token.None;
                        break;

                    case Token.Squared_Close:
                        _lookAheadToken = Token.None;
                        return array;

                    default:
                        array.Add(ParseValueDirect());
                        break;
                }
            }
        }

        /// <summary>
        /// Parses a JSON string into a raw object structure.
        /// </summary>
        /// <param name="jsonString">The JSON string to parse.</param>
        /// <returns>A dictionary or list representing the parsed JSON.</returns>
        public static object ParseDirect(string jsonString)
        {
            if (string.IsNullOrEmpty(jsonString)) return null;

            JsonParser parser = new JsonParser(jsonString);
            return parser.ParseValueDirect();
        }

        /// <summary>
        /// Parses a JSON number.
        /// </summary>
        /// <returns>The parsed number as long or double.</returns>
        private object ParseNumber()
        {
            _lookAheadToken = Token.None;
            _index--;

            long n = 0;
            bool neg = false;
            long decimalV = 0;
            long exp = 0;
            bool negExp = false;

            while (_index < _length)
            {
                c = _json[_index];

                if (c >= '0' && c <= '9')
                {
                    n = n * 10 + (c - '0');
                    decimalV *= 10;
                }
                else if (c == '.')
                {
                    decimalV = 1;
                }
                else if (c == '-') neg = true;
                else if (c == '+') neg = false;
                else if (c == 'e' || c == 'E')
                {
                    if (decimalV == 0) decimalV = 1;
                    _index++;
                    exp = 0;
                    while (_index < _length)
                    {
                        c = _json[_index];
                        if (c >= '0' && c <= '9') exp = exp * 10 + (c - '0');
                        else if (c == '-') negExp = true;
                        else if (c == '+') negExp = false;
                        else break;
                        _index++;
                    }

                    break;
                }
                else break;

                _index++;
            }

            if (neg) n = -n;
            if (decimalV == 0) return n;
            
            double v = n / (double)decimalV;
            if (exp <= 0) return v;
            if (negExp) v /= Math.Pow(10, exp);
            else v *= Math.Pow(10, exp);

            return v;
        }

        /// <summary>
        /// Parses a JSON object.
        /// </summary>
        /// <returns>A JsonObject containing the parsed key-value pairs.</returns>
        private JsonObject ParseObject()
        {
            JsonObject obj = new JsonObject();

            _lookAheadToken = Token.None;

            while (true)
            {
                switch (LookAhead())
                {
                    case Token.Comma:
                        _lookAheadToken = Token.None;
                        break;

                    case Token.Curly_Close:
                        _lookAheadToken = Token.None;
                        return obj;

                    default:
                    {
                        string name = ParseString();
                        if (NextToken() != Token.Colon) throw new Exception("Expected colon at index " + _index);
                        obj.Set(name, ParseValue());
                    }
                        break;
                }
            }
        }

        /// <summary>
        /// Parses a JSON object as raw objects.
        /// </summary>
        /// <returns>A dictionary of parsed key-value pairs.</returns>
        private Dictionary<string, object> ParseObjectDirect()
        {
            Dictionary<string, object> obj = new Dictionary<string, object>();

            _lookAheadToken = Token.None;

            while (true)
            {
                switch (LookAhead())
                {
                    case Token.Comma:
                        _lookAheadToken = Token.None;
                        break;

                    case Token.Curly_Close:
                        _lookAheadToken = Token.None;
                        return obj;

                    default:
                    {
                        string name = ParseString();
                        if (NextToken() != Token.Colon) throw new Exception("Expected colon at index " + _index);
                        obj.Add(name, ParseValueDirect());
                    }
                        break;
                }
            }
        }

        private uint ParseSingleChar(char c1, uint multiplier)
        {
            uint p1 = 0;
            if (c1 >= '0' && c1 <= '9') p1 = (uint)(c1 - '0') * multiplier;
            else if (c1 >= 'A' && c1 <= 'F') p1 = (uint)(c1 - 'A' + 10) * multiplier;
            else if (c1 >= 'a' && c1 <= 'f') p1 = (uint)(c1 - 'a' + 10) * multiplier;
            return p1;
        }

        /// <summary>
        /// Parses a JSON string.
        /// </summary>
        /// <returns>The parsed string value.</returns>
        private string ParseString()
        {
            _lookAheadToken = Token.None;
            _builder.Length = 0;
            int runIndex = _index;
            
            while (_index < _length)
            {
                c = _json[_index++];

                if (c == '"')
                {
                    if (_builder.Length == 0) return _json.Substring(runIndex, _index - runIndex - 1);
                    return _builder.Append(_json, runIndex, _index - runIndex - 1).ToString();
                }

                if (c != '\\') continue;
                if (_index == _length) break;
                
                _builder.Append(_json, runIndex, _index - runIndex - 1);
                runIndex = _index + 1;

                switch (_json[_index++])
                {
                    case '"':
                        _builder.Append('"');
                        break;

                    case '\\':
                        _builder.Append('\\');
                        break;

                    case '/':
                        _builder.Append('/');
                        break;

                    case 'b':
                        _builder.Append('\b');
                        break;

                    case 'f':
                        _builder.Append('\f');
                        break;

                    case 'n':
                        _builder.Append('\n');
                        break;

                    case 'r':
                        _builder.Append('\r');
                        break;

                    case 't':
                        _builder.Append('\t');
                        break;

                    case 'u':
                    {
                        int remainingLength = _length - _index;
                        if (remainingLength < 4) break;

                        uint codePoint = ParseUnicode(_json[_index], _json[_index + 1], _json[_index + 2], _json[_index + 3]);
                        _builder.Append((char)codePoint);

                        _index += 4;
                        runIndex = _index;
                    }
                        break;
                }
            }

            throw new Exception("Unexpectedly reached end of string");
        }

        private uint ParseUnicode(char c1, char c2, char c3, char c4)
        {
            uint p1 = ParseSingleChar(c1, 0x1000);
            uint p2 = ParseSingleChar(c2, 0x100);
            uint p3 = ParseSingleChar(c3, 0x10);
            uint p4 = ParseSingleChar(c4, 1);

            return p1 + p2 + p3 + p4;
        }

        /// <summary>
        /// Parses a JSON value.
        /// </summary>
        /// <returns>A Json instance representing the parsed value.</returns>
        private Json ParseValue()
        {
            switch (LookAhead())
            {
                case Token.Number:
                    object number = ParseNumber();
                    return new JsonValue(number, number is double ? JsonValueType.Double : JsonValueType.Long);

                case Token.String:
                    return new JsonValue(ParseString(), JsonValueType.String);

                case Token.Curly_Open:
                    return ParseObject();

                case Token.Squared_Open:
                    return ParseArray();

                case Token.True:
                    _lookAheadToken = Token.None;
                    return new JsonValue(true, JsonValueType.Boolean);

                case Token.False:
                    _lookAheadToken = Token.None;
                    return new JsonValue(false, JsonValueType.Boolean);

                case Token.Null:
                    _lookAheadToken = Token.None;
                    return new JsonValue(null, JsonValueType.Null);
            }

            throw new Exception("Unrecognized token at index" + _index);
        }
    
        /// <summary>
        /// Parses a JSON value as raw objects.
        /// </summary>
        /// <returns>The parsed value as a raw object.</returns>
        private object ParseValueDirect()
        {
            switch (LookAhead())
            {
                case Token.Number:
                    return ParseNumber();

                case Token.String:
                    return ParseString();

                case Token.Curly_Open:
                    return ParseObjectDirect();

                case Token.Squared_Open:
                    return ParseArrayDirect();

                case Token.True:
                    _lookAheadToken = Token.None;
                    return true;

                case Token.False:
                    _lookAheadToken = Token.None;
                    return false;

                case Token.Null:
                    _lookAheadToken = Token.None;
                    return null;
            }

            throw new Exception("Unrecognized token at index" + _index);
        }

        /// <summary>
        /// Token types for JSON parsing.
        /// </summary>
        private enum Token
        {
            None = -1,
            Curly_Open,
            Curly_Close,
            Squared_Open,
            Squared_Close,
            Colon,
            Comma,
            String,
            Number,
            True,
            False,
            Null
        }
    }
}
