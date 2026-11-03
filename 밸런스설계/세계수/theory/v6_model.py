# v6 (기획서 「세계수 단계별 장식 개편 Ver02」 칸 수): 구역당 최대 3칸, 8단계 12칸 < 장식 24개 → 고르고 바꿔 끼우는 꾸미기.
# wt_model.Tree를 상속해 놓기만 바꾼다:
#   policy 'pace'  : 접속마다 가진 장식으로 구역별 칸을 채우는 가장 좋은 조합을 다시 고른다(교체 = Remove → Place, 공짜).
#                    점수 = Σ 장식 가중치 + 세트(그 꼬마 장식 3개 모두 놓음) 가치. 가중치는 진행(버섯 획득) 우선.
#   policy 'grade' : 지금 모델 그대로(높은 등급부터 빈 칸, 교체 안 함) — 비교용
#   policy 'firstall': 'pace' + 아직 첫 배치 보상을 안 받은 장식을 잠깐 놓았다 빼서 보상을 다 받는다(같은 접속)
#   set_rule n: 세트 = 그 꼬마 장식 중 n개 이상 놓임 (지금 코드 = 전부 3). 대안 K4 확인용
# 첫 배치 보상(decoFirstMycelite·decoFirstAffinity)은 장식마다 처음 놓을 때 한 번 (WorldTree.Craft.Place).
import itertools, json, os, sys
sys.dont_write_bytecode = True
import wt_model as M, wt_tables as WT

W = dict(Harvest=10, BagMycelite=3, BagGold=2, CraftSpeed=1.5, BagHours=0.4, Hp=1, CritDamage=1, Attack=1)


class V6Tree(M.Tree):
    def __init__(self, T, feed, policy='pace', set_rule=None, weights=None, **kw):
        super().__init__(T, feed, **kw)
        self.policy, self.set_rule, self.w = policy, set_rule, dict(W, **(weights or {}))
        self._cache = {}
        self.log['sets'] = []

    def set_complete(self, kid):
        mine = [d['key'] for d in self.T['decos'] if d['owner'] == kid]
        n = sum(1 for m in mine if m in self.placed)
        need = len(mine) if not self.set_rule else min(self.set_rule, len(mine))
        return bool(mine) and n >= need

    def _util(self, keys):
        u = sum(self.w.get(self.deco(k)['effect'], 0) * self.deco(k)['value'] for k in keys)
        ks = set(keys)
        for kid, kk in self.kids.items():
            mine = [d['key'] for d in self.T['decos'] if d['owner'] == kid]
            need = len(mine) if not self.set_rule else min(self.set_rule, len(mine))
            if kk['owned'] and sum(1 for m in mine if m in ks) >= need:
                # 세트: 특기 ★ +1 (bonusValue 한 번 더) + 보따리 몫 × 1.2 (≈ 보따리 배율 +0.02~0.05)
                u += self.w.get(kk['row']['bonusEffect'], 0) * kk['row']['bonusValue'] + self.w['BagGold'] * 0.03
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

    def _first(self, k):
        if k not in self.first:
            self.first.add(k)
            self.myc_b += self.C['decoFirstMycelite']; self.acc['m_first'] += self.C['decoFirstMycelite']
            d = self.deco(k)
            if self.kids[d['owner']]['owned']:
                self.kids[d['owner']]['aff'] += self.C['decoFirstAffinity']

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


def run(feed, policy='pace', quests=None, set_rule=None, T=None, weights=None, **kw):
    T = T or WT.load_current()
    return V6Tree(T, json.load(open(os.path.join(M.HERE, feed), encoding='utf-8'))['feed'], policy=policy,
                  set_rule=set_rule, weights=weights, quests=quests, **kw).run()
