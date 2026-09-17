using System.Collections.Generic;
using UnityEngine;

namespace MNLTHII.Tools
{
    public class ObjectPooler : MonoBehaviour
    {
        public static ObjectPooler Instance { get; private set; }

        private Dictionary<string, Queue<GameObject>> _poolDictionary;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                _poolDictionary = new Dictionary<string, Queue<GameObject>>();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void CreatePool(string poolKey, GameObject prefab, int poolSize)
        {
            if (!_poolDictionary.ContainsKey(poolKey))
            {
                Queue<GameObject> objectPool = new Queue<GameObject>();

                for (int i = 0; i < poolSize; i++)
                {
                    GameObject obj = Instantiate(prefab);
                    obj.SetActive(false);
                    objectPool.Enqueue(obj);
                }

                _poolDictionary.Add(poolKey, objectPool);
            }
        }

        public GameObject SpawnFromPool(string poolKey, Vector3 position, Quaternion rotation)
        {
            if (!_poolDictionary.ContainsKey(poolKey))
            {
                Debug.LogWarning("Pool with key " + poolKey + " doesn't exist.");
                return null;
            }

            GameObject objectToSpawn = _poolDictionary[poolKey].Dequeue();
            
            // If the object is already active (meaning pool is exhausted), we could either instantiate a new one or reuse.
            // A simple expandable pool logic:
            if (objectToSpawn.activeInHierarchy)
            {
                GameObject newObj = Instantiate(objectToSpawn);
                newObj.SetActive(false);
                _poolDictionary[poolKey].Enqueue(objectToSpawn);
                objectToSpawn = newObj;
            }

            objectToSpawn.SetActive(true);
            objectToSpawn.transform.position = position;
            objectToSpawn.transform.rotation = rotation;

            _poolDictionary[poolKey].Enqueue(objectToSpawn);

            return objectToSpawn;
        }

        public void ReturnToPool(GameObject obj)
        {
            obj.SetActive(false);
        }
    }
}
