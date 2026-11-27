using UnityEngine;
using UnityEngine.InputSystem;

namespace DodgeBall
{
    /// <summary>
    /// Moves the player on the ground plane (X/Z) with WASD or the arrow keys,
    /// keeping it inside the arena.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        #region Variables

        [Tooltip("Movement speed in units per second.")]
        [SerializeField] float moveSpeed = 5f;

        [Tooltip("The player can't move farther than this from the arena center on X and Z.")]
        [SerializeField] Vector2 boundsHalfSize = new Vector2(9.5f, 9.5f);

        Rigidbody body;
        Vector3 inputDirection;

        #endregion

        #region Unity Event Methods

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true; // moved by script, but still reports collisions with the balls
            body.interpolation = RigidbodyInterpolation.Interpolate; // smooth visuals between physics steps
        }

        // Read input every frame so no key press is missed...
        void Update()
        {
            var keyboard = Keyboard.current;
            Vector3 direction = Vector3.zero;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) direction.z += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) direction.z -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) direction.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) direction.x -= 1f;
            }

            // Keep diagonal movement from being faster than straight movement.
            if (direction.sqrMagnitude > 1f) direction.Normalize();
            inputDirection = direction;
        }

        // ...but move in the physics step, so collisions with the balls stay reliable.
        void FixedUpdate()
        {
            Vector3 next = body.position + inputDirection * moveSpeed * Time.fixedDeltaTime;
            next.x = Mathf.Clamp(next.x, -boundsHalfSize.x, boundsHalfSize.x);
            next.z = Mathf.Clamp(next.z, -boundsHalfSize.y, boundsHalfSize.y);
            body.MovePosition(next);
        }

        #endregion
    }
}
