using System;

namespace Saessak.Language
{
    public enum SaessakErrorKind
    {
        Syntax,
        Runtime,
    }

    /// <summary>
    /// 새싹 코드에서 생긴 오류예요. 몇 번째 줄에서 났는지와, 초보자도 알아볼 수 있는 설명을 담아요.
    /// </summary>
    public sealed class SaessakError : Exception
    {
        #region Properties

        /// <summary>오류가 난 줄 번호 (1부터 시작, 모르면 0)</summary>
        public int Line { get; }

        /// <summary>오류가 난 칸 번호 (1부터 시작, 모르면 0)</summary>
        public int Column { get; }

        public SaessakErrorKind Kind { get; }

        #endregion

        #region Custom Methods

        public SaessakError(string message, int line = 0, int column = 0, SaessakErrorKind kind = SaessakErrorKind.Runtime)
            : base(message)
        {
            Line = line;
            Column = column;
            Kind = kind;
        }

        /// <summary>
        /// 화면에 보여 줄 한 줄짜리 설명. 예: "[3번째 줄] 문법 오류: 줄 끝에 ':'(쌍점)이 필요해요."
        /// </summary>
        public string Describe()
        {
            string kindText = Kind == SaessakErrorKind.Syntax ? "문법 오류" : "실행 오류";
            return Line > 0 ? $"[{Line}번째 줄] {kindText}: {Message}" : $"{kindText}: {Message}";
        }

        #endregion
    }
}
