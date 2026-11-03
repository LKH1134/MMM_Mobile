# 세계수 진행 모델 (결정적). 입력 = 방치 진행 feed(idle_feed.py: 접속별 처치 수·스테이지·처치 골드, 하루 골드·균사석·뽑기),
# 공식 = WorldTree*.cs 그대로 (나이테·공물·진화·창고·선물·부탁·주간 상자·꼬마 ★·호감도·공방·꾸미기·보따리·효과 합).
#   python wt_model.py <feed> <current|v1> [charFrac] [goldFrac] [mycFrac] [gifts/session] [rush]
# 기대값 근사: 창고 버섯은 10종 균등(IdleBattle 적 그림 무작위), 꼬마 조각은 뽑기 확률 기대값, 부탁 꼬마·버섯은 돌아가며.
import json, math, os, sys
import wt_tables as WT

HERE = os.path.dirname(os.path.abspath(__file__))
EFFECTS = ['Attack', 'Hp', 'Speed', 'CritChance', 'CritDamage', 'KillGold', 'BossMycelite', 'Harvest', 'BagGold', 'BagMycelite', 'BagHours', 'CraftSpeed']


def copies_to_next(lv):           # Equipment.CopiesToNext: copiesBase 1 + copiesPerLevel 1 × lv
    return 1 + lv


class Tree:
    def __init__(self, T, feed, char_frac=0.25, gold_frac=0.10, myc_frac=0.10, gifts=1, rush=False):
        self.T, self.feed = T, feed
        self.C = T['const']
        self.char_frac, self.gold_frac, self.myc_frac, self.gifts, self.rush = char_frac, gold_frac, myc_frac, gifts, rush
        self.stage, self.ring, self.exp = 1, 1, 0.0
        self.store = {e['index']: 0.0 for e in T['enemies']}
        self.enemy = {e['index']: e for e in T['enemies']}
        self.kids = {}
        for k in T['kids']:
            self.kids[k['key']] = dict(row=k, owned=k['initialOwned'] > 0, level=1 if k['initialOwned'] > 0 else 0,
                                       copies=0.0, star=0, aff=0)
        self.gacha_lv, self.gacha_exp = 1, 0
        self.decos_owned, self.placed, self.first = set(), [], set()
        self.crafts = []          # (deco key, end time h)
        self.t = 0.0              # 달력 시간(시간)
        self.bag_t = 0.0          # 마지막 보따리 정산
        self.week_done, self.week_claimed, self.week = 0, False, -1
        self.gold_b = self.myc_b = 0.0     # 세계수에 쓸 수 있는 예산 (골드는 지금 처치 골드 단위 KE)
        self.eg = None
        self.log = dict(stage_day={1: 0.0}, star_day={}, aff_max_day={}, own_day={}, deco_n=[], daily=[])
        self.reset_day_acc()

    # ── 효과 ──
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

    def aff_level(self, kid):
        p, lv = self.kids[kid]['aff'], 1
        for r in self.T['aff']:
            if r['need'] <= 0 or p < r['need']:
                break
            p -= r['need']; lv = r['level'] + 1
        return min(lv, len(self.T['aff']))

    def bag_mult(self):
        kids = 0.0
        for key, k in self.kids.items():
            if not k['owned']:
                continue
            s = self.C['kidBagShare'] * self.T['stars'][k['star']]['bagMul'] * (1 + self.T['aff'][self.aff_level(key) - 1]['bagBonus'])
            if self.set_complete(key):
                s *= 1 + self.C['decoSetBagMul']
            kids += s
        return (1 + (self.ring - 1) * self.C['treeRingBagBonus']) * (1 + kids)

    # ── 꼬마 뽑기 (기대값) ──
    def pull_kids(self, n):
        g = self.T['gacha']
        by_grade = {}
        for key, k in self.kids.items():
            by_grade.setdefault(k['row']['grade'], []).append(key)
        left = n
        while left > 0:
            lvrow = g[min(self.gacha_lv, len(g)) - 1]
            need = lvrow[5] - self.gacha_exp if self.gacha_lv < len(g) else left
            step = min(left, max(1, need))
            for gr, keys in by_grade.items():
                each = step * lvrow[gr] / 100 / len(keys)
                for key in keys:
                    self.kids[key]['copies'] += each
            left -= step
            if self.gacha_lv < len(g):
                self.gacha_exp += step
                if self.gacha_exp >= lvrow[5]:
                    self.gacha_exp -= lvrow[5]; self.gacha_lv += 1
        for key, k in self.kids.items():
            if not k['owned'] and k['copies'] >= 1:
                k['owned'], k['level'], k['copies'] = True, 1, k['copies'] - 1
                self.log['own_day'].setdefault(key, self.day)
            while k['owned'] and k['level'] < 100 and k['copies'] >= copies_to_next(k['level']):
                k['copies'] -= copies_to_next(k['level']); k['level'] += 1

    def need_level(self, kid):
        base = self.T['stars'][self.kids[kid]['star'] + 1]['needLevel']
        m = self.T.get('kidEvoLevelGradeMul')
        if m:
            return max(1, math.ceil(base * m ** self.kids[kid]['row']['grade'] - 1e-9))
        return base

    # ── 비용 (지금 처치 골드 단위) ──
    def ring_cost(self):
        return self.row()['ringKills'] * self.C['treeRingGrowth'] ** (self.ring - 1)

    def reset_day_acc(self):
        self.acc = dict(g_spend=0.0, g_ring=0.0, g_evo=0.0, g_craft=0.0, g_bag=0.0, g_req=0.0,
                        m_spend=0.0, m_evo=0.0, m_star=0.0, m_craft=0.0, m_rush=0.0, m_bag=0.0, m_week=0.0, m_first=0.0,
                        exp=0.0, gift_mush=0.0, harvest=0.0, req_done=0)

    def spend_gold(self, ke, kind):
        self.gold_b -= ke; self.acc['g_spend'] += ke; self.acc['g_' + kind] += ke

    def spend_myc(self, m, kind):
        self.myc_b -= m; self.acc['m_spend'] += m; self.acc['m_' + kind] += m

    # ── 한 접속 ──
    def session(self, s, day_gold_ke, day_myc, n_sess, si):
        b = self.bonus()
        # 처치 골드 단위 바뀜: 예산 가치 보정
        eg = s['eg']
        if self.eg:
            self.gold_b *= self.eg / eg
        self.eg = eg
        # 1) 보따리 (지난 정산부터, 보관 시간까지)
        cap = self.C['bagCapHours'] + b['BagHours']
        hrs = min(self.t - self.bag_t, cap)
        self.bag_t = self.t
        m = self.bag_mult()
        r = self.row()
        g_bag = r['bagKillsPerHour'] * m * (1 + b['BagGold']) * hrs
        m_bag = r['bagMycelitePerHour'] * m * (1 + b['BagMycelite']) * hrs
        self.gold_b += g_bag; self.acc['g_bag'] += g_bag
        self.myc_b += m_bag; self.acc['m_bag'] += m_bag
        # 방치 수입 중 세계수 예산 몫
        self.gold_b += self.gold_frac * day_gold_ke / n_sess
        self.myc_b += self.myc_frac * day_myc / n_sess
        # 2) 공방 받기 → 꾸미기
        self.claim_crafts()
        self.place_all()
        # 3) 전투: 창고
        b = self.bonus()
        h = s['kills'] * self.C['treeHarvestPerKill'] * (1 + b['Harvest'])
        self.acc['harvest'] += h
        for e in self.store:
            self.store[e] += h / len(self.store)
        self.t += s['mins'] / 60
        # 4) 부탁
        self.requests()
        # 5) 선물
        for _ in range(self.gifts):
            self.gift()
        # 6) 공물 (창고 통째로)
        if self.stage < len(self.T['tree']):
            prefer = self.row()['preferIds']
            ex = sum(c * self.enemy[e]['value'] * (self.C['tributePreferMul'] if e in prefer else 1) for e, c in self.store.items())
            self.exp += ex; self.acc['exp'] += ex
            for e in self.store:
                self.store[e] = 0.0
        # 7) 쓰기: 나이테·진화 → 꼬마 ★ → 공방
        self.grow()
        self.kid_evolve()
        self.start_crafts()
        if self.rush:
            self.do_rush()
        self.place_all()
        un = len(self.decos_owned) - len(self.placed)          # 접속 끝 기준 (라운드 4: 검증 w7_r4와 같은 시점)
        su = self.log.setdefault('unplaced_sess', {})
        su[self.stage] = max(su.get(self.stage, 0), un)
        self.t += s['gap_h']

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
                self.log.setdefault('deco_at', {})[self.stage] = (len(self.decos_owned) + len(self.crafts), sum(self.row(self.stage)['slots']))
                continue
            return

    def frac_day(self):
        return self.sess_i / 3

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

    def place_all(self):
        slots = dict(zip(WT.ZONES, self.row()['slots']))
        for d in sorted(self.T['decos'], key=lambda x: (-x['grade'], x['key'])):
            if d['key'] not in self.decos_owned or d['key'] in self.placed:
                continue
            if sum(1 for k in self.placed if self.deco(k)['zone'] == d['zone']) >= slots[d['zone']]:
                continue
            self.placed.append(d['key'])
            if d['key'] not in self.first:
                self.first.add(d['key'])
                self.myc_b += self.C['decoFirstMycelite']; self.acc['m_first'] += self.C['decoFirstMycelite']
                if self.kids[d['owner']]['owned']:
                    self.kids[d['owner']]['aff'] += self.C['decoFirstAffinity']

    def roll_requests(self):
        owned = [k for k, v in self.kids.items() if v['owned']]
        self.reqs = []
        for i in range(int(self.C['requestPerDay'])):
            kid = owned[(self.day * 3 + i) % len(owned)]
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
            self.kids[r['kid']]['aff'] += self.C['requestAffinity']
            self.week_done += 1; self.acc['req_done'] += 1
        if self.week_done >= self.C['requestWeekGoal'] and not self.week_claimed:
            self.week_claimed = True
            self.myc_b += self.C['requestWeekMycelite']; self.acc['m_week'] += self.C['requestWeekMycelite']

    def gift(self):
        cand = [k for k, v in self.kids.items() if v['owned'] and self.aff_level(k) < len(self.T['aff'])]
        if not cand:
            return
        kid = min(cand, key=lambda k: (self.aff_level(k), self.kids[k]['aff'], k))
        fav = self.kids[kid]['row']['favoriteIds']
        best = max(fav, key=lambda e: self.store[e])
        if self.store[best] >= 1:
            n = self.store[best]; mul = self.C['giftFavoriteMul']
        else:
            best = max(self.store, key=lambda e: self.store[e])
            n = min(self.store[best], self.C['giftMaxCount']); mul = 1
        if n < 1:
            return
        self.store[best] -= n
        self.kids[kid]['aff'] += int(n * self.enemy[best]['value'] * mul)
        self.acc['gift_mush'] += n
        if self.aff_level(kid) >= len(self.T['aff']):
            self.log['aff_max_day'].setdefault(kid, self.day)

    def run(self):
        for dayrec in self.feed:
            self.day = dayrec['day']
            if (self.day - 1) // 7 != self.week:
                self.week, self.week_done, self.week_claimed = (self.day - 1) // 7, 0, False
            self.reset_day_acc()
            self.pull_kids(self.char_frac * dayrec['pulls'])
            self.roll_requests()
            gold_ke = sum(dayrec['gold'].values()) / dayrec['eg']
            myc = sum(dayrec['myc'].values())
            n = len(dayrec['sessions'])
            for i, s in enumerate(dayrec['sessions']):
                self.sess_i = i + 1
                self.session(s, gold_ke, myc, n, i)
            for k in self.kids:
                if self.kids[k]['owned'] and self.aff_level(k) >= len(self.T['aff']):
                    self.log['aff_max_day'].setdefault(k, self.day)
            b = self.bonus()
            self.log['daily'].append(dict(day=self.day, idle_stage=dayrec['stage'], stage=self.stage, ring=self.ring, exp_have=round(self.exp),
                                          gold_ke=gold_ke, myc=myc, kills=sum(s['kills'] for s in dayrec['sessions']),
                                          owned_kids=sum(1 for k in self.kids.values() if k['owned']),
                                          stars={k: v['star'] for k, v in self.kids.items()},
                                          levels={k: v['level'] for k, v in self.kids.items()},
                                          aff={k: self.aff_level(k) for k in self.kids},
                                          decos=len(self.decos_owned), placed=len(self.placed), bag_mult=self.bag_mult(),
                                          bonus=b, gold_b=self.gold_b, myc_b=self.myc_b, **self.acc))
        return self


def run(feed_name, which='v1', **kw):
    feed = json.load(open(os.path.join(HERE, feed_name), encoding='utf-8'))['feed']
    T = WT.load_current()
    if which in ('v1', 'v2', 'v3', 'v4'):
        T = WT.apply(T, {'v1': WT.V1, 'v2': WT.V2, 'v3': WT.V3, 'v4': WT.V4}[which])
    return Tree(T, feed, **kw).run()


def summary(tr, label=''):
    L = tr.log
    D = L['daily']
    out = [f'## {label}']
    out.append('단계 도달(일): ' + ', '.join(f'{s}:{d}' for s, d in sorted(L['stage_day'].items())))
    for lo, hi in [(1, 7), (8, 14), (15, 30), (31, 45), (46, 60)]:
        rows = [r for r in D if lo <= r['day'] <= hi]
        if not rows:
            continue
        g_idle = sum(r['gold_ke'] for r in rows); m_idle = sum(r['myc'] for r in rows)
        f = lambda k: sum(r[k] for r in rows)
        out.append(f'd{lo}-{hi}: 골드 쓰기 {f("g_spend")/g_idle*100:.1f}% (나이테 {f("g_ring")/g_idle*100:.1f} 진화 {f("g_evo")/g_idle*100:.1f} 공방 {f("g_craft")/g_idle*100:.1f}) '
                   f'수입 {(f("g_bag")+f("g_req"))/g_idle*100:.1f}% (보따리 {f("g_bag")/g_idle*100:.1f} 부탁 {f("g_req")/g_idle*100:.1f}) | '
                   f'균사석 쓰기 {f("m_spend")/len(rows):.0f}/일 (진화 {f("m_evo")/len(rows):.0f} ★ {f("m_star")/len(rows):.0f} 공방 {f("m_craft")/len(rows):.0f} 가속 {f("m_rush")/len(rows):.0f}) '
                   f'수입 {(f("m_bag")+f("m_week")+f("m_first"))/len(rows):.0f}/일 (보따리 {f("m_bag")/len(rows):.0f} 주간 {f("m_week")/len(rows):.0f} 첫배치 {f("m_first")/len(rows):.0f}) '
                   f'방치 {m_idle/len(rows):.0f}/일 → 순 {(f("m_spend")-f("m_bag")-f("m_week")-f("m_first"))/m_idle*100:.1f}% | 경험치 {f("exp")/len(rows):.0f}/일 선물 {f("gift_mush")/max(1,f("harvest"))*100:.0f}% 부탁 {f("req_done")/len(rows):.1f}/일')
    last = D[-1]
    out.append('진화 순간 (장식 보유+제작중 / 새 단계 칸): ' + ', '.join(f'{k}:{v[0]}/{v[1]}' for k, v in L.get('deco_at', {}).items()))
    un = {}
    for r in D:
        un[r['stage']] = max(un.get(r['stage'], 0), r['decos'] - r['placed'])
    out.append('칸 없는 장식 최대 (단계: 개, 하루 끝): ' + ', '.join(f'{k}:{v}' for k, v in sorted(un.items())))
    out.append('칸 없는 장식 최대 (단계: 개, 접속 끝): ' + ', '.join(f'{k}:{v}' for k, v in sorted(L.get('unplaced_sess', {}).items())))
    full = next((r['day'] for r in D if r['decos'] >= len(tr.T['decos'])), None)
    allp = next((r['day'] for r in D if r['placed'] >= len(tr.T['decos'])), None)
    out.append(f'장식 24개 다 만듦 {full}일 · 다 놓음 {allp}일')
    out.append('★ 날짜: ' + '; '.join(f'{k} ' + ','.join(f'★{s}:{d}' for s, d in v.items()) for k, v in L['star_day'].items()))
    out.append('보유 날짜: ' + ', '.join(f'{k}:{d}' for k, d in L['own_day'].items()))
    out.append('호감도 최대 날짜: ' + ', '.join(f'{k}:{d}' for k, d in sorted(L['aff_max_day'].items(), key=lambda x: x[1])))
    for dd in (7, 14, 30, 45, 60):
        r = next((x for x in D if x['day'] == dd), None)
        if r:
            bb = {k: round(v, 3) for k, v in r['bonus'].items() if v}
            out.append(f'd{dd}: 단계 {r["stage"]} 장식 {r["decos"]}/{r["placed"]}놓음 보따리×{r["bag_mult"]:.2f} 꼬마Lv {r["levels"]} ★ {r["stars"]} 호감 {r["aff"]} 효과 {bb}')
    return '\n'.join(out)


if __name__ == '__main__':
    feed = sys.argv[1]
    which = sys.argv[2] if len(sys.argv) > 2 else 'v1'
    a = [float(x) for x in sys.argv[3:]]
    kw = {}
    for name, v in zip(['char_frac', 'gold_frac', 'myc_frac', 'gifts', 'rush'], a):
        kw[name] = int(v) if name in ('gifts', 'rush') else v
    tr = run(feed, which, **kw)
    print(summary(tr, f'{feed} {which} {kw}'))
    if os.environ.get('TL'):
        json.dump([dict(day=r['day'], **{k: round(v, 4) for k, v in r['bonus'].items()}) for r in tr.log['daily']],
                  open(os.path.join(HERE, os.environ['TL']), 'w', encoding='utf-8'))
