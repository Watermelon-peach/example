using System.Collections;
using System.Collections.Generic;
using Saessak.Language;
using UnityEngine;

/// <summary>
/// 새싹 코드를 실행해 보는 테스트용 실행기예요.
/// 게임 화면 왼쪽에 코드 입력창, 오른쪽에 출력창을 보여 주고, 같은 내용을 Console 창에도 찍어요.
/// 진짜 게임 화면이 생기기 전까지, '가짜 드론' 명령으로 시간이 걸리는 명령도 흉내 내요.
/// </summary>
public class SaessakConsoleRunner : MonoBehaviour
{
    #region Variables

    const string DefaultCode =
@"# 새싹 언어 맛보기
출력(""안녕, 새싹!"")

함수 팩토리얼(n):
    만약 n <= 1:
        돌려주기 1
    돌려주기 n * 팩토리얼(n - 1)

출력(""5! ="", 팩토리얼(5))

과일들 = [""사과"", ""배"", ""감""]
반복 과일 안에 과일들:
    출력(과일 + "" 수확!"")

# 가짜 드론: 한 칸씩 움직이며 수확할 수 있는 칸에서 수확해요
반복 4번:
    이동(동)
    만약 수확가능():
        수확()
    아니면:
        출력(""여기는 아직 덜 자랐어요"")

출력(""수확한 개수:"", 수확량())
";

    const int MaxLogLines = 300;
    const string CodeControlName = "SaessakCode";

    [Tooltip("실행할 새싹 코드 (게임 화면의 코드 입력창과 같은 내용이에요)")]
    [SerializeField, TextArea(12, 40)] string code = DefaultCode;

    [Tooltip("Play를 누르자마자 코드를 실행할지 여부")]
    [SerializeField] bool runOnStart = true;

    [Tooltip("한 프레임에 실행할 최대 문장 수. 무한 반복이어도 Unity가 멈추지 않게 막아 줘요.")]
    [SerializeField, Min(1)] int stepsPerFrame = 1000;

    [Tooltip("가짜 드론 명령(이동, 수확)이 걸리는 시간(초)")]
    [SerializeField, Min(0f)] float actionSeconds = 0.3f;

    [Tooltip("한글을 보여 줄 OS 폰트 이름들 (앞에서부터 찾아요)")]
    [SerializeField] string[] fontNames = { "Malgun Gothic", "맑은 고딕", "AppleSDGothicNeo", "Noto Sans CJK KR" };

    Coroutine _running;
    string _status = "대기 중";

    // 화면 출력창에 보여 줄 줄들 (오류 줄은 빨간색)
    readonly List<(string text, bool isError)> _log = new List<(string, bool)>();
    Vector2 _codeScroll;
    Vector2 _logScroll;
    bool _scrollLogToEnd;

    Font _koreanFont;
    GUIStyle _codeStyle;
    GUIStyle _logStyle;
    GUIStyle _errorStyle;
    GUIStyle _buttonStyle;
    GUIStyle _titleStyle;
    int _styledFontSize;

#if !ENABLE_LEGACY_INPUT_MANAGER
    bool _warnedNoLegacyInput;
#endif

    // 가짜 드론 상태
    int _droneX;
    int _droneY;
    int _harvested;

    #endregion

    #region Unity Event Methods

    void Awake()
    {
        // 유니티 기본 폰트에는 한글 글자가 없어서, 컴퓨터에 깔린 한글 폰트를 가져와요.
        _koreanFont = Font.CreateDynamicFontFromOSFont(fontNames, 32);
    }

    void Start()
    {
        if (runOnStart)
            Run();
    }

    void OnGUI()
    {
        PrepareStyles();

        float margin = 12f;
        float half = (Screen.width - margin * 3) / 2f;
        float top = margin;
        float buttonHeight = _styledFontSize * 2.2f;
        float panelHeight = Screen.height - margin * 3 - buttonHeight;

        // ── 왼쪽: 코드 입력창 ──
        var codeRect = new Rect(margin, top, half, panelHeight);
        GUI.Box(codeRect, GUIContent.none);
        GUI.Label(new Rect(codeRect.x + 8, codeRect.y + 4, half, _styledFontSize * 1.6f), "코드", _titleStyle);
        var codeInner = new Rect(codeRect.x + 4, codeRect.y + _styledFontSize * 1.8f, codeRect.width - 8, codeRect.height - _styledFontSize * 1.8f - 4);
        DrawCodeEditor(codeInner);

        // ── 오른쪽: 출력창 ──
        var logRect = new Rect(margin * 2 + half, top, half, panelHeight);
        GUI.Box(logRect, GUIContent.none);
        GUI.Label(new Rect(logRect.x + 8, logRect.y + 4, half, _styledFontSize * 1.6f), $"출력  ·  {_status}", _titleStyle);
        var logInner = new Rect(logRect.x + 4, logRect.y + _styledFontSize * 1.8f, logRect.width - 8, logRect.height - _styledFontSize * 1.8f - 4);
        DrawLog(logInner);

        // ── 아래: 버튼 ──
        float buttonY = top + panelHeight + margin;
        float buttonWidth = _styledFontSize * 7f;
        if (GUI.Button(new Rect(margin, buttonY, buttonWidth, buttonHeight), "▶ 실행", _buttonStyle))
            Run();
        if (GUI.Button(new Rect(margin * 2 + buttonWidth, buttonY, buttonWidth, buttonHeight), "■ 멈추기", _buttonStyle))
            Stop();
        if (GUI.Button(new Rect(margin * 2 + half, buttonY, buttonWidth, buttonHeight), "출력 지우기", _buttonStyle))
            _log.Clear();
    }

    #endregion

    #region Custom Methods

    [ContextMenu("코드 실행")]
    public void Run()
    {
        Stop();
        _log.Clear();
        _running = StartCoroutine(RunRoutine(code));
    }

    [ContextMenu("실행 멈추기")]
    public void Stop()
    {
        if (_running == null)
            return;
        StopCoroutine(_running);
        _running = null;
        _status = "멈춤";
        Log("실행을 멈췄어요.", false);
    }

    IEnumerator RunRoutine(string source)
    {
        Interpreter interpreter = null;
        IEnumerator<object> steps = null;
        _status = "실행 중…";

        try
        {
            ProgramNode program = Parser.Parse(source);
            interpreter = new Interpreter();
            interpreter.Output += text => Log(text, false);
            RegisterFakeDrone(interpreter);
            steps = interpreter.Execute(program).GetEnumerator();
        }
        catch (SaessakError error)
        {
            Log(error.Describe(), true);
        }

        if (steps == null)
        {
            _status = "오류";
            _running = null;
            yield break;
        }

        bool failed = false;
        int stepsThisFrame = 0;
        while (true)
        {
            bool hasNext;
            try
            {
                hasNext = steps.MoveNext();
            }
            catch (SaessakError error)
            {
                Log(error.Describe(), true);
                failed = true;
                hasNext = false;
            }

            if (!hasNext)
                break;

            if (steps.Current is Pause pause)
            {
                yield return new WaitForSeconds(pause.Seconds);
                stepsThisFrame = 0;
                continue;
            }

            if (++stepsThisFrame >= stepsPerFrame)
            {
                stepsThisFrame = 0;
                yield return null;
            }
        }

        _status = failed ? "오류" : "끝";
        if (!failed)
            Log($"── 실행 끝 (문장 {interpreter.StepCount}개 실행) ──", false);
        _running = null;
    }

    void Log(string text, bool isError)
    {
        _log.Add((text, isError));
        if (_log.Count > MaxLogLines)
            _log.RemoveAt(0);
        _scrollLogToEnd = true;

        if (isError)
            Debug.LogError($"<b>[새싹]</b> {text}");
        else
            Debug.Log($"<b>[새싹]</b> {text}");
    }

    void DrawCodeEditor(Rect area)
    {
        // Tab 키를 누르면 다음 칸으로 넘어가지 않고 빈칸 4개를 넣어요. (들여쓰기용)
        Event e = Event.current;
        if (e.type == EventType.KeyDown && GUI.GetNameOfFocusedControl() == CodeControlName
            && (e.keyCode == KeyCode.Tab || e.character == '\t'))
        {
            if (e.keyCode == KeyCode.Tab)
            {
                var editor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                editor.ReplaceSelection("    ");
                code = editor.text;
            }
            e.Use();
        }

        float contentHeight = Mathf.Max(area.height, _codeStyle.CalcHeight(new GUIContent(code), area.width - 20f) + 20f);
        _codeScroll = GUI.BeginScrollView(area, _codeScroll, new Rect(0, 0, area.width - 20f, contentHeight));
        GUI.SetNextControlName(CodeControlName);
        code = GUI.TextArea(new Rect(0, 0, area.width - 20f, contentHeight), code, _codeStyle);

        GUI.EndScrollView();

        if (Event.current.type == EventType.Repaint)
            UpdateIme(GUI.GetNameOfFocusedControl() == CodeControlName);
    }

    /// <summary>
    /// 한글 입력기(IME) 켜고 끄기. IMGUI 입력칸은 옛 입력 방식(Input Manager)으로만 IME를 쓸 수 있어서,
    /// Project Settings > Player > Active Input Handling 이 "Both"여야 한글이 쳐져요.
    /// </summary>
    void UpdateIme(bool codeFocused)
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        Input.imeCompositionMode = codeFocused ? IMECompositionMode.On : IMECompositionMode.Auto;
#else
        if (codeFocused && !_warnedNoLegacyInput)
        {
            _warnedNoLegacyInput = true;
            Log("한글을 치려면 Project Settings > Player > Active Input Handling 을 \"Both\"로 바꿔 주세요.", true);
        }
#endif
    }

    void DrawLog(Rect area)
    {
        float width = area.width - 20f;
        float contentHeight = 0f;
        var heights = new float[_log.Count];
        for (int i = 0; i < _log.Count; i++)
        {
            heights[i] = _logStyle.CalcHeight(new GUIContent(_log[i].text), width);
            contentHeight += heights[i];
        }

        if (_scrollLogToEnd && Event.current.type == EventType.Repaint)
        {
            _logScroll.y = Mathf.Max(0f, contentHeight - area.height);
            _scrollLogToEnd = false;
        }

        _logScroll = GUI.BeginScrollView(area, _logScroll, new Rect(0, 0, width, Mathf.Max(contentHeight, area.height)));
        float y = 0f;
        for (int i = 0; i < _log.Count; i++)
        {
            GUI.Label(new Rect(4, y, width - 4, heights[i]), _log[i].text, _log[i].isError ? _errorStyle : _logStyle);
            y += heights[i];
        }
        GUI.EndScrollView();
    }

    void PrepareStyles()
    {
        // 화면 크기에 맞춰 글자 크기를 정해요. 크기가 바뀔 때만 스타일을 다시 만들어요.
        int fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height / 42f), 12, 28);
        if (_codeStyle != null && fontSize == _styledFontSize)
            return;
        _styledFontSize = fontSize;

        _codeStyle = new GUIStyle(GUI.skin.textArea) { font = _koreanFont, fontSize = fontSize, wordWrap = false };
        _logStyle = new GUIStyle(GUI.skin.label) { font = _koreanFont, fontSize = fontSize, wordWrap = true };
        _errorStyle = new GUIStyle(_logStyle);
        _errorStyle.normal.textColor = new Color(1f, 0.45f, 0.45f);
        _buttonStyle = new GUIStyle(GUI.skin.button) { font = _koreanFont, fontSize = fontSize };
        _titleStyle = new GUIStyle(GUI.skin.label) { font = _koreanFont, fontSize = fontSize, fontStyle = FontStyle.Bold };
    }

    void RegisterFakeDrone(Interpreter interpreter)
    {
        _droneX = 0;
        _droneY = 0;
        _harvested = 0;

        foreach (string direction in new[] { "북", "남", "동", "서" })
            interpreter.Define(direction, direction);

        interpreter.DefineFunction("이동", 1, 1, args =>
        {
            switch (args[0] as string)
            {
                case "북": _droneY++; break;
                case "남": _droneY--; break;
                case "동": _droneX++; break;
                case "서": _droneX--; break;
                default:
                    throw new SaessakError("이동 함수에는 북, 남, 동, 서 중 하나를 넣어야 해요.");
            }
            Log($"[가짜 드론] {args[0]}쪽으로 이동 → 위치 ({_droneX}, {_droneY})", false);
            return null;
        }, actionSeconds);

        // 가짜 규칙: x + y 가 짝수인 칸만 다 자랐다고 쳐요.
        interpreter.DefineFunction("수확가능", 0, 0, args => (_droneX + _droneY) % 2 == 0);

        interpreter.DefineFunction("수확", 0, 0, args =>
        {
            _harvested++;
            Log($"[가짜 드론] ({_droneX}, {_droneY}) 칸 수확!", false);
            return null;
        }, actionSeconds);

        interpreter.DefineFunction("수확량", 0, 0, args => _harvested);
    }

    #endregion
}
