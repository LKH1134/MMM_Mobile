using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 버섯. 단일 개체는 maxHp 1(한 번 맞으면 캠), 군락 개체는 maxHp를 높여 체력이 다 닳을 때까지 맞아야 캔다.
    // 종류마다 프리팹을 따로 두고, HarvestField가 프리팹별 풀에서 꺼내 놓는다
    [RequireComponent(typeof(RectTransform))]
    public class Mushroom : MonoBehaviour, IPoolable
    {
        [SerializeField] int maxHp = 1;
        [SerializeField] long reward = 10;            // 캤을 때 오르는 돈
        [SerializeField] float radius = 50f;          // 충돌 반지름
        [SerializeField] RectTransform visual;        // 맞을 때 출렁이는 그림
        [SerializeField] GameObject hpRoot;           // 체력 바. 다치기 전엔 숨긴다. 비우면 표시 없음
        [SerializeField] RectTransform hpFill;        // 체력 바 안쪽 채움. 앵커 x 최대값 = 남은 비율

        RectTransform rect;
        Coroutine punch;

        public int MaxHp => maxHp;
        public int Hp { get; private set; }
        public bool Alive => Hp > 0;
        public long Reward => reward;
        public float Radius => radius;
        public Vector2 Position => rect.anchoredPosition;
        internal PrefabPool<Mushroom> Pool { get; set; }

        void Awake() => rect = (RectTransform)transform;

        public void OnSpawned()
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            Hp = maxHp;
            ShowHp();
            if (visual != null) visual.localScale = Vector3.one;
            Play(Pop(0.18f, 0f, 1f));
        }

        public void OnDespawned()
        {
            if (punch != null) StopCoroutine(punch);
            punch = null;
        }

        public void Place(Vector2 position) => rect.anchoredPosition = position;

        // 맞으면 체력을 깎는다. 이번에 캐졌으면 true
        public bool Hit(int damage)
        {
            if (!Alive) return false;
            Hp = Mathf.Max(0, Hp - Mathf.Max(1, damage));
            ShowHp();
            if (Alive) Play(Pop(0.15f, 0.8f, 1f));
            return !Alive;
        }

        void ShowHp()
        {
            if (hpRoot == null) return;
            hpRoot.SetActive(Hp < maxHp && Hp > 0);
            if (hpFill != null) hpFill.anchorMax = new Vector2((float)Hp / maxHp, hpFill.anchorMax.y);
        }

        void Play(IEnumerator routine)
        {
            if (visual == null || !isActiveAndEnabled) return;
            if (punch != null) StopCoroutine(punch);
            punch = StartCoroutine(routine);
        }

        // from에서 1.15배로 부풀었다가 to로 돌아온다
        IEnumerator Pop(float duration, float from, float to)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                float s = k < 0.5f ? Mathf.Lerp(from, 1.15f, k * 2f) : Mathf.Lerp(1.15f, to, k * 2f - 1f);
                visual.localScale = Vector3.one * s;
                yield return null;
            }
            visual.localScale = Vector3.one * to;
            punch = null;
        }
    }
}
