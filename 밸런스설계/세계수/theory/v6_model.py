# 꾸미기 = 고르고 바꿔 끼우기 (기획서 「세계수 단계별 장식 개편 Ver02」 칸 수: 구역당 최대 3칸, 8단계 12칸 < 장식 24개).
# wt_model.Tree를 상속해 놓기만 바꾼다 (2026-11-12 현행화: 보따리 효과·첫 배치 호감도 삭제를 반영):
#   policy 'pace'    : 접속마다 가진 장식으로 구역별 칸을 채우는 가장 좋은 조합을 다시 고른다(교체 = Remove → Place, 공짜, Craft.cs:173-191).
#                      점수 = Σ 장식 가중치 × value + 세트(그 꼬마 장식 3개 모두 놓음 → 특기 단계 +1, Kids.cs:78) 가치. 가중치는 진행(버섯 획득) 우선.
#   policy 'grade'   : 높은 등급부터 빈 칸, 교체 안 함 (wt_model.Tree.place_all)
#   policy 'firstall': 'pace' + 아직 첫 배치 보상을 안 받은 장식을 잠깐 놓았다 빼서 보상을 다 받는다(같은 접속)
#   weights          : 목표별 가중치 (W7′(c) 목표 = 진행 / 골드 / 균사석 / 제작 / 전투)
import itertools, os, sys
sys.dont_write_bytecode = True
import wt_model as M, wt_tables as WT

W = dict(Harvest=10, BossMycelite=3, KillGold=2, CraftSpeed=1.5, Hp=1, CritDamage=1, Attack=1)
GOALS = {
    '진행': W,
    '골드': dict(W, KillGold=20, Harvest=2),
    '균사석': dict(W, BossMycelite=20, Harvest=2),
    '제작': dict(W, CraftSpeed=20, Harvest=2),
    '전투': dict(W, Attack=40, Hp=40, CritDamage=20, Harvest=2),
}


class V6Tree(M.Tree):
    def __init__(self, T, feed, meta=None, policy='pace', weights=None, **kw):
        super().__init__(T, feed, meta, **kw)
        self.policy, self.w = policy, dict(weights or W)
        self._cache = {}
        self._mine = {k: [d['key'] for d in T['decos'] if d['owner'] == k] for k in self.kids}

    def _util(self, keys):
        u = sum(self.w.get(self.deco(k)['effect'], 0) * self.deco(k)['value'] for k in keys)
        ks = set(keys)
        for kid, kk in self.kids.items():
            mine = self._mine[kid]
            if kk['owned'] and mine and all(m in ks for m in mine):
                u += self.w.get(kk['row']['bonusEffect'], 0) * kk['row']['bonusValue']     # 세트 = 특기 bonusValue 한 번 더
        return u

    def _best(self):
        slots = dict(zip(WT.ZONES, self.row()['slots']))
        key = (frozenset(self.decos_owned), tuple(slots.values()), frozenset(k for k, v in self.kids.items() if v['owned']))
        if key in self._cache:
            return self._cache[key]
        per = []
        for z in WT.ZONES:
            own = sorted(d['key'] for d in self.T['decos'] if d['zone'] == z and d['key'] in self.decos_owned)
            n = min(slots[z], len(own))
            per.append(list(itertools.combinations(own, n)))
        best, bu = (), -1
        for combo in itertools.product(*per):
            keys = [k for c in combo for k in c]
            u = self._util(keys)
            if u > bu + 1e-12:
                best, bu = keys, u
        self._cache[key] = best
        return best

    def place_all(self):
        if self.policy == 'grade':
            return super().place_all()
        if self.policy == 'firstall':
            for k in sorted(self.decos_owned):
                self._first(k)
        best = self._best()
        self.placed = list(best)
        for k in best:
            self._first(k)


def run(feed, policy='pace', T=None, weights=None, ads=None, **kw):
    fd, meta = M.load_feed(feed)
    T = T or WT.load_current()
    return V6Tree(T, fd, meta, policy=policy, weights=weights, ads=meta.get('ads', 0.5) if ads is None else ads, **kw).run()
