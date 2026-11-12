# 라운드 8: 장식 고르기 정책 (r7/wt_v6.py를 지금 코드에 맞춤 — 보따리·호감도 몫을 뺐다). theory/는 보지 않음.
# 코드 근거 (WorldTree.Craft.cs):
#   CanPlace :170-171 = 가졌고 · 안 놓였고 · 그 자리 놓인 수 < ZoneSlots(지금 단계 행)
#   Place :173-184 = 첫 배치(decosFirstPlaced에 없을 때)만 균사석 decoFirstMycelite — 장식마다 평생 한 번, 호감도 없음(11-12)
#   Remove :186-192 = 비용 없음 (교체 무료) · 세트 = 그 꼬마 장식이 모두 놓임 (DecoSetComplete :197-207) → 꼬마 특기 단계 +1 (Kids.cs:78)
#   TrimPlacedToSlots (WorldTree.cs:135-149) = 불러올 때 칸을 넘는 배치를 뒤에서부터 뺌 — 칸이 단계마다 줄지 않아 진행 중에는 일이 없다
# 정책:
#   grade    = wt_port 기본 (높은 등급부터 빈 칸에, 교체 안 함)
#   best     = 접속마다(받기·진화 뒤) 가진 장식 전부로 다시 고름. 점수 = Σ 장식 가중치×값 + Σ 완성 세트(꼬마 특기 1단계) 가중치.
#              세트 조합(최대 4) 전수 + 남는 칸은 점수 순
#   firstall = best + 가진 장식을 모두 한 번씩 놓았다 빼서 첫 배치 보상을 받음
# 목표(obj): progress = 버섯 획득(Harvest) · gold = 처치 골드 · myc = 보스 균사석 · craft = 제작 속도 · combat = 공격·체력·치명 피해
import sys, os, itertools
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.dirname(HERE))
import wt_port as W

EPS = {'Harvest': 3e-3, 'KillGold': 2e-3, 'BossMycelite': 2e-3, 'CraftSpeed': 1.5e-3, 'Attack': 5e-4, 'Hp': 5e-4, 'CritDamage': 5e-4,
       'CritChance': 5e-4, 'Speed': 5e-4}
OBJ = {
    'progress': {'Harvest': 1.0},
    'gold': {'KillGold': 1.0},
    'myc': {'BossMycelite': 1.0},
    'craft': {'CraftSpeed': 1.0},
    'combat': {'Attack': 1.0, 'Hp': 1.0, 'CritDamage': 1.0},
}


class WT8(W.WT):
    def __init__(self, T, feed, policy='best', obj='progress', **kw):
        super().__init__(T, feed, **kw)
        self.policy, self.obj = policy, obj
        w = dict(EPS); w.update(OBJ[obj]); self.w = w
        self.by_owner = {}
        for d in T['decos']: self.by_owner.setdefault(d['owner'], []).append(d['key'])
        self.m7 = dict(fill={}, store_min={}, sets_at8=None, first_myc=0, eff8=None, placed8=None)

    def dval(self, key):
        d = self.decos[key]; return self.w.get(d['effect'], 0) * d['value']

    def setval(self, kid):
        r = self.kids[kid]['row']
        return self.w.get(r['bonusEffect'], 0) * r['bonusValue'] + 1e-4

    def first_reward(self, key):
        if key in self.first: return
        super().first_reward(key); self.m7['first_myc'] += self.C['decoFirstMycelite']

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

    def after_session(self):
        slots = sum(self.row()[c] for c in W.ZONES.values())
        st = self.stage
        if len(self.placed) >= slots and st not in self.m7['fill']:
            self.m7['fill'][st] = self.cur_day
        store = len(self.owned) - len(self.placed)
        self.m7['store_min'][st] = min(self.m7['store_min'].get(st, 99), store)
        if st == 8 and self.m7['sets_at8'] is None:
            self.m7['sets_at8'] = [k for k in self.by_owner if self.set_complete(k)]
            self.m7['eff8'] = {k: round(v, 3) for k, v in self.bonus().items() if v}
            self.m7['placed8'] = sorted(self.placed)
