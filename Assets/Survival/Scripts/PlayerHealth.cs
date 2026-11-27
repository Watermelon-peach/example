using System;
using UnityEngine;

namespace Survival
{
    public class PlayerHealth : MonoBehaviour
    {
        #region 변수

        public int maxHealth = 100;
        public float invulnerableTime = 0.5f;
        public Renderer bodyRenderer;

        public event Action<int, int> OnChanged;
        public event Action<int> OnDamaged;
        public event Action OnDied;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock block;
        Color baseColor = Color.white;
        float invulnerableUntil;
        float flashTimer;

        #endregion

        #region 프로퍼티

        public int Current { get; private set; }
        public bool IsDead => Current <= 0;
        public bool IsInvulnerable => Time.time < invulnerableUntil;

        #endregion

        #region 유니티 이벤트 메서드

        void Awake()
        {
            Current = maxHealth;
            block = new MaterialPropertyBlock();
            if (bodyRenderer != null) baseColor = bodyRenderer.sharedMaterial.GetColor(BaseColorId);
        }

        void Update()
        {
            if (flashTimer <= 0f) return;
            flashTimer -= Time.unscaledDeltaTime;
            if (flashTimer <= 0f) SetColor(baseColor);
        }

        #endregion

        #region 커스텀 메서드

        public void TakeDamage(int amount)
        {
            if (IsDead || IsInvulnerable) return;
            Current = Mathf.Max(0, Current - amount);
            invulnerableUntil = Time.time + invulnerableTime;
            flashTimer = 0.12f;
            SetColor(new Color(1f, 0.35f, 0.35f));
            OnDamaged?.Invoke(amount);
            OnChanged?.Invoke(Current, maxHealth);
            if (IsDead) OnDied?.Invoke();
        }

        public void Heal(int amount)
        {
            if (IsDead || amount <= 0) return;
            Current = Mathf.Min(maxHealth, Current + amount);
            OnChanged?.Invoke(Current, maxHealth);
        }

        public void IncreaseMax(int amount)
        {
            maxHealth += amount;
            Heal(amount);
            OnChanged?.Invoke(Current, maxHealth);
        }

        public void SetInvulnerable(float duration)
        {
            invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + duration);
        }

        void SetColor(Color c)
        {
            if (bodyRenderer == null) return;
            bodyRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            bodyRenderer.SetPropertyBlock(block);
        }

        #endregion
    }
}
