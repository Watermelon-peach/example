using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DodgeBall
{
    /// <summary>
    /// Holds the game state: game over, best record, and restart.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        #region 변수

        const string BestTimeKey = "DodgeBall.BestTime";

        [SerializeField] SurvivalTimer timer;
        [Tooltip("Small best-record label shown during play.")]
        [SerializeField] Text bestText;
        [Tooltip("Panel shown when the game ends (hidden at start).")]
        [SerializeField] GameObject gameOverPanel;
        [SerializeField] Text resultText;
        [SerializeField] Button restartButton;

        #endregion

        #region 프로퍼티

        public static GameManager Instance { get; private set; }

        public bool IsGameOver { get; private set; }
        public static float BestTime => PlayerPrefs.GetFloat(BestTimeKey, 0f);

        #endregion

        #region 유니티 이벤트 메서드

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (restartButton != null) restartButton.onClick.AddListener(Restart);
            if (bestText != null) bestText.text = BestTime > 0f ? $"최고 기록  {Mathf.FloorToInt(BestTime)}초" : "";
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (IsGameOver && keyboard != null && keyboard.rKey.wasPressedThisFrame) Restart();
        }

        #endregion

        #region 커스텀 메서드

        public void GameOver()
        {
            if (IsGameOver) return; // only once
            IsGameOver = true;

            // timeScale 0 stops everything that uses Time.deltaTime (player movement, spawner) and physics.
            Time.timeScale = 0f;

            float survived = timer != null ? timer.ElapsedSeconds : 0f;
            float previousBest = BestTime;
            bool newRecord = survived > previousBest;
            if (newRecord)
            {
                PlayerPrefs.SetFloat(BestTimeKey, survived);
                PlayerPrefs.Save();
            }

            if (resultText != null)
            {
                string record = newRecord
                    ? "<color=#FFD54F>새 기록!</color>"
                    : $"최고 기록  {Mathf.FloorToInt(previousBest)}초";
                resultText.text = $"생존 시간  {Mathf.FloorToInt(survived)}초\n<size=40>{record}</size>\n<size=30>R 키 또는 버튼으로 다시 시작</size>";
            }
            if (gameOverPanel != null) gameOverPanel.SetActive(true);
            Debug.Log($"[DodgeBall] Game Over! survived {survived:F1}s");
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        #endregion
    }
}
