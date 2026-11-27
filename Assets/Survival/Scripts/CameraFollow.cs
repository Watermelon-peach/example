using UnityEngine;

namespace Survival
{
    public class CameraFollow : MonoBehaviour
    {
        #region 변수

        public Transform target;
        public Vector3 offset = new Vector3(0f, 20f, -11f);
        public float smoothing = 8f;
        public float maxShakeOffset = 0.7f;
        public float shakeDecay = 1.6f;

        Vector3 smoothedPosition;
        float trauma;

        #endregion

        #region 유니티 이벤트 메서드

        void Start() => SnapToTarget();

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.unscaledDeltaTime; // keeps shaking during hit-stop
            smoothedPosition = Vector3.Lerp(smoothedPosition, target.position + offset, 1f - Mathf.Exp(-smoothing * dt));

            Vector3 shake = Vector3.zero;
            if (trauma > 0f)
            {
                float s = trauma * trauma * maxShakeOffset;
                float t = Time.unscaledTime * 25f;
                shake = new Vector3(Mathf.PerlinNoise(t, 0.1f) - 0.5f, 0f, Mathf.PerlinNoise(0.7f, t) - 0.5f) * (2f * s);
                trauma = Mathf.Max(0f, trauma - shakeDecay * dt);
            }
            transform.position = smoothedPosition + shake;
        }

        #endregion

        #region 커스텀 메서드

        /// <summary>Adds screen-shake "trauma" (0..1). Shake strength is trauma², so small hits stay subtle.</summary>
        public void Shake(float amount) => trauma = Mathf.Clamp01(trauma + amount);

        public void SnapToTarget()
        {
            if (target == null) return;
            smoothedPosition = target.position + offset;
            transform.position = smoothedPosition;
            transform.rotation = Quaternion.LookRotation(-offset);
        }

        #endregion
    }
}
