using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 세계수 그림: 지금 성장 단계의 그림(세계수 시트 resourceId)과 놓은 장식을 보여 준다. 세계수가 바뀌면 다시 그린다.
    // 단계마다 그림 크기가 달라 preserveAspect로 이 칸(패널) 안에 비율을 지켜 맞춘다.
    // 장식은 자리(뿌리·줄기·가지·수관)마다 정한 높이에 가로로 나란히 놓는다 (이 칸 기준 비율)
    [RequireComponent(typeof(Image))]
    public class WorldTreeView : MonoBehaviour
    {
        [SerializeField] float[] zoneHeights = { 0.08f, 0.35f, 0.6f, 0.85f };   // 뿌리·줄기·가지·수관 장식 높이 (아래 0 ~ 위 1)
        [SerializeField] float[] zoneWidths = { 0.9f, 0.4f, 0.7f, 0.6f };      // 장식을 펼치는 너비 (칸 너비 비율)
        [SerializeField] float decoSize = 0.2f;                                 // 장식 크기 (칸 너비 비율)

        Image image;
        int shownStage = -1;
        readonly List<Image> decoImages = new List<Image>();

        void Awake()
        {
            image = GetComponent<Image>();
            image.preserveAspect = true;
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
            }
            ShowDecos();
        }

        void ShowDecos()
        {
            var rt = (RectTransform)transform;
            Vector2 size = rt.rect.size;
            int used = 0;
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

        Image DecoImage(int i)
        {
            while (decoImages.Count <= i)
            {
                var img = new GameObject("Deco", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                img.transform.SetParent(transform, false);
                img.raycastTarget = false;
                img.preserveAspect = true;
                decoImages.Add(img);
            }
            return decoImages[i];
        }
    }
}
