using UnityEngine;

namespace Survival
{
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 20f, -11f);
        public float smoothing = 8f;

        void Start() => SnapToTarget();

        void LateUpdate()
        {
            if (target == null) return;
            float t = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, target.position + offset, t);
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            transform.position = target.position + offset;
            transform.rotation = Quaternion.LookRotation(-offset);
        }
    }
}
