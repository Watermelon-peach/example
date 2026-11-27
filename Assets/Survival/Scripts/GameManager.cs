using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Survival
{
    /// <summary>
    /// Game state, best records, and the single owner of Time.timeScale (pause / hit-stop).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        #region 변수

        const string BestWaveKey = "Survival.BestWave";
        const string BestKillsKey = "Survival.BestKills";
        const float HitStopScale = 0.05f;

        public PlayerHealth player;
        public WaveSpawner spawner;
        public HUD hud;
        public UpgradeSystem upgrades;

        PlayerController playerController;
        float hitStopUntil;

        #endregion

        #region 프로퍼티

        public static GameManager Instance { get; private set; }

        public int Kills { get; private set; }
        public bool IsGameOver { get; private set; }
        public bool IsPaused => IsGameOver || (upgrades != null && upgrades.IsOpen);
        public static int BestWave => PlayerPrefs.GetInt(BestWaveKey, 0);
        public static int BestKills => PlayerPrefs.GetInt(BestKillsKey, 0);

        #endregion

        #region 유니티 이벤트 메서드

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
            playerController = player.GetComponent<PlayerController>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            player.OnDied += GameOver;
            player.OnDamaged += OnPlayerDamaged;
        }

        void Update()
        {
            Time.timeScale = IsPaused ? 0f : (Time.unscaledTime < hitStopUntil ? HitStopScale : 1f);

            var kb = Keyboard.current;
            if (IsGameOver && kb != null && kb.rKey.wasPressedThisFrame) Restart();
        }

        #endregion

        #region 커스텀 메서드

        void OnPlayerDamaged(int amount)
        {
            HitStop(0.05f);
            var fx = Effects.Instance;
            if (fx != null)
            {
                fx.Shake(0.45f);
                fx.Burst(player.transform.position + Vector3.up, new Color(1f, 0.3f, 0.3f), 10, 5f, 0.7f);
            }
            if (hud != null) hud.FlashDamage();
        }

        public void HitStop(float duration)
        {
            hitStopUntil = Mathf.Max(hitStopUntil, Time.unscaledTime + duration);
        }

        public void AddKill()
        {
            Kills++;
            if (hud != null) hud.SetKills(Kills);
            if (playerController != null && playerController.healOnKill > 0) player.Heal(playerController.healOnKill);
        }

        public IEnumerator OfferUpgrades()
        {
            if (upgrades == null || IsGameOver) yield break;
            upgrades.Open();
            while (upgrades.IsOpen) yield return null;
        }

        void GameOver()
        {
            IsGameOver = true;
            Time.timeScale = 0f;

            int wave = spawner.Wave;
            bool newRecord = wave > BestWave || (wave == BestWave && Kills > BestKills);
            if (newRecord)
            {
                PlayerPrefs.SetInt(BestWaveKey, wave);
                PlayerPrefs.SetInt(BestKillsKey, Kills);
                PlayerPrefs.Save();
            }
            if (hud != null) hud.ShowGameOver(wave, Kills, newRecord);
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        #endregion
    }
}
