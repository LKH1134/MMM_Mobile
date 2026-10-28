using System.Collections.Generic;
using UnityEngine;

namespace MoreMush.Mobile
{
    // 스킬 트리 한 가지의 자동 배치. 표의 lane·row 자리는 쓰지 않고 link(부모)를 따라 정한다.
    // 1) 층: 시작 노드가 0층. 깊이 우선(형제는 작은 갈래부터)으로 쌓으며 부모보다 위이면서 칸이 남은 가장 낮은 층에 놓는다.
    //    첫 갈림에서는 갈래마다 따로 쌓아 함께 키운다 (한 갈래가 다 쌓일 때까지 기다리는 갈래의 긴 선이 한쪽에 몰리지 않게).
    // 2) 한 층 안의 좌우 순서: 트리의 좌우 배치(형제 중 먼저 쌓는 갈래가 가운데, 나머지는 왼쪽·오른쪽)를 깊이 우선으로 훑은 순서.
    //    여러 층을 건너는 선은 거치는 층마다 '지나가는 점'을 두고 도착 노드의 순서를 따른다 → 이웃한 두 층 사이의 선끼리 엇갈리지 않는다.
    //    곁가지를 왼쪽·오른쪽 어디에 붙일지는 지나가는 선이 가운데 기준 좌우로 고르게 나뉘도록 찾는다.
    // 3) 자리: 노드는 칸 가운데, 지나가는 점은 칸 사이 틈이나 빈 칸에. 순서를 지키면서 아래 층 점과 가장 가까운 자리를 고른다.
    // 4) 선: 아래 점 → 두 층 사이 띠의 트랙 → 가로로 위 점의 x → 위 점. 같은 띠에서 가로 구간이 겹치는 선은
    //    엇갈리지 않는 높이로 나눈다 (오른쪽으로 가는 선은 오른쪽에서 출발한 것이 아래, 왼쪽으로 가는 선은 왼쪽에서 출발한 것이 아래)
    public static class SkillTreeLayout
    {
        public struct Settings
        {
            public int columns;
            public float centerX;        // 가운데 칸의 x
            public float laneGap;        // 칸 사이 거리
            public float outerGap;       // 맨 바깥 칸 노드 중심에서 바깥 틈까지
            public float bottom;         // 0층(시작 노드) 중심 높이
            public float rowGap;
            public float bandLow, bandHigh;   // 층 중심에서 띠(선이 가로로 지나는 곳)의 아래·위 끝까지 (노드 위 ~ 위층 이름 아래)
            public float trackGap;       // 같은 틈·띠를 쓰는 선끼리의 간격
            public float innerReach;     // 칸 사이 틈(과 빈 칸)에서 지나가는 선이 가운데에서 벗어날 수 있는 거리 (노드에 닿지 않게)
            public float outerReach;     // 바깥 틈에서 같은 거리
        }

        public class Result
        {
            public readonly Dictionary<int, Vector2> positions = new Dictionary<int, Vector2>();
            public readonly List<(int from, int to, List<Vector2> points)> links = new List<(int, int, List<Vector2>)>();
            public int topLevel;
        }

        // 한 층의 점 하나: 노드이거나, 여러 층을 건너는 선이 지나가는 점
        class Item
        {
            public int target;           // 노드면 그 노드, 지나가는 점이면 선이 도착하는 노드
            public bool real;
            public Item down;            // 같은 선에서 바로 아래 층의 점 (부모 노드 또는 지나가는 점)
            public float x;
        }

        const int Restarts = 16;
        const int SplitDepth = 1;         // 이 단계까지의 갈림에서 갈래별로 따로 쌓는다 (2 이상이면 갈래가 뒤섞여 긴 선이 2배쯤)

        static int FreeLevel(SkillData skill, Dictionary<int, int> level, List<int> count, int columns)
        {
            int l = skill.link != 0 && level.TryGetValue(skill.link, out int parent) ? parent + 1 : 0;
            while (l < count.Count && count[l] >= columns) l++;
            return l;
        }

        public static Result Build(IList<SkillData> skills, Settings s)
        {
            var byIndex = new Dictionary<int, SkillData>();
            foreach (var skill in skills) byIndex[skill.index] = skill;
            var children = new Dictionary<int, List<SkillData>>();
            var roots = new List<SkillData>();
            foreach (var skill in skills)
            {
                if (skill.link != 0 && byIndex.ContainsKey(skill.link))
                {
                    if (!children.TryGetValue(skill.link, out var list)) children[skill.link] = list = new List<SkillData>();
                    list.Add(skill);
                }
                else roots.Add(skill);
            }
            System.Comparison<SkillData> byLane = (a, b) => a.lane != b.lane ? a.lane.CompareTo(b.lane) : a.row != b.row ? a.row.CompareTo(b.row) : a.index.CompareTo(b.index);
            roots.Sort(byLane);
            // 쌓는 순서: 형제 중 작은 갈래부터 (크기가 같으면 lane 순서). 작은 갈래를 먼저 끝내야 큰 갈래가 기다리며 생기는 긴 선이 적다
            var size = new Dictionary<int, int>();
            int SizeOf(SkillData skill)
            {
                if (size.TryGetValue(skill.index, out int n)) return n;
                n = 1;
                if (children.TryGetValue(skill.index, out var list))
                    foreach (var child in list) n += SizeOf(child);
                return size[skill.index] = n;
            }
            foreach (var list in children.Values)
                list.Sort((a, b) => SizeOf(a) != SizeOf(b) ? SizeOf(a).CompareTo(SizeOf(b)) : byLane(a, b));

            // 1) 층: 깊이 우선으로 쌓으며, 부모보다 위이면서 칸이 남은 가장 낮은 층에 놓는다.
            //    한 갈래를 끝까지 먼저 쌓으므로 부모 바로 위 층에 놓이는 노드가 많다 (표의 row 순서로 쌓으면 갈래가 뒤섞여 여러 층을 건너는 선이 4배쯤 많았다).
            //    다만 위쪽 갈림(SplitDepth 단계까지)에서는 갈래마다 따로 쌓기 줄기를 만들어 함께 키운다 (더 낮은 층에 놓일 줄기부터).
            //    한 갈래를 다 쌓은 뒤 다른 갈래를 쌓으면 늦은 갈래 전체의 긴 선이 한쪽에 몰리기 때문
            var preorder = new List<SkillData>();   // 쌓은 순서
            var level = new Dictionary<int, int>();
            var count = new List<int>();
            var streams = new List<(Stack<SkillData> stack, int depth, int used)>();
            foreach (var root in roots)
            {
                var stack = new Stack<SkillData>();
                stack.Push(root);
                streams.Add((stack, 0, 0));
            }
            int tick = 0;
            while (streams.Count > 0)
            {
                int pick = -1, pickLevel = int.MaxValue;
                for (int i = 0; i < streams.Count; i++)
                {
                    int l = FreeLevel(streams[i].stack.Peek(), level, count, s.columns);
                    if (l < pickLevel || (l == pickLevel && streams[i].used < streams[pick].used))
                    {
                        pick = i;
                        pickLevel = l;
                    }
                }
                var (stack, depth, _) = streams[pick];
                var skill = stack.Pop();
                streams[pick] = (stack, depth, ++tick);
                while (count.Count <= pickLevel) count.Add(0);
                count[pickLevel]++;
                level[skill.index] = pickLevel;
                preorder.Add(skill);
                if (children.TryGetValue(skill.index, out var list))
                {
                    if (list.Count > 1 && depth < SplitDepth)
                        foreach (var child in list)
                        {
                            var split = new Stack<SkillData>();
                            split.Push(child);
                            streams.Add((split, depth + 1, 0));
                        }
                    else
                        for (int i = list.Count - 1; i >= 0; i--) stack.Push(list[i]);
                }
                if (stack.Count == 0) streams.RemoveAt(pick);
            }

            // 좌우 순서: 쌓는 순서를 그대로 쓰면 늦게 쌓여 여러 층을 건너는 갈래가 모두 오른쪽 끝으로 몰린다.
            // 형제 중 먼저 쌓는 갈래를 가운데에 두고 나머지를 왼쪽·오른쪽에 번갈아 붙이되, 갈래가 나뉘는 노드마다
            // 첫 곁가지를 어느 쪽에 붙일지 하나씩 뒤집어 보며 지나가는 선이 가운데 기준 좌우로 더 고르게 나뉠 때만 받아들인다.
            // 층은 바뀌지 않으므로 선 수·높이는 그대로이고 좌우만 바뀐다
            var forks = new List<int>();
            foreach (var skill in preorder)
                if (children.TryGetValue(skill.index, out var list) && list.Count > 1) forks.Add(skill.index);
            // 하나씩 뒤집기만 하면 중간에 막히므로, 시작 상태를 바꿔(고정 시드라 매번 같은 결과) 여러 번 해 보고 가장 고른 것을 쓴다
            Result best = null;
            int bestScore = int.MaxValue;
            var random = new System.Random(1);
            var leftFirst = new Dictionary<int, bool>();
            int attempts = forks.Count > 0 ? Restarts : 1;
            for (int attempt = 0; attempt < attempts && bestScore > 0; attempt++)
            {
                for (int i = 0; i < forks.Count; i++) leftFirst[forks[i]] = attempt == 0 ? i % 2 == 0 : random.Next(2) == 0;
                var current = Lay(preorder, level, count.Count, SideOrder(roots, children, leftFirst), s, out int currentScore);
                for (int pass = 0; pass < 4 && currentScore > 0; pass++)
                {
                    bool improved = false;
                    foreach (var fork in forks)
                    {
                        leftFirst[fork] = !leftFirst[fork];
                        var result = Lay(preorder, level, count.Count, SideOrder(roots, children, leftFirst), s, out int score);
                        if (score < currentScore)
                        {
                            current = result;
                            currentScore = score;
                            improved = true;
                        }
                        else leftFirst[fork] = !leftFirst[fork];
                    }
                    if (!improved) break;
                }
                if (currentScore < bestScore)
                {
                    best = current;
                    bestScore = currentScore;
                }
            }
            return best;
        }

        // 좌우 순서 번호. 자식 목록(쌓는 순서)의 첫째를 가운데, 그 뒤를 왼쪽·오른쪽 번갈아 (leftFirst면 왼쪽부터)
        static Dictionary<int, int> SideOrder(List<SkillData> roots, Dictionary<int, List<SkillData>> children, Dictionary<int, bool> leftFirst)
        {
            var order = new Dictionary<int, int>();
            void Visit(SkillData skill)
            {
                order[skill.index] = order.Count;
                if (!children.TryGetValue(skill.index, out var list)) return;
                var sides = new LinkedList<SkillData>();
                sides.AddLast(list[0]);
                bool left = leftFirst.TryGetValue(skill.index, out bool first) && first;
                for (int i = 1; i < list.Count; i++)
                {
                    if (left) sides.AddFirst(list[i]);
                    else sides.AddLast(list[i]);
                    left = !left;
                }
                foreach (var child in sides) Visit(child);
            }
            foreach (var root in roots) Visit(root);
            return order;
        }

        // 2)~4) 정해진 층·좌우 순서로 자리와 선을 만든다. imbalance = 가운데 기준 좌우 치우침 (작을수록 고름)
        static Result Lay(List<SkillData> preorder, Dictionary<int, int> level, int levels, Dictionary<int, int> order, Settings s, out int imbalance)
        {
            var result = new Result { topLevel = levels - 1 };

            // 2) 층마다 점들 (아래 층부터 만들어서 down을 잇는다)
            var layers = new List<List<Item>>();
            for (int l = 0; l < levels; l++) layers.Add(new List<Item>());
            var nodeItem = new Dictionary<int, Item>();
            var byLevel = new List<SkillData>(preorder);
            byLevel.Sort((a, b) => level[a.index].CompareTo(level[b.index]));
            foreach (var skill in byLevel)
            {
                Item down = null;
                if (skill.link != 0 && nodeItem.TryGetValue(skill.link, out var parent))
                {
                    down = parent;
                    for (int l = level[skill.link] + 1; l < level[skill.index]; l++)
                    {
                        var pass = new Item { target = skill.index, down = down };
                        layers[l].Add(pass);
                        down = pass;
                    }
                }
                var item = new Item { target = skill.index, real = true, down = down };
                layers[level[skill.index]].Add(item);
                nodeItem[skill.index] = item;
            }

            // 3) 자리
            int leftPass = 0, rightPass = 0;
            var side = new Dictionary<int, int>();   // 여러 층을 건너는 선 → 지나가는 점이 가운데 왼쪽(−)·오른쪽(+)에 몇 개 더 있나
            foreach (var layer in layers)
            {
                layer.Sort((a, b) => order[a.target].CompareTo(order[b.target]));
                PlaceLayer(layer, s);
                foreach (var item in layer)
                {
                    if (item.real) continue;
                    int sign = item.x < s.centerX - 1f ? -1 : item.x > s.centerX + 1f ? 1 : 0;
                    if (sign < 0) leftPass++;
                    else if (sign > 0) rightPass++;
                    side.TryGetValue(item.target, out int sum);
                    side[item.target] = sum + sign;
                }
            }
            int leftLinks = 0, rightLinks = 0;
            foreach (int sum in side.Values)
            {
                if (sum < 0) leftLinks++;
                else if (sum > 0) rightLinks++;
            }
            // 긴 선 개수의 좌우 차(한 개 = 다섯 층 길이로 셈)와 지나가는 길이(층 수)의 좌우 차
            imbalance = Mathf.Abs(leftLinks - rightLinks) * 5 + Mathf.Abs(leftPass - rightPass);

            // 4) 띠마다 트랙 → 선
            var bend = new Dictionary<Item, float>();   // 위 점 → 그 점으로 들어오는 가로 구간의 높이 (곧은 선이면 없음)
            for (int l = 0; l + 1 < layers.Count; l++)
                AssignTracks(layers[l + 1], RowY(l, s), s, bend);

            foreach (var skill in preorder)
            {
                var item = nodeItem[skill.index];
                result.positions[skill.index] = new Vector2(item.x, RowY(level[skill.index], s));
                if (item.down == null) continue;
                var points = new List<Vector2>();
                var chain = new List<Item>();
                for (var it = item; it != null; it = it.down)
                {
                    chain.Add(it);
                    if (it.real && it != item) break;
                }
                chain.Reverse();
                points.Add(new Vector2(chain[0].x, RowY(level[skill.link], s)));
                for (int i = 1; i < chain.Count; i++)
                {
                    if (!bend.TryGetValue(chain[i], out float y)) continue;
                    points.Add(new Vector2(chain[i - 1].x, y));
                    points.Add(new Vector2(chain[i].x, y));
                }
                points.Add(result.positions[skill.index]);
                result.links.Add((skill.link, skill.index, points));
            }
            return result;
        }

        static float RowY(int level, Settings s) => s.bottom + level * s.rowGap;

        // 자리 번호: 홀수 = 칸 가운데 (1 → 0번 칸), 짝수 = 틈 (0 = 왼쪽 바깥, 2·columns = 오른쪽 바깥)
        static float SlotX(int slot, Settings s)
        {
            float left = s.centerX - (s.columns - 1) * 0.5f * s.laneGap;
            if (slot == 0) return left - s.outerGap;
            if (slot == s.columns * 2) return left + (s.columns - 1) * s.laneGap + s.outerGap;
            return left + (slot - 1) * 0.5f * s.laneGap;
        }

        // 순서를 지키며 아래 점과 가장 가까운 자리 (동적 계획법). 노드는 칸 가운데에 하나씩,
        // 지나가는 점은 노드가 없는 자리라면 여럿이 함께 쓸 수 있다 (같은 자리는 트랙 간격만큼 비켜 놓는다)
        static void PlaceLayer(List<Item> layer, Settings s)
        {
            int n = layer.Count, slots = s.columns * 2 + 1;
            if (n == 0) return;
            var cost = new float[n, slots];
            var from = new int[n, slots];
            for (int i = 0; i < n; i++)
            {
                var item = layer[i];
                float want = item.down != null ? item.down.x : s.centerX;
                for (int slot = 0; slot < slots; slot++)
                {
                    cost[i, slot] = float.MaxValue;
                    if (item.real && slot % 2 == 0) continue;
                    float c = Mathf.Abs(SlotX(slot, s) - want) / s.laneGap;
                    if (!item.real && (slot == 0 || slot == slots - 1)) c += 0.3f;   // 바깥 틈은 화살표 밑이라 덜 쓴다
                    if (i == 0)
                    {
                        cost[i, slot] = c;
                        continue;
                    }
                    for (int prev = 0; prev <= slot; prev++)
                    {
                        if (cost[i - 1, prev] == float.MaxValue) continue;
                        if (prev == slot && (item.real || layer[i - 1].real)) continue;
                        if (cost[i - 1, prev] + c < cost[i, slot])
                        {
                            cost[i, slot] = cost[i - 1, prev] + c;
                            from[i, slot] = prev;
                        }
                    }
                }
            }
            int best = 0;
            for (int slot = 1; slot < slots; slot++) if (cost[n - 1, slot] < cost[n - 1, best]) best = slot;
            var chosen = new int[n];
            for (int i = n - 1; i >= 0; i--)
            {
                chosen[i] = best;
                best = from[i, best];
            }
            // 같은 자리를 쓰는 지나가는 점들은 트랙 간격만큼 비켜 놓되, 아래 층에서도 같은 자리였으면 그 x를 이어 써서 선이 곧게 간다
            for (int i = 0; i < n;)
            {
                int j = i;
                while (j + 1 < n && chosen[j + 1] == chosen[i]) j++;
                float center = SlotX(chosen[i], s);
                if (layer[i].real)
                {
                    layer[i].x = center;
                    i++;
                    continue;
                }
                // 간격을 지키면서 원하는 x에 가장 가깝게 (아래 층에서 이 자리를 지나던 선은 무겁게, 새로 들어온 선은 가볍게).
                // y = x - k·간격 이 줄지 않게 하는 가중 단조 회귀 (이웃끼리 어긋나면 합쳐서 평균).
                // 바깥 틈에 새로 들어오는 선은 바깥쪽 끝을 원한다 (나중에 안쪽에서 들어오는 선이 비킬 자리를 남긴다)
                bool outer = chosen[i] == 0 || chosen[i] == slots - 1;
                float reach = outer ? s.outerReach : s.innerReach;
                float fresh = chosen[i] == 0 ? center - reach : chosen[i] == slots - 1 ? center + reach : center;
                var blocks = new List<(float sum, float weight, int count)>();
                for (int k = i; k <= j; k++)
                {
                    var down = layer[k].down;
                    bool stay = down != null && Mathf.Abs(down.x - center) <= reach + 0.5f;
                    float want = stay ? down.x : outer ? fresh : Mathf.Clamp(down != null ? down.x : center, center - reach, center + reach);
                    float w = stay ? 100f : 1f;
                    blocks.Add((w * (want - (k - i) * s.trackGap), w, 1));
                    while (blocks.Count > 1)
                    {
                        var b = blocks[blocks.Count - 1]; var a = blocks[blocks.Count - 2];
                        if (a.sum / a.weight <= b.sum / b.weight) break;
                        blocks.RemoveAt(blocks.Count - 1);
                        blocks[blocks.Count - 1] = (a.sum + b.sum, a.weight + b.weight, a.count + b.count);
                    }
                }
                int at = i;
                foreach (var block in blocks)
                    for (int c = 0; c < block.count; c++, at++) layer[at].x = block.sum / block.weight + (at - i) * s.trackGap;
                // 틈 밖(노드 쪽)으로 나가지 않게
                for (int k = i; k <= j; k++) layer[k].x = Mathf.Round(Mathf.Max(layer[k].x, center - reach + (k - i) * s.trackGap));
                for (int k = j; k >= i; k--) layer[k].x = Mathf.Round(Mathf.Min(layer[k].x, center + reach - (j - k) * s.trackGap));
                i = j + 1;
            }
        }

        // 아래 층 → 위 층 선들의 가로 구간 높이. 같은 출발 점에서 같은 쪽으로 가는 선은 한 구간(갈래)으로 묶는다
        static void AssignTracks(List<Item> upper, float rowY, Settings s, Dictionary<Item, float> bend)
        {
            var groups = new Dictionary<(Item, int), List<Item>>();
            foreach (var item in upper)
            {
                if (item.down == null || Mathf.Abs(item.x - item.down.x) < 0.5f) continue;
                var key = (item.down, item.x > item.down.x ? 1 : -1);
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<Item>();
                list.Add(item);
            }
            var spans = new List<(float src, float lo, float hi, int dir, List<Item> items)>();
            foreach (var pair in groups)
            {
                float src = pair.Key.Item1.x, lo = src, hi = src;
                foreach (var item in pair.Value) { lo = Mathf.Min(lo, item.x); hi = Mathf.Max(hi, item.x); }
                spans.Add((src, lo, hi, pair.Key.Item2, pair.Value));
            }
            // 오른쪽으로 가는 구간은 오른쪽 출발부터, 왼쪽으로 가는 구간은 왼쪽 출발부터 아래 트랙을 잡는다
            spans.Sort((a, b) => a.dir != b.dir ? a.dir.CompareTo(b.dir) : (a.dir > 0 ? b.src.CompareTo(a.src) : a.src.CompareTo(b.src)));
            var track = new int[spans.Count];
            int top = 0;
            for (int i = 0; i < spans.Count; i++)
            {
                for (int j = 0; j < i; j++)
                    if (spans[j].dir == spans[i].dir && spans[j].lo <= spans[i].hi + 0.5f && spans[i].lo <= spans[j].hi + 0.5f)
                        track[i] = Mathf.Max(track[i], track[j] + 1);
                top = Mathf.Max(top, track[i]);
            }
            float mid = rowY + (s.bandLow + s.bandHigh) * 0.5f;
            for (int i = 0; i < spans.Count; i++)
            {
                float y = mid + (track[i] - top * 0.5f) * s.trackGap;
                foreach (var item in spans[i].items) bend[item] = y;
            }
        }
    }
}
