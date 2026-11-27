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
        #region Variables

        Text label;
        int shownSeconds = -1;

        #endregion

        #region Properties

        public float ElapsedSeconds { get; private set; }

        #endregion

        #region Unity Event Methods

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

        #region Custom Methods

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
