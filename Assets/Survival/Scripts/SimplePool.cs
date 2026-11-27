using System.Collections.Generic;
using UnityEngine;

namespace Survival
{
    /// <summary>
    /// Minimal component pool: objects are deactivated instead of destroyed and reused on the next Get.
    /// </summary>
    public class SimplePool<T> where T : Component
    {
        #region Variables

        readonly T prefab;
        readonly Transform root;
        readonly Stack<T> free = new Stack<T>();

        #endregion

        #region Properties

        public int CountActive { get; private set; }
        public int CountTotal { get; private set; }
        public Transform Root => root;

        #endregion

        #region Custom Methods

        public SimplePool(T prefab, int prewarm, string rootName, Transform parent = null)
        {
            this.prefab = prefab;
            root = new GameObject(rootName, typeof(RectTransform)).transform;
            if (parent != null) root.SetParent(parent, false);
            for (int i = 0; i < prewarm; i++)
            {
                var obj = Create();
                obj.gameObject.SetActive(false);
                free.Push(obj);
            }
        }

        T Create()
        {
            CountTotal++;
            return Object.Instantiate(prefab, root);
        }

        public T Get(Vector3 position, Quaternion rotation)
        {
            var obj = free.Count > 0 ? free.Pop() : Create();
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.gameObject.SetActive(true);
            CountActive++;
            return obj;
        }

        public void Release(T obj)
        {
            if (!obj.gameObject.activeSelf) return; // already released
            obj.gameObject.SetActive(false);
            free.Push(obj);
            CountActive--;
        }

        #endregion
    }

    static class HitSort
    {
        #region Variables

        static readonly IComparer<RaycastHit> ByDistance = Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

        #endregion

        #region Custom Methods

        public static void Sort(RaycastHit[] hits, int count) => System.Array.Sort(hits, 0, count, ByDistance);

        #endregion
    }
}
