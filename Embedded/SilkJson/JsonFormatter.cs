using System;
using System.Text;

namespace SilkJson
{
    internal static class JsonFormatter
    {
        public static string Compact(string jsonString) => Format(jsonString, pretty: false, indent: string.Empty);

        public static string Pretty(string jsonString, string indent = "  ") => Format(jsonString, pretty: true, indent: indent ?? "  ");

        private static string Format(string jsonString, bool pretty, string indent)
        {
            if (jsonString == null) return "null";

            StringBuilder builder = new StringBuilder(jsonString.Length);
            int depth = 0;
            bool inString = false;
            bool escaped = false;
            char previousSignificant = '\0';

            for (int i = 0; i < jsonString.Length; i++)
            {
                char c = jsonString[i];

                if (inString)
                {
                    builder.Append(c);

                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (c == '"') inString = false;
                    continue;
                }

                if (c <= ' ')
                {
                    continue;
                }

                if (c == '/' && i + 1 < jsonString.Length && jsonString[i + 1] == '/')
                {
                    i += 2;
                    while (i < jsonString.Length && jsonString[i] != '\r' && jsonString[i] != '\n') i++;
                    i--;
                    continue;
                }

                if (pretty && NeedsIndentBeforeToken(previousSignificant, c))
                {
                    builder.Append('\n');
                    AppendIndent(builder, depth, indent);
                }

                switch (c)
                {
                    case '"':
                        inString = true;
                        builder.Append(c);
                        previousSignificant = 'v';
                        break;

                    case '{':
                    case '[':
                        builder.Append(c);
                        depth++;
                        previousSignificant = c;
                        break;

                    case '}':
                    case ']':
                        depth--;
                        if (depth < 0) throw new FormatException("Unexpected closing bracket in JSON string.");
                        if (pretty && previousSignificant != '{' && previousSignificant != '[')
                        {
                            builder.Append('\n');
                            AppendIndent(builder, depth, indent);
                        }
                        builder.Append(c);
                        previousSignificant = c;
                        break;

                    case ',':
                        builder.Append(c);
                        previousSignificant = c;
                        break;

                    case ':':
                        builder.Append(c);
                        if (pretty) builder.Append(' ');
                        previousSignificant = c;
                        break;

                    default:
                        builder.Append(c);
                        previousSignificant = 'v';
                        break;
                }
            }

            if (inString) throw new FormatException("Unterminated JSON string literal.");
            if (depth != 0) throw new FormatException("Unexpected end of JSON string.");

            return builder.ToString();
        }

        private static bool NeedsIndentBeforeToken(char previousSignificant, char current)
        {
            if (current == '}' || current == ']') return false;
            return previousSignificant == '{' || previousSignificant == '[' || previousSignificant == ',';
        }

        private static void AppendIndent(StringBuilder builder, int depth, string indent)
        {
            for (int i = 0; i < depth; i++) builder.Append(indent);
        }
    }
}
