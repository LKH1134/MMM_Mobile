using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 스킬 트리 한 가지(독·식용·약용)를 한 화면에 그린다. 시작 노드는 어느 가지에서나 맨 아래 가운데에 두고 그 가지의 첫 노드로 잇는다.
    // 노드 자리와 선은 SkillTreeLayout이 link(연결 노드)를 보고 정한다 (가로 3칸, 위로 갈수록 깊은 노드). 가로는 화면 폭에 맞고 위아래로만 끈다.
    // 처음엔 맨 아래(시작 노드)를 보여 준다
    public class SkillTreeView : MonoBehaviour
    {
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform content;       // 왼쪽 아래 기준. 크기는 층 수에 맞춰 정한다
        [SerializeField] RectTransform lineRoot;
        [SerializeField] RectTransform nodeRoot;
        [SerializeField] SkillNode nodePrefab;

        [Header("배치")]
        [SerializeField] int columns = 3;
        [SerializeField] float laneGap = 320f;        // 칸 사이 거리
        [SerializeField] float outerGap = 115f;       // 맨 바깥 칸 노드 중심에서 바깥 틈(긴 선이 지나는 곳)까지
        [SerializeField] float rowGap = 290f;
        [SerializeField] float bottomPadding = 650f;  // 시작 노드 중심 높이. 하단 메뉴와 떨어지게
        [SerializeField] float topPadding = 480f;     // 상단 재화에 가리지 않게

        [Header("이름")]
        [SerializeField] TMP_FontAsset labelFont;
        [SerializeField] float labelSize = 24f;
        [SerializeField] float labelHeight = 60f;
        [SerializeField] Vector2 labelPadding = new Vector2(6f, 2f);
        [SerializeField] Color labelBackColor = new Color(0.05f, 0.08f, 0.18f, 0.85f);
        [SerializeField] Color startColor = new Color(0.92f, 0.92f, 0.88f);
        [SerializeField] Color edibleColor = new Color(1f, 0.72f, 0.3f);
        [SerializeField] Color medicinalColor = new Color(0.45f, 0.88f, 0.48f);
        [SerializeField] Color toxicColor = new Color(0.78f, 0.6f, 1f);

        [Header("선")]
        [SerializeField] float lineWidth = 6f;
        [SerializeField] float trackGap = 10f;        // 같은 틈·띠를 쓰는 선끼리 비키는 간격
        [SerializeField] Color lineColor = new Color(0.55f, 0.47f, 0.36f, 0.7f);
        [SerializeField] Color learnedLineColor = new Color(1f, 0.86f, 0.5f, 1f);   // 양 끝을 모두 배웠을 때

        class Link
        {
            public readonly List<Image> segments = new List<Image>();
            public int from, to;
        }

        readonly List<SkillNode> nodes = new List<SkillNode>();
        readonly List<Link> links = new List<Link>();

        public IReadOnlyList<SkillNode> Nodes => nodes;
        public SkillBranch Branch { get; private set; }

        void OnEnable() => SkillProgress.Changed += OnChanged;
        void OnDisable() => SkillProgress.Changed -= OnChanged;

        // 시작 노드 + branch 가지의 노드를 깐다. 이전 가지의 노드는 지운다 (노드 클릭은 부른 쪽이 Nodes로 다시 건다)
        public void Build(SkillBranch branch)
        {
            Clear();
            Branch = branch;
            var skills = new List<SkillData>();
            foreach (var skill in GameData.Skills.All)
                if (skill.branch == branch || skill.branch == SkillBranch.Start) skills.Add(skill);

            var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            float viewWidth = viewport.rect.width > 0f ? viewport.rect.width : 1080f;
            var prefabRect = ((RectTransform)nodePrefab.transform).rect;
            float nodeHalf = prefabRect.height * 0.5f;
            float nodeHalfWidth = prefabRect.width * 0.5f;
            const float margin = 4f;
            var layout = SkillTreeLayout.Build(skills, new SkillTreeLayout.Settings
            {
                columns = columns,
                centerX = viewWidth * 0.5f,
                laneGap = laneGap,
                outerGap = outerGap,
                bottom = bottomPadding,
                rowGap = rowGap,
                // 띠: 노드 위 ~ 위층 이름 아래
                bandLow = nodeHalf + margin,
                bandHigh = rowGap - nodeHalf - 6f - labelHeight - labelPadding.y * 2f - margin,
                trackGap = trackGap,
                // 틈에서 벗어날 수 있는 거리: 노드 가장자리와 선 사이에 선 두께만큼 띄운다
                innerReach = laneGap * 0.5f - nodeHalfWidth - lineWidth * 2f,
                outerReach = outerGap - nodeHalfWidth - lineWidth * 2f,
            });

            content.anchorMin = content.anchorMax = content.pivot = Vector2.zero;
            content.localScale = Vector3.one;
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(viewWidth, bottomPadding + layout.topLevel * rowGap + topPadding);
            scroll.horizontal = false;

            foreach (var skill in skills)
            {
                if (!layout.positions.TryGetValue(skill.index, out var position))
                {
                    Debug.LogWarning($"[SkillTreeView] {skill.index}이(가) 시작 노드와 이어지지 않아 트리에 놓지 못했다");
                    continue;
                }
                var node = Instantiate(nodePrefab, nodeRoot);
                node.name = $"Skill_{skill.index}";
                var rt = (RectTransform)node.transform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.anchoredPosition = position;
                node.Setup(skill.index);
                AddLabel(node, skill, rt.rect.width);
                nodes.Add(node);
            }

            foreach (var (from, to, points) in layout.links)
            {
                var link = new Link { from = from, to = to };
                for (int i = 0; i + 1 < points.Count; i++) link.segments.Add(AddSegment(points[i], points[i + 1]));
                links.Add(link);
            }
            RefreshLines();

            // 처음엔 맨 아래 (시작 노드)
            Canvas.ForceUpdateCanvases();
            scroll.velocity = Vector2.zero;
            scroll.verticalNormalizedPosition = 0f;
        }

        // Destroy는 프레임 끝에 지우므로, 가지를 넘긴 그 프레임에 겹쳐 보이지 않게 먼저 끈다
        void Clear()
        {
            foreach (var node in nodes)
                if (node != null)
                {
                    node.gameObject.SetActive(false);
                    Destroy(node.gameObject);
                }
            foreach (var link in links)
                foreach (var segment in link.segments)
                    if (segment != null)
                    {
                        segment.gameObject.SetActive(false);
                        Destroy(segment.gameObject);
                    }
            nodes.Clear();
            links.Clear();
        }

        // 노드 아래 이름. 가지 색으로 구분한다. 틈을 가리지 않게 노드 너비만큼만 쓴다.
        // 아래에서 노드로 들어오는 선이 이름 뒤로 지나가게 글자 크기만 한 어두운 판을 깐다
        void AddLabel(SkillNode node, SkillData skill, float width)
        {
            var go = new GameObject("Name", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(node.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -6f);
            var back = go.AddComponent<Image>();
            back.color = labelBackColor;
            back.raycastTarget = false;

            var textGo = new GameObject("Text", typeof(RectTransform));
            var textRt = (RectTransform)textGo.transform;
            textRt.SetParent(rt, false);
            textRt.anchorMin = textRt.anchorMax = textRt.pivot = new Vector2(0.5f, 1f);
            textRt.anchoredPosition = new Vector2(0f, -labelPadding.y);
            textRt.sizeDelta = new Vector2(width, labelHeight);

            var text = textGo.AddComponent<TextMeshProUGUI>();
            if (labelFont != null) text.font = labelFont;
            text.fontSize = labelSize;
            text.enableAutoSizing = true;    // 긴 이름은 두 줄 안에 들게 줄인다
            text.fontSizeMin = labelSize * 0.75f;
            text.fontSizeMax = labelSize;
            text.alignment = TextAlignmentOptions.Top;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.lineSpacing = -20f;
            text.raycastTarget = false;
            text.color = BranchColor(skill.branch);
            text.text = skill.name ?? skill.key;

            text.ForceMeshUpdate();
            var size = text.GetRenderedValues(true);
            rt.sizeDelta = new Vector2(Mathf.Min(width, size.x) + labelPadding.x * 2f, Mathf.Min(labelHeight, size.y) + labelPadding.y * 2f);
        }

        Color BranchColor(SkillBranch branch)
        {
            switch (branch)
            {
                case SkillBranch.Edible: return edibleColor;
                case SkillBranch.Medicinal: return medicinalColor;
                case SkillBranch.Toxic: return toxicColor;
                default: return startColor;
            }
        }

        // 선분 하나. 꺾이는 곳이 비지 않게 가로·세로 선분은 양 끝을 선 두께 절반씩 늘린다
        Image AddSegment(Vector2 a, Vector2 b)
        {
            var go = new GameObject("Line", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(lineRoot, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            Vector2 d = b - a;
            bool straight = Mathf.Abs(d.x) < 0.01f || Mathf.Abs(d.y) < 0.01f;
            rt.anchoredPosition = (a + b) * 0.5f;
            rt.sizeDelta = new Vector2(d.magnitude + (straight ? lineWidth : 0f), lineWidth);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);

            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        void OnChanged(int index) => RefreshLines();

        void RefreshLines()
        {
            foreach (var link in links)
            {
                bool learned = SkillProgress.GetLevel(link.from) > 0 && SkillProgress.GetLevel(link.to) > 0;
                foreach (var segment in link.segments) segment.color = learned ? learnedLineColor : lineColor;
                // 배운 선이 위로 오게
                if (learned) foreach (var segment in link.segments) segment.transform.SetAsLastSibling();
            }
        }
    }
}
