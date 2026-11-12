# [11-12] 기록용: 보따리·호감도·의뢰 시트·꼬마 grade가 지워진 지금 표·wt_port에서는 돌지 않는다. 지금 표 재측정은 r8/run_r8.py.
# 라운드 7: 기획서 Ver02 칸(합 1/2/3/4/6/8/10/12) — 장식 24개 > 칸 12개라 "고르고 바꿔 끼우기" 정책을 넣은 포팅 (wt-verify)
# 코드 근거 (WorldTree.Craft.cs):
#   CanPlace :170-171 = 가졌고 · 안 놓였고 · 그 자리 놓인 수 < ZoneSlots(지금 단계 행)
#   Place :173-186 = 첫 배치(decosFirstPlaced에 없을 때)만 균사석 decoFirstMycelite + 주인 꼬마 호감도 decoFirstAffinity — 장식마다 평생 한 번
#   Remove :188-195 = 비용 없음 (교체 무료) · 세트 = 그 꼬마 장식이 모두 놓임 (DecoSetComplete :200-211)
#   TrimPlacedToSlots (WorldTree.cs:95-109) = 불러올 때 칸을 넘는 배치를 뒤에서부터 뺌 — 칸이 단계마다 줄지 않으므로 진행 중에는 일이 없다
# 정책:
#   grade    = wt_port 기본 (높은 등급부터 빈 칸에, 교체 안 함)
#   best     = 접속마다(받기·진화 뒤) 가진 장식 전부로 다시 고름. 점수 = Σ 장식 가중치×값 + Σ 완성 세트(꼬마 특기 1단계 + 보따리 몫 ×1.2) 가중치
#              세트 조합(최대 4) 전수 + 남는 칸은 점수 순 — 검증이 따로 짠 탐색 (theory/v6_model.py는 보지 않음)
#   firstall = best + 가진 장식을 모두 한 번씩 놓았다 빼서 첫 배치 보상을 받음
import sys, os, itertools
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.join(os.path.dirname(HERE), 'r6')); sys.path.insert(0, os.path.dirname(HERE))
import wt_port as W
from wt_quest import WTQ

EPS = {'Harvest': 3e-3, 'BagMycelite': 2e-3, 'CraftSpeed': 1.5e-3, 'BagGold': 1e-3, 'BagHours': 2e-4, 'Attack': 5e-4, 'Hp': 5e-4, 'CritDamage': 5e-4}
OBJ = {
    'progress': {'Harvest': 1.0},
    'bag': {'BagGold': 1.0, 'BagMycelite': 1.0, 'BagHours': 0.04, '_set_bag': 0.5},
    'craft': {'CraftSpeed': 1.0},
    'combat': {'Attack': 1.0, 'Hp': 1.0, 'CritDamage': 1.0},
}


class WT6(WTQ):
    def __init__(self, T, feed, policy='best', obj='progress', **kw):
        super().__init__(T, feed, **kw)
        self.policy, self.obj = policy, obj
        w = dict(EPS); w.update(OBJ[obj]); self.w = w
        self.by_owner = {}
        for d in T['decos']: self.by_owner.setdefault(d['owner'], []).append(d['key'])
        self.m7 = dict(fill={}, store_min={}, sets_at8=None, first_myc=0, eff8=None)
        self.cur_day = 0.0

    def dval(self, key):
        d = self.decos[key]; return self.w.get(d['effect'], 0) * d['value']

    def setval(self, kid):
        r = self.kids[kid]['row']
        return self.w.get(r['bonusEffect'], 0) * r['bonusValue'] + self.w.get('_set_bag', 0) * self.C['kidBagShare'] * self.C['decoSetBagMul'] + 1e-4

    def first_reward(self, key):
        if key in self.first: return
        self.first.add(key); self.myc += self.C['decoFirstMycelite']; self.acc('mi_first', self.C['decoFirstMycelite'])
        self.m7['first_myc'] += self.C['decoFirstMycelite']
        o = self.decos[key]['owner']
        if self.kids[o]['level'] > 0: self.kids[o]['aff'] += self.C['decoFirstAffinity']

    def best_layout(self):
        cap = {z: self.row()[c] for z, c in W.ZONES.items()}
        owned = sorted(self.owned, key=lambda k: self.decos[k]['index'])
        cand = [k for k, ks in self.by_owner.items() if self.kids[k]['level'] > 0 and all(x in self.owned for x in ks)]
        best = (-1, None)
        for n in range(0, min(4, len(cand)) + 1):
            for S in itertools.combinations(cand, n):
                use = {z: 0 for z in cap}; chosen = []
                ok = True
                for kid in S:
                    for key in self.by_owner[kid]:
                        z = self.decos[key]['zone']; use[z] += 1; chosen.append(key)
                        if use[z] > cap[z]: ok = False
                if not ok: continue
                score = sum(self.setval(k) for k in S) + sum(self.dval(k) for k in chosen)
                rest = sorted([k for k in owned if k not in chosen], key=lambda k: -self.dval(k))
                for key in rest:
                    z = self.decos[key]['zone']
                    if use[z] < cap[z]:
                        use[z] += 1; chosen.append(key); score += self.dval(key)
                if score > best[0] + 1e-12: best = (score, chosen)
        return best[1] or []

    def place_all(self):
        if self.policy == 'grade':
            return super().place_all()
        if self.policy == 'firstall':
            for k in sorted(self.owned): self.first_reward(k)
        self.placed = self.best_layout()
        for k in self.placed: self.first_reward(k)

    def rush_all(self):
        super().rush_all()
        slots = sum(self.row()[c] for c in W.ZONES.values())
        st = self.stage
        if len(self.placed) >= slots and st not in self.m7['fill']:
            self.m7['fill'][st] = self.cur_day
        store = len(self.owned) - len(self.placed)
        self.m7['store_min'][st] = min(self.m7['store_min'].get(st, 99), store)
        if st == 8 and self.m7['sets_at8'] is None:
            self.m7['sets_at8'] = [k for k in self.by_owner if self.set_complete(k)]
            self.m7['eff8'] = {k: round(v, 3) for k, v in self.bonus().items() if v}
