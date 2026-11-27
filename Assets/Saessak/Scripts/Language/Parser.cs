using System.Collections.Generic;

namespace Saessak.Language
{
    /// <summary>
    /// 파서(Parser): 렉서가 만든 토큰 줄을 읽어서 AST(코드의 뼈대 나무)로 정리해요.
    /// 식은 우선순위가 낮은 것부터 높은 것 순서로 함수를 나눠 읽어요 (재귀 하강 파싱).
    ///   또는 → 그리고 → 아님 → 비교(== < …) → 더하기/빼기 → 곱하기/나누기 → 부호(-) → 호출/꺼내기 → 기본 값
    /// </summary>
    public sealed class Parser
    {
        #region Variables

        readonly List<Token> _tokens;
        int _current;

        // 지금 반복 / 함수 안에 있는지 세어서, 엉뚱한 곳의 멈추기·돌려주기를 미리 잡아내요.
        int _loopDepth;
        int _functionDepth;

        #endregion

        #region Properties

        Token Peek => _tokens[_current];
        Token PeekNext => _current + 1 < _tokens.Count ? _tokens[_current + 1] : _tokens[_tokens.Count - 1];
        Token Previous => _tokens[_current - 1];

        #endregion

        #region Custom Methods

        public Parser(List<Token> tokens)
        {
            _tokens = tokens;
        }

        /// <summary>코드 글자를 바로 AST로 바꿔 줘요. 문법이 틀리면 SaessakError를 던져요.</summary>
        public static ProgramNode Parse(string source)
        {
            return new Parser(new Lexer(source).Tokenize()).ParseProgram();
        }

        public ProgramNode ParseProgram()
        {
            var statements = new List<Stmt>();
            while (!Check(TokenType.EndOfFile))
                statements.Add(Statement());
            return new ProgramNode(statements);
        }

        // ───────── 문장 ─────────

        Stmt Statement()
        {
            switch (Peek.Type)
            {
                case TokenType.If:
                    return IfStatement();
                case TokenType.Loop:
                    return LoopStatement();
                case TokenType.Function:
                    return FunctionStatement();
                case TokenType.Indent:
                    throw Error(Peek, "여기서는 들여쓰기를 하면 안 돼요. 줄 앞의 빈칸을 확인해 보세요.");
                case TokenType.Else:
                    throw Error(Peek, "'아니면'은 '만약' 블록 바로 다음 줄에, 같은 들여쓰기로 써야 해요.");
                default:
                    Stmt statement = SimpleStatement();
                    ExpectLineEnd();
                    return statement;
            }
        }

        Stmt IfStatement()
        {
            int line = Advance().Line; // 만약
            var branches = new List<IfBranch>();

            Expr condition = Condition("만약");
            branches.Add(new IfBranch(condition, Block()));

            List<Stmt> elseBody = null;
            while (Match(TokenType.Else))
            {
                if (Match(TokenType.If))
                {
                    Expr elseIfCondition = Condition("아니면 만약");
                    branches.Add(new IfBranch(elseIfCondition, Block()));
                }
                else
                {
                    elseBody = Block();
                    break;
                }
            }

            return new IfStmt(branches, elseBody, line);
        }

        Stmt LoopStatement()
        {
            int line = Advance().Line; // 반복

            if (Check(TokenType.Colon) || Check(TokenType.Newline))
                throw Error(Peek, "'반복' 뒤에 조건이나 횟수를 써 주세요. 예) 반복 참:   반복 5 번:   반복 i 안에 범위(5):");

            // 반복 i 안에 목록:
            if (Check(TokenType.Identifier) && PeekNext.Type == TokenType.In)
            {
                string variable = Advance().Lexeme;
                Advance(); // 안에
                Expr iterable = Expression();
                return new ForStmt(variable, iterable, LoopBody(), line);
            }

            Expr expression = Expression();

            // 반복 5 번:
            if (Match(TokenType.Times))
                return new RepeatStmt(expression, LoopBody(), line);

            // 반복 조건:
            return new WhileStmt(expression, LoopBody(), line);
        }

        List<Stmt> LoopBody()
        {
            _loopDepth++;
            try
            {
                return Block();
            }
            finally
            {
                _loopDepth--;
            }
        }

        Stmt FunctionStatement()
        {
            int line = Advance().Line; // 함수
            string name = Expect(TokenType.Identifier, "'함수' 뒤에 함수 이름을 써 주세요. 예) 함수 심기(작물):").Lexeme;
            Expect(TokenType.LeftParen, "함수 이름 뒤에 '('가 필요해요. 매개변수가 없어도 () 를 써 주세요.");

            var parameters = new List<string>();
            if (!Check(TokenType.RightParen))
            {
                do
                {
                    Token parameter = Expect(TokenType.Identifier, "매개변수 이름을 써 주세요.");
                    if (parameters.Contains(parameter.Lexeme))
                        throw Error(parameter, $"매개변수 이름이 겹쳐요: {parameter.Lexeme}");
                    parameters.Add(parameter.Lexeme);
                }
                while (Match(TokenType.Comma));
            }
            Expect(TokenType.RightParen, "매개변수 목록을 ')'로 닫아 주세요.");

            // 함수 안은 새 출발이라 바깥 반복의 멈추기/건너뛰기는 쓸 수 없어요.
            int savedLoopDepth = _loopDepth;
            _loopDepth = 0;
            _functionDepth++;
            try
            {
                return new FunctionStmt(name, parameters, Block(), line);
            }
            finally
            {
                _functionDepth--;
                _loopDepth = savedLoopDepth;
            }
        }

        Stmt SimpleStatement()
        {
            Token start = Peek;

            switch (start.Type)
            {
                case TokenType.Return:
                    Advance();
                    if (_functionDepth == 0)
                        throw Error(start, "'돌려주기'는 함수 안에서만 쓸 수 있어요.");
                    Expr value = IsLineEnd() ? null : Expression();
                    return new ReturnStmt(value, start.Line);

                case TokenType.Break:
                    Advance();
                    if (_loopDepth == 0)
                        throw Error(start, "'멈추기'는 반복 안에서만 쓸 수 있어요.");
                    return new BreakStmt(start.Line);

                case TokenType.Continue:
                    Advance();
                    if (_loopDepth == 0)
                        throw Error(start, "'건너뛰기'는 반복 안에서만 쓸 수 있어요.");
                    return new ContinueStmt(start.Line);

                case TokenType.Pass:
                    Advance();
                    return new PassStmt(start.Line);

                case TokenType.If:
                case TokenType.Loop:
                case TokenType.Function:
                    throw Error(start, "':' 바로 뒤 같은 줄에는 만약·반복·함수를 쓸 수 없어요. 다음 줄에 들여써서 써 주세요.");
            }

            Expr target = Expression();

            if (IsAssignOperator(Peek.Type))
            {
                Token op = Advance();
                if (!(target is VariableExpr) && !(target is IndexExpr))
                    throw Error(op, "'=' 왼쪽에는 변수 이름이나 목록[위치]만 올 수 있어요.");
                Expr assigned = Expression();
                return new AssignStmt(target, op, assigned, start.Line);
            }

            return new ExprStmt(target, start.Line);
        }

        /// <summary>
        /// ':' 뒤의 블록을 읽어요.
        ///   만약 x:          ← 다음 줄부터 들여쓴 여러 줄
        ///       수확()
        ///   만약 x: 수확()   ← 같은 줄에 명령 하나
        /// </summary>
        List<Stmt> Block()
        {
            if (!Check(TokenType.Colon))
            {
                if (Check(TokenType.Equal))
                    throw Error(Peek, "같은지 비교할 때는 '=' 대신 '=='를 써요. 예) 만약 x == 3:");
                throw Error(Peek, $"줄 끝에 ':'(쌍점)이 필요해요. (지금은 {Describe(Peek)} 가 있어요)");
            }
            Advance();

            if (!Match(TokenType.Newline))
            {
                Stmt single = SimpleStatement();
                ExpectLineEnd();
                return new List<Stmt> { single };
            }

            Expect(TokenType.Indent, "':' 다음 줄은 안쪽으로 들여써야 해요. (빈칸 4개)");

            var statements = new List<Stmt>();
            while (!Check(TokenType.Dedent) && !Check(TokenType.EndOfFile))
                statements.Add(Statement());
            Match(TokenType.Dedent);

            return statements;
        }

        Expr Condition(string keyword)
        {
            if (Check(TokenType.Colon))
                throw Error(Peek, $"'{keyword}' 뒤에 조건을 써 주세요. 예) {keyword} x > 3:");
            return Expression();
        }

        void ExpectLineEnd()
        {
            if (Match(TokenType.Newline) || Check(TokenType.EndOfFile))
                return;
            throw Error(Peek, $"한 줄에는 명령을 하나만 쓸 수 있어요. ({Describe(Peek)} 앞에서 줄을 바꿔 보세요)");
        }

        // ───────── 식 (우선순위 낮은 것부터) ─────────

        Expr Expression() => Or();

        Expr Or()
        {
            Expr left = And();
            while (Match(TokenType.Or))
            {
                Token op = Previous;
                left = new BinaryExpr(left, op, And(), op.Line);
            }
            return left;
        }

        Expr And()
        {
            Expr left = Not();
            while (Match(TokenType.And))
            {
                Token op = Previous;
                left = new BinaryExpr(left, op, Not(), op.Line);
            }
            return left;
        }

        Expr Not()
        {
            if (Match(TokenType.Not))
            {
                Token op = Previous;
                return new UnaryExpr(op, Not(), op.Line);
            }
            return Comparison();
        }

        Expr Comparison()
        {
            Expr left = Term();
            while (Match(TokenType.EqualEqual, TokenType.BangEqual, TokenType.Less, TokenType.LessEqual, TokenType.Greater, TokenType.GreaterEqual))
            {
                Token op = Previous;
                left = new BinaryExpr(left, op, Term(), op.Line);
            }
            return left;
        }

        Expr Term()
        {
            Expr left = Factor();
            while (Match(TokenType.Plus, TokenType.Minus))
            {
                Token op = Previous;
                left = new BinaryExpr(left, op, Factor(), op.Line);
            }
            return left;
        }

        Expr Factor()
        {
            Expr left = Unary();
            while (Match(TokenType.Star, TokenType.Slash, TokenType.SlashSlash, TokenType.Percent))
            {
                Token op = Previous;
                left = new BinaryExpr(left, op, Unary(), op.Line);
            }
            return left;
        }

        Expr Unary()
        {
            if (Match(TokenType.Minus))
            {
                Token op = Previous;
                return new UnaryExpr(op, Unary(), op.Line);
            }
            return Postfix();
        }

        Expr Postfix()
        {
            Expr expression = Primary();

            while (true)
            {
                if (Match(TokenType.LeftParen))
                {
                    int line = Previous.Line;
                    var arguments = new List<Expr>();
                    if (!Check(TokenType.RightParen))
                    {
                        do arguments.Add(Expression());
                        while (Match(TokenType.Comma));
                    }
                    Expect(TokenType.RightParen, "함수에 넣는 값들 끝을 ')'로 닫아 주세요. 값이 여러 개면 쉼표(,)로 나눠요.");
                    expression = new CallExpr(expression, arguments, line);
                }
                else if (Match(TokenType.LeftBracket))
                {
                    int line = Previous.Line;
                    Expr index = Expression();
                    Expect(TokenType.RightBracket, "꺼낼 위치 뒤를 ']'로 닫아 주세요.");
                    expression = new IndexExpr(expression, index, line);
                }
                else
                {
                    return expression;
                }
            }
        }

        Expr Primary()
        {
            Token token = Peek;

            switch (token.Type)
            {
                case TokenType.Number:
                case TokenType.String:
                case TokenType.True:
                case TokenType.False:
                    Advance();
                    return new LiteralExpr(token.Value, token.Line);

                case TokenType.None:
                    Advance();
                    return new LiteralExpr(null, token.Line);

                case TokenType.Identifier:
                    Advance();
                    return new VariableExpr(token.Lexeme, token.Line);

                case TokenType.LeftParen:
                {
                    Advance();
                    Expr inner = Expression();
                    Expect(TokenType.RightParen, "괄호를 ')'로 닫아 주세요.");
                    return inner;
                }

                case TokenType.LeftBracket:
                {
                    Advance();
                    var items = new List<Expr>();
                    while (!Check(TokenType.RightBracket))
                    {
                        items.Add(Expression());
                        if (!Match(TokenType.Comma))
                            break;
                    }
                    Expect(TokenType.RightBracket, "목록을 ']'로 닫아 주세요. 값이 여러 개면 쉼표(,)로 나눠요.");
                    return new ListExpr(items, token.Line);
                }

                case TokenType.Newline:
                case TokenType.EndOfFile:
                    throw Error(token, "값이 와야 하는데 줄이 끝났어요.");

                default:
                    throw Error(token, $"여기에는 값이 와야 해요. (지금은 {Describe(token)} 가 있어요)");
            }
        }

        // ───────── 토큰 다루기 도우미 ─────────

        Token Advance()
        {
            Token token = Peek;
            if (token.Type != TokenType.EndOfFile)
                _current++;
            return token;
        }

        bool Check(TokenType type) => Peek.Type == type;

        bool Match(params TokenType[] types)
        {
            foreach (TokenType type in types)
            {
                if (Check(type))
                {
                    Advance();
                    return true;
                }
            }
            return false;
        }

        Token Expect(TokenType type, string message)
        {
            if (Check(type))
                return Advance();
            throw Error(Peek, message);
        }

        bool IsLineEnd() => Check(TokenType.Newline) || Check(TokenType.EndOfFile);

        static bool IsAssignOperator(TokenType type)
            => type == TokenType.Equal || type == TokenType.PlusEqual || type == TokenType.MinusEqual
            || type == TokenType.StarEqual || type == TokenType.SlashEqual;

        static string Describe(Token token) => token.Type switch
        {
            TokenType.Newline => "줄 끝",
            TokenType.EndOfFile => "코드 끝",
            TokenType.Indent => "들여쓰기",
            TokenType.Dedent => "내어쓰기",
            _ => $"'{token.Lexeme}'",
        };

        static SaessakError Error(Token token, string message)
            => new SaessakError(message, token.Line, token.Column, SaessakErrorKind.Syntax);

        #endregion
    }
}
