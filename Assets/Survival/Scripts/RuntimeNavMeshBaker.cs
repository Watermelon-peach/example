using Unity.AI.Navigation;
using UnityEngine;

namespace Survival
{
    /// <summary>
    /// Bakes the arena NavMesh on load (before anything else runs), so the scene needs no baked NavMesh asset.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(NavMeshSurface))]
    public class RuntimeNavMeshBaker : MonoBehaviour
    {
        void Awake() => GetComponent<NavMeshSurface>().BuildNavMesh();
    }
}
