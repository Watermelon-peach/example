using UnityEngine;

/// <summary>
/// Spins the object around its Y axis at a constant speed.
/// </summary>
public class Rotator : MonoBehaviour
{
    [Tooltip("Rotation speed in degrees per second (negative = reverse direction).")]
    [SerializeField] float degreesPerSecond = 90f;

    void Update()
    {
        transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f);
    }
}
