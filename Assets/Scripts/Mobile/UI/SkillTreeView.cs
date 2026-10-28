using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMush.Mobile
{
    // 스킬 트리 배치. Skill 표의 lane·row 자리에 SkillNode 프리팹을 깔고, link(연결 노드)를 선으로 잇는다.
    // row 0(시작 노드)이 맨 아래이고 위로 갈수록 레어도가 높다. 칸 수는 표에 따라 정해지고, 화면보다 넓으면
    // 가로·세로로 끌어서 본다. 처음엔 시작 노드가 가운데 아래에 오게 보여 준다.
    // 사이에 다른 노드·이름이 없으면 곧은 선으로 잇는다. 그 밖의 선은 다른 노드를 지나지 않게
    // 출발 노드 위 띠 → 노드 사이 틈(통로) → 도착 줄 아래 띠 → 도착 노드로 돌아간다.
    // 같은 통로·띠를 쓰는 선끼리는 조금씩 비켜 그린다
    public class SkillTreeView : MonoBehaviour
    {
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform content;       // 왼쪽 아래 기준. 크기는 칸·줄 수에 맞춰 정한다
        [SerializeField] RectTransform lineRoot;
        [SerializeField] RectTransform nodeRoot;
        [SerializeField] SkillNode nodePrefab;

        [Header("배치")]
        [SerializeField] float laneGap = 200f;
        [SerializeField] float rowGap = 290f;
        [SerializeField] float sidePadding = 300f;    // 맨 바깥 칸 노드 중심에서 가장자리까지
        [SerializeField] float bottomPadding = 600f;  // 시작 노드 중심 높이. 하단 메뉴에 가리지 않게
        [SerializeField] float topPadding = 480f;     // 상단 재화에 가리지 않게

        [Header("이름")]
        [SerializeField] TMP_FontAsset labelFont;
        [SerializeField] float labelSize = 24f;
        [SerializeField] float labelHeight = 60f;
        [SerializeField] Color startColor = new Color(0.92f, 0.92f, 0.88f);
        [SerializeField] Color edibleColor = new Color(1f, 0.72f, 0.3f);
        [SerializeField] Color medicinalColor = new Color(0.45f, 0.88f, 0.48f);
        [SerializeField] Color toxicColor = new Color(0.78f, 0.6f, 1f);

        [Header("선")]
        [SerializeField] float lineWidth = 6f;
        [SerializeField] float trackGap = 9f;         // 같은 통로·띠를 쓰는 선끼리 비키는 간격
        [SerializeField] Color lineColor = new Color(0.55f, 0.47f, 0.36f, 0.7f);
        [SerializeField] Color learnedLineColor = new Color(1f, 0.86f, 0.5f, 1f);   // 양 끝을 모두 배웠을 때

        class Link
        {
            public readonly List<Image> segments = new List<Image>();
            public int from, to;
        }

        // 통로·띠 하나에서 이미 쓴 구간들. 겹치면 다음 트랙(비킨 자리)을 쓴다
        class Tracks
        {
            readonly List<List<Vector2>> used = new List<List<Vector2>>();

            public int Take(float a, float b)
            {
                float lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
                for (int t = 0; ; t++)
                {
                    if (t == used.Count) used.Add(new List<Vector2>());
                    bool free = true;
                    foreach (var span in used[t])
                        if (lo < span.y + 1f && span.x < hi + 1f) { free = false; break; }
                    if (!free) continue;
                    used[t].Add(new Vector2(lo, hi));
                    return t;
                }
            }
        }

        readonly List<SkillNode> nodes = new List<SkillNode>();
        readonly List<Link> links = new List<Link>();
        readonly List<Rect> blockers = new List<Rect>();     // 노드와 그 이름이 차지하는 칸 (선이 피해 갈 곳)
        readonly Dictionary<int, Tracks> channels = new Dictionary<int, Tracks>();   // 통로 번호(lane×2±1) → 세로 트랙
        readonly Dictionary<int, Tracks> bands = new Dictionary<int, Tracks>();      // 줄 번호 → 그 줄 위 띠의 가로 트랙
        float nodeHalf;

        public IReadOnlyList<SkillNode> Nodes => nodes;

        void OnEnable() => SkillProgress.Changed += OnChanged;
        void OnDisable() => SkillProgress.Changed -= OnChanged;

        public void Build()
        {
            Clear();
            var skills = GameData.Skills.All;
            int topRow = 0, rightLane = 0;
            foreach (var skill in skills)
            {
                topRow = Mathf.Max(topRow, skill.row);
                rightLane = Mathf.Max(rightLane, skill.lane);
            }
            var prefabRect = ((RectTransform)nodePrefab.transform).rect;
            nodeHalf = prefabRect.height * 0.5f;
            foreach (var skill in skills)
            {
                Vector2 c = Position(skill.lane, skill.row);
                float bottom = c.y - nodeHalf - 6f - labelHeight;
                blockers.Add(Rect.MinMaxRect(c.x - prefabRect.width * 0.5f, bottom, c.x + prefabRect.width * 0.5f, c.y + nodeHalf));
            }

            content.anchorMin = content.anchorMax = content.pivot = Vector2.zero;
            content.anchoredPosition = Vector2.zero;
            float viewWidth = scroll.viewport != null ? scroll.viewport.rect.width : 1080f;
            content.sizeDelta = new Vector2(Mathf.Max(viewWidth, sidePadding * 2f + rightLane * laneGap),
                                            bottomPadding + topRow * rowGap + topPadding);
            scroll.horizontal = content.sizeDelta.x > viewWidth;

            foreach (var skill in skills)
            {
                var node = Instantiate(nodePrefab, nodeRoot);
                node.name = $"Skill_{skill.index}";
                var rt = (RectTransform)node.transform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.anchoredPosition = Position(skill.lane, skill.row);
                node.Setup(skill.index);
                AddLabel(node, skill, rt.rect.width);
                nodes.Add(node);
            }

            // 짧은 선부터 트랙을 잡아 긴 선이 바깥 트랙으로 비키게 한다
            var ordered = new List<SkillData>();
            foreach (var skill in skills) if (skill.link != 0) ordered.Add(skill);
            ordered.Sort((a, b) => Span(a).CompareTo(Span(b)));
            foreach (var skill in ordered)
            {
                var from = GameData.Skills.Get(skill.link);
                if (from == null)
                {
                    Debug.LogWarning($"[SkillTreeView] {skill.index}의 연결 노드 {skill.link}이(가) 표에 없다");
                    continue;
                }
                var link = new Link { from = from.index, to = skill.index };
                foreach (var (a, b) in Route(from, skill)) link.segments.Add(AddSegment(a, b));
                links.Add(link);
            }
            RefreshLines();

            // 처음엔 시작 노드가 가운데 아래에 오게
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 0f;
            foreach (var skill in skills)
            {
                if (skill.link != 0) continue;
                float range = content.sizeDelta.x - viewWidth;
                if (range > 0f) scroll.horizontalNormalizedPosition = Mathf.Clamp01((Position(skill.lane, 0).x - viewWidth * 0.5f) / range);
                break;
            }
        }

        void Clear()
        {
            foreach (var node in nodes) if (node != null) Destroy(node.gameObject);
            foreach (var link in links)
                foreach (var segment in link.segments) if (segment != null) Destroy(segment.gameObject);
            nodes.Clear();
            links.Clear();
            blockers.Clear();
            channels.Clear();
            bands.Clear();
        }

        int Span(SkillData skill)
        {
            var from = GameData.Skills.Get(skill.link);
            return from == null ? 0 : Mathf.Abs(skill.row - from.row) * 10 + Mathf.Abs(skill.lane - from.lane);
        }

        float LaneX(float lane) => sidePadding + lane * laneGap;
        Vector2 Position(int lane, int row) => new Vector2(LaneX(lane), bottomPadding + row * rowGap);

        // 선이 지나갈 점들을 선분 목록으로 돌려준다
        List<(Vector2, Vector2)> Route(SkillData from, SkillData to)
        {
            var result = new List<(Vector2, Vector2)>();
            Vector2 p = Position(from.lane, from.row), q = Position(to.lane, to.row);
            int dLane = to.lane - from.lane, dRow = to.row - from.row;

            // 사이에 다른 노드·이름이 없으면 곧은 선
            if (Clear(p, q))
            {
                result.Add((p, q));
                return result;
            }

            // 출발 노드 위쪽 띠로 올라가서 → (바로 위 줄이면) 띠를 따라 도착 칸까지 → 도착 노드로 올라간다.
            // 더 멀면 띠에서 통로로 옮겨 타고 도착 줄 바로 아래 띠까지 올라간 뒤 도착 칸으로.
            // 옆으로 나가는 짧은 선이 없어서 이웃 노드끼리 이어진 것처럼 보이지 않는다
            float y1;
            if (dRow == 1)
            {
                y1 = BandY(from.row, p.x, q.x);
                result.Add((p, new Vector2(p.x, y1)));
                result.Add((new Vector2(p.x, y1), new Vector2(q.x, y1)));
                result.Add((new Vector2(q.x, y1), q));
                return result;
            }

            // 통로: 도착 칸 옆의 출발 쪽 틈. 같은 칸이면 오른쪽 틈
            int side = dLane != 0 ? (int)Mathf.Sign(dLane) : 1;
            int channel = dLane != 0 ? to.lane * 2 - side : from.lane * 2 + side;   // 통로 위치 = 통로 번호 / 2 (lane 단위)
            float cx = ChannelBase(channel);
            y1 = BandY(from.row, p.x, cx);
            float y2 = BandY(to.row - 1, cx, q.x);
            float x = ChannelX(channel, y1, y2);
            result.Add((p, new Vector2(p.x, y1)));
            result.Add((new Vector2(p.x, y1), new Vector2(x, y1)));
            result.Add((new Vector2(x, y1), new Vector2(x, y2)));
            result.Add((new Vector2(x, y2), new Vector2(q.x, y2)));
            result.Add((new Vector2(q.x, y2), q));
            return result;
        }

        // 선분 p→q가 양 끝 노드 말고 다른 노드·이름 칸을 지나지 않는지 (도착 노드 이름은 지나가도 된다)
        bool Clear(Vector2 p, Vector2 q)
        {
            const float margin = 8f;
            foreach (var box in blockers)
            {
                if (box.Contains(p) || box.Contains(q)) continue;   // 출발·도착 노드 (도착 노드 이름 포함)
                var r = Rect.MinMaxRect(box.xMin - margin, box.yMin - margin, box.xMax + margin, box.yMax + margin);
                if (SegmentHits(p, q, r)) return false;
            }
            return true;
        }

        // Liang–Barsky 선분·사각형 교차
        static bool SegmentHits(Vector2 p, Vector2 q, Rect r)
        {
            float t0 = 0f, t1 = 1f;
            Vector2 d = q - p;
            float[] pp = { -d.x, d.x, -d.y, d.y };
            float[] qq = { p.x - r.xMin, r.xMax - p.x, p.y - r.yMin, r.yMax - p.y };
            for (int i = 0; i < 4; i++)
            {
                if (Mathf.Abs(pp[i]) < 1e-5f)
                {
                    if (qq[i] < 0f) return false;
                    continue;
                }
                float t = qq[i] / pp[i];
                if (pp[i] < 0f) t0 = Mathf.Max(t0, t);
                else t1 = Mathf.Min(t1, t);
            }
            return t0 < t1;
        }

        float ChannelBase(int channel) => LaneX(channel * 0.5f);

        float ChannelX(int channel, float y1, float y2)
        {
            if (!channels.TryGetValue(channel, out var tracks)) channels[channel] = tracks = new Tracks();
            return ChannelBase(channel) + Offset(tracks.Take(y1, y2));
        }

        // row 줄 노드 위와 row+1 줄 이름 아래 사이의 띠
        float BandY(int row, float x1, float x2)
        {
            if (!bands.TryGetValue(row, out var tracks)) bands[row] = tracks = new Tracks();
            float baseY = bottomPadding + row * rowGap + nodeHalf + (rowGap - 2f * nodeHalf - labelHeight - 6f) * 0.5f;
            return baseY + Offset(tracks.Take(x1, x2));
        }

        // 트랙 0, 1, 2, 3, 4 … → 0, +1, −1, +2, −2 … 간격
        float Offset(int track) => ((track + 1) / 2) * (track % 2 == 1 ? 1f : -1f) * trackGap;

        // 노드 아래 이름. 가지 색으로 구분한다. 통로를 가리지 않게 노드 너비만큼만 쓴다
        void AddLabel(SkillNode node, SkillData skill, float width)
        {
            var go = new GameObject("Name", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(node.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -6f);
            rt.sizeDelta = new Vector2(width, labelHeight);

            var text = go.AddComponent<TextMeshProUGUI>();
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
