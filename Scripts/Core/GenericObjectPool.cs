using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Wooduduk.Core
{
    public class GenericObjectPool : MonoBehaviour
    {
        // 싱글톤 인스턴스
        public static GenericObjectPool Instance { get; private set; }

        // 프리팹(원본)을 Key로, 유니티 내장 풀을 Value로 관리하는 딕셔너리
        private Dictionary<GameObject, ObjectPool<GameObject>> _pools = new Dictionary<GameObject, ObjectPool<GameObject>>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }
        public void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null) return;

            if (!_pools.ContainsKey(prefab))
            {
                _pools[prefab] = new ObjectPool<GameObject>(
                    createFunc: () => CreatePooledItem(prefab),
                    actionOnGet: null,
                    actionOnRelease: OnReturnedToPool,
                    actionOnDestroy: OnDestroyPoolObject,
                    collectionCheck: true,
                    defaultCapacity: count,
                    maxSize: 200
                );
            }

            GameObject[] tempItems = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                tempItems[i] = _pools[prefab].Get();
            }
            for (int i = 0; i < count; i++)
            {
                _pools[prefab].Release(tempItems[i]);
            }
        }

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            // 넘어온 프리팹 전용 풀이 없다면 즉석에서 생성 (자동 확장)
            if (!_pools.ContainsKey(prefab))
            {
                _pools[prefab] = new ObjectPool<GameObject>(
                    createFunc: () => CreatePooledItem(prefab),
                    actionOnGet: null,
                    actionOnRelease: OnReturnedToPool,
                    actionOnDestroy: OnDestroyPoolObject,
                    collectionCheck: true,
                    defaultCapacity: 10,
                    maxSize: 200
                );
            }

            // 해당 프리팹의 풀에서 꺼내어 전달
            GameObject go = _pools[prefab].Get();
            go.transform.position = position;
            go.transform.rotation = rotation;

            go.SetActive(true);
            if (go.TryGetComponent<IPoolable>(out var poolable)) poolable.OnSpawn();

            return go;
        }

        private GameObject CreatePooledItem(GameObject prefab)
        {
            GameObject go = Instantiate(prefab, transform);

            go.SetActive(false);

            // 프리팹에 IPoolable이 있다면 "자신이 속한 딕셔너리의 풀"로 돌아가도록 세팅
            if (go.TryGetComponent<IPoolable>(out var poolable))
            {
                poolable.SetupPoolReturn((obj) => _pools[prefab].Release(obj));
            }

            return go;
        }

        private void OnReturnedToPool(GameObject go)
        {
            if (go.TryGetComponent<IPoolable>(out var poolable)) poolable.OnReturn();
            go.SetActive(false);
        }

        private void OnDestroyPoolObject(GameObject go)
        {
            Destroy(go);
        }
    }
}