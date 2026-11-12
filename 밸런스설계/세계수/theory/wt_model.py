# 세계수 진행 모델 (결정적, 기대값). 2026-11-12 현행화 — 공식은 지금 코드 그대로:
#   WorldTree.Growth.cs  나이테 RingCostAt :22-23 (ringKills × treeRingGrowth^(ring−1) KE) · 진화 CanEvolve :45-53 (나이테 최대·경험치·골드·균사석)
#                        · Evolve :55-67 (남은 경험치 이어 감) · 가호 BlessActive :70 · 창고 AddHarvest :135-162 (treeHarvestPerKill × (1 + Harvest))
#                        · 공물 Tribute :190-198 (창고 통째로, value × tributePreferMul if preferIds) · AddQuestExp :201-206 (메인 퀘스트 treeExp)
#   WorldTree.Kids.cs    ★ EvoStageNeeded :48-52 (min(MaxStage, evoStage + ★ + 1)) · EvoLevelNeeded :55 (needLevel, 등급 없음)
#                        · KidStep :78 (★ + 세트) · 부탁 RollRequests :116-134 (requestPerDay건, favoriteIds 버섯, requestCount개)
#                        · DoRequest :143-153 (골드 requestGoldKills KE만) · ClaimWeek :157-164 (requestWeekGoal건 → requestWeekMycelite)
#   WorldTree.Craft.cs   제작 CanCraft/StartCraft :67-91 (장식등급 goldKills KE·mycelite, hours / (1 + CraftSpeed)) · 가속 RushCost :109
#                        · 꾸미기 Place :173-184 (칸 = 단계 rootSlots…, 처음 놓으면 decoFirstMycelite만) · 세트 DecoSetComplete :197-207
#   WorldTree.Bonus.cs   효과 합 RecomputeBonus :23-53 = 가호 + 꼬마 bonusValue × KidStep + 놓은 장식
#   IdleBattle.cs        처치 골드 × Mul(KillGold) :749 · 보스 균사석 × Mul(BossMycelite) :500 · 창고는 일반 적만 :494
#                        · 적 그림 = 독성/약용/식용 비율 + 묶음 안 조각 균등 :738-739·:784-791 (wt_tables.store_weights)
#   IdleProgress.cs      FinalAttack·FinalHp·FinalSpeed·FinalCrit* :347-352
#   Formation.cs         꼬마 레벨 = 조각 (ggomaCopiesBase + ggomaCopiesPerLevel × Lv) :181, 관문 ggomaEvoLevel2·3에서 성장(균사석) LevelCap :336-340 · TryGrow :353-363
# 없어진 것(넣지 않음): 수확 보따리(11-03), 꼬마 등급·kidEvoLevelGradeMul(11-04), 세계수 의뢰 시트(11-10 → quest_model.MainQuestBook),
#   호감도·선물·requestAffinity·decoFirstAffinity(11-12).
#   python wt_model.py <feed.json> [ads]
import json, math, os, sys
sys.dont_write_bytecode = True
import wt_tables as WT
from quest_model import MainQuestBook

HERE = os.path.dirname(os.path.abspath(__file__))
EFFECTS = WT.EFFECTS
KILL_GOLD_SRC = ('battle', 'offline', 'offline_ad')     # 처치 골드 배율이 닿는 방치 골드 (앱 밖 = 접속 중 초당 골드 × 비율, IdleAway.cs:108)


def load_feed(name):
    F = json.load(open(os.path.join(HERE, name), encoding='utf-8'))
    return F['feed'], {k: v for k, v in F.items() if k != 'feed'}


class Tree:
    def __init__(self, T, feed, meta=None, ads=0.5, gold_frac=0.10, myc_frac=0.10, rush=False, quests=True,
                 quest_only=None, kid_grow=True, kid_pull_mul=1.0, kill_mul=1.0):
        self.T, self.feed, self.meta = T, feed, meta or {}
        self.C = T['const']
        self.ads, self.gold_frac, self.myc_frac, self.rush, self.kid_grow, self.kid_pull_mul = ads, gold_frac, myc_frac, rush, kid_grow, kid_pull_mul
        self.kill_mul = kill_mul          # feed 처치 수 오차 민감도 (창고·퀘스트 처치 진행에 곱함)
        self.quests = MainQuestBook(T['quests'], ads, only=quest_only) if quests else None
        self.stage, self.ring, self.exp = 1, 1, 0.0
        self.enemy = {e['index']: e for e in T['enemies']}
        self.store = {e: 0.0 for e in self.enemy}
        self.kids = {}
        for k in T['kids']:
            own = k['initialOwned'] > 0
            self.kids[k['key']] = dict(row=k, owned=own, level=1 if own else 0, copies=0.0, star=0, grown=0)
        self.decos_owned, self.placed, self.first = set(), [], set()
        self.crafts = []          # (deco key, 끝 시각 h)
        self.t = 0.0              # 달력 시간(시간)
        self.week_done, self.week_claimed, self.week = 0, False, -1
        self.gold_b = self.myc_b = 0.0     # 세계수에 쓸 수 있는 예산 (골드는 지금 처치 골드 단위 KE)
        self.eg = None
        self.reqs = []
        self.log = dict(stage_day={1: 0.0}, star_day={}, own_day={}, grow_day={}, daily=[], unplaced_sess={}, deco_at={})
        self.reset_day_acc()

    # ── 효과 (WorldTree.Bonus.cs:23-53) ──
    def bonus(self):
        b = {e: 0.0 for e in EFFECTS}
        for r in self.T['bless']:
            if r['stage'] <= self.stage:
                b[r['effect']] += r['value']
        for key, k in self.kids.items():
            if k['owned']:
                b[k['row']['bonusEffect']] += k['row']['bonusValue'] * self.kid_step(key)
        for key in self.placed:
            d = self.deco(key)
            b[d['effect']] += d['value']
        return b

    def deco(self, key):
        return next(d for d in self.T['decos'] if d['key'] == key)

    def row(self, s=None):
        return self.T['tree'][min(s or self.stage, len(self.T['tree'])) - 1]

    def set_complete(self, kid):
        mine = [d['key'] for d in self.T['decos'] if d['owner'] == kid]
        return bool(mine) and all(m in self.placed for m in mine)

    def kid_step(self, kid):
        return self.kids[kid]['star'] + (1 if self.set_complete(kid) else 0)

    # ── 꼬마 뽑기 (기대값): 8종 같은 확률, 처음 = Lv1, 그 뒤 조각 → 합성 ──
    def kid_pulls_today(self, dayrec):
        """지금 규칙의 하루 꼬마 뽑기 수 (feed는 11-03 규칙: 광고 뽑기 4종 × pullCount 4, 균사석 뽑기 4종 순환).
        = (feed 뽑기 − feed 광고 뽑기) / 지금 뽑기 종류 수(3: 무기·방어구·꼬마, 펫 삭제) + 꼬마 광고 뽑기 pullCount × dailyLimit × 광고 비율"""
        ad_old = self.meta.get('ad_gacha', {})
        n_old = sum(v[1] for v in ad_old.values())
        old_ad = sum(v[0] for v in ad_old.values()) / max(1, len(ad_old)) * round(n_old * self.ads)
        paid = max(0.0, dayrec['pulls'] - old_ad)
        kinds = len(self.T['ad_gacha']) or 3
        pc, lim = self.T['ad_gacha'].get('Character', (0, 0))
        return (paid / kinds + pc * lim * self.ads) * self.kid_pull_mul

    def copies_to_next(self, lv):          # Formation.CopiesToNext :181
        return self.C['ggomaCopiesBase'] + self.C['ggomaCopiesPerLevel'] * lv

    def level_cap(self, k):                # Formation.LevelCap :336-340 (성장 = 방치 쪽 균사석, kid_grow=False면 성장 안 함)
        if self.kid_grow:
            return int(self.C['ggomaMaxLevel'])
        return int(self.C['ggomaEvoLevel2'])

    def pull_kids(self, n):
        each = n / len(self.kids)
        for key, k in self.kids.items():
            k['copies'] += each
            if not k['owned'] and k['copies'] >= 1:
                k['owned'], k['level'], k['copies'] = True, 1, k['copies'] - 1
                self.log['own_day'].setdefault(key, self.day)
            while k['owned'] and k['level'] < self.level_cap(k) and k['copies'] >= self.copies_to_next(k['level']):
                k['copies'] -= self.copies_to_next(k['level']); k['level'] += 1
                for g, lv in ((1, self.C['ggomaEvoLevel2']), (2, self.C['ggomaEvoLevel3'])):
                    if k['level'] >= lv and k['grown'] < g:      # 관문에 닿은 날 성장한다고 본다 (균사석 100·300은 방치 쪽 몫)
                        k['grown'] = g
                        self.acc['m_kidgrow'] += self.C['ggomaGrowMycelite2' if g == 1 else 'ggomaGrowMycelite3']
                        self.log['grow_day'].setdefault(key, {})[g] = self.day

    # ── 비용 ──
    def ring_cost(self):
        return self.row()['ringKills'] * self.C['treeRingGrowth'] ** (self.ring - 1)

    def reset_day_acc(self):
        self.acc = dict(g_spend=0.0, g_ring=0.0, g_evo=0.0, g_craft=0.0, g_req=0.0, g_kill=0.0, g_qweek=0.0,
                        m_spend=0.0, m_evo=0.0, m_star=0.0, m_craft=0.0, m_rush=0.0, m_week=0.0, m_first=0.0, m_boss=0.0,
                        m_qweek=0.0, m_kidgrow=0.0, exp=0.0, q_exp=0.0, harvest=0.0, req_done=0, req_gold_sess=0)

    def spend_gold(self, ke, kind):
        self.gold_b -= ke; self.acc['g_spend'] += ke; self.acc['g_' + kind] += ke

    def spend_myc(self, m, kind):
        self.myc_b -= m; self.acc['m_spend'] += m; self.acc['m_' + kind] += m

    # ── 한 접속 ──
    def session(self, s, day_gold_ke, day_myc, n_sess, dayrec):
        if self.kill_mul != 1.0:
            s = dict(s, kills=s['kills'] * self.kill_mul)
        eg = s['eg']
        if self.eg:
            self.gold_b *= self.eg / eg        # 처치 골드 단위가 바뀐 만큼 예산 가치 보정
        self.eg = eg
        # 방치 수입 중 세계수 예산 몫 + 세계수 효과로 늘어난 방치 수입(처치 골드·보스 균사석)의 같은 몫
        b = self.bonus()
        kill_ke = sum(dayrec['gold'].get(k, 0) for k in KILL_GOLD_SRC) / dayrec['eg'] / n_sess
        g_kill = kill_ke * b['KillGold']
        m_boss = dayrec['myc'].get('boss', 0) / n_sess * b['BossMycelite']
        self.acc['g_kill'] += g_kill; self.acc['m_boss'] += m_boss
        self.gold_b += self.gold_frac * (day_gold_ke / n_sess + g_kill)
        self.myc_b += self.myc_frac * (day_myc / n_sess + m_boss)
        # 1) 공방 받기 → 꾸미기
        self.claim_crafts()
        self.place_all()
        # 2) 전투: 창고 (일반 적만, 버섯 획득 기대값)
        b = self.bonus()
        h = s['kills'] * self.C['treeHarvestPerKill'] * (1 + b['Harvest'])
        self.acc['harvest'] += h
        for e, w in self.T['store_w'].items():
            self.store[e] += h * w
        self.t += s['mins'] / 60
        # 3) 메인 퀘스트 받기 → 세계수 경험치 (공물 앞, 같은 풀) · 주간 행 골드·균사석은 방치 수입에 더함
        if self.quests:
            qx, qg, qm = self.quests.session(self, s)
            self.exp += qx; self.acc['q_exp'] += qx
            self.acc['g_qweek'] += qg; self.acc['m_qweek'] += qm
            self.gold_b += self.gold_frac * qg; self.myc_b += self.myc_frac * qm
        # 4) 부탁 (골드만) → 주간 상자
        self.requests()
        # 5) 공물 (창고 통째로)
        if self.stage < len(self.T['tree']):
            prefer = self.row()['preferIds']
            ex = sum(c * self.enemy[e]['value'] * (self.C['tributePreferMul'] if e in prefer else 1) for e, c in self.store.items())
            self.exp += ex; self.acc['exp'] += ex
            for e in self.store:
                self.store[e] = 0.0
        # 6) 쓰기: 나이테·진화 → 꼬마 ★ → 공방
        self.grow()
        self.kid_evolve()
        self.start_crafts()
        if self.rush:
            self.do_rush()
        self.place_all()
        un = len(self.decos_owned) - len(self.placed)
        su = self.log['unplaced_sess']
        su[self.stage] = max(su.get(self.stage, 0), un)
        self.t += s['gap_h']

    def frac_day(self):
        return self.sess_i / 3

    def grow(self):
        R = len(self.T['tree'])
        while True:
            if self.ring < self.C['treeRingMax']:
                c = self.ring_cost()
                if self.gold_b >= c:
                    self.spend_gold(c, 'ring'); self.ring += 1
                    continue
                return
            if self.stage >= R:
                return
            r = self.row()
            if self.exp >= r['evoExp'] and self.gold_b >= r['evoGoldKills'] and self.myc_b >= r['evoMycelite']:
                self.spend_gold(r['evoGoldKills'], 'evo'); self.spend_myc(r['evoMycelite'], 'evo')
                self.exp -= r['evoExp']; self.stage += 1; self.ring = 1
                self.log['stage_day'][self.stage] = round(self.day - 1 + self.frac_day(), 2)
                self.log['deco_at'][self.stage] = (len(self.decos_owned) + len(self.crafts), sum(self.row(self.stage)['slots']))
                continue
            return

    def need_level(self, kid):             # Kids.cs:55 EvoLevelNeeded
        return max(1, self.T['stars'][self.kids[kid]['star'] + 1]['needLevel'])

    def kid_evolve(self):
        R = len(self.T['tree'])
        while True:
            best = None
            for key, k in self.kids.items():
                if not k['owned'] or k['star'] >= len(self.T['stars']) - 1:
                    continue
                if k['level'] < self.need_level(key):
                    continue
                if self.stage < min(R, k['row']['evoStage'] + k['star'] + 1):
                    continue
                cost = self.T['stars'][k['star'] + 1]['mycelite']
                if best is None or cost < best[1]:
                    best = (key, cost)
            if not best or self.myc_b < best[1]:
                return
            self.spend_myc(best[1], 'star')
            self.kids[best[0]]['star'] += 1
            self.log['star_day'].setdefault(best[0], {})[self.kids[best[0]]['star']] = round(self.day - 1 + self.frac_day(), 2)

    def dgrade(self, g):
        return next(x for x in self.T['dgrades'] if x['grade'] == g)

    def start_crafts(self):
        b = self.bonus()
        while len(self.crafts) < self.row()['craftSlots']:
            busy = {c[0] for c in self.crafts}
            cand = [d for d in self.T['decos'] if self.kids[d['owner']]['owned'] and d['key'] not in self.decos_owned and d['key'] not in busy]
            if not cand:
                return
            d = min(cand, key=lambda x: (x['grade'], x['key']))
            g = self.dgrade(d['grade'])
            if self.gold_b < g['goldKills'] or self.myc_b < g['mycelite']:
                return
            self.spend_gold(g['goldKills'], 'craft'); self.spend_myc(g['mycelite'], 'craft')
            self.crafts.append((d['key'], self.t + g['hours'] / (1 + b['CraftSpeed'])))

    def do_rush(self):
        used = 0
        for i, (key, end) in enumerate(list(self.crafts)):
            if used >= self.C['craftRushPerDay'] / 3:
                break
            left = (end - self.t) * 3600
            if left <= 0:
                continue
            cost = max(1, math.ceil(left / self.C['craftRushSeconds']))
            if self.myc_b >= cost:
                self.spend_myc(cost, 'rush'); self.crafts[i] = (key, self.t); used += 1

    def claim_crafts(self):
        keep = []
        for key, end in self.crafts:
            if end <= self.t + 1e-9:
                self.decos_owned.add(key)
            else:
                keep.append((key, end))
        self.crafts = keep

    def _first(self, k):                   # Craft.cs:176-180 첫 배치 = 균사석만 (11-12 호감도 삭제)
        if k not in self.first:
            self.first.add(k)
            self.myc_b += self.C['decoFirstMycelite']; self.acc['m_first'] += self.C['decoFirstMycelite']

    def place_all(self):
        """grade 정책: 높은 등급부터 빈 칸에, 교체 안 함 (v6_model.V6Tree가 고르는 정책을 덮어씀)"""
        slots = dict(zip(WT.ZONES, self.row()['slots']))
        for d in sorted(self.T['decos'], key=lambda x: (-x['grade'], x['key'])):
            if d['key'] not in self.decos_owned or d['key'] in self.placed:
                continue
            if sum(1 for k in self.placed if self.deco(k)['zone'] == d['zone']) >= slots[d['zone']]:
                continue
            self.placed.append(d['key'])
            self._first(d['key'])

    def roll_requests(self):
        owned = [k for k, v in self.kids.items() if v['owned']]
        self.reqs = []
        for i in range(int(self.C['requestPerDay'])):
            kid = owned[(self.day * 3 + i) % len(owned)]          # 게임은 무작위(겹치지 않게), 모델은 돌려가며
            fav = self.kids[kid]['row']['favoriteIds']
            e = fav[(self.day + i) % len(fav)]
            self.reqs.append(dict(kid=kid, enemy=e, count=max(1, self.enemy[e]['requestCount']), done=False))

    def requests(self):
        for r in self.reqs:
            if r['done'] or self.store[r['enemy']] < r['count']:
                continue
            self.store[r['enemy']] -= r['count']
            r['done'] = True
            g = self.C['requestGoldKills']
            self.gold_b += g; self.acc['g_req'] += g
            self.week_done += 1; self.acc['req_done'] += 1
        if self.week_done >= self.C['requestWeekGoal'] and not self.week_claimed:
            self.week_claimed = True
            self.myc_b += self.C['requestWeekMycelite']; self.acc['m_week'] += self.C['requestWeekMycelite']

    def run(self):
        for dayrec in self.feed:
            self.day = dayrec['day']
            if (self.day - 1) // 7 != self.week:
                self.week, self.week_done, self.week_claimed = (self.day - 1) // 7, 0, False
            self.reset_day_acc()
            if self.quests:
                self.quests.new_day(self, dayrec)
            kp = self.kid_pulls_today(dayrec)
            self.pull_kids(kp)
            self.roll_requests()
            gold_ke = sum(dayrec['gold'].values()) / dayrec['eg']
            myc = sum(dayrec['myc'].values())
            n = len(dayrec['sessions'])
            for i, s in enumerate(dayrec['sessions']):
                self.sess_i = i + 1
                self.session(s, gold_ke, myc, n, dayrec)
            b = self.bonus()
            self.log['daily'].append(dict(day=self.day, idle_stage=dayrec['stage'], stage=self.stage, ring=self.ring, exp_have=round(self.exp),
                                          gold_ke=gold_ke + self.acc['g_qweek'], myc=myc + self.acc['m_qweek'],
                                          kill_gold_ke=sum(dayrec['gold'].get(k, 0) for k in KILL_GOLD_SRC) / dayrec['eg'],
                                          boss_myc=dayrec['myc'].get('boss', 0),
                                          kills=sum(s['kills'] for s in dayrec['sessions']), kid_pulls=kp,
                                          owned_kids=sum(1 for k in self.kids.values() if k['owned']),
                                          stars={k: v['star'] for k, v in self.kids.items()},
                                          levels={k: v['level'] for k, v in self.kids.items()},
                                          decos=len(self.decos_owned), placed=len(self.placed),
                                          bonus=b, gold_b=self.gold_b, myc_b=self.myc_b, **self.acc))
        return self


def run(feed_name, ads=None, T=None, **kw):
    feed, meta = load_feed(feed_name)
    T = T or WT.load_current()
    return Tree(T, feed, meta, ads=meta.get('ads', 0.5) if ads is None else ads, **kw).run()


WINDOWS = [(1, 7), (8, 14), (15, 30), (31, 45), (46, 60), (61, 90)]


def flows(tr, lo, hi):
    """구간 재화 흐름 (하루 평균). 골드는 그 구간 방치 하루 골드(KE) 대비 %, 균사석은 개/일과 방치 균사석 대비 순소모 %"""
    rows = [r for r in tr.log['daily'] if lo <= r['day'] <= hi]
    if not rows:
        return None
    f = lambda k: sum(r[k] for r in rows)
    n = len(rows)
    g_idle, m_idle = f('gold_ke'), f('myc')
    m_in = f('m_week') + f('m_first') + f('m_boss')
    return dict(n=n, g_spend=f('g_spend') / g_idle * 100, g_ring=f('g_ring') / g_idle * 100, g_evo=f('g_evo') / g_idle * 100,
                g_craft=f('g_craft') / g_idle * 100, g_req=f('g_req') / g_idle * 100, g_kill=f('g_kill') / g_idle * 100,
                m_spend=f('m_spend') / n, m_evo=f('m_evo') / n, m_star=f('m_star') / n, m_craft=f('m_craft') / n, m_rush=f('m_rush') / n,
                m_week=f('m_week') / n, m_first=f('m_first') / n, m_boss=f('m_boss') / n, m_in=m_in / n, m_idle=m_idle / n,
                m_net=(f('m_spend') - m_in) / m_idle * 100, exp=f('exp') / n, q_exp=f('q_exp') / n,
                q_share=f('q_exp') / max(1, f('q_exp') + f('exp')) * 100, req=f('req_done') / n, harvest=f('harvest') / n,
                kid_pulls=f('kid_pulls') / n, kidgrow=f('m_kidgrow') / n)


def summary(tr, label=''):
    L = tr.log
    D = L['daily']
    out = [f'## {label}']
    out.append('단계 도달(일): ' + ', '.join(f'{s}:{d}' for s, d in sorted(L['stage_day'].items())))
    for lo, hi in WINDOWS:
        x = flows(tr, lo, hi)
        if not x:
            continue
        out.append(f'd{lo}-{hi}: 골드 쓰기 {x["g_spend"]:.1f}% (나이테 {x["g_ring"]:.1f} 진화 {x["g_evo"]:.1f} 공방 {x["g_craft"]:.1f}) '
                   f'수입 부탁 {x["g_req"]:.1f}% · 처치골드효과 {x["g_kill"]:.1f}% | 균사석 쓰기 {x["m_spend"]:.0f}/일 (진화 {x["m_evo"]:.0f} ★ {x["m_star"]:.0f} '
                   f'공방 {x["m_craft"]:.0f} 가속 {x["m_rush"]:.0f}) 수입 {x["m_in"]:.0f}/일 (주간상자 {x["m_week"]:.0f} 첫배치 {x["m_first"]:.0f} 보스효과 {x["m_boss"]:.1f}) '
                   f'방치 {x["m_idle"]:.0f}/일 → 순 {x["m_net"]:.1f}% | 공물 {x["exp"]:.0f}/일 퀘스트 {x["q_exp"]:.0f}/일 ({x["q_share"]:.0f}%) '
                   f'부탁 {x["req"]:.1f}/일 | 꼬마뽑기 {x["kid_pulls"]:.1f}/일 꼬마성장 {x["kidgrow"]:.0f}/일(방치 몫)')
    out.append('진화 순간 (장식 보유+제작중 / 새 단계 칸): ' + ', '.join(f'{k}:{v[0]}/{v[1]}' for k, v in L['deco_at'].items()))
    out.append('못 놓은 장식 최대 (단계: 개, 접속 끝): ' + ', '.join(f'{k}:{v}' for k, v in sorted(L['unplaced_sess'].items())))
    full = next((r['day'] for r in D if r['decos'] >= len(tr.T['decos'])), None)
    out.append(f'장식 {len(tr.T["decos"])}개 다 만듦 {full}일')
    out.append('★ 날짜: ' + '; '.join(f'{k} ' + ','.join(f'★{s}:{d}' for s, d in v.items()) for k, v in L['star_day'].items()))
    out.append('보유 날짜: ' + ', '.join(f'{k}:{d}' for k, d in L['own_day'].items()) + ' | 성장(관문) 날짜: ' +
               ', '.join(f'{k}:{v}' for k, v in L['grow_day'].items()))
    for dd in (7, 14, 30, 45, 60, 90):
        r = next((x for x in D if x['day'] == dd), None)
        if r:
            bb = {k: round(v, 3) for k, v in r['bonus'].items() if v}
            out.append(f'd{dd}: 단계 {r["stage"]} 장식 {r["decos"]}/{r["placed"]}놓음 꼬마Lv {r["levels"]} ★ {r["stars"]} 효과 {bb}')
    return '\n'.join(out)


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    feed = sys.argv[1]
    ads = float(sys.argv[2]) if len(sys.argv) > 2 else None
    tr = run(feed, ads)
    print(summary(tr, f'{feed} grade'))
