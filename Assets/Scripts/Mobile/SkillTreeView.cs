using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 스킬 트리 배치. Skill 표의 lane·row 자리에 SkillNode 프리팹을 깔고, requires를 선으로 잇는다.
    // row 0(시작 노드)이 맨 아래이고 위로 갈수록 레어도가 높다. 세로 스크롤(ScrollRect)로 보며, 처음엔 맨 아래를 보여 준다
    public class SkillTreeView : MonoBehaviour
    {
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform content;       // 아래 기준(pivot y 0). 높이는 줄 수에 맞춰 정한다
        [SerializeField] RectTransform lineRoot;
        [SerializeField] RectTransform nodeRoot;
        [SerializeField] SkillNode nodePrefab;

        [Header("배치")]
        [SerializeField] int laneCount = 5;
        [SerializeField] float laneGap = 200f;
        [SerializeField] float rowGap = 260f;
        [SerializeField] float bottomPadding = 600f;  // 시작 노드 중심 높이. 하단 메뉴에 가리지 않게
        [SerializeField] float topPadding = 480f;     // 상단 재화에 가리지 않게

        [Header("이름")]
        [SerializeField] TMP_FontAsset labelFont;
        [SerializeField] float labelSize = 26f;
        [SerializeField] Color startColor = new Color(0.92f, 0.92f, 0.88f);
        [SerializeField] Color edibleColor = new Color(1f, 0.72f, 0.3f);
        [SerializeField] Color medicinalColor = new Color(0.45f, 0.88f, 0.48f);
        [SerializeField] Color toxicColor = new Color(0.78f, 0.6f, 1f);

        [Header("선")]
        [SerializeField] float lineWidth = 10f;
        [SerializeField] Color lineColor = new Color(0.55f, 0.47f, 0.36f, 0.6f);
        [SerializeField] Color learnedLineColor = new Color(1f, 0.86f, 0.5f, 1f);   // 양 끝을 모두 배웠을 때

        struct Link
        {
            public Image line;
            public int from, to;
        }

        readonly List<SkillNode> nodes = new List<SkillNode>();
        readonly List<Link> links = new List<Link>();

        public IReadOnlyList<SkillNode> Nodes => nodes;

        void OnEnable() => SkillProgress.Changed += OnChanged;
        void OnDisable() => SkillProgress.Changed -= OnChanged;

        public void Build()
        {
            Clear();
            var skills = GameData.Skills.All;
            int topRow = 0;
            foreach (var skill in skills) topRow = Mathf.Max(topRow, skill.row);
            content.sizeDelta = new Vector2(content.sizeDelta.x, bottomPadding + topRow * rowGap + topPadding);

            foreach (var skill in skills)
            {
                var node = Instantiate(nodePrefab, nodeRoot);
                node.name = $"Skill_{skill.index}";
                var rt = (RectTransform)node.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                rt.anchoredPosition = Position(skill);
                node.Setup(skill.index);
                AddLabel(node, skill);
                nodes.Add(node);
            }

            foreach (var skill in skills)
            {
                if (skill.requires == null) continue;
                foreach (int index in skill.requires)
                {
                    var from = GameData.Skills.Get(index);
                    if (from == null)
                    {
                        Debug.LogWarning($"[SkillTreeView] {skill.index}의 선행 스킬 {index}이(가) 표에 없다");
                        continue;
                    }
                    links.Add(new Link { line = AddLine(Position(from), Position(skill)), from = index, to = skill.index });
                }
            }
            RefreshLines();

            // 처음엔 시작 노드가 보이게 맨 아래로
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 0f;
        }

        void Clear()
        {
            foreach (var node in nodes) if (node != null) Destroy(node.gameObject);
            foreach (var link in links) if (link.line != null) Destroy(link.line.gameObject);
            nodes.Clear();
            links.Clear();
        }

        Vector2 Position(SkillData skill) =>
            new Vector2((skill.lane - (laneCount - 1) * 0.5f) * laneGap, bottomPadding + skill.row * rowGap);

        // 노드 아래 이름. 가지 색으로 구분한다
        void AddLabel(SkillNode node, SkillData skill)
        {
            var go = new GameObject("Name", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(node.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -6f);
            rt.sizeDelta = new Vector2(laneGap - 8f, 70f);

            var text = go.AddComponent<TextMeshProUGUI>();
            if (labelFont != null) text.font = labelFont;
            text.fontSize = labelSize;
            text.alignment = TextAlignmentOptions.Top;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            text.color = BranchColor(skill.branch);
            text.text = skill.name ?? skill.key;
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

        Image AddLine(Vector2 a, Vector2 b)
        {
            var go = new GameObject("Line", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(lineRoot, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            Vector2 d = b - a;
            rt.anchoredPosition = (a + b) * 0.5f;
            rt.sizeDelta = new Vector2(d.magnitude, lineWidth);
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
                link.line.color = learned ? learnedLineColor : lineColor;
            }
        }
    }
}
