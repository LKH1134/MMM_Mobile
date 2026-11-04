# 세계수 진행 독립 포팅 (wt-verify, theory/ 를 보기 전에 코드에서 직접 옮김). 기대값 모델(난수 없음).
# 공식 출처 (Assets/Scripts/Mobile/...):
#   KillGold(k) = ceil(EnemyGold(지금 스테이지) × k)                         WorldTree/WorldTree.cs:141
#   나이테 비용 = KillGold(ringKills × treeRingGrowth^(ring-1))               WorldTree.Growth.cs:22-23
#   진화 = 나이테 최대 + exp ≥ evoExp + 골드 KillGold(evoGoldKills) + 균사석, exp 이월 · ring=1   Growth.cs:46-69
#   창고 = 일반 적 처치마다 treeHarvestPerKill × (1+Harvest) (소수는 확률 → 기대값)   Growth.cs:118-137, IdleBattle.cs:387
#   공물 = Σ count × value × (선호 ? tributePreferMul : 1), 최대 단계면 불가           Growth.cs:146-173
#   선물 = 좋아하는 버섯 중 가장 많은 것 전부 × value × giftFavoriteMul (없으면 최다 버섯 min(giftMaxCount)) ×1   Kids.cs:128-158
#   호감도 레벨 = need 누적, bagBonus[레벨-1]                                   Kids.cs:88-98, Bonus.cs:61-62
#   ★진화: 레벨 ≥ ceil(needLevel × kidEvoLevelGradeMul^grade), 단계 ≥ evoStage+★+1, 균사석   Kids.cs:49-79 (K1)
#   부탁: 하루 requestPerDay건, 보유 꼬마를 돌아가며, 좋아하는 버섯 requestCount개 → 골드 KillGold(requestGoldKills)·호감도   Kids.cs:177-234
#   주간 상자: 주 requestWeekGoal건 → requestWeekMycelite                        Kids.cs:236-245
#   공방: 슬롯 craftSlots, 주인 꼬마 보유, 비용 KillGold(goldKills)·mycelite, 시간 hours/(1+CraftSpeed)   Craft.cs:65-91
#   꾸미기: 자리별 칸 수, 첫 배치 decoFirstMycelite·decoFirstAffinity, 세트 = 주인 장식 모두 놓음   Craft.cs:171-211
#   효과 합 = 가호(단계≤지금) + bonusValue × (★ + 세트) + 놓은 장식                Bonus.cs:25-48
#   보따리 배율 = (1+(ring-1)·treeRingBagBonus) × (1 + Σ kidBagShare·bagMul[★]·(1+bagBonus[호감])·(세트?1+decoSetBagMul)) Bonus.cs:58-75
#   보따리 시간당 골드 = EnemyGold × bagKillsPerHour × 배율 × (1+BagGold), 균사석 = bagMycelitePerHour × 배율 × (1+BagMycelite),
#   보관 = (bagCapHours + BagHours)h, 정산 시점의 비율로 쌓임                     Bonus.cs:77-104
#   꼬마 레벨: 첫 조각 = Lv1, Lv→Lv+1 조각 copiesBase + copiesPerLevel×Lv           Formation.cs:125-150, Equipment.cs:197
#   꼬마 뽑기: 뽑기 레벨 확률로 등급, 등급 안 균등                                   GachaProgress.cs:68-112
# 방치 입력: feed.py (KHD Sim, 접속별 처치·스테이지·적 처치 골드, 하루 골드·균사석·뽑기)
# python wt_port.py <feed.json> [옵션 key=value ...]
import sys, json, math, os
sys.stdout.reconfigure(encoding='utf-8')
import tables

EFFECTS = ['Attack', 'Hp', 'Speed', 'CritChance', 'CritDamage', 'KillGold', 'BossMycelite', 'Harvest', 'BagGold', 'BagMycelite',
           'BagHours', 'CraftSpeed']
ZONES = {'Root': 'rootSlots', 'Trunk': 'trunkSlots', 'Branch': 'branchSlots', 'Crown': 'crownSlots'}


class WT:
    def __init__(self, T, feed, gold_share=0.10, myc_share=0.10, kid_share=0.25, gifts=1, k1=True, rush=False, requests=True,
                 grade_unlock=None):
        self.T, self.feed = T, feed
        self.C = T['const']
        self.gold_share, self.myc_share, self.kid_share, self.gifts, self.k1, self.rush = gold_share, myc_share, kid_share, gifts, k1, rush
        self.do_requests = requests
        self.grade_unlock = grade_unlock      # D4 민감도: 등급 g가 열리는 뽑기 레벨 목록
        self.stage, self.ring, self.exp = 1, 1, 0.0
        self.enemies = {e['index']: e for e in T['enemies']}
        self.store = {i: 0.0 for i in self.enemies}
        self.kids = {}
        for k in T['kids']:
            self.kids[k['key']] = dict(row=k, level=1 if k['initialOwned'] > 0 else 0, copies=0.0, star=0, aff=0, frac=0.0)
        self.gacha_lv, self.gacha_exp = 1, 0.0
        self.decos = {d['key']: d for d in T['decos']}
        self.owned, self.placed, self.first = set(), [], set()
        self.crafts = []                       # (key, end_hour)
        self.gold = 0.0                        # 세계수 지갑 (절대 골드)
        self.myc = 0.0
        self.bag_g = self.bag_m = 0.0
        self.t = 0.0                           # 시각 (시간)
        self.last_claim = 0.0
        self.week_done, self.week_claimed = 0, False
        self.log = dict(reach={1: 0.0}, star={}, affmax={}, decos_at={}, owned_at={}, eff_at={}, day=[], all24=None, placed24=None,
                        req_done=0, req_total=0, week_boxes=0)
        self.day_acc = None

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

    def aff_level(self, kid):
        p, lv = self.kids[kid]['aff'], 1
        for r in self.T['aff']:
            if r['need'] <= 0 or p < r['need']: break
            p -= r['need']; lv = r['level'] + 1
        return min(lv, len(self.T['aff']))

    def bag_mul(self):
        kids = 0.0
        for k, s in self.kids.items():
            if s['level'] <= 0: continue
            sh = self.C['kidBagShare'] * self.T['stars'][s['star']]['bagMul'] * (1 + self.T['aff'][self.aff_level(k) - 1]['bagBonus'])
            if self.set_complete(k): sh *= 1 + self.C['decoSetBagMul']
            kids += sh
        return (1 + (self.ring - 1) * self.C['treeRingBagBonus']) * (1 + kids)

    def kg(self, eg, kills): return math.ceil(eg * kills)

    # ── 기록 ──
    def acc(self, k, v):
        self.day_acc[k] = self.day_acc.get(k, 0.0) + v

    def spend_gold(self, cat, amt, eg):
        self.gold -= amt; self.acc('g_' + cat, amt)

    def spend_myc(self, cat, amt):
        self.myc -= amt; self.acc('m_' + cat, amt)

    # ── 꼬마 뽑기 (기대값) ──
    def kid_pulls(self, n):
        rows = self.T['gacha']
        cols = ['oddsNormal', 'oddsRare', 'oddsEpic', 'oddsLegend']   # 4등급 (2026-11-04 고급 삭제)
        by_grade = {}
        for k, s in self.kids.items(): by_grade.setdefault(s['row']['grade'], []).append(k)
        left = n
        while left > 1e-9:
            r = rows[self.gacha_lv - 1]
            step = left if r['expToNext'] <= 0 else min(left, r['expToNext'] - self.gacha_exp)
            odds = [r[c] for c in cols]
            if self.grade_unlock:
                odds = [o if self.gacha_lv >= self.grade_unlock[g] else 0 for g, o in enumerate(odds)]
                tot = sum(odds); odds = [o * 100 / tot for o in odds]
            for g, ids in by_grade.items():
                per = step * odds[g] / 100 / len(ids)
                for k in ids: self.add_copies(k, per)
            left -= step
            if r['expToNext'] > 0:
                self.gacha_exp += step
                if self.gacha_exp >= r['expToNext'] - 1e-9: self.gacha_exp = 0; self.gacha_lv = min(len(rows), self.gacha_lv + 1)

    def add_copies(self, k, x):
        s = self.kids[k]
        if s['level'] == 0:
            s['frac'] += x
            if s['frac'] < 1: return
            x = s['frac'] - 1; s['level'] = 1
        s['copies'] += x
        while s['level'] < self.C['ggomaMaxLevel']:
            need = self.C['copiesBase'] + self.C['copiesPerLevel'] * s['level']
            if s['copies'] < need: break
            s['copies'] -= need; s['level'] += 1

    def need_level(self, k):
        s = self.kids[k]; base = self.T['stars'][s['star'] + 1]['needLevel']
        g = s['row']['grade'] if self.k1 else 0
        return max(1, math.ceil(base * self.C['kidEvoLevelGradeMul'] ** g - 1e-4))

    # ── 접속 ──
    def login(self, eg, elapsed_h):
        b = self.bonus()
        cap = self.C['bagCapHours'] + b['BagHours']
        use = min(elapsed_h, cap)
        m = self.bag_mul()
        g = eg * self.row()['bagKillsPerHour'] * m * (1 + b['BagGold']) * use
        y = self.row()['bagMycelitePerHour'] * m * (1 + b['BagMycelite']) * use
        self.bag_g += g; self.bag_m += y
        gi, mi = math.floor(self.bag_g), math.floor(self.bag_m)
        self.bag_g -= gi; self.bag_m -= mi
        self.gold += gi; self.myc += mi
        self.acc('gi_bag', gi); self.acc('mi_bag', mi)
        if use < elapsed_h - 1e-9: self.acc('bag_full_h', elapsed_h - use)
        # 다 된 장식 받기 → 놓기
        done = [c for c in self.crafts if c[1] <= self.t + 1e-9]
        self.crafts = [c for c in self.crafts if c[1] > self.t + 1e-9]
        for key, _ in done: self.owned.add(key)
        self.place_all()

    def place_all(self):
        cand = sorted([k for k in self.owned if k not in self.placed], key=lambda k: -self.decos[k]['grade'])
        for key in cand:
            d = self.decos[key]
            n_in = sum(1 for p in self.placed if self.decos[p]['zone'] == d['zone'])
            if n_in < self.row()[ZONES[d['zone']]]:
                self.placed.append(key)
                if key not in self.first:
                    self.first.add(key); self.myc += self.C['decoFirstMycelite']; self.acc('mi_first', self.C['decoFirstMycelite'])
                    if self.kids[d['owner']]['level'] > 0: self.kids[d['owner']]['aff'] += self.C['decoFirstAffinity']

    def battle(self, kills):
        h = self.C['treeHarvestPerKill'] * (1 + self.bonus()['Harvest'])
        per = kills * h / len(self.store)          # 버섯 10종 균등 (IdleBattle 적 그림 무작위)
        for i in self.store: self.store[i] += per
        self.acc('harvest', kills * h)

    def requests(self, day_reqs, eg):
        for r in day_reqs:
            if r['done']: continue
            if self.store[r['enemy']] >= r['count']:
                self.store[r['enemy']] -= r['count']; r['done'] = True
                gg = self.kg(eg, self.C['requestGoldKills']); self.gold += gg; self.acc('gi_req', gg)
                self.kids[r['kid']]['aff'] += self.C['requestAffinity']
                self.week_done += 1; self.log['req_done'] += 1
        if self.week_done >= self.C['requestWeekGoal'] and not self.week_claimed:
            self.week_claimed = True; self.myc += self.C['requestWeekMycelite']; self.acc('mi_week', self.C['requestWeekMycelite'])
            self.log['week_boxes'] += 1

    def gift(self):
        maxlv = len(self.T['aff'])
        cand = [k for k, s in self.kids.items() if s['level'] > 0 and self.aff_level(k) < maxlv]
        if not cand: return
        k = min(cand, key=lambda x: self.kids[x]['aff'])
        fav = self.kids[k]['row']['favoriteIds']
        best = max(fav, key=lambda e: self.store[e]) if fav else None
        if best is not None and self.store[best] >= 1:
            n = math.floor(self.store[best]); v = n * self.enemies[best]['value'] * self.C['giftFavoriteMul']
        else:
            best = max(self.store, key=lambda e: self.store[e]); n = min(math.floor(self.store[best]), self.C['giftMaxCount'])
            v = n * self.enemies[best]['value']
        if n <= 0: return
        self.store[best] -= n; self.kids[k]['aff'] += int(v); self.acc('gift_n', n)

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
                self.spend_gold('ring', c, eg); self.ring += 1; continue
            if self.stage >= self.max_stage: return
            r = self.row()
            gc, mc = self.kg(eg, r['evoGoldKills']), r['evoMycelite']
            if self.exp < r['evoExp'] or self.gold < gc or self.myc < mc: return
            self.spend_gold('evo', gc, eg); self.spend_myc('evo', mc)
            self.exp -= r['evoExp']; self.stage += 1; self.ring = 1
            self.log['reach'][self.stage] = day_f
            self.log['decos_at'][self.stage] = (len(self.owned) + len(self.crafts), sum(self.row()[z] for z in ZONES.values()))
            self.log['eff_at'][self.stage] = self.bonus()
            self.place_all()

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
            self.spend_gold('craft', gc, eg); self.spend_myc('craft', mc)
            self.crafts.append((d['key'], self.t + g['hours'] / speed))

    def rush_all(self):
        if not self.rush: return
        n = int(self.C['craftRushPerDay'] - self.day_acc.get('rush_n', 0))
        for i, c in enumerate(sorted(self.crafts, key=lambda c: c[1])):
            if n <= 0: break
            rem = c[1] - self.t
            if rem <= 0: continue
            cost = max(1, math.ceil(rem * 3600 / self.C['craftRushSeconds']))
            if self.myc < cost: break
            self.spend_myc('rush', cost); self.acc('rush_n', 1); n -= 1
            self.crafts = [(k, (self.t if k == c[0] else e)) for k, e in self.crafts]
        self.login_claim_crafts()

    def login_claim_crafts(self):
        done = [c for c in self.crafts if c[1] <= self.t + 1e-9]
        self.crafts = [c for c in self.crafts if c[1] > self.t + 1e-9]
        for key, _ in done: self.owned.add(key)
        self.place_all()

    # ── 하루 ──
    def run(self):
        days = self.feed['days']
        kid_order = 0
        prev_eg = days[0]['sessions'][0]['eg']
        for di, D in enumerate(days):
            self.day_acc = {}
            if di % 7 == 0: self.week_done, self.week_claimed = 0, False
            if self.kid_share > 0: self.kid_pulls(D['pulls'] * self.kid_share)
            # 부탁 굴리기 (보유 꼬마를 돌아가며, 좋아하는 버섯은 날마다 번갈아)
            owned = [k for k, s in self.kids.items() if s['level'] > 0]
            reqs = []
            if self.do_requests:
                for i in range(int(self.C['requestPerDay'])):
                    k = owned[(kid_order) % len(owned)]; kid_order += 1
                    fav = self.kids[k]['row']['favoriteIds']
                    e = fav[(di + i) % len(fav)]
                    reqs.append(dict(kid=k, enemy=e, count=max(1, self.enemies[e]['requestCount']), done=False))
                self.log['req_total'] += len(reqs)
            tot_min = sum(s['mins'] for s in D['sessions'])
            for si, S in enumerate(D['sessions']):
                day_f = di + (si + 1) / len(D['sessions'])
                eg_login = prev_eg
                elapsed = (self.t - self.last_claim)
                self.login(eg_login, elapsed); self.last_claim = self.t
                eg = S['eg']
                self.battle(S['kills'])
                self.t += S['mins'] / 60
                # 방치 예산: 이번 접속 몫
                frac = S['mins'] / tot_min
                bg = D['gold_total'] * frac * self.gold_share; self.gold += bg; self.acc('gb', bg)
                bm = D['myc_total'] * frac * self.myc_share; self.myc += bm; self.acc('mb', bm)
                self.requests(reqs, eg)
                for _ in range(self.gifts): self.gift()
                self.tribute()
                self.grow(eg, day_f)
                self.kid_stars(day_f)
                self.craft(eg)
                self.rush_all()
                self.t += S['gap']
                prev_eg = eg
                for k in self.kids:
                    if self.aff_level(k) >= len(self.T['aff']) and k not in self.log['affmax']: self.log['affmax'][k] = day_f
                if self.log['all24'] is None and len(self.owned) >= len(self.decos): self.log['all24'] = day_f
                if self.log['placed24'] is None and len(self.placed) >= len(self.decos): self.log['placed24'] = day_f
            b = self.bonus()
            self.day_acc.update(day=di + 1, stage=self.stage, ring=self.ring, idle_gold=D['gold_total'], idle_myc=D['myc_total'], eg=D['eg'],
                                kills=D['kills'], bag_mul=self.bag_mul(), bonus=b, owned=len(self.owned), placed=len(self.placed),
                                idle_stage=D['stage'], cc=D.get('cc'), cd=D.get('cd'), wallet_g=self.gold, wallet_m=self.myc,
                                kid_lv={k: s['level'] for k, s in self.kids.items()}, gacha_lv=self.gacha_lv)
            self.log['day'].append(self.day_acc)
        return self


def max_totals(T):
    """가호 전부 + 꼬마 ★최대(+세트) + 장식 24개 모두 놓았을 때 효과 합 (Bonus.cs:25-48)"""
    b = dict.fromkeys(EFFECTS, 0.0)
    for r in T['bless']: b[r['effect']] += r['value']
    maxstar = len(T['stars']) - 1
    owners = {d['owner'] for d in T['decos']}
    for k in T['kids']: b[k['bonusEffect']] += k['bonusValue'] * (maxstar + (1 if k['key'] in owners else 0))
    for d in T['decos']: b[d['effect']] += d['value']
    return b


def summarize(w, label):
    L = w.log
    out = dict(label=label, reach=L['reach'], star=L['star'], affmax=L['affmax'], all24=L['all24'], placed24=L['placed24'],
               decos_at=L['decos_at'], req_done=L['req_done'], req_total=L['req_total'], week_boxes=L['week_boxes'])
    return out


def period_table(w, ranges=((1, 7), (8, 14), (15, 30), (31, 45), (46, 60))):
    rows = []
    days = w.log['day']
    for a, b in ranges:
        ds = [d for d in days if a <= d['day'] <= b]
        if not ds: continue
        def s(k): return sum(d.get(k, 0) for d in ds)
        ig, im = s('idle_gold'), s('idle_myc')
        gsp = s('g_ring') + s('g_evo') + s('g_craft'); ginc = s('gi_bag') + s('gi_req')
        msp = s('m_evo') + s('m_star') + s('m_craft') + s('m_rush'); minc = s('mi_bag') + s('mi_week') + s('mi_first')
        n = len(ds)
        rows.append(dict(r=f'{a}~{b}', g_sp=gsp / ig * 100, g_ring=s('g_ring') / ig * 100, g_evo=s('g_evo') / ig * 100, g_craft=s('g_craft') / ig * 100,
                         g_bag=s('gi_bag') / ig * 100, g_req=s('gi_req') / ig * 100, m_sp=msp / n, m_inc=minc / n, m_idle=im / n,
                         m_net=(msp - minc) / im * 100, m_bag=s('mi_bag') / n, harvest=s('harvest') / n, exp=s('exp') / n,
                         bag_full=s('bag_full_h') / n, gift_share=s('gift_n') / max(1, s('harvest')) * 100))
    return rows


if __name__ == '__main__':
    feed = json.load(open(sys.argv[1], encoding='utf-8'))
    opts = {}
    for a in sys.argv[2:]:
        k, v = a.split('='); opts[k] = (v == '1') if k in ('k1', 'rush', 'requests') else (int(v) if k == 'gifts' else float(v))
    T = tables.load()
    w = WT(T, feed, **opts).run()
    print(json.dumps(summarize(w, sys.argv[1]), ensure_ascii=False, default=str))
    for r in period_table(w):
        print({k: (round(v, 2) if isinstance(v, float) else v) for k, v in r.items()})
