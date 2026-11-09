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
        // 프리팹에 정한 자리 (앵커·오프셋). 매번 지금 부모 크기로 다시 풀어 쓴다 → 재생 중 화면 비율이 바뀌어도 처음 비율의 폭·높이가 남지 않는다
        Vector2 anchorMin0, anchorMax0, offsetMin0, offsetMax0;
        float bottomGap;   // Stage 아래와의 간격 (Stage 단위)
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

            anchorMin0 = rect.anchorMin;
            anchorMax0 = rect.anchorMax;
            offsetMin0 = rect.offsetMin;
            offsetMax0 = rect.offsetMax;
            rect.GetWorldCorners(corners);
            bottomGap = stage.InverseTransformPoint(corners[0]).y - stage.rect.yMin;
            ready = true;
            Apply();
        }

        // 매 프레임 다시 계산하고, 결과가 달라졌을 때만 쓴다.
        // 화면 크기가 바뀔 때 캔버스(렌더 직전)·SafeArea·StageFrame(Update, 순서 미정)이 여러 프레임에 걸쳐 바뀌어서
        // "모서리가 바뀌었을 때만" 다시 맞추면 중간 상태로 계산한 자리에 굳는다 (PC 창 최대화·해상도 변경 때 탭바가 화면 밖으로 사라짐)
        void LateUpdate()
        {
            if (ready) Apply();
        }

        void Apply()
        {
            var pc = parent.rect.center;
            safe.GetWorldCorners(corners);
            // 안전 영역 아래 끝 + 처음 간격 (Stage 단위로 더한 뒤 부모 좌표로)
            var safeBottomStage = stage.InverseTransformPoint(corners[0]);
            var dockWorld = stage.TransformPoint(new Vector3(0f, safeBottomStage.y + bottomGap, 0f));
            float dockY = parent.InverseTransformPoint(dockWorld).y - pc.y;
            screen.GetWorldCorners(corners);
            var fMin = (Vector2)parent.InverseTransformPoint(corners[0]) - pc;   // 화면 끝 (부모 가운데 기준)
            var fMax = (Vector2)parent.InverseTransformPoint(corners[2]) - pc;

            if (coverScreen)
            {
                SetRect(fMin, fMax);
                return;
            }
            // 프리팹 자리를 지금 부모 크기로 (부모 가운데 기준)
            var pr = parent.rect;
            var min0 = pr.min + Vector2.Scale(pr.size, anchorMin0) + offsetMin0 - pc;
            var max0 = pr.min + Vector2.Scale(pr.size, anchorMax0) + offsetMax0 - pc;
            float l = stretchWidth ? fMin.x : min0.x;
            float r = stretchWidth ? fMax.x : max0.x;
            float b = min0.y, t = max0.y;
            float bottom0 = min0.y, top0 = max0.y;
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
            SetRect(new Vector2(l, b), new Vector2(r, t));
        }

        // 같은 값이면 건드리지 않는다 (매 프레임 부르므로 레이아웃·캔버스를 쓸데없이 더럽히지 않게)
        const float Epsilon = 0.01f;
        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        void SetRect(Vector2 offsetMin, Vector2 offsetMax)
        {
            if (rect.anchorMin == Center && rect.anchorMax == Center
                && (rect.offsetMin - offsetMin).sqrMagnitude < Epsilon * Epsilon
                && (rect.offsetMax - offsetMax).sqrMagnitude < Epsilon * Epsilon) return;
            rect.anchorMin = rect.anchorMax = Center;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
