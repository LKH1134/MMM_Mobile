using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // Stage(디자인 영역: 폭 1080~1316, 높이 2340, 화면 가운데)보다 화면이 넓거나 길 때, 이 UI를 화면 끝에 붙인다.
    // - stretchWidth: 좌우를 화면 끝까지 늘인다 (Stage 폭 1316을 넘는 넓은 화면·태블릿에서 양옆이 비지 않게)
    // - dockBottom: 아래를 안전 영역(SafeArea) 아래 끝에 붙인다 (Z플립처럼 긴 화면에서 Stage 아래 남는 공간에 뜨지 않게). 처음 Stage 아래와의 간격은 지킨다
    // - keepTop: 위 가장자리는 Stage 안 처음 자리 그대로 → 아래로 내려간 만큼 높이가 늘어난다 (끄면 높이를 지킨 채 통째로 내려간다)
    // - bleedBottom: 바탕을 안전 영역 밖 화면 맨 아래(홈바 자리)까지 늘인다. 안의 버튼은 LayoutGroup 아래 여백을 늘려 안전 영역 안에 남긴다
    // Canvas > SafeArea > Stage 아래 어디에 두어도 된다 (부모가 Stage가 아니어도 Stage·SafeArea는 위로 찾아 쓴다).
    // 플레이 중에만 고친다 — 편집 중에 바꾸면 씬에 화면별 값이 저장된다
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaDock : MonoBehaviour
    {
        [SerializeField] bool stretchWidth = true;
        [SerializeField] bool dockBottom = true;
        [SerializeField] bool keepTop = true;
        [SerializeField] bool bleedBottom = false;
        [SerializeField] bool coverScreen = false;   // 화면 전체를 덮는다 (팝업 뒤 어둡게 하는 판). 다른 옵션은 무시

        RectTransform rect, parent, stage, safe, screen;
        LayoutGroup layout;
        int basePaddingBottom;
        float left0, right0, bottom0, top0, bottomGap;   // 처음 자리 (부모 가운데 기준) · Stage 아래와의 간격 (Stage 단위)
        Vector3 lastSafeMin, lastSafeMax, lastScreenMin, lastScreenMax;
        bool ready;
        readonly Vector3[] corners = new Vector3[4];

        void Start()
        {
            rect = (RectTransform)transform;
            parent = rect.parent as RectTransform;
            var frame = GetComponentInParent<StageFrame>(true);
            var area = GetComponentInParent<SafeArea>(true);
            var canvas = GetComponentInParent<Canvas>();
            stage = frame != null ? (RectTransform)frame.transform : null;
            safe = area != null ? (RectTransform)area.transform : null;
            screen = canvas != null ? (RectTransform)canvas.rootCanvas.transform : null;
            if (parent == null || stage == null || safe == null || screen == null)
            {
                // 화면 전체 캔버스에 직접 만든 팝업(코드 팝업)은 Stage가 없다 → 앵커 그대로(이미 화면 끝 기준) 둔다
                enabled = false;
                return;
            }
            layout = GetComponent<LayoutGroup>();
            if (layout != null) basePaddingBottom = layout.padding.bottom;

            rect.GetWorldCorners(corners);
            var min = parent.InverseTransformPoint(corners[0]);
            var max = parent.InverseTransformPoint(corners[2]);
            var pc = parent.rect.center;
            left0 = min.x - pc.x;
            right0 = max.x - pc.x;
            bottom0 = min.y - pc.y;
            top0 = max.y - pc.y;
            bottomGap = stage.InverseTransformPoint(corners[0]).y - stage.rect.yMin;
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
            // 안전 영역 아래 끝 + 처음 간격 (Stage 단위로 더한 뒤 부모 좌표로)
            var safeBottomStage = stage.InverseTransformPoint(corners[0]);
            var dockWorld = stage.TransformPoint(new Vector3(0f, safeBottomStage.y + bottomGap, 0f));
            float dockY = parent.InverseTransformPoint(dockWorld).y - pc.y;
            screen.GetWorldCorners(corners);
            lastScreenMin = corners[0];
            lastScreenMax = corners[2];
            var fMin = (Vector2)parent.InverseTransformPoint(corners[0]) - pc;   // 화면 끝 (부모 가운데 기준)
            var fMax = (Vector2)parent.InverseTransformPoint(corners[2]) - pc;

            if (coverScreen)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.offsetMin = fMin;
                rect.offsetMax = fMax;
                return;
            }
            float l = stretchWidth ? fMin.x : left0;
            float r = stretchWidth ? fMax.x : right0;
            float b = bottom0, t = top0;
            if (dockBottom)
            {
                b = dockY;
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
