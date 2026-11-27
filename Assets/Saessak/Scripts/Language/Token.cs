namespace Saessak.Language
{
    public enum TokenType
    {
        // 값
        Number,
        String,
        Identifier,

        // 키워드
        If,         // 만약
        Else,       // 아니면
        Loop,       // 반복
        In,         // 안에
        Times,      // 번
        Function,   // 함수
        Return,     // 돌려주기
        Break,      // 멈추기
        Continue,   // 건너뛰기
        Pass,       // 통과
        And,        // 그리고
        Or,         // 또는
        Not,        // 아님
        True,       // 참
        False,      // 거짓
        None,       // 없음

        // 기호
        LeftParen,
        RightParen,
        LeftBracket,
        RightBracket,
        Comma,
        Colon,
        Plus,
        Minus,
        Star,
        Slash,
        SlashSlash,
        Percent,
        Equal,
        PlusEqual,
        MinusEqual,
        StarEqual,
        SlashEqual,
        EqualEqual,
        BangEqual,
        Less,
        LessEqual,
        Greater,
        GreaterEqual,

        // 줄 구조
        Newline,
        Indent,     // 들여쓰기가 한 단계 깊어짐
        Dedent,     // 들여쓰기가 한 단계 얕아짐
        EndOfFile,
    }

    /// <summary>
    /// 토큰: 코드를 잘라 낸 낱말 조각 하나. 예) "만약", "x", ">", "3", ":"
    /// </summary>
    public readonly struct Token
    {
        #region Properties

        public TokenType Type { get; }

        /// <summary>코드에 실제로 적혀 있던 글자 그대로</summary>
        public string Lexeme { get; }

        /// <summary>숫자·문자·참거짓 토큰이 담고 있는 실제 값 (나머지는 null)</summary>
        public object Value { get; }

        public int Line { get; }
        public int Column { get; }

        #endregion

        #region Custom Methods

        public Token(TokenType type, string lexeme, object value, int line, int column)
        {
            Type = type;
            Lexeme = lexeme;
            Value = value;
            Line = line;
            Column = column;
        }

        public override string ToString() => $"{Type} '{Lexeme}' ({Line}:{Column})";

        #endregion
    }
}
