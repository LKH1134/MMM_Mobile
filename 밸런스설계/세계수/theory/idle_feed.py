# 방치 진행 → 세계수 입력 (하루 단위), 라운드 4 = KHD 방치 밸런스 v3 머지(c53ecf2) 뒤.
# KHD 방치 모델(밸런스설계/theory: formulas·progress·battle·dungeon)을 읽기 전용으로 import해 돌리고,
# 입력은 **라이브 표**(Assets/Data/Tables/GameBalance.xlsx)에서 직접 만든다 (KHD run_v3d.setup → gen_stage는 옛 1000행 전제라 쓰지 않음):
#   스테이지 1~700 = 스테이지 시트 그대로 (enemyHp·bossHp 포함),
#   701~ = IdleProgress.cs 꼬리: TailStage = 표 끝(10의 배수면 그 앞 = 699), 값 × growth^(s−TailStage),
#          챕터 보스(10의 배수) bossHp × tailWallMul, EnemyGold ClampToLong, BossGold = EnemyGold × bossGoldMul, BossMycelite = 표 끝 값,
#   업그레이드 valueMode Power = valueExponent·valueDivisor,
#   광고 뽑기 = 뽑기상품 시트 requiresAd 행의 pullCount × dailyLimit (상수 adGachaPulls는 43c2308에서 지워짐),
#   던전 = 던전 시트(unlockStage·baseStage·stageStep·rewardKills·rewardMyc·rewardGrowth) + 상수 dungeonSweepMul,
#   보스 퀘스트 = 보스 등장 때 셈 (IdleBattle.cs:346), 편성 = 꼬마 시트 defaultSlot 0~4 attackMultiplier.
# 하루 루프 = progress.py Sim.run_days (v3d: dungeon_after_play) + 접속별 기록. 처치 수는 보스 제외(창고 AddHarvest는 !e.boss).
#   python idle_feed.py v3 60            → feed_v3.json        (보통: 광고 0.5, 하루 90분)
#   python idle_feed.py v3 60 0.0        → feed_v3_a0.json     (무광고)
#   python idle_feed.py v3 60 1.0        → feed_v3_a1.json     (광고 많이)
#   python idle_feed.py v3 60 0.5 0.5    → feed_v3_t0.5.json   (플레이 시간 ×0.5)
#   python idle_feed.py v3 60 0.5 1 d1   → feed_v3_d1.json     (던전 1만 — 지금 게임에 씬이 있는 것)
# 난수 없음 → 다시 돌리면 같은 결과.
import json, os, sys, time
sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
KHD = os.path.join(ROOT, '밸런스설계', 'theory')
sys.path.insert(0, KHD)
import openpyxl
import formulas as F
import progress as P
import dungeon as DG

XLSX = os.path.join(ROOT, 'Assets', 'Data', 'Tables', 'GameBalance.xlsx')
KINDS = ['Weapon', 'Pet', 'Accessory', 'Character']


def _sheet(wb, name):
    rows = list(wb[name].iter_rows(values_only=True))
    hdr = rows[0]
    return [dict(zip(hdr, r)) for r in rows[3:] if r[0] is not None]


def setup_live(d1_only=False):
    wb = openpyxl.load_workbook(XLSX, data_only=True)
    C = {r['key']: float(r['value']) for r in _sheet(wb, '상수')}
    rows = {}
    for r in _sheet(wb, '스테이지'):
        rows[int(r['stage'])] = dict(waves=int(r['wavesPerStage']), bossTime=float(r['bossTime']), cb=int(r['enemyCountBase']),
                                     cw=int(r['enemyCountPerWave']), cs=int(r['enemyCountPerStage']), cmax=int(r['enemyCountMax']),
                                     gold=int(r['enemyGold']), bossGold=int(r['bossGold']), myc=int(r['bossMycelite']),
                                     atk=float(r['enemyAttack']), bossAtk=float(r['bossAttack']),
                                     ehp=float(r['enemyHp']), bhp=float(r['bossHp']))
    end = max(rows)
    ts = end - 1 if end % 10 == 0 else end
    T0, cache = rows[ts], {}

    def stage_fn(s):
        if s <= end:
            return rows[s]
        r = cache.get(s)
        if r is None:
            n = s - ts
            r = dict(T0)
            r['ehp'] = T0['ehp'] * C['tailEnemyHpGrowth'] ** n
            r['bhp'] = T0['bhp'] * C['tailBossHpGrowth'] ** n * (C['tailWallMul'] if s % 10 == 0 else 1)
            r['atk'] = T0['atk'] * C['tailAttackGrowth'] ** n
            r['bossAtk'] = T0['bossAtk'] * C['tailAttackGrowth'] ** n
            r['gold'] = max(1.0, min(9e18, T0['gold'] * C['tailGoldGrowth'] ** n))
            r['bossGold'] = min(9e18, r['gold'] * C['bossGoldMul'])
            r['myc'] = T0['myc']
            cache[s] = r
        return r

    up = {r['stat']: dict(vexp=float(r['valueExponent']), vdiv=float(r['valueDivisor']))
          for r in _sheet(wb, '업그레이드') if r['valueMode'] == 'Power'}
    form = [float(r['attackMultiplier']) for r in sorted((r for r in _sheet(wb, '꼬마') if int(r['defaultSlot']) >= 0),
                                                         key=lambda r: int(r['defaultSlot']))]
    ad = {r['kind']: (int(r['pullCount']), int(r['dailyLimit'])) for r in _sheet(wb, '뽑기상품') if r['requiresAd']}
    F.set_overrides({'upgrade': up, 'quest_stage_boss': True, 'formation': form, 'stage_fn': stage_fn,
                     'const': {'adGachaPulls': 0}})          # progress.py 광고 뽑기는 아래 run_days에서 표 값으로 대신한다
    for r in _sheet(wb, '던전'):
        dn = int(r['dungeon'])
        DG.DUNGEONS[dn].update(baseStage=int(r['baseStage']), stageStep=int(r['stageStep']), rewardKills=float(r['rewardKills']),
                               rewardMyc=float(r['rewardMyc']), rewardGrowth=float(r['rewardGrowth']))
        F.T.dungeon[dn] = int(r['unlockStage'])
    DG.SWEEP_MUL = C['dungeonSweepMul']
    DG.KINDS = {1} if d1_only else set(DG.DUNGEONS)
    return dict(ad=ad, end=end, tail_stage=ts, offline_h=C['offlineCapHours'], offline_rate=C['offlineGoldRate'])


class FeedSim(P.Sim):
    """KHD Sim + 접속별 기록. 게임 규칙은 progress.py 그대로, 광고 뽑기만 뽑기상품 시트 값"""

    def run_days(self, days, ad):
        self.quest_log, self.feed = [], []
        self.dungeon_after_play = True
        for day in range(days):
            self.day_counters()
            g0, m0 = dict(self.src), dict(self.myc_src)
            pulls0 = sum(self.gear.pulls.values())
            n_watch = int(round(sum(ad[k][1] for k in KINDS) * self.ads))       # 광고 뽑기: 종류마다 dailyLimit번 × 광고 비율 (공격 쪽부터)
            for i in range(n_watch):
                k = KINDS[i % 4]
                self.first.setdefault('gacha', self.wall)
                self.gear.pull(k, ad[k][0]); self.d_pulls += ad[k][0]; self.d_ads += 1
            self.sync_gear()
            self.d_dun = {}
            sess = []
            for i, (mins, gap_h) in enumerate(self.sessions):
                s0, k0, st0 = self.stage, self.d_kills, self.d_stages
                if self.dungeon:
                    self.run_dungeons()
                g = self.play(mins * 60)
                if self.dungeon:
                    self.run_dungeons()
                self.adbox(mins * 60)
                q = self.quests()
                self.spend_myc()
                if i == len(self.sessions) - 1:
                    self.quest_log.append((day + 1, self.stage, q))
                off = g / (mins * 60) * min(gap_h, F.C('offlineCapHours')) * 3600 * F.C('offlineGoldRate')
                self.gold += off; self.src['offline'] += off
                if self.watch('report'):
                    self.gold += off; self.src['offline_ad'] += off
                kills = (self.d_kills - k0) - (self.d_stages - st0)      # 보스 처치(= 돌파 수) 제외
                sess.append(dict(mins=mins, gap_h=gap_h, stage_start=s0, stage_end=self.stage,
                                 eg=F.enemy_gold(self.stage), kills=kills, battle_gold=g))
                self.wall += gap_h * 3600
                self.buy()
            self.feed.append(dict(day=day + 1, stage=self.stage, eg=F.enemy_gold(self.stage), boss_myc=F.boss_myc(self.stage),
                                  sessions=sess, gold={k: self.src[k] - g0[k] for k in self.src},
                                  myc={k: self.myc_src[k] - m0[k] for k in self.myc_src},
                                  pulls=sum(self.gear.pulls.values()) - pulls0))
        return self


if __name__ == '__main__':
    which = sys.argv[1]
    if which != 'v3':
        raise SystemExit('라운드 4부터는 v3(머지된 라이브 표)만. 옛 live/v2 feed_*.json은 보관용')
    days = int(sys.argv[2]) if len(sys.argv) > 2 else 60
    ads = float(sys.argv[3]) if len(sys.argv) > 3 else 0.5
    scale = float(sys.argv[4]) if len(sys.argv) > 4 else 1.0
    d1 = 'd1' in sys.argv[5:]
    t0 = time.time()
    info = setup_live(d1)
    sim = FeedSim(pattern='daily', ads=ads, sessions=[(m * scale, g) for m, g in [(30, 4.0), (20, 5.0), (40, 15.0)]])
    sim.dungeon = DG
    sim.run_days(days, info['ad'])
    name = 'feed_v3' + ('' if ads == 0.5 else f'_a{ads:g}') + ('' if scale == 1 else f'_t{scale:g}') + ('_d1' if d1 else '') + \
           ('' if days == 60 else f'_{days}') + '.json'
    json.dump(dict(which=which, ads=ads, scale=scale, d1=d1, days=days, ad_gacha=info['ad'], table_end=info['end'],
                   feed=sim.feed), open(os.path.join(HERE, name), 'w', encoding='utf-8'), indent=0)
    print(which, ads, scale, 'd1' if d1 else '', 'stage', sim.stage, 'secs', round(time.time() - t0, 1), '→', name)
