# 세계수 진행 독립 포팅 (wt-verify). theory/는 보지 않고 지금 게임 코드에서 직접 옮겼다. 기대값 모델(난수 없음).
# 2026-11-12 현행화 (보따리 삭제 11-03 · 꼬마 grade 삭제 11-04 · 적 5등급×3종류 11-05 · 의뢰→메인 퀘스트 11-10 · 호감도·선물 삭제 11-12).
# 공식 출처 (Assets/Scripts/Mobile/...):
#   KillGold(k) = ceil(EnemyGold(지금 방치 스테이지) × k)                               WorldTree/WorldTree.cs:212
#   나이테 비용 = KillGold(ringKills × treeRingGrowth^(ring-1)), ring < treeRingMax        WorldTree.Growth.cs:20-38
#   진화 = 나이테 최대 · exp ≥ evoExp · 골드 KillGold(evoGoldKills) · 균사석 evoMycelite, exp 이월 · ring=1   Growth.cs:40-67
#   가호 = 지금 단계 이하 모두                                                           Growth.cs:70, Bonus.cs:260-261
#   창고 = 일반 적(보스 아님) 처치마다 treeHarvestPerKill × (1+Harvest), 소수는 확률 → 기대값   Growth.cs:135-162, IdleBattle.cs:494
#     적 종류: IdleBattle.PickEnemyPool (IdleBattle.cs:784-793) 독성 enemyToxicRate · 약용 enemyMedicinalRate · 나머지 식용,
#     묶음 안에서 그림 조각 균등(:737-738). 보스 조각(보스 시트 resourceId+cell)은 뺀다(:135). 조각 이름 "시트_번호" → 시트 이름으로 적 행(Growth.cs:147-153)
#     → 독성·약용도 창고에 들어간다 (상수 9079 설명 "들어가지 않는다"는 코드와 다름 — 코드를 따른다)
#   공물 = Σ count × value × (지금 단계 preferIds면 tributePreferMul), 최대 단계면 불가            Growth.cs:165-198
#   퀘스트 세계수 경험치 = 퀘스트 시트 treeExp, 받을 때 D.exp += (최대 단계에서도)                 Quest/DailyQuests.cs:153-167, Growth.cs:201-206
#     일일 = daily 행 전부(무작위 뽑기 없음), KST 00시 초기화 · 주간 = weekly 행 전부, 월요일 KST 초기화   DailyQuests.cs:66-97
#     진행: kill = 모든 처치(보스 포함, IdleBattle.cs:491) · stage = 보스 등장(:443, 막혀도 셈) · dungeon = 던전 판(IdleBattle.Dungeon.cs:225, HarvestGame.cs:176)
#           gacha = 뽑은 개수(GachaPanel.cs:161) · upgrade = 강화 횟수(IdleProgress.cs:321) · ad = 광고 시청(AdService.cs:184)
#           daily = 일일 퀘스트를 받은 수(DailyQuests.cs:164, 주간만)
#   꼬마 = Formation 꼬마. 가짐 = 레벨 > 0 (Kids.cs:14-20, Formation.cs:169), 시작 레벨 = 꼬마 시트 initialOwned (Formation.cs:107)
#     꼬마 뽑기 1회 = 8종 균등 (GachaProgress.cs:79-91, Formation.cs:268), 처음 = Lv1, 그다음 조각 (Formation.cs:224-235)
#     Lv→Lv+1 조각 ggomaCopiesBase + ggomaCopiesPerLevel×Lv (Formation.cs:181), 레벨 상한 = 성장 0회 ggomaEvoLevel2 · 1회 ggomaEvoLevel3 · 2회 ggomaMaxLevel
#     (Formation.cs:336-341), 성장 균사석 ggomaGrowMycelite2·3 (Formation.cs:350-364)
#   ★진화: 레벨 ≥ 꼬마진화 needLevel(★+1) (등급 배수 없음), 세계수 단계 ≥ min(최대, evoStage+★+1), 균사석 mycelite(★+1)   Kids.cs:47-75
#   부탁: 하루 requestPerDay건, 가진 꼬마에서 겹치지 않게 무작위, 좋아하는 버섯 무작위, requestCount개 → 골드 KillGold(requestGoldKills)
#     호감도 없음. 날이 바뀌면 다시 뽑음 (Kids.cs:96-153)
#   주간 상자: 이번 주 부탁 requestWeekGoal건 → requestWeekMycelite (Kids.cs:155-163)
#   공방: 슬롯 craftSlots(지금 단계), 주인 꼬마를 가져야 열림, 비용 KillGold(goldKills)·mycelite, 시간 hours/(1+CraftSpeed)   Craft.cs:36-104
#   가속: 남은 craftRushSeconds초마다 균사석 1 (최소 1), 하루 craftRushPerDay번                         Craft.cs:106-123
#   꾸미기: 자리별 칸 = 지금 단계 행, 첫 배치만 decoFirstMycelite (호감도 없음), 교체 무료, 세트 = 주인 장식 모두 놓음   Craft.cs:125-207
#   효과 합 = 가호 + bonusValue × (★ + 세트) + 놓은 장식 (보따리 없음, 효과 9종)                         Bonus.cs:249-279, WorldTree.cs:8
#   전투 반영: 공격·체력·재생·공속 ×(1+효과), 치명 확률·피해 +효과 (IdleProgress.cs:347-352), 처치 골드 ×(1+KillGold) (IdleBattle.cs:749),
#     보스 균사석 ×(1+BossMycelite) (IdleBattle.cs:500)
# 방치 입력(feed): 접속별 처치·보스 등장·던전 판·스테이지·적 처치 골드, 하루 골드·균사석·뽑기·꼬마 뽑기·광고 수.
#   r8/feed_now.py(지금 표) 또는 r6/feed_v3_*.json(11-03 KHD 모델, 옛 표).
# python wt_port.py <feed.json> [key=value ...]
import sys, json, math, os, re
sys.stdout.reconfigure(encoding='utf-8')
import tables

ROOT = tables.ROOT
EFFECTS = ['Attack', 'Hp', 'Speed', 'CritChance', 'CritDamage', 'KillGold', 'BossMycelite', 'Harvest', 'CraftSpeed']   # WorldTree.cs:8
ZONES = {'Root': 'rootSlots', 'Trunk': 'trunkSlots', 'Branch': 'branchSlots', 'Crown': 'crownSlots'}


def sprite_cells(T, rid):
    """리소스 rid 시트의 조각 번호들 (Assets/Resources/<path>.png.meta sprites)"""
    path = T['resources'][rid]['path']
    meta = os.path.join(ROOT, 'Assets/Resources', path + '.png.meta')
    base = path.rsplit('/', 1)[-1]
    names = re.findall(r'^      name: (' + re.escape(base) + r'_\d+)\s*$', open(meta, encoding='utf-8').read(), re.M)
    return sorted({int(n.rsplit('_', 1)[1]) for n in names})


def species_weights(T):
    """일반 적 1마리가 각 적 행일 확률 (IdleBattle.cs:135-138, :784-793). 보스 조각 제외"""
    C = T['const']
    boss = {(b['resourceId'], b['cell']) for b in T['bosses']}
    pools = {'Edible': [], 'Toxic': [], 'Medicinal': []}
    for e in T['enemies']:
        path = T['resources'][e['resourceId']]['path'].rsplit('/', 1)[-1]
        kind = 'Toxic' if path.startswith('Mush_Toxic') else 'Medicinal' if path.startswith('Mush_Medicinal') else 'Edible'   # GgomaSkills.cs:113-119
        n = sum(1 for c in sprite_cells(T, e['resourceId']) if (e['resourceId'], c) not in boss)
        pools[kind].append((e['index'], n))
    rate = {'Toxic': C['enemyToxicRate'], 'Medicinal': C['enemyMedicinalRate']}
    rate['Edible'] = 1 - rate['Toxic'] - rate['Medicinal']
    w = {}
    for kind, lst in pools.items():
        tot = sum(n for _, n in lst)
        for i, n in lst: w[i] = rate[kind] * n / tot
    return w, {k: [(i, n) for i, n in v] for k, v in pools.items()}


class WT:
    def __init__(self, T, feed, gold_share=0.10, myc_share=0.10, rush=False, requests=True, quests=True, grow=True,
                 kid_share=None, skip=(), ad_quest=None):
        self.T, self.feed = T, feed
        self.C = T['const']
        self.gold_share, self.myc_share, self.rush = gold_share, myc_share, rush
        self.do_requests, self.q_on, self.grow_on = requests, quests, grow
        self.kid_share = kid_share            # 옛 feed(kid_pulls 없음)일 때 하루 뽑기 중 꼬마 몫
        self.skip = set(skip)                 # 하지 않는 퀘스트 key
        self.ad_quest = ad_quest              # None = feed 광고 수로 판단
        self.stage, self.ring, self.exp = 1, 1, 0.0
        self.enemies = {e['index']: e for e in T['enemies']}
        self.sw, self.pools = species_weights(T)
        self.store = {i: 0.0 for i in self.enemies}
        self.kids = {}
        for k in T['kids']:
            lv = int(k['initialOwned'])
            self.kids[k['key']] = dict(row=k, level=lv, copies=0.0, growth=0, star=0, frac=0.0)
        self.decos = {d['key']: d for d in T['decos']}
        self.owned, self.placed, self.first = set(), [], set()
        self.crafts = []                       # (key, end_hour)
        self.gold = 0.0                        # 세계수에 쓰는 지갑 몫 (절대 골드)
        self.myc = 0.0
        self.t = 0.0                           # 시각 (시간)
        self.week_done, self.week_claimed = 0, False
        self.qd = [r for r in T['quests'] if r['period'] == 'daily']
        self.qw = [r for r in T['quests'] if r['period'] == 'weekly']
        self.log = dict(reach={1: 0.0}, star={}, decos_at={}, eff_at={}, day=[], all24=None, placed24=None,
                        req_done=0, req_total=0, week_boxes=0, grow={}, own_day={}, q_done={}, q_shown={})
        self.day_acc = None
        self.cur_day = 0.0

    # ── 표 ──
    def row(self, s=None): return self.T['tree'][min(s or self.stage, len(self.T['tree'])) - 1]
    @property
    def max_stage(self): return len(self.T['tree'])

    def bonus(self):
        b = dict.fromkeys(EFFECTS, 0.0)
        for r in self.T['bless']:
            if r['stage'] <= self.stage: b[r['effect']] += r['value']
        for k, s in self.kids.items():
            if s['level'] > 0: b[s['row']['bonusEffect']] += s['row']['bonusValue'] * self.kid_step(k)
        for key in self.placed:
            d = self.decos[key]; b[d['effect']] += d['value']
        return b

    def set_complete(self, kid):
        mine = [d for d in self.T['decos'] if d['owner'] == kid]
        return bool(mine) and all(d['key'] in self.placed for d in mine)

    def kid_step(self, kid): return self.kids[kid]['star'] + (1 if self.set_complete(kid) else 0)

    def kg(self, eg, kills): return math.ceil(eg * kills)

    # ── 기록 ──
    def acc(self, k, v):
        self.day_acc[k] = self.day_acc.get(k, 0.0) + v

    def spend_gold(self, cat, amt):
        self.gold -= amt; self.acc('g_' + cat, amt)

    def spend_myc(self, cat, amt):
        self.myc -= amt; self.acc('m_' + cat, amt)

    # ── 꼬마 (Formation) ──
    def level_cap(self, s):
        g = s['growth']
        if g >= 2: return int(self.C['ggomaMaxLevel'])
        return min(int(self.C['ggomaMaxLevel']), int(self.C['ggomaEvoLevel3'] if g == 1 else self.C['ggomaEvoLevel2']))

    def kid_pulls(self, n, day_f):
        per = n / len(self.kids)
        for k in self.kids: self.add_copies(k, per, day_f)

    def add_copies(self, k, x, day_f):
        s = self.kids[k]
        if s['level'] == 0:
            s['frac'] += x
            if s['frac'] < 1: return
            x = s['frac'] - 1; s['level'] = 1
            self.log['own_day'][k] = day_f
        s['copies'] += x
        self.merge(k, day_f)

    def merge(self, k, day_f):
        s = self.kids[k]
        while True:
            cap = self.level_cap(s)
            need = self.C['ggomaCopiesBase'] + self.C['ggomaCopiesPerLevel'] * s['level']
            if s['level'] < cap and s['copies'] >= need:
                s['copies'] -= need; s['level'] += 1; continue
            # 관문에서 막힘 → 성장 (방치 쪽 균사석, 세계수 몫에서 빼지 않고 기록만)
            if self.grow_on and s['growth'] < 2 and s['level'] >= cap and s['copies'] >= need:
                cost = self.C['ggomaGrowMycelite2'] if s['growth'] == 0 else self.C['ggomaGrowMycelite3']
                s['growth'] += 1; self.acc('grow_myc', cost)
                self.log['grow'].setdefault(k, []).append(day_f); continue
            return

    def need_level(self, k):
        s = self.kids[k]
        return max(1, int(self.T['stars'][s['star'] + 1]['needLevel']))        # Kids.cs:55

    # ── 퀘스트 (메인 일일·주간) ──
    def q_new_day(self, di):
        self.dq = [dict(r=r, prog=0.0, done=False) for r in self.qd]
        for q in self.dq: self.log['q_shown'][q['r']['key']] = self.log['q_shown'].get(q['r']['key'], 0) + 1
        if di % 7 == 0:
            self.wq = [dict(r=r, prog=0.0, done=False, start=di) for r in self.qw]
            for q in self.wq: self.log['q_shown'][q['r']['key']] = self.log['q_shown'].get(q['r']['key'], 0) + 1

    def q_add(self, kind, n):
        for q in self.dq + self.wq:
            if q['r']['kind'] == kind and not q['done']: q['prog'] = min(q['r']['target'], q['prog'] + n)

    def q_claim(self):
        got = 0
        for q in self.dq:
            if not q['done'] and q['r']['key'] not in self.skip and q['prog'] >= q['r']['target'] - 1e-9:
                q['done'] = True; got += q['r']['treeExp']
                self.log['q_done'][q['r']['key']] = self.log['q_done'].get(q['r']['key'], 0) + 1
                self.q_add('daily', 1)                                   # DailyQuests.cs:164
        for q in self.wq:
            if not q['done'] and q['r']['key'] not in self.skip and q['prog'] >= q['r']['target'] - 1e-9:
                q['done'] = True; got += q['r']['treeExp']
                self.log['q_done'][q['r']['key']] = self.log['q_done'].get(q['r']['key'], 0) + 1
        if got:
            self.exp += got; self.acc('exp_q', got)

    # ── 접속 ──
    def claim_crafts(self):
        done = [c for c in self.crafts if c[1] <= self.t + 1e-9]
        self.crafts = [c for c in self.crafts if c[1] > self.t + 1e-9]
        for key, _ in done: self.owned.add(key)
        self.place_all()

    def first_reward(self, key):
        if key in self.first: return
        self.first.add(key); self.myc += self.C['decoFirstMycelite']; self.acc('mi_first', self.C['decoFirstMycelite'])

    def place_all(self):
        """grade 정책: 빈 칸에 높은 등급부터, 교체 안 함 (Craft.cs:170-184)"""
        cand = sorted([k for k in self.owned if k not in self.placed], key=lambda k: (-self.decos[k]['grade'], self.decos[k]['index']))
        for key in cand:
            d = self.decos[key]
            n_in = sum(1 for p in self.placed if self.decos[p]['zone'] == d['zone'])
            if n_in < self.row()[ZONES[d['zone']]]:
                self.placed.append(key); self.first_reward(key)

    def battle(self, kills):
        h = self.C['treeHarvestPerKill'] * (1 + self.bonus()['Harvest'])
        for i, w in self.sw.items(): self.store[i] += kills * w * h
        self.acc('harvest', kills * h)

    def requests(self, day_reqs, eg):
        for r in day_reqs:
            if r['done']: continue
            if self.store[r['enemy']] >= r['count']:
                self.store[r['enemy']] -= r['count']; r['done'] = True
                gg = self.kg(eg, self.C['requestGoldKills']); self.gold += gg; self.acc('gi_req', gg)
                self.week_done += 1; self.log['req_done'] += 1
        if self.week_done >= self.C['requestWeekGoal'] and not self.week_claimed:
            self.week_claimed = True; self.myc += self.C['requestWeekMycelite']; self.acc('mi_week', self.C['requestWeekMycelite'])
            self.log['week_boxes'] += 1

    def tribute(self):
        if self.stage >= self.max_stage: return
        pref = self.row()['preferIds']
        e = sum(c * self.enemies[i]['value'] * (self.C['tributePreferMul'] if i in pref else 1) for i, c in self.store.items())
        self.exp += e; self.acc('exp', e); self.acc('trib_n', sum(self.store.values()))
        for i in self.store: self.store[i] = 0.0

    def grow(self, eg, day_f):
        while True:
            if self.ring < self.C['treeRingMax']:
                c = self.kg(eg, self.row()['ringKills'] * self.C['treeRingGrowth'] ** (self.ring - 1))
                if self.gold < c: return
                self.spend_gold('ring', c); self.ring += 1; continue
            if self.stage >= self.max_stage: return
            r = self.row()
            gc, mc = self.kg(eg, r['evoGoldKills']), r['evoMycelite']
            if self.exp < r['evoExp'] or self.gold < gc or self.myc < mc: return
            self.spend_gold('evo', gc); self.spend_myc('evo', mc)
            self.exp -= r['evoExp']; self.stage += 1; self.ring = 1
            self.log['reach'][self.stage] = day_f
            self.log['decos_at'][self.stage] = (len(self.owned) + len(self.crafts), sum(self.row()[z] for z in ZONES.values()))
            self.place_all()
            self.log['eff_at'][self.stage] = self.bonus()

    def kid_stars(self, day_f):
        maxstar = len(self.T['stars']) - 1
        while True:
            best = None
            for k, s in self.kids.items():
                if s['level'] <= 0 or s['star'] >= maxstar: continue
                if s['level'] < self.need_level(k): continue
                if self.stage < min(self.max_stage, s['row']['evoStage'] + s['star'] + 1): continue
                cost = self.T['stars'][s['star'] + 1]['mycelite']
                if self.myc < cost: continue
                if best is None or cost < best[1]: best = (k, cost)
            if not best: return
            k, cost = best
            self.spend_myc('star', cost); self.kids[k]['star'] += 1
            self.log['star'].setdefault(k, []).append(day_f)

    def craft(self, eg):
        slots = self.row()['craftSlots']
        busy = {c[0] for c in self.crafts}
        cand = sorted([d for d in self.T['decos'] if self.kids[d['owner']]['level'] > 0 and d['key'] not in self.owned and d['key'] not in busy],
                      key=lambda d: (d['grade'], d['index']))
        speed = 1 + self.bonus()['CraftSpeed']
        for d in cand:
            if len(self.crafts) >= slots: break
            g = self.T['dgrades'][d['grade']]
            gc, mc = self.kg(eg, g['goldKills']), g['mycelite']
            if self.gold < gc or self.myc < mc: break
            self.spend_gold('craft', gc); self.spend_myc('craft', mc)
            self.crafts.append((d['key'], self.t + g['hours'] / speed))

    def rush_all(self):
        if self.rush:
            n = int(self.C['craftRushPerDay'] - self.day_acc.get('rush_n', 0))
            for c in sorted(self.crafts, key=lambda c: c[1]):
                if n <= 0: break
                rem = c[1] - self.t
                if rem <= 0: continue
                cost = max(1, math.ceil(rem * 3600 / self.C['craftRushSeconds']))
                if self.myc < cost: break
                self.spend_myc('rush', cost); self.acc('rush_n', 1); n -= 1
                self.crafts = [(k, (self.t if k == c[0] else e)) for k, e in self.crafts]
            self.claim_crafts()
        self.after_session()

    def after_session(self):
        pass

    # ── 하루 ──
    def day_ads(self, D):
        if self.ad_quest is not None: return 3 if self.ad_quest else 0
        if 'ads' in D: return D['ads']
        return 3 if self.feed.get('ads', 0) > 0 else 0          # 옛 feed: 광고 시청 수 기록 없음 → 광고를 보면 3번 이상으로 가정

    def run(self):
        days = self.feed['days']
        kid_order = 0
        prev_eg = days[0]['sessions'][0]['eg']
        self.dq, self.wq = [], []
        for di, D in enumerate(days):
            self.day_acc = {}
            if di % 7 == 0: self.week_done, self.week_claimed = 0, False      # 1일 = 월요일 가정
            if self.q_on: self.q_new_day(di)
            kp = D['kid_pulls'] if 'kid_pulls' in D else D['pulls'] * (self.kid_share if self.kid_share is not None else 0.25)
            self.kid_pulls(kp, di)
            self.acc('kid_pulls', kp)
            owned = [k for k, s in self.kids.items() if s['level'] > 0]
            reqs = []
            if self.do_requests:
                for i in range(int(self.C['requestPerDay'])):
                    k = owned[kid_order % len(owned)]; kid_order += 1
                    fav = self.kids[k]['row']['favoriteIds']
                    e = fav[(di + i) % len(fav)]
                    reqs.append(dict(kid=k, enemy=e, count=max(1, self.enemies[e]['requestCount']), done=False))
                self.log['req_total'] += len(reqs)
            tot_min = sum(s['mins'] for s in D['sessions'])
            n_s = len(D['sessions'])
            ads = self.day_ads(D)
            for si, S in enumerate(D['sessions']):
                day_f = di + (si + 1) / n_s
                self.cur_day = day_f
                self.claim_crafts()
                eg = S['eg']
                self.battle(S['kills'])
                if self.q_on:
                    self.q_add('kill', S.get('kills_all', S['kills']))
                    self.q_add('stage', S['bosses'])
                    self.q_add('dungeon', S.get('dun', S.get('d1n', 0)))
                    if si == 0:
                        self.q_add('upgrade', 10 ** 6)                     # 강화 20번은 접속마다 넘는다 (가정)
                        self.q_add('gacha', D['pulls'])
                    self.q_add('ad', ads / n_s)
                    self.q_claim()
                self.t += S['mins'] / 60
                frac = S['mins'] / tot_min
                bg = D['gold_total'] * frac * self.gold_share; self.gold += bg; self.acc('gb', bg)
                bm = D['myc_total'] * frac * self.myc_share; self.myc += bm; self.acc('mb', bm)
                self.requests(reqs, eg)
                self.tribute()
                self.grow(eg, day_f)
                self.kid_stars(day_f)
                self.craft(eg)
                self.rush_all()
                self.t += S['gap']
                prev_eg = eg
                if self.log['all24'] is None and len(self.owned) >= len(self.decos): self.log['all24'] = day_f
                if self.log['placed24'] is None and len(self.placed) >= len(self.decos): self.log['placed24'] = day_f
            b = self.bonus()
            self.day_acc.update(day=di + 1, stage=self.stage, ring=self.ring, idle_gold=D['gold_total'], idle_myc=D['myc_total'], eg=D['eg'],
                                kills=D['kills'], bonus=b, owned=len(self.owned), placed=len(self.placed), exp_wallet=self.exp,
                                idle_stage=D['stage'], wallet_g=self.gold, wallet_m=self.myc,
                                kid_lv={k: s['level'] for k, s in self.kids.items()}, kid_star={k: s['star'] for k, s in self.kids.items()})
            self.log['day'].append(self.day_acc)
        return self


def max_totals(T):
    """가호 전부 + 꼬마 ★최대(+세트) + 장식 24개 모두 놓았을 때 효과 합 (Bonus.cs:249-279) — 칸이 12개라 실제로는 닿을 수 없는 상한"""
    b = dict.fromkeys(EFFECTS, 0.0)
    for r in T['bless']: b[r['effect']] += r['value']
    maxstar = len(T['stars']) - 1
    owners = {d['owner'] for d in T['decos']}
    for k in T['kids']: b[k['bonusEffect']] += k['bonusValue'] * (maxstar + (1 if k['key'] in owners else 0))
    for d in T['decos']: b[d['effect']] += d['value']
    return b


def period_table(w, ranges=((1, 7), (8, 14), (15, 30), (31, 45), (46, 60), (61, 90))):
    rows = []
    days = w.log['day']
    for a, b in ranges:
        ds = [d for d in days if a <= d['day'] <= b]
        if not ds: continue
        def s(k): return sum(d.get(k, 0) for d in ds)
        ig, im = s('idle_gold'), s('idle_myc')
        gsp = s('g_ring') + s('g_evo') + s('g_craft'); ginc = s('gi_req')
        msp = s('m_evo') + s('m_star') + s('m_craft') + s('m_rush'); minc = s('mi_week') + s('mi_first')
        n = len(ds)
        rows.append(dict(r=f'{a}~{b}', g_sp=gsp / ig * 100, g_ring=s('g_ring') / ig * 100, g_evo=s('g_evo') / ig * 100, g_craft=s('g_craft') / ig * 100,
                         g_req=ginc / ig * 100, m_sp=msp / n, m_inc=minc / n, m_idle=im / n, m_sp_pct=msp / im * 100,
                         m_net=(msp - minc) / im * 100, grow_myc=s('grow_myc') / n, harvest=s('harvest') / n, exp=s('exp') / n, exp_q=s('exp_q') / n,
                         kid_pulls=s('kid_pulls') / n))
    return rows


if __name__ == '__main__':
    feed = json.load(open(sys.argv[1], encoding='utf-8'))
    opts = {}
    for a in sys.argv[2:]:
        k, v = a.split('='); opts[k] = (v == '1') if k in ('rush', 'requests', 'quests', 'grow') else float(v)
    T = tables.load()
    w = WT(T, feed, **opts).run()
    print('reach', {k: round(v, 2) for k, v in w.log['reach'].items()})
    for r in period_table(w):
        print({k: (round(v, 2) if isinstance(v, float) else v) for k, v in r.items()})
