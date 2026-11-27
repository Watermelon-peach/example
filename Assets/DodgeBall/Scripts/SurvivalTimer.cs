using UnityEngine;
using UnityEngine.UI;

namespace DodgeBall
{
    /// <summary>
    /// Counts how long the player has survived and shows it in whole seconds.
    /// Stops counting once the game is over.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class SurvivalTimer : MonoBehaviour
    {
        #region 변수

        Text label;
        int shownSeconds = -1;

        #endregion

        #region 프로퍼티

        public float ElapsedSeconds { get; private set; }

        #endregion

        #region 유니티 이벤트 메서드

        void Awake()
        {
            label = GetComponent<Text>();
            Refresh();
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.IsGameOver) return; // freeze the time on game over

            ElapsedSeconds += Time.deltaTime;
            Refresh();
        }

        #endregion

        #region 커스텀 메서드

        void Refresh()
        {
            int seconds = Mathf.FloorToInt(ElapsedSeconds);
            if (seconds == shownSeconds) return; // only rebuild the text when the number changes
            shownSeconds = seconds;
            label.text = $"생존 시간  {seconds}초";
        }

        #endregion
    }
}
