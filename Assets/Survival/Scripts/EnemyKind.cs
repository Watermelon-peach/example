using UnityEngine;

namespace Survival
{
    public enum EnemyKind { Chaser, Charger, Shooter, Splitter, Tank, Mini, Boss }

    public struct EnemyStats
    {
        #region 변수

        public float scale;
        public float healthMul;
        public float speedMul;
        public int damage;
        public Color color;

        #endregion
    }

    public static class EnemyCatalog
    {
        #region 커스텀 메서드

        public static EnemyStats Get(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Charger: return Make(0.9f, 1f, 0.9f, 15, new Color(1f, 0.55f, 0.1f));
                case EnemyKind.Shooter: return Make(0.9f, 0.8f, 0.85f, 8, new Color(0.65f, 0.3f, 1f));
                case EnemyKind.Splitter: return Make(1.15f, 1.5f, 0.9f, 10, new Color(0.3f, 0.85f, 0.3f));
                case EnemyKind.Tank: return Make(1.6f, 4f, 0.55f, 20, new Color(0.5f, 0.12f, 0.12f));
                case EnemyKind.Mini: return Make(0.6f, 0.5f, 1.35f, 6, new Color(0.6f, 1f, 0.45f));
                case EnemyKind.Boss: return Make(2.6f, 1f, 0.6f, 25, new Color(0.9f, 0.1f, 0.6f));
                default: return Make(1f, 1f, 1f, 10, new Color(0.9f, 0.2f, 0.2f));
            }
        }

        static EnemyStats Make(float scale, float health, float speed, int damage, Color color) =>
            new EnemyStats { scale = scale, healthMul = health, speedMul = speed, damage = damage, color = color };

        #endregion
    }
}
