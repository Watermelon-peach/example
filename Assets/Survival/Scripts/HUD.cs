using UnityEngine;
using UnityEngine.UI;

namespace Survival
{
    public class HUD : MonoBehaviour
    {
        public PlayerHealth player;
        public WaveSpawner spawner;

        public RectTransform healthFill;
        public Text healthText;
        public Text waveText;
        public Text remainingText;
        public Text killsText;
        public Text announceText;

        public GameObject gameOverPanel;
        public Text gameOverStats;
        public Button restartButton;

        float announceTimer;

        void Awake()
        {
            // The built-in legacy font has no Hangul glyphs; use the Windows Korean font when available.
            Font korean = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 32);
            if (korean != null)
                foreach (var t in GetComponentsInChildren<Text>(true)) t.font = korean;
        }

        void Start()
        {
            player.OnChanged += UpdateHealth;
            UpdateHealth(player.Current, player.maxHealth);
            spawner.OnWaveStarted += Announce;
            restartButton.onClick.AddListener(() => GameManager.Instance.Restart());

            gameOverPanel.SetActive(false);
            announceText.text = "";
            waveText.text = "";
            SetKills(0);
        }

        void Announce(int wave)
        {
            waveText.text = $"WAVE {wave}";
            announceText.text = $"WAVE {wave}";
            announceTimer = 2f;
        }

        void UpdateHealth(int current, int max)
        {
            float f = max > 0 ? (float)current / max : 0f;
            healthFill.anchorMax = new Vector2(f, 1f);
            healthText.text = $"{current} / {max}";
        }

        void Update()
        {
            remainingText.text = $"남은 적  {spawner.Remaining}";

            if (announceTimer > 0f)
            {
                announceTimer -= Time.deltaTime;
                Color c = announceText.color;
                c.a = Mathf.Clamp01(announceTimer);
                announceText.color = c;
            }
        }

        public void SetKills(int kills) => killsText.text = $"처치  {kills}";

        public void ShowGameOver(int wave, int kills)
        {
            gameOverPanel.SetActive(true);
            gameOverStats.text = $"웨이브 {wave} 도달  ·  {kills}마리 처치\n<size=26>R 키 또는 아래 버튼으로 재시작</size>";
        }
    }
}
