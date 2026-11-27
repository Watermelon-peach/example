using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Saessak.Language
{
    /// <summary>
    /// 렉서(Lexer): 코드 글자들을 토큰(낱말 조각)으로 잘라 줘요.
    /// 파이썬처럼 줄 앞의 빈칸 수를 세어서 Indent(들여쓰기) / Dedent(내어쓰기) 토큰도 만들어요.
    /// </summary>
    public sealed class Lexer
    {
        #region Variables

        const int TabWidth = 4;

        static readonly Dictionary<string, TokenType> Keywords = new Dictionary<string, TokenType>
        {
            { "만약", TokenType.If },
            { "아니면", TokenType.Else },
            { "반복", TokenType.Loop },
            { "안에", TokenType.In },
            { "번", TokenType.Times },
            { "함수", TokenType.Function },
            { "돌려주기", TokenType.Return },
            { "멈추기", TokenType.Break },
            { "건너뛰기", TokenType.Continue },
            { "통과", TokenType.Pass },
            { "그리고", TokenType.And },
            { "또는", TokenType.Or },
            { "아님", TokenType.Not },
            { "참", TokenType.True },
            { "거짓", TokenType.False },
            { "없음", TokenType.None },
        };

        readonly string _source;
        readonly List<Token> _tokens = new List<Token>();

        // 지금까지 열린 들여쓰기 칸 수들. 맨 바닥은 항상 0이에요.
        readonly Stack<int> _indents = new Stack<int>();

        // 아직 닫히지 않은 괄호들. 괄호 안에서는 줄을 바꿔도 한 줄로 이어진 것으로 봐요.
        readonly Stack<Token> _openBrackets = new Stack<Token>();

        int _pos;
        int _line = 1;
        int _lineStart;

        #endregion

        #region Properties

        bool IsAtEnd => _pos >= _source.Length;
        char Current => IsAtEnd ? '\0' : _source[_pos];
        char Next => _pos + 1 < _source.Length ? _source[_pos + 1] : '\0';
        int Column => _pos - _lineStart + 1;

        #endregion

        #region Custom Methods

        public Lexer(string source)
        {
            _source = source ?? string.Empty;
            _indents.Push(0);
        }

        public List<Token> Tokenize()
        {
            bool atLineStart = true;

            while (!IsAtEnd)
            {
                if (atLineStart && _openBrackets.Count == 0)
                {
                    int indent = ReadIndent();
                    if (IsAtEnd)
                        break;

                    // 빈 줄이나 주석만 있는 줄은 들여쓰기를 따지지 않고 통째로 건너뛰어요.
                    if (Current == '\n' || Current == '\r' || Current == '#')
                    {
                        SkipBlankLine();
                        continue;
                    }

                    ApplyIndent(indent);
                    atLineStart = false;
                }

                char c = Current;

                if (c == ' ' || c == '\t' || c == '\r')
                {
                    _pos++;
                }
                else if (c == '#')
                {
                    while (!IsAtEnd && Current != '\n')
                        _pos++;
                }
                else if (c == '\n')
                {
                    if (_openBrackets.Count == 0)
                    {
                        AddNewline();
                        atLineStart = true;
                    }
                    ConsumeNewline();
                }
                else if (IsDigit(c))
                {
                    ReadNumber();
                }
                else if (IsIdentifierStart(c))
                {
                    ReadWord();
                }
                else if (c == '"' || c == '\'')
                {
                    ReadString(c);
                }
                else
                {
                    ReadSymbol();
                }
            }

            if (_openBrackets.Count > 0)
            {
                Token open = _openBrackets.Peek();
                throw SyntaxError($"괄호 '{open.Lexeme}'가 닫히지 않았어요.", open.Line, open.Column);
            }

            AddNewline();
            while (_indents.Peek() > 0)
            {
                _indents.Pop();
                Add(TokenType.Dedent, "", null, Column);
            }
            Add(TokenType.EndOfFile, "", null, Column);

            return _tokens;
        }

        int ReadIndent()
        {
            int width = 0;
            while (!IsAtEnd)
            {
                if (Current == ' ')
                    width++;
                else if (Current == '\t')
                    width += TabWidth - width % TabWidth;
                else
                    break;
                _pos++;
            }
            return width;
        }

        void ApplyIndent(int indent)
        {
            if (indent > _indents.Peek())
            {
                _indents.Push(indent);
                Add(TokenType.Indent, "", null, 1);
                return;
            }

            while (indent < _indents.Peek())
            {
                _indents.Pop();
                Add(TokenType.Dedent, "", null, 1);
            }

            if (indent != _indents.Peek())
                throw SyntaxError("들여쓰기 칸 수가 위쪽 줄들과 맞지 않아요. 줄 앞의 빈칸 수를 확인해 보세요.", _line, 1);
        }

        void SkipBlankLine()
        {
            while (!IsAtEnd && Current != '\n')
                _pos++;
            if (!IsAtEnd)
                ConsumeNewline();
        }

        void ConsumeNewline()
        {
            _pos++;
            _line++;
            _lineStart = _pos;
        }

        void ReadNumber()
        {
            int start = _pos;
            int column = Column;

            while (IsDigit(Current))
                _pos++;

            // "3.14"처럼 점 뒤에 숫자가 있을 때만 소수로 읽어요.
            if (Current == '.' && IsDigit(Next))
            {
                _pos++;
                while (IsDigit(Current))
                    _pos++;
            }

            string text = _source.Substring(start, _pos - start);
            Add(TokenType.Number, text, double.Parse(text, CultureInfo.InvariantCulture), column);
        }

        void ReadWord()
        {
            int start = _pos;
            int column = Column;

            while (!IsAtEnd && IsIdentifierPart(Current))
                _pos++;

            string text = _source.Substring(start, _pos - start);

            if (!Keywords.TryGetValue(text, out TokenType type))
                type = TokenType.Identifier;

            object value = type == TokenType.True ? true
                         : type == TokenType.False ? (object)false
                         : null;

            Add(type, text, value, column);
        }

        void ReadString(char quote)
        {
            int start = _pos;
            int column = Column;
            var builder = new StringBuilder();
            _pos++; // 여는 따옴표

            while (true)
            {
                if (IsAtEnd || Current == '\n' || Current == '\r')
                    throw SyntaxError($"문자가 끝나지 않았어요. 같은 줄에서 따옴표({quote})로 닫아 주세요.", _line, column);

                char c = Current;

                if (c == quote)
                {
                    _pos++;
                    break;
                }

                if (c == '\\')
                {
                    char escaped = Next;
                    builder.Append(escaped switch
                    {
                        'n' => '\n',
                        't' => '\t',
                        '\\' => '\\',
                        '"' => '"',
                        '\'' => '\'',
                        _ => throw SyntaxError($"알 수 없는 탈출 문자예요: \\{escaped}  (\\n, \\t, \\\", \\\\ 만 쓸 수 있어요)", _line, Column),
                    });
                    _pos += 2;
                    continue;
                }

                builder.Append(c);
                _pos++;
            }

            Add(TokenType.String, _source.Substring(start, _pos - start), builder.ToString(), column);
        }

        void ReadSymbol()
        {
            int column = Column;
            char c = Current;

            TokenType? twoChar = (c, Next) switch
            {
                ('=', '=') => TokenType.EqualEqual,
                ('!', '=') => TokenType.BangEqual,
                ('<', '=') => TokenType.LessEqual,
                ('>', '=') => TokenType.GreaterEqual,
                ('/', '/') => TokenType.SlashSlash,
                ('+', '=') => TokenType.PlusEqual,
                ('-', '=') => TokenType.MinusEqual,
                ('*', '=') => TokenType.StarEqual,
                ('/', '=') => TokenType.SlashEqual,
                _ => (TokenType?)null,
            };

            if (twoChar.HasValue)
            {
                Add(twoChar.Value, _source.Substring(_pos, 2), null, column);
                _pos += 2;
                return;
            }

            TokenType? oneChar = c switch
            {
                '(' => TokenType.LeftParen,
                ')' => TokenType.RightParen,
                '[' => TokenType.LeftBracket,
                ']' => TokenType.RightBracket,
                ',' => TokenType.Comma,
                ':' => TokenType.Colon,
                '+' => TokenType.Plus,
                '-' => TokenType.Minus,
                '*' => TokenType.Star,
                '/' => TokenType.Slash,
                '%' => TokenType.Percent,
                '=' => TokenType.Equal,
                '<' => TokenType.Less,
                '>' => TokenType.Greater,
                _ => (TokenType?)null,
            };

            if (!oneChar.HasValue)
            {
                if (c == '!')
                    throw SyntaxError("'!' 대신 '아님'을 써 주세요. 예: 만약 아님 수확가능():", _line, column);
                throw SyntaxError($"알 수 없는 기호예요: {c}", _line, column);
            }

            var token = new Token(oneChar.Value, c.ToString(), null, _line, column);
            TrackBracket(token);
            _tokens.Add(token);
            _pos++;
        }

        void TrackBracket(Token token)
        {
            if (token.Type == TokenType.LeftParen || token.Type == TokenType.LeftBracket)
            {
                _openBrackets.Push(token);
                return;
            }

            if (token.Type != TokenType.RightParen && token.Type != TokenType.RightBracket)
                return;

            if (_openBrackets.Count == 0)
                throw SyntaxError($"여는 괄호 없이 닫는 괄호 '{token.Lexeme}'가 나왔어요.", token.Line, token.Column);

            Token open = _openBrackets.Pop();
            bool matches = (open.Type == TokenType.LeftParen && token.Type == TokenType.RightParen)
                        || (open.Type == TokenType.LeftBracket && token.Type == TokenType.RightBracket);
            if (!matches)
                throw SyntaxError($"괄호 짝이 맞지 않아요. (연 괄호: '{open.Lexeme}' / 닫은 괄호: '{token.Lexeme}')", token.Line, token.Column);
        }

        void AddNewline()
        {
            if (_tokens.Count > 0 && _tokens[_tokens.Count - 1].Type != TokenType.Newline)
                Add(TokenType.Newline, "줄바꿈", null, Column);
        }

        void Add(TokenType type, string lexeme, object value, int column)
        {
            _tokens.Add(new Token(type, lexeme, value, _line, column));
        }

        static bool IsDigit(char c) => c >= '0' && c <= '9';

        // char.IsLetter는 한글 음절(가~힣)도 글자로 쳐 줘요.
        static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

        static bool IsIdentifierPart(char c) => char.IsLetter(c) || IsDigit(c) || c == '_';

        static SaessakError SyntaxError(string message, int line, int column)
            => new SaessakError(message, line, column, SaessakErrorKind.Syntax);

        #endregion
    }
}
