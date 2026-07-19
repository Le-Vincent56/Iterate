using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Iterate.Application.Content.Json
{
    /// <summary>
    /// A strict, deterministic recursive-descent JSON reader for the catalog format. Rejects duplicate
    /// keys, trailing content, comments, single quotes, unquoted keys, NaN/Infinity, a leading '+',
    /// and leading zeros; parses numbers in the invariant culture; records a 1-indexed line and column
    /// on every node and error. Lenience is deliberately absent - the catalog is authored
    /// deterministic JSON, so anything unusual is an authoring defect.
    /// </summary>
    public sealed class CatalogJsonReader
    {
        /// <summary>
        /// Parses the given text into a JSON value tree.
        /// </summary>
        /// <param name="text">The JSON document text.</param>
        /// <returns>The root JSON value.</returns>
        /// <exception cref="CatalogJsonParseException">Thrown on any strictness or syntax violation.</exception>
        public JsonValue Parse(string text)
        {
            Scanner scanner = new(text);
            scanner.SkipByteOrderMark();
            scanner.SkipWhitespace();
            if (scanner.AtEnd)
                throw new CatalogJsonParseException(scanner.Line, scanner.Column, "the document is empty.");

            JsonValue value = ParseValue(scanner);
            scanner.SkipWhitespace();
            if (!scanner.AtEnd)
                throw new CatalogJsonParseException(scanner.Line, scanner.Column, "unexpected trailing content after the root value.");

            return value;
        }

        /// <summary>
        /// Parses a single JSON value at the scanner's current position.
        /// </summary>
        /// <param name="scanner">The source scanner.</param>
        /// <returns>The parsed value.</returns>
        private JsonValue ParseValue(Scanner scanner)
        {
            if (scanner.AtEnd)
                throw new CatalogJsonParseException(scanner.Line, scanner.Column, "expected a value.");

            char current = scanner.Current;
            switch (current)
            {
                case '{':
                    return ParseObject(scanner);
                
                case '[':
                    return ParseArray(scanner);
                
                case '"':
                    return ParseString(scanner);
                
                case '-':
                case >= '0' and <= '9':
                    return ParseNumber(scanner);
                
                case 't':
                    return ParseTrue(scanner);
                
                case 'f':
                    return ParseFalse(scanner);
                
                case 'n':
                    return ParseNull(scanner);
                
                default:
                    throw new CatalogJsonParseException(scanner.Line, scanner.Column, "unexpected character; expected a value.");
            }
        }

        /// <summary>
        /// Parses a JSON object at the scanner's current position.
        /// </summary>
        /// <param name="scanner">The source scanner.</param>
        /// <returns>The parsed object.</returns>
        private JsonObject ParseObject(Scanner scanner)
        {
            int line = scanner.Line;
            int column = scanner.Column;
            scanner.Advance();
            List<string> keys = new();
            Dictionary<string, JsonValue> members = new(StringComparer.Ordinal);

            scanner.SkipWhitespace();
            if (!scanner.AtEnd && scanner.Current == '}')
            {
                scanner.Advance();
                return new JsonObject(line, column, keys, members);
            }

            while (true)
            {
                scanner.SkipWhitespace();
                if (scanner.AtEnd || scanner.Current != '"')
                    throw new CatalogJsonParseException(scanner.Line, scanner.Column, "expected a double-quoted string key.");

                int keyLine = scanner.Line;
                int keyColumn = scanner.Column;
                string key = ParseStringValue(scanner);
                if (members.ContainsKey(key))
                    throw new CatalogJsonParseException(keyLine, keyColumn, "duplicate key '" + key + "'.");

                scanner.SkipWhitespace();
                if (scanner.AtEnd || scanner.Current != ':')
                    throw new CatalogJsonParseException(scanner.Line, scanner.Column, "expected ':' after the key.");

                scanner.Advance();
                scanner.SkipWhitespace();
                JsonValue value = ParseValue(scanner);
                keys.Add(key);
                members[key] = value;

                scanner.SkipWhitespace();
                if (scanner.AtEnd)
                    throw new CatalogJsonParseException(scanner.Line, scanner.Column, "expected ',' or '}'.");

                switch (scanner.Current)
                {
                    case ',':
                        scanner.Advance();
                        continue;
                    
                    case '}':
                        scanner.Advance();
                        return new JsonObject(line, column, keys, members);
                    
                    default:
                        throw new CatalogJsonParseException(scanner.Line, scanner.Column, "expected ',' or '}'.");
                }
            }
        }

        /// <summary>
        /// Parses a JSON array at the scanner's current position.
        /// </summary>
        /// <param name="scanner">The source scanner.</param>
        /// <returns>The parsed array.</returns>
        private JsonArray ParseArray(Scanner scanner)
        {
            int line = scanner.Line;
            int column = scanner.Column;
            scanner.Advance();
            List<JsonValue> items = new();

            scanner.SkipWhitespace();
            if (!scanner.AtEnd && scanner.Current == ']')
            {
                scanner.Advance();
                return new JsonArray(line, column, items);
            }

            while (true)
            {
                scanner.SkipWhitespace();
                JsonValue value = ParseValue(scanner);
                items.Add(value);

                scanner.SkipWhitespace();
                if (scanner.AtEnd)
                    throw new CatalogJsonParseException(scanner.Line, scanner.Column, "expected ',' or ']'.");

                switch (scanner.Current)
                {
                    case ',':
                        scanner.Advance();
                        continue;
                    
                    case ']':
                        scanner.Advance();
                        return new JsonArray(line, column, items);
                    
                    default:
                        throw new CatalogJsonParseException(scanner.Line, scanner.Column, "expected ',' or ']'.");
                }
            }
        }

        /// <summary>
        /// Parses a JSON string node at the scanner's current position.
        /// </summary>
        /// <param name="scanner">The source scanner.</param>
        /// <returns>The parsed string node.</returns>
        private JsonString ParseString(Scanner scanner)
        {
            int line = scanner.Line;
            int column = scanner.Column;
            string value = ParseStringValue(scanner);
            return new JsonString(line, column, value);
        }

        /// <summary>
        /// Parses a double-quoted string's decoded value, consuming both quotes.
        /// </summary>
        /// <param name="scanner">The source scanner, positioned at the opening quote.</param>
        /// <returns>The decoded string.</returns>
        private string ParseStringValue(Scanner scanner)
        {
            scanner.Advance();
            StringBuilder builder = new();
            while (true)
            {
                if (scanner.AtEnd)
                    throw new CatalogJsonParseException(scanner.Line, scanner.Column, "unterminated string.");

                char current = scanner.Current;
                switch (current)
                {
                    case '"':
                        scanner.Advance();
                        return builder.ToString();
                    
                    case '\\':
                        AppendEscape(scanner, builder);
                        continue;
                    
                    case < ' ':
                        throw new CatalogJsonParseException(scanner.Line, scanner.Column, "unescaped control character in string.");
                    
                    default:
                        builder.Append(current);
                        scanner.Advance();
                        break;
                }
            }
        }

        /// <summary>
        /// Decodes one backslash escape onto the builder, consuming the escape sequence.
        /// </summary>
        /// <param name="scanner">The source scanner, positioned at the backslash.</param>
        /// <param name="builder">The decoded-string builder.</param>
        private void AppendEscape(Scanner scanner, StringBuilder builder)
        {
            scanner.Advance();
            if (scanner.AtEnd)
                throw new CatalogJsonParseException(scanner.Line, scanner.Column, "unterminated escape sequence.");

            char escape = scanner.Current;
            switch (escape)
            {
                case '"': 
                    builder.Append('"');
                    scanner.Advance();
                    break;
                
                case '\\':
                    builder.Append('\\');
                    scanner.Advance();
                    break;
                
                case '/': 
                    builder.Append('/'); 
                    scanner.Advance();
                    break;
                
                case 'b': 
                    builder.Append('\b');
                    scanner.Advance();
                    break;
                
                case 'f': 
                    builder.Append('\f');
                    scanner.Advance();
                    break;
                
                case 'n': 
                    builder.Append('\n');
                    scanner.Advance();
                    break;
                
                case 'r': 
                    builder.Append('\r');
                    scanner.Advance();
                    break;
                
                case 't': 
                    builder.Append('\t');
                    scanner.Advance();
                    break;
                
                case 'u': 
                    builder.Append(ReadUnicodeEscape(scanner));
                    break;
                
                default:
                    throw new CatalogJsonParseException(scanner.Line, scanner.Column, "invalid escape sequence.");
            }
        }

        /// <summary>
        /// Reads a four-hex-digit unicode escape, consuming the 'u' and the four digits.
        /// </summary>
        /// <param name="scanner">The source scanner, positioned at the 'u'.</param>
        /// <returns>The decoded character.</returns>
        private char ReadUnicodeEscape(Scanner scanner)
        {
            scanner.Advance();
            int value = 0;
            for (int digitIndex = 0; digitIndex < 4; digitIndex++)
            {
                if (scanner.AtEnd)
                    throw new CatalogJsonParseException(scanner.Line, scanner.Column, "unterminated unicode escape.");

                char hex = scanner.Current;
                int digit = hex switch
                {
                    >= '0' and <= '9' => hex - '0',
                    >= 'a' and <= 'f' => 10 + (hex - 'a'),
                    >= 'A' and <= 'F' => 10 + (hex - 'A'),
                    _ => throw new CatalogJsonParseException(scanner.Line, scanner.Column, "invalid unicode escape digit.")
                };

                value = (value * 16) + digit;
                scanner.Advance();
            }

            return (char)value;
        }

        /// <summary>
        /// Parses a JSON number at the scanner's current position.
        /// </summary>
        /// <param name="scanner">The source scanner.</param>
        /// <returns>The parsed number.</returns>
        private JsonNumber ParseNumber(Scanner scanner)
        {
            int line = scanner.Line;
            int column = scanner.Column;
            StringBuilder builder = new();
            bool isInteger = true;

            if (scanner.Current == '-')
            {
                builder.Append('-');
                scanner.Advance();
            }

            if (scanner.AtEnd || scanner.Current < '0' || scanner.Current > '9')
                throw new CatalogJsonParseException(scanner.Line, scanner.Column, "expected a digit.");

            if (scanner.Current == '0')
            {
                builder.Append('0');
                scanner.Advance();
                if (!scanner.AtEnd && scanner.Current is >= '0' and <= '9')
                    throw new CatalogJsonParseException(scanner.Line, scanner.Column, "leading zeros are not allowed.");
            }
            else
            {
                while (!scanner.AtEnd && scanner.Current is >= '0' and <= '9')
                {
                    builder.Append(scanner.Current);
                    scanner.Advance();
                }
            }

            if (!scanner.AtEnd && scanner.Current == '.')
            {
                isInteger = false;
                builder.Append('.');
                scanner.Advance();
                if (scanner.AtEnd || scanner.Current < '0' || scanner.Current > '9')
                    throw new CatalogJsonParseException(scanner.Line, scanner.Column, "expected a digit after the decimal point.");

                while (!scanner.AtEnd && scanner.Current is >= '0' and <= '9')
                {
                    builder.Append(scanner.Current);
                    scanner.Advance();
                }
            }

            if (!scanner.AtEnd && scanner.Current is 'e' or 'E')
            {
                isInteger = false;
                builder.Append(scanner.Current);
                scanner.Advance();
                if (!scanner.AtEnd && scanner.Current is '+' or '-')
                {
                    builder.Append(scanner.Current);
                    scanner.Advance();
                }

                if (scanner.AtEnd || scanner.Current < '0' || scanner.Current > '9')
                    throw new CatalogJsonParseException(scanner.Line, scanner.Column, "expected a digit in the exponent.");

                while (!scanner.AtEnd && scanner.Current is >= '0' and <= '9')
                {
                    builder.Append(scanner.Current);
                    scanner.Advance();
                }
            }

            string text = builder.ToString();
            if (isInteger)
            {
                long integerValue = long.Parse(text, CultureInfo.InvariantCulture);
                return new JsonNumber(line, column, true, integerValue, integerValue);
            }

            double doubleValue = double.Parse(text, CultureInfo.InvariantCulture);
            return new JsonNumber(line, column, false, 0L, doubleValue);
        }

        /// <summary>
        /// Parses the <c>true</c> literal.
        /// </summary>
        /// <param name="scanner">The source scanner.</param>
        /// <returns>The parsed boolean node.</returns>
        private JsonBool ParseTrue(Scanner scanner)
        {
            int line = scanner.Line;
            int column = scanner.Column;
            Expect(scanner, "true");
            return new JsonBool(line, column, true);
        }

        /// <summary>
        /// Parses the <c>false</c> literal.
        /// </summary>
        /// <param name="scanner">The source scanner.</param>
        /// <returns>The parsed boolean node.</returns>
        private JsonBool ParseFalse(Scanner scanner)
        {
            int line = scanner.Line;
            int column = scanner.Column;
            Expect(scanner, "false");
            return new JsonBool(line, column, false);
        }

        /// <summary>
        /// Parses the <c>null</c> literal.
        /// </summary>
        /// <param name="scanner">The source scanner.</param>
        /// <returns>The parsed null node.</returns>
        private JsonNull ParseNull(Scanner scanner)
        {
            int line = scanner.Line;
            int column = scanner.Column;
            Expect(scanner, "null");
            return new JsonNull(line, column);
        }

        /// <summary>
        /// Consumes an exact keyword or fails at the divergence point.
        /// </summary>
        /// <param name="scanner">The source scanner.</param>
        /// <param name="keyword">The keyword to consume.</param>
        private void Expect(Scanner scanner, string keyword)
        {
            for (int index = 0; index < keyword.Length; index++)
            {
                if (scanner.AtEnd || scanner.Current != keyword[index])
                    throw new CatalogJsonParseException(scanner.Line, scanner.Column, "invalid literal; expected '" + keyword + "'.");

                scanner.Advance();
            }
        }

        /// <summary>
        /// A forward-only cursor over the source text tracking a 1-indexed line and column.
        /// </summary>
        private sealed class Scanner
        {
            private readonly string _text;

            private int _index;

            private int _line;

            private int _column;

            /// <summary>
            /// Whether the cursor has consumed the entire text.
            /// </summary>
            public bool AtEnd => _index >= _text.Length;

            /// <summary>
            /// The character at the cursor. Valid only when not at the end.
            /// </summary>
            public char Current => _text[_index];

            /// <summary>
            /// The current 1-indexed line.
            /// </summary>
            public int Line => _line;

            /// <summary>
            /// The current 1-indexed column.
            /// </summary>
            public int Column => _column;

            public Scanner(string text)
            {
                _text = text ?? string.Empty;
                _index = 0;
                _line = 1;
                _column = 1;
            }

            /// <summary>
            /// Consumes the current character, advancing the line and column.
            /// </summary>
            public void Advance()
            {
                char consumed = _text[_index];
                _index++;
                if (consumed == '\n')
                {
                    _line++;
                    _column = 1;
                }
                else
                {
                    _column++;
                }
            }

            /// <summary>
            /// Skips a single leading UTF-8 byte-order mark without affecting the reported position.
            /// </summary>
            public void SkipByteOrderMark()
            {
                if (_index == 0 && !AtEnd && _text[_index] == (char)0xFEFF)
                    _index++;
            }

            /// <summary>
            /// Skips JSON whitespace (space, tab, carriage return, newline).
            /// </summary>
            public void SkipWhitespace()
            {
                while (!AtEnd)
                {
                    char current = _text[_index];
                    if (current is ' ' or '\t' or '\n' or '\r') Advance();
                    else break;
                }
            }
        }
    }
}