using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 끝난 카드(받은 일일 퀘스트·세계수 의뢰)를 회색으로 덮는다: 카드와 같은 모양(카드 바탕 그림)의 반투명 회색 막을 맨 위에 얹고,
    // keepOnTop(카드 바로 아래 자식, 예: "완료" 글자 버튼)만 막 위로 올린다. 막은 처음 쓸 때 코드로 만들고, 끄면 숨긴다
    public static class DoneCover
    {
        const string CoverName = "DoneCover";
        static readonly Color Tint = new Color(0.42f, 0.42f, 0.42f, 0.6f);

        public static void Set(Transform card, bool on, Transform keepOnTop = null)
        {
            if (card == null) return;
            var cover = card.Find(CoverName);
            if (!on)
            {
                if (cover != null) cover.gameObject.SetActive(false);
                return;
            }
            if (cover == null) cover = Create(card);
            cover.gameObject.SetActive(true);
            // 순서가 이미 맞으면 건드리지 않는다 (형제 순서를 바꾸면 레이아웃이 다시 돈다)
            bool keep = keepOnTop != null && keepOnTop.parent == card;
            int last = card.childCount - 1;
            if (cover.GetSiblingIndex() != (keep ? last - 1 : last) || (keep && keepOnTop.GetSiblingIndex() != last))
            {
                cover.SetAsLastSibling();
                if (keep) keepOnTop.SetAsLastSibling();
            }
        }

        static Transform Create(Transform card)
        {
            var go = new GameObject(CoverName, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.GetComponent<LayoutElement>().ignoreLayout = true;     // 카드에 레이아웃이 있어도 자리를 차지하지 않게
            go.layer = card.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(card, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;      // 막이 눌림을 가로채지 않게 (받은 카드는 버튼이 꺼져 있다)
            img.color = Tint;
            // 카드 바탕과 같은 그림이면 둥근 모서리까지 맞는다 (바탕 그림이 없으면 네모)
            if (card.TryGetComponent<Image>(out var bg) && bg.sprite != null)
            {
                img.sprite = bg.sprite;
                img.type = bg.type;
                img.pixelsPerUnitMultiplier = bg.pixelsPerUnitMultiplier;
            }
            return rt;
        }
    }
}
