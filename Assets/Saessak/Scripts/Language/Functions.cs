using System;

namespace Saessak.Language
{
    /// <summary>
    /// 새싹 코드에서 '함수'로 만든 함수.
    /// 함수가 만들어진 곳의 스코프(Closure)를 기억해 둬서, 나중에 불려도 바깥 변수를 찾을 수 있어요.
    /// </summary>
    public sealed class UserFunction
    {
        #region Properties

        public FunctionStmt Declaration { get; }
        public Scope Closure { get; }
        public string Name => Declaration.Name;

        #endregion

        #region Custom Methods

        public UserFunction(FunctionStmt declaration, Scope closure)
        {
            Declaration = declaration;
            Closure = closure;
        }

        #endregion
    }

    /// <summary>
    /// C#으로 만들어서 새싹에 넣어 준 함수. (출력, 길이, 나중에는 이동·수확 같은 드론 명령)
    /// </summary>
    public sealed class NativeFunction
    {
        #region Properties

        public string Name { get; }

        /// <summary>받을 수 있는 값 개수 최솟값</summary>
        public int MinArgs { get; }

        /// <summary>받을 수 있는 값 개수 최댓값 (-1이면 제한 없음)</summary>
        public int MaxArgs { get; }

        public Func<object[], object> Body { get; }

        /// <summary>
        /// 이 명령이 게임 안에서 걸리는 시간(초). 0보다 크면 실행기가 Pause를 내보내서
        /// 게임 쪽이 그만큼 기다렸다가(드론 애니메이션 등) 다음 명령을 실행하게 해요.
        /// </summary>
        public float Duration { get; }

        #endregion

        #region Custom Methods

        public NativeFunction(string name, int minArgs, int maxArgs, Func<object[], object> body, float duration)
        {
            Name = name;
            MinArgs = minArgs;
            MaxArgs = maxArgs;
            Body = body;
            Duration = duration;
        }

        #endregion
    }
}
