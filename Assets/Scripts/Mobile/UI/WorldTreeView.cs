using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 세계수 그림: 지금 성장 단계의 그림(세계수 시트 resourceId)과 놓은 장식을 보여 준다. 세계수가 바뀌면 다시 그린다.
    // 그림은 preserveAspect로 이 칸 안에 비율을 지켜 맞춘다. 칸은 단계마다 stageSizes만큼 커진다 —
    // 씬에 놓인 칸의 아래 변(땅)과 가로 가운데는 그대로 두고 위로 자란다.
    // 장식은 자리(뿌리·줄기·가지·수관)마다 정한 높이에 가로로 나란히 놓는다 (이 칸 기준 비율이라 나무와 같이 커진다)
    [RequireComponent(typeof(Image))]
    public class WorldTreeView : MonoBehaviour
    {
        [SerializeField] float[] zoneHeights = { 0.08f, 0.35f, 0.6f, 0.85f };   // 뿌리·줄기·가지·수관 장식 높이 (아래 0 ~ 위 1)
        [SerializeField] float[] zoneWidths = { 0.9f, 0.4f, 0.7f, 0.6f };      // 장식을 펼치는 너비 (칸 너비 비율)
        [SerializeField] float decoSize = 0.2f;                                 // 장식 크기 (칸 너비 비율)
        // 단계별 나무 칸 크기 (정사각, 기준 해상도 px). 1단계 새싹 → 8단계 세계수. 단계가 더 많으면 마지막 값
        [SerializeField] float[] stageSizes = { 210f, 300f, 360f, 420f, 500f, 570f, 640f, 760f };
        [SerializeField] Image decoTemplate;    // 장식 그림 견본 (이 칸 아래에 꺼 둔 DecoTemplate)

        Image image;
        RectTransform rt;
        Vector2 ground;     // 칸 아래 변 가운데 (부모 기준)
        int shownStage = -1;
        readonly List<Image> decoImages = new List<Image>();

        void Awake()
        {
            image = GetComponent<Image>();
            image.preserveAspect = true;
            rt = (RectTransform)transform;
            // 씬에 놓인 자리(늘이기 앵커일 수 있다)를 가운데 앵커 + 아래 가운데 피벗으로 바꿔 둔다
            var parent = (RectTransform)rt.parent;
            Rect r = rt.rect;
            Vector2 local = (Vector2)rt.localPosition;
            ground = new Vector2(local.x + r.center.x, local.y + r.yMin) - parent.rect.center;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = ground;
        }

        float SizeFor(int stage)
        {
            if (stageSizes == null || stageSizes.Length == 0) return rt.rect.width;
            return stageSizes[Mathf.Clamp(stage - 1, 0, stageSizes.Length - 1)];
        }

        void OnEnable()
        {
            WorldTree.Changed += Refresh;
            Refresh();
        }

        void OnDisable() => WorldTree.Changed -= Refresh;

        void Refresh()
        {
            if (shownStage != WorldTree.Stage)
            {
                var sprite = WorldTree.TreeSprite(WorldTree.Stage);
                if (sprite != null) image.sprite = sprite;
                shownStage = WorldTree.Stage;
                float size = SizeFor(shownStage);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = ground;
            }
            ShowDecos();
        }

        void ShowDecos()
        {
            var rt = (RectTransform)transform;
            Vector2 size = rt.rect.size;
            int used = 0;
            if (decoTemplate == null) return;
            foreach (TreeZone zone in System.Enum.GetValues(typeof(TreeZone)))
            {
                var placed = WorldTree.PlacedIn(zone);
                int z = (int)zone;
                for (int i = 0; i < placed.Count; i++)
                {
                    var img = DecoImage(used++);
                    img.sprite = WorldTree.DecoSprite(placed[i]);
                    float t = placed.Count == 1 ? 0.5f : i / (float)(placed.Count - 1);
                    float x = (t - 0.5f) * zoneWidths[z] * size.x;
                    float y = (zoneHeights[z] - 0.5f) * size.y;
                    img.rectTransform.sizeDelta = Vector2.one * decoSize * size.x;
                    img.rectTransform.anchoredPosition = new Vector2(x, y);
                    img.gameObject.SetActive(true);
                }
            }
            for (int i = used; i < decoImages.Count; i++) decoImages[i].gameObject.SetActive(false);
        }

        // 놓은 장식 수만큼 견본(씬에 배치해 꺼 둔 decoTemplate)을 복사해 쓴다. 모양(그림자·재질 등)은 견본에서 고친다
        Image DecoImage(int i)
        {
            while (decoImages.Count <= i)
            {
                var img = Instantiate(decoTemplate, transform, false);
                img.name = "Deco";
                decoImages.Add(img);
            }
            return decoImages[i];
        }
    }
}
