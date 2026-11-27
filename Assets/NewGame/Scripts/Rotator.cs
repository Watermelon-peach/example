using UnityEngine;

/// <summary>
/// Spins the object around its Y axis at a constant speed.
/// </summary>
public class Rotator : MonoBehaviour
{
    #region 변수

    [Tooltip("Rotation speed in degrees per second (negative = reverse direction).")]
    [SerializeField] float degreesPerSecond = 90f;

    #endregion

    #region 유니티 이벤트 메서드

    void Update()
    {
        transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f);
    }

    #endregion
}
