using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class ObjectPool : MonoBehaviour
{
    public static ObjectPool Instance;

    [System.Serializable]
    public class Pool
    {
        public string tag;
        public GameObject prefab;
        public int size;
    }

    public List<Pool> pools;
    private Dictionary<string, Queue<GameObject>> poolDictionary;
    private Dictionary<string, Transform> poolContainers;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        poolDictionary = new Dictionary<string, Queue<GameObject>>();
        poolContainers = new Dictionary<string, Transform>();

        foreach (Pool pool in pools)
        {
            // Create a named empty GameObject as parent
            GameObject container = new GameObject($"Pool_{pool.tag}");
            container.transform.SetParent(transform);
            poolContainers.Add(pool.tag, container.transform);

            Queue<GameObject> objectPool = new Queue<GameObject>();
            for (int i = 0; i < pool.size; i++)
            {
                GameObject obj = Instantiate(pool.prefab, container.transform);
                obj.SetActive(false);
                objectPool.Enqueue(obj);
            }
            poolDictionary.Add(pool.tag, objectPool);
        }
    }

    // Grabs an object from the pool and changes its position and orientation to the new ones
    public GameObject GetPooledObject(string tag, Vector3 position, Quaternion rotation,
                                   IPooledSpawnOwner owner, bool shouldBeRequeued = false)
    {
        if (!poolDictionary.TryGetValue(tag, out var queue) || queue.Count == 0)
        {
            Debug.LogWarning($"Pool '{tag}' is empty or doesn't exist.");
            return null;
        }

        GameObject obj = queue.Dequeue();
        IPoolable poolable = obj.GetComponent<IPoolable>();
        poolable?.HandleDepool(tag, position, rotation);

        if (owner != null && poolable is IOwnedPoolable owned)
            owned.ClaimOwnership(owner);

        if (shouldBeRequeued)
            queue.Enqueue(obj);

        return obj;
    }

    // Grabs an object from the pool without changing its position and orientation, and optionally - without depooling handling
    public GameObject GetPooledObject(string tag, bool shouldBeRequeued, bool shouldBeDepooled)
    {
        if (!poolDictionary.ContainsKey(tag))
        {
            //Debug.LogWarning("Pool with tag " + tag + " doesn't exist.");
            return null;
        }

        GameObject obj = poolDictionary[tag].Dequeue();
        IPoolable poolable = obj.GetComponent<IPoolable>();

        // This is for objects we grab only when we need their data
        if(shouldBeDepooled)
            poolable?.HandleDepool(tag, obj.transform.position, obj.transform.rotation);

        // This is for objects we might run out, but it's not that critical so we can reuse them
        if (shouldBeRequeued)
            poolDictionary[tag].Enqueue(obj);

        return obj;
    }

    // Returns the object to pool
    public void ReturnToPool(GameObject obj, string tag)
    {
        if (!poolDictionary.ContainsKey(tag))
        {
            Debug.LogWarning($"Pool '{tag}' doesn't exist — can't return object.");
            return;
        }

        IPoolable poolable = obj.GetComponent<IPoolable>();

        // Release fires before HandleRepool, so an owner's cleanup can still
        // read the object's state if it needs to (e.g. last known position).
        if (poolable is IOwnedPoolable owned)
            owned.ReleaseOwnership();

        poolable?.HandleRepool();
        poolDictionary[tag].Enqueue(obj);
    }
}
