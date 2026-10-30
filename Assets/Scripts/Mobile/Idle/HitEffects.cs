using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 타격 이펙트: MoreMustMush의 HitAnim(FX/hit_0..4 프레임 넘김)을 UI Image로 옮긴 것.
    // 맞은 자리에서 무작위 각도로 터지고, 앞 60%는 그대로 보였다가 나머지 동안 사라진다. 일반 = 크림색, 치명타 = 붉은빛·조금 크게.
    // Image는 풀로 다시 쓴다. IdleBattle이 Hit 때 Play, 매 프레임 Tick을 부른다
    public class HitEffects
    {
        const string FramePath = "Art/Dream/FX/hit_";
        const int FrameCount = 5;
        const float Life = 0.2f;
        const float NormalScale = 1.4f;     // 맞은 적 크기 대비 (MoreMustMush: 버섯 반지름 × 3.4 ≒ 지름 × 1.7, 화면이 작아 조금 줄임)
        const float CritScale = 1.7f;
        static readonly Color NormalColor = new Color(1f, 0.97f, 0.86f, 1f);
        static readonly Color CritColor = new Color(1f, 0.72f, 0.6f, 1f);

        class Fx { public Image img; public float t; }

        readonly RectTransform layer;
        readonly Sprite[] frames = new Sprite[FrameCount];
        readonly List<Fx> playing = new List<Fx>();
        readonly Stack<Fx> free = new Stack<Fx>();

        public HitEffects(RectTransform layer)
        {
            this.layer = layer;
            for (int i = 0; i < FrameCount; i++) frames[i] = Resources.Load<Sprite>(FramePath + i);
            if (frames[0] == null) Debug.LogWarning($"[HitEffects] 그림 없음: Resources/{FramePath}0");
        }

        // local: layer 안 좌표, targetSize: 맞은 적 그림 크기
        public void Play(Vector2 local, float targetSize, bool crit)
        {
            if (frames[0] == null) return;
            var fx = free.Count > 0 ? free.Pop() : New();
            float size = targetSize * (crit ? CritScale : NormalScale);
            var rt = fx.img.rectTransform;
            rt.anchoredPosition = local;
            rt.sizeDelta = new Vector2(size, size);
            rt.localEulerAngles = new Vector3(0f, 0f, Random.Range(0f, 360f));
            fx.img.sprite = frames[0];
            fx.img.color = crit ? CritColor : NormalColor;
            fx.t = 0f;
            fx.img.gameObject.SetActive(true);
            rt.SetAsLastSibling();
            playing.Add(fx);
        }

        public void Tick(float dt)
        {
            for (int i = playing.Count - 1; i >= 0; i--)
            {
                var fx = playing[i];
                fx.t += dt;
                float k = fx.t / Life;
                if (k >= 1f)
                {
                    fx.img.gameObject.SetActive(false);
                    free.Push(fx);
                    playing.RemoveAt(i);
                    continue;
                }
                var sp = frames[Mathf.Min(FrameCount - 1, (int)(k * FrameCount))];
                if (sp != null && fx.img.sprite != sp) fx.img.sprite = sp;
                var c = fx.img.color;
                c.a = k < 0.6f ? 1f : (1f - k) / 0.4f;
                fx.img.color = c;
            }
        }

        Fx New()
        {
            var img = new GameObject("HitFx", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(layer, false);
            img.raycastTarget = false;
            img.preserveAspect = true;
            return new Fx { img = img };
        }
    }
}
