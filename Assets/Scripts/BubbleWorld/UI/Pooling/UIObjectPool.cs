using System.Collections.Generic;
using UnityEngine;

namespace BubbleWorld
{
    /// <summary>
    /// Minimal GameObject pool for UI list items. Reuses entries across
    /// refreshes so long histories do not churn allocations.
    /// </summary>
    public sealed class UIObjectPool
    {
        private readonly GameObject prefab;
        private readonly Transform parent;
        private readonly Stack<GameObject> available = new Stack<GameObject>();
        private readonly List<GameObject> active = new List<GameObject>();

        public int ActiveCount => active.Count;
        public int AvailableCount => available.Count;

        public UIObjectPool(GameObject prefab, Transform parent, int prewarm = 0)
        {
            this.prefab = prefab;
            this.parent = parent;
            for (int i = 0; i < prewarm; i++)
            {
                GameObject go = Create();
                go.SetActive(false);
                available.Push(go);
            }
        }

        public bool IsValid => prefab != null && parent != null;

        private GameObject Create()
        {
            GameObject go = Object.Instantiate(prefab, parent);
            go.name = prefab.name;
            return go;
        }

        /// <summary>Gets a pooled entry, creating one if the pool is empty.</summary>
        public GameObject Get()
        {
            GameObject go;
            if (available.Count > 0)
            {
                go = available.Pop();
                go.SetActive(true);
            }
            else
            {
                go = Create();
            }
            go.transform.SetAsLastSibling();
            active.Add(go);
            return go;
        }

        /// <summary>Returns a single entry to the pool.</summary>
        public void Release(GameObject go)
        {
            if (go == null) return;
            if (active.Remove(go))
            {
                go.SetActive(false);
                go.transform.SetAsLastSibling();
                available.Push(go);
            }
        }

        /// <summary>Returns every active entry to the pool.</summary>
        public void ReleaseAll()
        {
            for (int i = 0; i < active.Count; i++)
            {
                GameObject go = active[i];
                if (go == null) continue;
                go.SetActive(false);
                go.transform.SetAsLastSibling();
                available.Push(go);
            }
            active.Clear();
        }
    }
}
