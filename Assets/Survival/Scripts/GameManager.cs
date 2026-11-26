using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Survival
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public PlayerHealth player;
        public WaveSpawner spawner;
        public HUD hud;

        public int Kills { get; private set; }
        public bool IsGameOver { get; private set; }

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start() => player.OnDied += GameOver;

        public void AddKill()
        {
            Kills++;
            if (hud != null) hud.SetKills(Kills);
        }

        void GameOver()
        {
            IsGameOver = true;
            Time.timeScale = 0f;
            if (hud != null) hud.ShowGameOver(spawner.Wave, Kills);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (IsGameOver && kb != null && kb.rKey.wasPressedThisFrame) Restart();
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
