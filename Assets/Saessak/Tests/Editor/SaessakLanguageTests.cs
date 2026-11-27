using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Saessak.Language;

/// <summary>
/// 새싹 언어가 문법대로 잘 동작하는지 자동으로 확인하는 테스트들.
/// Unity 메뉴 Window > General > Test Runner > EditMode 에서 실행할 수 있어요.
/// </summary>
public class SaessakLanguageTests
{
    #region Custom Methods

    static string Code(params string[] lines) => string.Join("\n", lines);

    static string Run(string code)
    {
        var interpreter = new Interpreter();
        var output = new List<string>();
        interpreter.Output += output.Add;
        interpreter.RunToEnd(code, 100000);
        return string.Join("\n", output);
    }

    static SaessakError RunError(string code) => Assert.Throws<SaessakError>(() => Run(code));

    // ───────── 값과 계산 ─────────

    [Test]
    public void 숫자_계산()
    {
        Assert.AreEqual("7", Run("출력(1 + 2 * 3)"));
        Assert.AreEqual("9", Run("출력((1 + 2) * 3)"));
        Assert.AreEqual("3.5", Run("출력(7 / 2)"));
        Assert.AreEqual("3", Run("출력(7 // 2)"));
        Assert.AreEqual("-4", Run("출력(-7 // 2)"));
        Assert.AreEqual("1", Run("출력(7 % 3)"));
        Assert.AreEqual("2", Run("출력(-1 % 3)"));
        Assert.AreEqual("0.3", Run("출력(0.1 + 0.2)"));
        Assert.AreEqual("-5", Run("출력(-(2 + 3))"));
    }

    [Test]
    public void 문자와_출력()
    {
        Assert.AreEqual("개수: 3", Run("출력(\"개수: \" + 3)"));
        Assert.AreEqual("a 1 참 없음", Run("출력('a', 1, 참, 없음)"));
        Assert.AreEqual("줄\n바꿈", Run("출력(\"줄\\n바꿈\")"));
        Assert.AreEqual("[1, \"가\", 거짓]", Run("출력([1, \"가\", 거짓])"));
    }

    [Test]
    public void 비교와_논리()
    {
        Assert.AreEqual("참 거짓 참 참", Run("출력(1 < 2, 2 <= 1, \"가\" == \"가\", [1, 2] == [1, 2])"));
        Assert.AreEqual("거짓 참", Run("출력(아님 참, 아님 0)"));
        Assert.AreEqual("참", Run("출력(1 < 2 그리고 2 < 3 또는 거짓)"));
    }

    [Test]
    public void 단락_평가는_오른쪽을_계산하지_않음()
    {
        // 오른쪽에 없는 함수가 있어도, 왼쪽만으로 답이 정해지면 오류가 나지 않아요.
        Assert.AreEqual("거짓 참", Run("출력(거짓 그리고 없는함수(), 참 또는 없는함수())"));
    }

    // ───────── 변수 ─────────

    [Test]
    public void 변수와_복합_대입()
    {
        Assert.AreEqual("10", Run(Code("x = 1", "x += 4", "x *= 2", "출력(x)")));
        Assert.AreEqual("2.5", Run(Code("x = 10", "x -= 5", "x /= 2", "출력(x)")));
    }

    [Test]
    public void 한글_변수_이름()
    {
        Assert.AreEqual("당근", Run(Code("작물_1 = \"당근\"", "출력(작물_1)")));
        // '참'으로 시작하지만 키워드가 아닌 이름
        Assert.AreEqual("5", Run(Code("참외 = 5", "출력(참외)")));
    }

    // ───────── 조건과 반복 ─────────

    [Test]
    public void 만약_아니면만약_아니면()
    {
        string code = Code(
            "함수 크기(n):",
            "    만약 n > 10:",
            "        돌려주기 \"큼\"",
            "    아니면 만약 n > 5:",
            "        돌려주기 \"중간\"",
            "    아니면:",
            "        돌려주기 \"작음\"",
            "출력(크기(20), 크기(7), 크기(1))");
        Assert.AreEqual("큼 중간 작음", Run(code));
    }

    [Test]
    public void 한_줄_블록()
    {
        Assert.AreEqual("한줄", Run(Code("만약 참: 출력(\"한줄\")", "아니면: 출력(\"아님\")")));
    }

    [Test]
    public void 조건_반복()
    {
        Assert.AreEqual("10", Run(Code("i = 0", "반복 i < 10:", "    i += 1", "출력(i)")));
    }

    [Test]
    public void 횟수_반복()
    {
        Assert.AreEqual("3", Run(Code("n = 0", "반복 3 번:", "    n += 1", "출력(n)")));
        // 숫자와 '번'을 붙여 써도 돼요.
        Assert.AreEqual("2", Run(Code("n = 0", "반복 2번:", "    n += 1", "출력(n)")));
        Assert.AreEqual("0", Run(Code("n = 0", "반복 0번:", "    n += 1", "출력(n)")));
    }

    [Test]
    public void 안에_반복()
    {
        Assert.AreEqual("10", Run(Code("합 = 0", "반복 i 안에 범위(5):", "    합 += i", "출력(합)")));
        Assert.AreEqual("가\n나", Run(Code("반복 글자 안에 \"가나\":", "    출력(글자)")));
        Assert.AreEqual("[2, 4, 6]", Run(Code("결과 = []", "반복 x 안에 [1, 2, 3]:", "    추가(결과, x * 2)", "출력(결과)")));
    }

    [Test]
    public void 멈추기와_건너뛰기()
    {
        string code = Code(
            "반복 i 안에 범위(10):",
            "    만약 i == 5:",
            "        멈추기",
            "    만약 i % 2 == 0:",
            "        건너뛰기",
            "    출력(i)");
        Assert.AreEqual("1\n3", Run(code));
    }

    [Test]
    public void 반복_안의_돌려주기는_함수를_끝냄()
    {
        string code = Code(
            "함수 찾기(목록, 값):",
            "    반복 i 안에 범위(길이(목록)):",
            "        만약 목록[i] == 값:",
            "            돌려주기 i",
            "    돌려주기 -1",
            "출력(찾기([5, 6, 7], 6), 찾기([5], 9))");
        Assert.AreEqual("1 -1", Run(code));
    }

    // ───────── 함수 ─────────

    [Test]
    public void 재귀_함수()
    {
        string code = Code(
            "함수 팩토리얼(n):",
            "    만약 n <= 1:",
            "        돌려주기 1",
            "    돌려주기 n * 팩토리얼(n - 1)",
            "출력(팩토리얼(10))");
        Assert.AreEqual("3628800", Run(code));
    }

    [Test]
    public void 돌려주기_없는_함수는_없음()
    {
        Assert.AreEqual("없음", Run(Code("함수 아무것도():", "    통과", "출력(아무것도())")));
    }

    [Test]
    public void 함수_안에서_바깥_변수_바꾸기()
    {
        string code = Code(
            "수확량 = 0",
            "함수 수확하기():",
            "    수확량 += 1",
            "    임시 = 99",
            "수확하기()",
            "수확하기()",
            "출력(수확량)");
        Assert.AreEqual("2", Run(code));
        // 함수 안에서 처음 만든 변수는 바깥에서 보이지 않아요.
        Assert.AreEqual(8, RunError(code + "\n출력(임시)").Line);
    }

    [Test]
    public void 주석과_빈_줄()
    {
        string code = Code(
            "# 맨 위 주석",
            "",
            "반복 2번:   # 줄 끝 주석",
            "",
            "        # 들여쓰기가 다른 주석 줄은 무시돼요",
            "    출력(\"안녕\")",
            "   ",
            "출력(\"끝\")");
        Assert.AreEqual("안녕\n안녕\n끝", Run(code));
    }

    [Test]
    public void 괄호_안에서는_줄을_바꿔도_됨()
    {
        Assert.AreEqual("6", Run(Code("합 = 최대(1,", "    6,", "  3)", "출력(합)")));
    }

    // ───────── 목록과 기본 함수 ─────────

    [Test]
    public void 목록_다루기()
    {
        string code = Code(
            "목록 = [10, 20, 30]",
            "목록[0] = 1",
            "목록[-1] += 5",
            "추가(목록, 40)",
            "출력(목록, 길이(목록), 목록[1])",
            "출력(꺼내기(목록), 목록)");
        Assert.AreEqual("[1, 20, 35, 40] 4 20\n40 [1, 20, 35]", Run(code));
    }

    [Test]
    public void 기본_함수들()
    {
        Assert.AreEqual("[0, 1, 2]", Run("출력(범위(3))"));
        Assert.AreEqual("[2, 4]", Run("출력(범위(2, 6, 2))"));
        Assert.AreEqual("[3, 2, 1]", Run("출력(범위(3, 0, -1))"));
        Assert.AreEqual("3 -2 12", Run("출력(정수(3.9), 정수(-2.5), 정수(\"12\"))"));
        Assert.AreEqual("1 9 2", Run("출력(최소(3, 1, 2), 최대([9, 4]), 절댓값(-2))"));
        Assert.AreEqual("5개", Run("출력(문자(5) + \"개\")"));

        string dice = Run("출력(무작위(1, 6))");
        Assert.That(int.Parse(dice), Is.InRange(1, 6));
    }

    // ───────── 오류 메시지 ─────────

    [Test]
    public void 오류_없는_이름은_줄번호와_함께()
    {
        SaessakError error = RunError(Code("x = 1", "", "출력(y)"));
        Assert.AreEqual(3, error.Line);
        Assert.AreEqual(SaessakErrorKind.Runtime, error.Kind);
        StringAssert.Contains("y", error.Message);
    }

    [Test]
    public void 오류_문법()
    {
        // ':' 빠짐
        Assert.AreEqual(SaessakErrorKind.Syntax, RunError("만약 참\n    출력(1)").Kind);
        // 비교에 '=' 사용
        StringAssert.Contains("==", RunError("만약 x = 3:\n    출력(1)").Message);
        // 들여쓰기 안 함
        StringAssert.Contains("들여써야", RunError("만약 참:\n출력(1)").Message);
        // 들여쓰기 칸 수가 어긋남
        Assert.AreEqual(3, RunError(Code("만약 참:", "    출력(1)", "  출력(2)")).Line);
        // 이유 없이 들여씀
        StringAssert.Contains("들여쓰기", RunError("출력(1)\n    출력(2)").Message);
        // 괄호 안 닫음
        StringAssert.Contains("닫히지", RunError("출력(1, 2").Message);
        // 엉뚱한 곳의 멈추기 / 돌려주기
        StringAssert.Contains("반복 안에서만", RunError("멈추기").Message);
        StringAssert.Contains("함수 안에서만", RunError("돌려주기 3").Message);
        // 문자 안 닫음
        StringAssert.Contains("따옴표", RunError("출력(\"안녕)").Message);
    }

    [Test]
    public void 오류_실행()
    {
        StringAssert.Contains("0으로", RunError("출력(1 / 0)").Message);
        StringAssert.Contains("범위를 벗어", RunError("출력([1, 2][5])").Message);
        StringAssert.Contains("2개", RunError("추가([1])").Message);
        StringAssert.Contains("()", RunError("함수 수확():\n    통과\n수확").Message);
        StringAssert.Contains("계산", RunError("출력([1] - 1)").Message);
        StringAssert.Contains("너무 깊게", RunError("함수 끝없이():\n    끝없이()\n끝없이()").Message);
    }

    [Test]
    public void 오류_기본함수_오류에도_줄번호()
    {
        Assert.AreEqual(2, RunError("출력(1)\n출력(정수(\"사과\"))").Line);
    }

    [Test]
    public void 무한_반복은_정해진_횟수에서_멈춤()
    {
        StringAssert.Contains("끝나지 않는 반복", RunError("반복 참:\n    통과").Message);
    }

    // ───────── 게임 연결용 기능 ─────────

    [Test]
    public void 한_걸음씩_실행됨()
    {
        // 무한 반복이어도 원하는 만큼만 실행하고 멈출 수 있어야 게임이 멈추지 않아요.
        var interpreter = new Interpreter();
        var steps = interpreter.Execute(Parser.Parse("반복 참:\n    x = 1")).Take(500).ToList();
        Assert.AreEqual(500, steps.Count);
    }

    [Test]
    public void 시간이_걸리는_명령은_Pause를_내보냄()
    {
        var interpreter = new Interpreter();
        int moves = 0;
        interpreter.Define("북", "북");
        interpreter.DefineFunction("이동", 1, 1, args => { moves++; return true; }, 0.5f);

        List<Pause> pauses = interpreter.Execute(Parser.Parse("반복 3번:\n    이동(북)"))
            .OfType<Pause>()
            .ToList();

        Assert.AreEqual(3, moves);
        Assert.AreEqual(3, pauses.Count);
        Assert.AreEqual(0.5f, pauses[0].Seconds);
    }

    [Test]
    public void 게임_함수가_돌려준_int는_숫자로_바뀜()
    {
        var interpreter = new Interpreter();
        var output = new List<string>();
        interpreter.Output += output.Add;
        interpreter.DefineFunction("개수", 0, 0, args => 7);
        interpreter.RunToEnd("출력(개수() + 1)");
        Assert.AreEqual("8", output.Single());
    }

    #endregion
}
