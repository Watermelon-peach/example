using System;
using UnityEngine;

namespace Survival
{
    public class PlayerHealth : MonoBehaviour
    {
        public int maxHealth = 100;
        public float invulnerableTime = 0.25f;
        public Renderer bodyRenderer;

        public int Current { get; private set; }
        public bool IsDead => Current <= 0;

        public event Action<int, int> OnChanged;
        public event Action OnDied;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock block;
        Color baseColor = Color.white;
        float invulnerableUntil;
        float flashTimer;

        void Awake()
        {
            Current = maxHealth;
            block = new MaterialPropertyBlock();
            if (bodyRenderer != null) baseColor = bodyRenderer.sharedMaterial.GetColor(BaseColorId);
        }

        public void TakeDamage(int amount)
        {
            if (IsDead || Time.time < invulnerableUntil) return;
            Current = Mathf.Max(0, Current - amount);
            invulnerableUntil = Time.time + invulnerableTime;
            flashTimer = 0.12f;
            SetColor(new Color(1f, 0.35f, 0.35f));
            OnChanged?.Invoke(Current, maxHealth);
            if (IsDead) OnDied?.Invoke();
        }

        void Update()
        {
            if (flashTimer <= 0f) return;
            flashTimer -= Time.unscaledDeltaTime;
            if (flashTimer <= 0f) SetColor(baseColor);
        }

        void SetColor(Color c)
        {
            if (bodyRenderer == null) return;
            bodyRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            bodyRenderer.SetPropertyBlock(block);
        }
    }
}
