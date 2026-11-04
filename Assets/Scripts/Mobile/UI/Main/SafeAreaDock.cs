using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // Stage(디자인 영역: 폭 1080~1316, 높이 2340, 화면 가운데)보다 화면이 넓거나 길 때, 이 UI를 화면 끝에 붙인다.
    // - stretchWidth: 좌우를 화면 끝까지 늘인다 (Stage 폭 1316을 넘는 넓은 화면·태블릿에서 양옆이 비지 않게)
    // - dockBottom: 아래를 안전 영역(SafeArea) 아래 끝에 붙인다 (Z플립처럼 긴 화면에서 Stage 아래 남는 공간에 뜨지 않게). 처음 Stage 아래와의 간격은 지킨다
    // - keepTop: 위 가장자리는 Stage 안 처음 자리 그대로 → 아래로 내려간 만큼 높이가 늘어난다 (끄면 높이를 지킨 채 통째로 내려간다)
    // - bleedBottom: 바탕을 안전 영역 밖 화면 맨 아래(홈바 자리)까지 늘인다. 안의 버튼은 LayoutGroup 아래 여백을 늘려 안전 영역 안에 남긴다
    // Stage 바로 아래 오브젝트에 붙인다. 플레이 중에만 고친다 — 편집 중에 바꾸면 씬에 화면별 값이 저장된다
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaDock : MonoBehaviour
    {
        [SerializeField] bool stretchWidth = true;
        [SerializeField] bool dockBottom = true;
        [SerializeField] bool keepTop = true;
        [SerializeField] bool bleedBottom = false;

        RectTransform rect, parent, safe, screen;
        LayoutGroup layout;
        int basePaddingBottom;
        float left0, right0, bottom0, top0, bottomGap;   // 처음 자리 (부모 가운데 기준, 부모 단위)
        Vector3 lastSafeMin, lastSafeMax, lastScreenMin, lastScreenMax;
        bool ready;
        readonly Vector3[] corners = new Vector3[4];

        void Start()
        {
            rect = (RectTransform)transform;
            parent = rect.parent as RectTransform;
            safe = parent != null ? parent.parent as RectTransform : null;
            var canvas = GetComponentInParent<Canvas>();
            screen = canvas != null ? (RectTransform)canvas.rootCanvas.transform : null;
            if (parent == null || safe == null || screen == null)
            {
                Debug.LogWarning("[SafeAreaDock] Canvas > SafeArea > Stage(부모) 아래에 둔다", this);
                enabled = false;
                return;
            }
            layout = GetComponent<LayoutGroup>();
            if (layout != null) basePaddingBottom = layout.padding.bottom;

            // 처음 자리를 부모(Stage) 가운데 기준 좌표로 적어 둔다. Stage 높이는 늘 같으므로 위·아래는 이 값을 기준으로 삼는다
            rect.GetWorldCorners(corners);
            var min = parent.InverseTransformPoint(corners[0]);
            var max = parent.InverseTransformPoint(corners[2]);
            var pc = parent.rect.center;
            left0 = min.x - pc.x;
            right0 = max.x - pc.x;
            bottom0 = min.y - pc.y;
            top0 = max.y - pc.y;
            bottomGap = min.y - parent.rect.yMin;
            ready = true;
            Apply();
        }

        void LateUpdate()
        {
            if (!ready) return;
            safe.GetWorldCorners(corners);
            var sMin = corners[0];
            var sMax = corners[2];
            screen.GetWorldCorners(corners);
            if (sMin != lastSafeMin || sMax != lastSafeMax || corners[0] != lastScreenMin || corners[2] != lastScreenMax) Apply();
        }

        void Apply()
        {
            var pc = parent.rect.center;
            safe.GetWorldCorners(corners);
            lastSafeMin = corners[0];
            lastSafeMax = corners[2];
            var sMin = (Vector2)parent.InverseTransformPoint(corners[0]) - pc;   // 안전 영역 끝 (Stage 가운데 기준, Stage 단위)
            screen.GetWorldCorners(corners);
            lastScreenMin = corners[0];
            lastScreenMax = corners[2];
            var fMin = (Vector2)parent.InverseTransformPoint(corners[0]) - pc;   // 화면 끝
            var fMax = (Vector2)parent.InverseTransformPoint(corners[2]) - pc;

            float l = stretchWidth ? fMin.x : left0;
            float r = stretchWidth ? fMax.x : right0;
            float b = bottom0, t = top0;
            if (dockBottom)
            {
                b = sMin.y + bottomGap;
                if (!keepTop) t = b + (top0 - bottom0);
            }
            int extra = 0;
            if (bleedBottom && fMin.y < b)
            {
                extra = Mathf.RoundToInt(b - fMin.y);
                b = fMin.y;
            }
            if (layout != null && layout.padding.bottom != basePaddingBottom + extra)
            {
                layout.padding.bottom = basePaddingBottom + extra;
                LayoutRebuilder.MarkLayoutForRebuild(rect);
            }
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(l, b);
            rect.offsetMax = new Vector2(r, t);
        }
    }
}
