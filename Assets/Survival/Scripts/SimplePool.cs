using System.Collections.Generic;
using UnityEngine;

namespace Survival
{
    /// <summary>
    /// Minimal component pool: objects are deactivated instead of destroyed and reused on the next Get.
    /// </summary>
    public class SimplePool<T> where T : Component
    {
        readonly T prefab;
        readonly Transform root;
        readonly Stack<T> free = new Stack<T>();

        public int CountActive { get; private set; }
        public int CountTotal { get; private set; }

        public SimplePool(T prefab, int prewarm, string rootName)
        {
            this.prefab = prefab;
            root = new GameObject(rootName).transform;
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
    }
}
