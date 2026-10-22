using UnityEngine;
using UnityEngine.Pool;

namespace MoreMush.Mobile
{
    // 풀에서 꺼낼 때·돌려놓을 때 알림을 받는 쪽. 상태 초기화는 여기서 한다
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }

    // 프리팹 하나당 풀 하나. 꺼내면 parent 아래에서 켜고, 돌려놓으면 끈다.
    // 버섯·수확기처럼 자주 생기고 사라지는 것(나중의 분신·소환체 포함)은 Instantiate/Destroy 대신 이걸 쓴다
    public class PrefabPool<T> where T : Component
    {
        readonly T prefab;
        readonly Transform parent;
        readonly ObjectPool<T> pool;

        public T Prefab => prefab;
        public int CountActive => pool.CountActive;

        public PrefabPool(T prefab, Transform parent, int prewarm = 0, int maxSize = 1000)
        {
            this.prefab = prefab;
            this.parent = parent;
            pool = new ObjectPool<T>(Create, OnGet, OnRelease, item => Object.Destroy(item.gameObject),
                Application.isEditor, Mathf.Max(prewarm, 10), maxSize);

            // 처음 몇 개는 미리 만들어 둬서 게임 중 첫 생성에 걸리는 끊김을 없앤다
            var warm = new T[prewarm];
            for (int i = 0; i < prewarm; i++) warm[i] = pool.Get();
            for (int i = 0; i < prewarm; i++) pool.Release(warm[i]);
        }

        public T Get() => pool.Get();
        public void Release(T item) => pool.Release(item);
        public void Clear() => pool.Clear();

        T Create()
        {
            var item = Object.Instantiate(prefab, parent, false);
            item.name = prefab.name;
            item.gameObject.SetActive(false);
            return item;
        }

        static void OnGet(T item)
        {
            item.gameObject.SetActive(true);
            if (item is IPoolable p) p.OnSpawned();
        }

        static void OnRelease(T item)
        {
            if (item is IPoolable p) p.OnDespawned();
            item.gameObject.SetActive(false);
        }
    }
}
