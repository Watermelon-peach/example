using UnityEngine;
using UnityEngine.UI;

namespace Survival
{
    public class HUD : MonoBehaviour
    {
        #region Variables

        public PlayerHealth player;
        public PlayerController playerController;
        public WaveSpawner spawner;

        [Header("Top bar")]
        public RectTransform healthFill;
        public Text healthText;
        public RectTransform dashFill;
        public Image dashFillImage;
        public Text waveText;
        public Text remainingText;
        public Text killsText;
        public Text bestText;
        public Text announceText;

        [Header("Boss")]
        public GameObject bossBar;
        public RectTransform bossFill;

        [Header("Feedback")]
        public Image damageOverlay;

        [Header("Game over")]
        public GameObject gameOverPanel;
        public Text gameOverStats;
        public Button restartButton;

        static readonly Color DashReady = new Color(0.35f, 0.85f, 1f);
        static readonly Color DashCharging = new Color(0.25f, 0.4f, 0.55f);
        static readonly Color WaveAnnounce = new Color(1f, 0.85f, 0.3f);
        static readonly Color BossAnnounce = new Color(1f, 0.31f, 0.69f);

        float announceTimer;
        float damageFlash;

        #endregion

        #region Unity Event Methods

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
            bossBar.SetActive(false);
            announceText.text = "";
            waveText.text = "";
            bestText.text = GameManager.BestWave > 0 ? $"최고 기록  웨이브 {GameManager.BestWave}" : "";
            SetKills(0);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            remainingText.text = $"남은 적  {spawner.Remaining}";

            float dash = playerController.DashReadiness;
            dashFill.anchorMax = new Vector2(dash, 1f);
            dashFillImage.color = dash >= 1f ? DashReady : DashCharging;

            var boss = spawner.CurrentBoss;
            bool showBoss = boss != null && boss.IsAlive;
            if (bossBar.activeSelf != showBoss) bossBar.SetActive(showBoss);
            if (showBoss) bossFill.anchorMax = new Vector2((float)boss.Health / boss.MaxHealth, 1f);

            if (announceTimer > 0f)
            {
                announceTimer -= dt;
                Color c = announceText.color;
                c.a = Mathf.Clamp01(announceTimer);
                announceText.color = c;
            }

            if (damageFlash > 0f || damageOverlay.color.a > 0f)
            {
                damageFlash = Mathf.Max(0f, damageFlash - dt);
                Color c = damageOverlay.color;
                c.a = damageFlash;
                damageOverlay.color = c;
            }
        }

        #endregion

        #region Custom Methods

        void Announce(int wave, bool bossWave)
        {
            waveText.text = $"WAVE {wave}";
            // No <color> tags here: rich-text colors ignore Text.color alpha, which would break the fade-out.
            announceText.text = bossWave ? $"WAVE {wave}\n<size=60>BOSS 출현!</size>" : $"WAVE {wave}";
            announceText.color = bossWave ? BossAnnounce : WaveAnnounce;
            announceTimer = 2.2f;
        }

        void UpdateHealth(int current, int max)
        {
            float f = max > 0 ? (float)current / max : 0f;
            healthFill.anchorMax = new Vector2(f, 1f);
            healthText.text = $"{current} / {max}";
        }

        public void FlashDamage() => damageFlash = 0.35f;

        public void SetKills(int kills) => killsText.text = $"처치  {kills}";

        public void ShowGameOver(int wave, int kills, bool newRecord)
        {
            gameOverPanel.SetActive(true);
            string record = newRecord
                ? "<color=#FFD54F>★ 새 기록! ★</color>"
                : $"최고 기록: 웨이브 {GameManager.BestWave} · {GameManager.BestKills}마리";
            gameOverStats.text = $"웨이브 {wave} 도달  ·  {kills}마리 처치\n<size=30>{record}</size>\n<size=24>R 키 또는 아래 버튼으로 재시작</size>";
        }

        #endregion
    }
}
