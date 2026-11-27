namespace Saessak.Language
{
    /// <summary>
    /// 실행기가 "여기서 잠깐 기다려 주세요"라고 게임에 알리는 신호예요.
    /// 시간이 걸리는 함수(Duration &gt; 0)를 실행한 직후에 나와요.
    /// </summary>
    public sealed class Pause
    {
        #region Properties

        public float Seconds { get; }

        #endregion

        #region Custom Methods

        public Pause(float seconds)
        {
            Seconds = seconds;
        }

        #endregion
    }
}
