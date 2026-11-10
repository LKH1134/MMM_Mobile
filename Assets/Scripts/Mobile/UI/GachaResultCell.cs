using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 뽑기 결과 칸 하나 (GachaResultPopup 안 Grid 아래 CellTemplate). 등급 테두리 + 그림 + 이름(등급) + 처음 얻은 것은 "NEW!".
    // 모양은 템플릿에서 고친다. 칸 크기는 GachaResultPopup이 배율(localScale)로 맞춘다.
    // 등급 이펙트 (칸이 다 뜬 뒤 창이 열려 있는 동안 반복): 일반 = 없음 · 희귀 = 반짝(빛줄기가 칸을 훑음) · 영웅 = 잠깐 커졌다 줄어듦 · 전설 = 둘 다
    public class GachaResultCell : MonoBehaviour
    {
        public Image glow;          // 등급별 테두리 그림
        public Image icon;
        public TMP_Text nameText;
        public GameObject newBadge;

        public int Grade { get; set; }      // GachaProgress.GradeNames 번호 (0 일반 · 1 희귀 · 2 영웅 · 3 전설)

        const int GradeRare = 1, GradeHero = 2, GradeLegend = 3;

        // 반짝: 기울어진 흰 띠가 왼쪽 아래 → 오른쪽 위로 지나간다
        const float ShineTime = 0.45f, ShineInterval = 1.4f, ShineAlpha = 0.55f, ShineAngle = -30f, ShineWidth = 0.22f;
        // 커졌다 줄어듦: 1 → PulseScale → 1
        const float PulseTime = 0.32f, PulseInterval = 1.2f, PulseScale = 1.14f;

        bool effectStarted;

        public void StartGradeEffect(float baseScale)
        {
            if (effectStarted) return;
            effectStarted = true;
            if (Grade == GradeRare || Grade >= GradeLegend) StartCoroutine(ShineLoop());
            if (Grade >= GradeHero) StartCoroutine(PulseLoop(baseScale));
        }

        IEnumerator PulseLoop(float baseScale)
        {
            var rt = (RectTransform)transform;
            while (true)
            {
                for (float t = 0f; t < PulseTime; t += Time.unscaledDeltaTime)
                {
                    float s = Mathf.Lerp(1f, PulseScale, Mathf.Sin(t / PulseTime * Mathf.PI));
                    rt.localScale = Vector3.one * (baseScale * s);
                    yield return null;
                }
                rt.localScale = Vector3.one * baseScale;
                yield return new WaitForSecondsRealtime(PulseInterval);
            }
        }

        IEnumerator ShineLoop()
        {
            var band = CreateShine();
            var size = ((RectTransform)transform).rect.size;
            float travel = size.x + size.y;     // 기울어진 띠가 칸 밖에서 밖으로 지나갈 거리
            while (true)
            {
                band.gameObject.SetActive(true);
                for (float t = 0f; t < ShineTime; t += Time.unscaledDeltaTime)
                {
                    float k = t / ShineTime;
                    band.anchoredPosition = new Vector2(Mathf.Lerp(-travel, travel, k) * 0.5f, 0f);
                    yield return null;
                }
                band.gameObject.SetActive(false);
                yield return new WaitForSecondsRealtime(ShineInterval);
            }
        }

        // 칸 크기 가림막(RectMask2D) 안에 흰 띠 하나. 그림 위·NEW 글자 아래에 둔다
        RectTransform CreateShine()
        {
            var mask = new GameObject("ShineMask", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            mask.SetParent(transform, false);
            mask.anchorMin = Vector2.zero; mask.anchorMax = Vector2.one;
            mask.offsetMin = Vector2.zero; mask.offsetMax = Vector2.zero;
            if (newBadge != null && newBadge.transform.parent == transform) mask.SetSiblingIndex(newBadge.transform.GetSiblingIndex());

            var size = ((RectTransform)transform).rect.size;
            var band = new GameObject("Shine", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            band.SetParent(mask, false);
            band.sizeDelta = new Vector2(size.x * ShineWidth, (size.x + size.y) * 1.2f);
            band.localRotation = Quaternion.Euler(0f, 0f, ShineAngle);
            var img = band.GetComponent<Image>();
            img.color = new Color(1f, 1f, 1f, ShineAlpha);
            img.raycastTarget = false;
            band.gameObject.SetActive(false);
            return band;
        }
    }
}
