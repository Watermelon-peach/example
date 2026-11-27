using System.Collections.Generic;

// AST(추상 구문 트리): 파서가 토큰들을 읽고 만든 "코드의 뼈대"예요.
// 예) "출력(1 + 2)" → CallExpr(출력, [BinaryExpr(1, +, 2)])
// 실행기(Interpreter)는 이 나무를 위에서부터 따라 내려가며 실행해요.
namespace Saessak.Language
{
    // ───────── 식(Expression): 계산하면 값이 나오는 것 ─────────

    public abstract record Expr(int Line);

    /// <summary>숫자, 문자, 참, 거짓, 없음 같은 값 그대로</summary>
    public sealed record LiteralExpr(object Value, int Line) : Expr(Line);

    /// <summary>변수 이름. 예) 개수</summary>
    public sealed record VariableExpr(string Name, int Line) : Expr(Line);

    /// <summary>목록 만들기. 예) [1, 2, 3]</summary>
    public sealed record ListExpr(IReadOnlyList<Expr> Items, int Line) : Expr(Line);

    /// <summary>앞에 붙는 연산. 예) -x, 아님 x</summary>
    public sealed record UnaryExpr(Token Operator, Expr Operand, int Line) : Expr(Line);

    /// <summary>두 값 사이의 연산. 예) a + b, a 그리고 b</summary>
    public sealed record BinaryExpr(Expr Left, Token Operator, Expr Right, int Line) : Expr(Line);

    /// <summary>함수 부르기. 예) 이동(북)</summary>
    public sealed record CallExpr(Expr Callee, IReadOnlyList<Expr> Arguments, int Line) : Expr(Line);

    /// <summary>목록에서 꺼내기. 예) 목록[0]</summary>
    public sealed record IndexExpr(Expr Target, Expr Index, int Line) : Expr(Line);

    // ───────── 문장(Statement): 실행하면 무언가를 하는 것 ─────────

    public abstract record Stmt(int Line);

    /// <summary>식 하나만 있는 줄. 예) 수확()</summary>
    public sealed record ExprStmt(Expr Expression, int Line) : Stmt(Line);

    /// <summary>값 넣기. 예) x = 3, x += 1, 목록[0] = 5</summary>
    public sealed record AssignStmt(Expr Target, Token Operator, Expr Value, int Line) : Stmt(Line);

    /// <summary>만약 / 아니면 만약 한 갈래</summary>
    public sealed record IfBranch(Expr Condition, IReadOnlyList<Stmt> Body);

    /// <summary>만약 … 아니면 만약 … 아니면 … (ElseBody는 없으면 null)</summary>
    public sealed record IfStmt(IReadOnlyList<IfBranch> Branches, IReadOnlyList<Stmt> ElseBody, int Line) : Stmt(Line);

    /// <summary>반복 조건:</summary>
    public sealed record WhileStmt(Expr Condition, IReadOnlyList<Stmt> Body, int Line) : Stmt(Line);

    /// <summary>반복 횟수 번:</summary>
    public sealed record RepeatStmt(Expr Count, IReadOnlyList<Stmt> Body, int Line) : Stmt(Line);

    /// <summary>반복 변수 안에 목록:</summary>
    public sealed record ForStmt(string Variable, Expr Iterable, IReadOnlyList<Stmt> Body, int Line) : Stmt(Line);

    /// <summary>함수 이름(매개변수들):</summary>
    public sealed record FunctionStmt(string Name, IReadOnlyList<string> Parameters, IReadOnlyList<Stmt> Body, int Line) : Stmt(Line);

    /// <summary>돌려주기 값 (값이 없으면 Value는 null)</summary>
    public sealed record ReturnStmt(Expr Value, int Line) : Stmt(Line);

    public sealed record BreakStmt(int Line) : Stmt(Line);

    public sealed record ContinueStmt(int Line) : Stmt(Line);

    public sealed record PassStmt(int Line) : Stmt(Line);

    /// <summary>코드 전체</summary>
    public sealed record ProgramNode(IReadOnlyList<Stmt> Statements);
}
