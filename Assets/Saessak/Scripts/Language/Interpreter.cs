using System;
using System.Collections.Generic;

namespace Saessak.Language
{
    /// <summary>
    /// 실행기(Interpreter): 파서가 만든 AST를 따라가며 코드를 실제로 실행해요.
    ///
    /// 게임에서는 코드를 한 번에 끝까지 돌리면 안 돼요. (무한 반복이면 게임이 멈추고,
    /// 드론이 움직이는 동안 기다려야 하니까요.) 그래서 실행 함수들을 C#의 이터레이터
    /// (yield return을 쓰는 함수)로 만들어서, 문장 하나를 실행할 때마다 잠깐 멈출 수 있게 했어요.
    ///   - null  : 문장 하나 실행함 (게임은 한 프레임에 여러 개를 실행해도 돼요)
    ///   - Pause : 시간이 걸리는 명령을 실행함 → 게임이 그 시간만큼 기다려 줘야 해요
    /// </summary>
    public sealed class Interpreter
    {
        // break / continue / return이 일어났다는 표시. 블록을 실행하다 이게 None이 아니면 멈춰요.
        enum Flow
        {
            None,
            Break,
            Continue,
            Return,
        }

        #region Variables

        /// <summary>함수가 함수를 부르는 깊이 제한 (끝없는 재귀를 막아요)</summary>
        public const int MaxCallDepth = 200;

        /// <summary>출력 함수가 글자를 내보낼 때 불려요.</summary>
        public event Action<string> Output;

        readonly Scope _globals = new Scope(null);
        Scope _scope;
        Flow _flow;
        int _callDepth;

        // 식을 계산한 결과는 여기에 담겨요. (이터레이터는 값을 바로 돌려줄 수 없어서 이렇게 해요)
        object _value;
        object _returnValue;

        #endregion

        #region Properties

        /// <summary>지금까지 실행한 문장 수</summary>
        public long StepCount { get; private set; }

        #endregion

        #region Custom Methods

        public Interpreter(bool withBuiltins = true)
        {
            _scope = _globals;
            if (withBuiltins)
                Builtins.Register(this);
        }

        // ───────── 게임(C#) 쪽에서 쓰는 기능 ─────────

        /// <summary>전역 변수를 미리 넣어 둬요. 예) Define("북", "북")</summary>
        public void Define(string name, object value)
        {
            _globals.Define(name, SaessakValues.FromHost(value));
        }

        /// <summary>
        /// C# 함수를 새싹 함수로 넣어 줘요.
        /// maxArgs가 -1이면 값을 몇 개든 받아요. duration은 게임 안에서 걸리는 시간(초)이에요.
        /// </summary>
        public void DefineFunction(string name, int minArgs, int maxArgs, Func<object[], object> body, float duration = 0f)
        {
            _globals.Define(name, new NativeFunction(name, minArgs, maxArgs, body, duration));
        }

        public void Print(string text)
        {
            Output?.Invoke(text);
        }

        /// <summary>
        /// 코드를 한 걸음씩 실행해요. foreach로 돌리거나, GetEnumerator().MoveNext()를 직접 불러서
        /// 원하는 만큼씩 실행하면 돼요. 오류가 나면 MoveNext()에서 SaessakError가 나와요.
        /// </summary>
        public IEnumerable<object> Execute(ProgramNode program)
        {
            _scope = _globals;
            _flow = Flow.None;
            _callDepth = 0;
            StepCount = 0;

            foreach (object step in ExecBlock(program.Statements))
                yield return step;
        }

        /// <summary>
        /// 코드를 끝까지 한 번에 실행해요. (테스트용. Pause는 기다리지 않고 넘어가요)
        /// maxSteps보다 많이 실행하면 무한 반복으로 보고 멈춰요.
        /// </summary>
        public void RunToEnd(string source, long maxSteps = 1000000)
        {
            ProgramNode program = Parser.Parse(source);
            foreach (object _ in Execute(program))
            {
                if (StepCount > maxSteps)
                    throw new SaessakError($"명령을 {maxSteps}번 넘게 실행해서 멈췄어요. 끝나지 않는 반복이 있는지 확인해 보세요.");
            }
        }

        // ───────── 문장 실행 ─────────

        IEnumerable<object> ExecBlock(IReadOnlyList<Stmt> statements)
        {
            foreach (Stmt statement in statements)
            {
                foreach (object step in Exec(statement))
                    yield return step;

                if (_flow != Flow.None)
                    yield break;
            }
        }

        IEnumerable<object> Exec(Stmt statement)
        {
            StepCount++;
            yield return null;

            switch (statement)
            {
                case ExprStmt s:
                    if (s.Expression is VariableExpr bare && SaessakValues.IsFunction(Lookup(bare)))
                        throw Error($"함수를 실행하려면 이름 뒤에 ()를 붙여야 해요. 예) {bare.Name}()", s.Line);
                    foreach (object step in Eval(s.Expression))
                        yield return step;
                    break;

                case AssignStmt s:
                    foreach (object step in ExecAssign(s))
                        yield return step;
                    break;

                case IfStmt s:
                    foreach (IfBranch branch in s.Branches)
                    {
                        foreach (object step in Eval(branch.Condition))
                            yield return step;

                        if (SaessakValues.IsTruthy(_value))
                        {
                            foreach (object step in ExecBlock(branch.Body))
                                yield return step;
                            yield break;
                        }
                    }
                    if (s.ElseBody != null)
                    {
                        foreach (object step in ExecBlock(s.ElseBody))
                            yield return step;
                    }
                    break;

                case WhileStmt s:
                    while (true)
                    {
                        foreach (object step in Eval(s.Condition))
                            yield return step;
                        if (!SaessakValues.IsTruthy(_value))
                            break;

                        foreach (object step in ExecBlock(s.Body))
                            yield return step;
                        if (ShouldLeaveLoop())
                            break;
                    }
                    break;

                case RepeatStmt s:
                {
                    foreach (object step in Eval(s.Count))
                        yield return step;
                    int times = ToRepeatCount(_value, s.Line);

                    for (int i = 0; i < times; i++)
                    {
                        foreach (object step in ExecBlock(s.Body))
                            yield return step;
                        if (ShouldLeaveLoop())
                            break;
                    }
                    break;
                }

                case ForStmt s:
                {
                    foreach (object step in Eval(s.Iterable))
                        yield return step;
                    List<object> items = ToIterable(_value, s.Line);

                    foreach (object item in items)
                    {
                        _scope.Assign(s.Variable, item);
                        foreach (object step in ExecBlock(s.Body))
                            yield return step;
                        if (ShouldLeaveLoop())
                            break;
                    }
                    break;
                }

                case FunctionStmt s:
                    _scope.Define(s.Name, new UserFunction(s, _scope));
                    break;

                case ReturnStmt s:
                    _value = null;
                    if (s.Value != null)
                    {
                        foreach (object step in Eval(s.Value))
                            yield return step;
                    }
                    _returnValue = _value;
                    _flow = Flow.Return;
                    break;

                case BreakStmt _:
                    _flow = Flow.Break;
                    break;

                case ContinueStmt _:
                    _flow = Flow.Continue;
                    break;

                case PassStmt _:
                    break;

                default:
                    throw new InvalidOperationException($"처리하지 못한 문장 종류: {statement.GetType().Name}");
            }
        }

        IEnumerable<object> ExecAssign(AssignStmt s)
        {
            bool compound = s.Operator.Type != TokenType.Equal;

            switch (s.Target)
            {
                case VariableExpr variable:
                {
                    foreach (object step in Eval(s.Value))
                        yield return step;
                    object value = _value;

                    if (compound)
                        value = ApplyCompound(s.Operator, Lookup(variable), value, s.Line);

                    _scope.Assign(variable.Name, value);
                    break;
                }

                case IndexExpr indexed:
                {
                    foreach (object step in Eval(indexed.Target))
                        yield return step;
                    object target = _value;

                    foreach (object step in Eval(indexed.Index))
                        yield return step;
                    object index = _value;

                    foreach (object step in Eval(s.Value))
                        yield return step;
                    object value = _value;

                    if (compound)
                        value = ApplyCompound(s.Operator, GetIndex(target, index, s.Line), value, s.Line);

                    SetIndex(target, index, value, s.Line);
                    break;
                }
            }
        }

        /// <summary>반복 블록이 끝난 뒤 불러요. 반복을 그만둬야 하면 true.</summary>
        bool ShouldLeaveLoop()
        {
            switch (_flow)
            {
                case Flow.Break:
                    _flow = Flow.None;
                    return true;
                case Flow.Continue:
                    _flow = Flow.None;
                    return false;
                case Flow.Return:
                    return true; // 함수 끝까지 그대로 올라가야 하니 표시는 지우지 않아요.
                default:
                    return false;
            }
        }

        // ───────── 식 계산 (결과는 _value에 담겨요) ─────────

        IEnumerable<object> Eval(Expr expression)
        {
            switch (expression)
            {
                case LiteralExpr e:
                    _value = e.Value;
                    break;

                case VariableExpr e:
                    _value = Lookup(e);
                    break;

                case ListExpr e:
                {
                    var list = new List<object>(e.Items.Count);
                    foreach (Expr item in e.Items)
                    {
                        foreach (object step in Eval(item))
                            yield return step;
                        list.Add(_value);
                    }
                    _value = list;
                    break;
                }

                case UnaryExpr e:
                    foreach (object step in Eval(e.Operand))
                        yield return step;
                    _value = ApplyUnary(e.Operator, _value, e.Line);
                    break;

                case BinaryExpr e when e.Operator.Type == TokenType.And || e.Operator.Type == TokenType.Or:
                {
                    // 그리고/또는: 왼쪽만 보고 답이 정해지면 오른쪽은 계산하지 않아요. (단락 평가)
                    foreach (object step in Eval(e.Left))
                        yield return step;

                    bool leftTruthy = SaessakValues.IsTruthy(_value);
                    bool decided = e.Operator.Type == TokenType.Or ? leftTruthy : !leftTruthy;
                    if (decided)
                        break;

                    foreach (object step in Eval(e.Right))
                        yield return step;
                    break;
                }

                case BinaryExpr e:
                {
                    foreach (object step in Eval(e.Left))
                        yield return step;
                    object left = _value;

                    foreach (object step in Eval(e.Right))
                        yield return step;

                    _value = ApplyBinary(e.Operator.Type, e.Operator.Lexeme, left, _value, e.Line);
                    break;
                }

                case IndexExpr e:
                {
                    foreach (object step in Eval(e.Target))
                        yield return step;
                    object target = _value;

                    foreach (object step in Eval(e.Index))
                        yield return step;

                    _value = GetIndex(target, _value, e.Line);
                    break;
                }

                case CallExpr e:
                    foreach (object step in Call(e))
                        yield return step;
                    break;

                default:
                    throw new InvalidOperationException($"처리하지 못한 식 종류: {expression.GetType().Name}");
            }
        }

        IEnumerable<object> Call(CallExpr call)
        {
            foreach (object step in Eval(call.Callee))
                yield return step;
            object callee = _value;

            var args = new object[call.Arguments.Count];
            for (int i = 0; i < args.Length; i++)
            {
                foreach (object step in Eval(call.Arguments[i]))
                    yield return step;
                args[i] = _value;
            }

            switch (callee)
            {
                case NativeFunction native:
                {
                    CheckArgumentCount(native.Name, native.MinArgs, native.MaxArgs, args.Length, call.Line);

                    object result;
                    try
                    {
                        result = SaessakValues.FromHost(native.Body(args));
                    }
                    catch (SaessakError error) when (error.Line == 0)
                    {
                        // C# 함수는 몇 번째 줄인지 모르니, 여기서 줄 번호를 붙여 줘요.
                        throw Error(error.Message, call.Line);
                    }

                    if (native.Duration > 0f)
                        yield return new Pause(native.Duration);

                    _value = result;
                    break;
                }

                case UserFunction function:
                {
                    IReadOnlyList<string> parameters = function.Declaration.Parameters;
                    CheckArgumentCount(function.Name, parameters.Count, parameters.Count, args.Length, call.Line);

                    if (_callDepth >= MaxCallDepth)
                        throw Error($"함수가 너무 깊게 불렸어요. (최대 {MaxCallDepth}단계) 함수가 자기 자신을 끝없이 부르고 있지 않은지 확인해 보세요.", call.Line);

                    var local = new Scope(function.Closure);
                    for (int i = 0; i < parameters.Count; i++)
                        local.Define(parameters[i], args[i]);

                    Scope saved = _scope;
                    _scope = local;
                    _callDepth++;

                    foreach (object step in ExecBlock(function.Declaration.Body))
                        yield return step;

                    _callDepth--;
                    _scope = saved;

                    _value = _flow == Flow.Return ? _returnValue : null;
                    _returnValue = null;
                    _flow = Flow.None;
                    break;
                }

                default:
                    throw Error($"함수가 아닌 값은 실행할 수 없어요. (지금: {SaessakValues.TypeName(callee)})", call.Line);
            }
        }

        // ───────── 계산 규칙 ─────────

        object Lookup(VariableExpr variable)
        {
            if (_scope.TryGet(variable.Name, out object value))
                return value;
            throw Error($"찾을 수 없는 이름이에요: {variable.Name}  (오타가 있거나, 아직 값을 넣지 않은 변수일 수 있어요)", variable.Line);
        }

        static object ApplyUnary(Token op, object operand, int line)
        {
            if (op.Type == TokenType.Not)
                return !SaessakValues.IsTruthy(operand);

            if (operand is double d)
                return -d;
            throw Error($"'-'는 숫자 앞에만 붙일 수 있어요. (지금: {SaessakValues.TypeName(operand)})", line);
        }

        static object ApplyCompound(Token op, object current, object value, int line)
        {
            TokenType basic = op.Type switch
            {
                TokenType.PlusEqual => TokenType.Plus,
                TokenType.MinusEqual => TokenType.Minus,
                TokenType.StarEqual => TokenType.Star,
                _ => TokenType.Slash,
            };
            string symbol = op.Lexeme.Substring(0, 1);
            return ApplyBinary(basic, symbol, current, value, line);
        }

        static object ApplyBinary(TokenType op, string symbol, object left, object right, int line)
        {
            switch (op)
            {
                case TokenType.Plus:
                    if (left is double a && right is double b)
                        return a + b;
                    if (left is string || right is string)
                        return SaessakValues.ToDisplay(left) + SaessakValues.ToDisplay(right);
                    if (left is List<object> listA && right is List<object> listB)
                    {
                        var joined = new List<object>(listA);
                        joined.AddRange(listB);
                        return joined;
                    }
                    throw TypeMismatch(symbol, left, right, line);

                case TokenType.Minus:
                {
                    (double l, double r) = BothNumbers(symbol, left, right, line);
                    return l - r;
                }

                case TokenType.Star:
                {
                    (double l, double r) = BothNumbers(symbol, left, right, line);
                    return l * r;
                }

                case TokenType.Slash:
                {
                    (double l, double r) = BothNumbers(symbol, left, right, line);
                    CheckNotZero(r, line);
                    return l / r;
                }

                case TokenType.SlashSlash:
                {
                    (double l, double r) = BothNumbers(symbol, left, right, line);
                    CheckNotZero(r, line);
                    return Math.Floor(l / r);
                }

                case TokenType.Percent:
                {
                    // 파이썬처럼 나머지의 부호는 나누는 수를 따라가요. (-1 % 3 == 2)
                    (double l, double r) = BothNumbers(symbol, left, right, line);
                    CheckNotZero(r, line);
                    return l - r * Math.Floor(l / r);
                }

                case TokenType.EqualEqual:
                    return SaessakValues.AreEqual(left, right);

                case TokenType.BangEqual:
                    return !SaessakValues.AreEqual(left, right);

                case TokenType.Less:
                    return Compare(symbol, left, right, line) < 0;
                case TokenType.LessEqual:
                    return Compare(symbol, left, right, line) <= 0;
                case TokenType.Greater:
                    return Compare(symbol, left, right, line) > 0;
                case TokenType.GreaterEqual:
                    return Compare(symbol, left, right, line) >= 0;

                default:
                    throw new InvalidOperationException($"처리하지 못한 연산자: {symbol}");
            }
        }

        static (double, double) BothNumbers(string symbol, object left, object right, int line)
        {
            if (left is double a && right is double b)
                return (a, b);
            throw TypeMismatch(symbol, left, right, line);
        }

        static int Compare(string symbol, object left, object right, int line)
        {
            if (left is double a && right is double b)
                return a.CompareTo(b);
            if (left is string x && right is string y)
                return string.CompareOrdinal(x, y);
            throw Error($"크기 비교({symbol})는 숫자끼리나 문자끼리만 할 수 있어요. (지금: {SaessakValues.TypeName(left)} {symbol} {SaessakValues.TypeName(right)})", line);
        }

        static void CheckNotZero(double divisor, int line)
        {
            if (divisor == 0)
                throw Error("0으로 나눌 수 없어요.", line);
        }

        static object GetIndex(object target, object index, int line)
        {
            switch (target)
            {
                case List<object> list:
                    return list[ToIndex(index, list.Count, line)];
                case string text:
                    return text[ToIndex(index, text.Length, line)].ToString();
                default:
                    throw Error($"[ ]로 꺼낼 수 있는 건 목록이나 문자뿐이에요. (지금: {SaessakValues.TypeName(target)})", line);
            }
        }

        static void SetIndex(object target, object index, object value, int line)
        {
            if (target is List<object> list)
            {
                list[ToIndex(index, list.Count, line)] = value;
                return;
            }
            if (target is string)
                throw Error("문자 속 글자는 바꿀 수 없어요. 새 문자를 만들어서 넣어 주세요.", line);
            throw Error($"[ ]로 값을 넣을 수 있는 건 목록뿐이에요. (지금: {SaessakValues.TypeName(target)})", line);
        }

        /// <summary>목록 위치 계산. -1은 맨 끝, -2는 끝에서 두 번째 … (파이썬과 같아요)</summary>
        static int ToIndex(object index, int count, int line)
        {
            if (!(index is double d) || d != Math.Floor(d))
                throw Error($"꺼낼 위치는 정수여야 해요. (지금: {SaessakValues.ToDisplay(index)})", line);

            double position = d < 0 ? d + count : d;
            if (position < 0 || position >= count)
                throw Error($"목록 범위를 벗어났어요. (위치 {SaessakValues.ToDisplay(d)}, 길이 {count}) 위치는 0부터 시작해요.", line);
            return (int)position;
        }

        static int ToRepeatCount(object value, int line)
        {
            if (value is double d && d >= 0 && d == Math.Floor(d) && d <= int.MaxValue)
                return (int)d;
            throw Error($"반복 횟수에는 0 이상의 정수를 써야 해요. (지금: {SaessakValues.ToDisplay(value)})", line);
        }

        static List<object> ToIterable(object value, int line)
        {
            switch (value)
            {
                case List<object> list:
                    // 반복하는 도중에 목록이 바뀌어도 헷갈리지 않게 복사본으로 돌아요.
                    return new List<object>(list);
                case string text:
                {
                    var letters = new List<object>(text.Length);
                    foreach (char c in text)
                        letters.Add(c.ToString());
                    return letters;
                }
                default:
                    throw Error($"'안에' 뒤에는 목록이나 문자가 와야 해요. 숫자만큼 반복하려면 범위(5)처럼 써 보세요. (지금: {SaessakValues.TypeName(value)})", line);
            }
        }

        static void CheckArgumentCount(string name, int min, int max, int count, int line)
        {
            if (count >= min && (max < 0 || count <= max))
                return;

            string expected = max < 0 ? $"{min}개 이상"
                            : min == max ? $"{min}개"
                            : $"{min}~{max}개";
            throw Error($"{name} 함수에는 값을 {expected} 넣어야 해요. (지금 {count}개)", line);
        }

        static SaessakError TypeMismatch(string symbol, object left, object right, int line)
            => Error($"'{symbol}' 계산을 할 수 없는 값들이에요. (지금: {SaessakValues.TypeName(left)} {symbol} {SaessakValues.TypeName(right)})", line);

        static SaessakError Error(string message, int line)
            => new SaessakError(message, line, 0, SaessakErrorKind.Runtime);

        #endregion
    }
}
