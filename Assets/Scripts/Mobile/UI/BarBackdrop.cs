using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 하단바에서 선택된 버튼 뒤에 까는 반투명 색 판 (그림 없이 색만). 어느 탭인지 알아보게 한다.
    // 버튼의 첫 자식으로 버튼 전체를 채워 넣는다 — 버튼 크기는 하단바 레이아웃이 나중에 정하므로 값을 베끼지 않고 늘 같은 크기로 따라가게 하고,
    // 첫 자식이라 버튼의 아이콘·글자 뒤에 그려진다. TabIconLift가 지금 탭일 때 쓴다
    public static class BarBackdrop
    {
        const string ObjectName = "SelectedBackdrop";

        // 판을 만들거나 재사용해 target 안으로 넣는다. 이미 있던 판(existing)을 넘기면 그것을 쓴다
        public static Image Place(Image existing, Button target, Color color)
        {
            if (existing == null)
            {
                var go = new GameObject(ObjectName, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                go.GetComponent<LayoutElement>().ignoreLayout = true;
                existing = go.GetComponent<Image>();
                existing.raycastTarget = false;                            // 눌림은 버튼이 받는다
            }
            existing.color = color;
            var rt = (RectTransform)existing.transform;
            if (rt.parent != target.transform) rt.SetParent(target.transform, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.SetAsFirstSibling();                                        // 버튼의 아이콘·글자 뒤
            existing.gameObject.SetActive(true);
            return existing;
        }
    }
}
