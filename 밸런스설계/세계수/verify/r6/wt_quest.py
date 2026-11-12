# [11-12] 기록용: 보따리·호감도·의뢰 시트·꼬마 grade가 지워진 지금 표·wt_port에서는 돌지 않는다. 지금 표 재측정은 r8/run_r8.py.
# 라운드 6: 세계수 의뢰(일일·주간)를 wt_port에 더한 독립 포팅 (wt-verify, theory/quest_model.py는 보지 않고 코드에서 옮김)
# 출처 Assets/Scripts/Mobile/WorldTree/WorldTree.Quests.cs:
#   일일 = 의뢰 시트 daily 행에서 하루(KST) treeQuestDaily개 무작위 (:96-110) — 여기서는 날마다 한 행씩 돌아가며 뺀다(각 행 3/4 날, 기대값 같음)
#     날이 바뀌면 다시 뽑는다(받지 않은 진행은 사라짐, :66-70)
#   주간 = weekly 행 전부, 월요일 KST에 새로 (:71-78, WorldTree.cs:127-135) — 1일 = 월요일 가정(주 = 1~7, 8~14 …)
#   진행 (AddQuest :120-134, 목표에서 멈춤):
#     stage   = 보스 등장 (IdleBattle.StartWave :347, 벽에서 막혀도 셈) → feed 접속 'bosses' (progress.py d_boss: 보스까지 간 도전)
#     kill    = 일반 적 처치 (WorldTree.Growth.cs:131 AddHarvest 안 = !e.boss) → feed 'kills' (보스 제외)
#     species = 지정 1종 (arg = 적 시트 무작위) → kills / 적 종 수 (IdleBattle 적 그림 10종 균등)
#     dungeon = 던전 1 판 (HarvestGame.Finish :171 · DungeonRewards.TrySweep :67·:75) → feed 'd1n'
#     daily   = 일일 의뢰를 받은 수 (ClaimQuest → AddDailyDone :150-157)
#   받기 = D.exp += exp (:139-148). 진행이 찬 그 접속에서 받는다(접속 단위). 받는 시점 = 전투 뒤 · 공물 앞
# 앱 밖 진행은 세지 않는다(라이브만) — feed의 처치·도전·판은 모두 접속 중 값이다.
import sys, os, math, json
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.dirname(HERE))
import openpyxl
import wt_port as W
XLSX = os.path.abspath(os.path.join(HERE, '../../../../Assets/Data/Tables/MainTable.xlsx'))


def load_quests():
    wb = openpyxl.load_workbook(XLSX, data_only=True)
    it = list(wb['의뢰'].iter_rows(values_only=True)); H = it[0]
    rows = [dict(zip(H, r)) for r in it[3:] if r[0] is not None]
    per = [r for r in wb['상수'].iter_rows(values_only=True) if r[1] == 'treeQuestDaily'][0][2]
    return rows, int(per)


class WTQ(W.WT):
    def __init__(self, T, feed, quests=True, qrows=None, per_day=None, skip=(), **kw):
        super().__init__(T, feed, **kw)
        self.q_on = quests
        rows, per = load_quests()
        self.qrows = qrows or rows
        self.per_day = per if per_day is None else per_day
        self.skip = set(skip)          # 하지 않는 의뢰 key (민감도)
        self.daily_rows = [r for r in self.qrows if r['period'] == 'daily']
        self.weekly_rows = [r for r in self.qrows if r['period'] == 'weekly']
        self.qlog = dict(daily_shown={}, daily_done={}, weekly_done={}, weekly_days={}, exp_q=0.0, day_q=[])

    def q_new_day(self, di):
        n = len(self.daily_rows)
        drop = [self.daily_rows[(di + j) % n]['key'] for j in range(n - self.per_day)] if self.per_day < n else []
        self.dq = [dict(r=r, prog=0, done=False) for r in self.daily_rows if r['key'] not in drop]
        for q in self.dq: self.qlog['daily_shown'][q['r']['key']] = self.qlog['daily_shown'].get(q['r']['key'], 0) + 1
        if di % 7 == 0:
            self.wq = [dict(r=r, prog=0, done=False, start=di) for r in self.weekly_rows]
        self.q_spec = 0.0

    def q_progress(self, S):
        inc = dict(stage=S['bosses'], kill=S['kills'], species=S['kills'] / len(self.enemies), dungeon=S.get('d1n', 0))
        for q in self.dq + self.wq:
            k = q['r']['kind']
            if k in inc and not q['done']: q['prog'] = min(q['r']['target'], q['prog'] + inc[k])

    def q_claim(self, di, day_f):
        got = 0
        for q in self.dq:
            if not q['done'] and q['r']['key'] not in self.skip and q['prog'] >= q['r']['target'] - 1e-9:
                q['done'] = True; got += q['r']['exp']
                self.qlog['daily_done'][q['r']['key']] = self.qlog['daily_done'].get(q['r']['key'], 0) + 1
                for w in self.wq:
                    if w['r']['kind'] == 'daily' and not w['done']: w['prog'] = min(w['r']['target'], w['prog'] + 1)
        for q in self.wq:
            if not q['done'] and q['r']['key'] not in self.skip and q['prog'] >= q['r']['target'] - 1e-9:
                q['done'] = True; got += q['r']['exp']
                self.qlog['weekly_done'][q['r']['key']] = self.qlog['weekly_done'].get(q['r']['key'], 0) + 1
                self.qlog['weekly_days'].setdefault(q['r']['key'], []).append(round(day_f - q['start'], 2))
        if got:
            self.exp += got; self.acc('exp_q', got); self.qlog['exp_q'] += got

    # WT.run과 같고 의뢰 세 줄(q_new_day · q_progress · q_claim)만 더했다
    def run(self):
        days = self.feed['days']
        kid_order = 0
        prev_eg = days[0]['sessions'][0]['eg']
        for di, D in enumerate(days):
            self.day_acc = {}
            if di % 7 == 0: self.week_done, self.week_claimed = 0, False
            if self.q_on: self.q_new_day(di)
            if self.kid_share > 0: self.kid_pulls(D['pulls'] * self.kid_share)
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
                self.cur_day = day_f      # 라운드 7: 접속 끝 측정용
                elapsed = (self.t - self.last_claim)
                self.login(prev_eg, elapsed); self.last_claim = self.t
                eg = S['eg']
                self.battle(S['kills'])
                if self.q_on: self.q_progress(S); self.q_claim(di, day_f)
                self.t += S['mins'] / 60
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
                                idle_stage=D['stage'], wallet_g=self.gold, wallet_m=self.myc,
                                kid_lv={k: s['level'] for k, s in self.kids.items()}, gacha_lv=self.gacha_lv)
            self.log['day'].append(self.day_acc)
        return self
