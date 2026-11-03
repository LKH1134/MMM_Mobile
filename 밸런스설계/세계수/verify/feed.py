# 방치 입력 (wt-verify 독립 래퍼, 라운드 4 = KHD 방치 밸런스 v3 머지 뒤).
# KHD 방치 진행 모델(밸런스설계/theory/progress.py Sim · battle.py · dungeon.py, 읽기만)을 돌리되
# 입력은 KHD가 만든 행(run_v3d.setup → gen_stage)이 아니라 **머지된 라이브 표**(GameBalance.xlsx)에서 직접 만든다:
#   스테이지 1~700 = 스테이지 시트 그대로, 701~ = IdleProgress.cs 꼬리 공식(:306-372, TailRow = 699, growth^(s−699),
#     챕터 보스 ×tailWallMul, 보스 균사석 = 표 마지막 값), 업그레이드 Power 모드 = 시트 valueExponent·valueDivisor,
#   광고 뽑기 = 뽑기상품 시트 광고 행(requiresAd) pullCount·dailyLimit (상수 adGachaPulls는 43c2308에서 지워짐),
#   던전 = 던전 시트 unlockStage·baseStage·stageStep·rewardKills·rewardMyc·rewardGrowth, 소탕 = 상수 dungeonSweepMul (DungeonRewards.cs),
#   퀘스트 = 퀘스트 시트(목표·goldKills·mycelite), 보스 퀘스트는 보스 등장 때 센다(IdleBattle.cs:346 → quest_stage_boss),
#   편성 = 꼬마 시트 defaultSlot 0~4의 attackMultiplier, 그 밖의 상수(앱 밖 12h·0.5, 광고 상자 4000 등) = 상수 시트.
# 하루 루프는 progress.py Sim.run_days(:288-318, v3d = dungeon_after_play)를 그대로 옮기고 기록만 더했다.
# python feed.py v3 <days> <ads> [시간배수] [tl=<timeline.json> | wt] [d1]   (d1 = 던전 1만 — 지금 게임에 씬이 있는 것)
import sys, os, json, math, copy
sys.stdout.reconfigure(encoding='utf-8')
sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
TH = os.path.abspath(os.path.join(HERE, '../../theory'))
sys.path.insert(0, TH)
import openpyxl
import formulas as F, progress as P, battle as B, dungeon as DG

XLSX = os.path.abspath(os.path.join(HERE, '../../../Assets/Data/Tables/GameBalance.xlsx'))


def sheet(wb, name):
    it = list(wb[name].iter_rows(values_only=True))
    H = it[0]
    return [dict(zip(H, r)) for r in it[3:] if r[0] is not None]


def ad_gacha(wb):
    """뽑기상품 시트 광고 행: 종류별 (pullCount, dailyLimit). GachaPanel.cs가 requiresAd 행을 광고 버튼으로 쓴다"""
    out = {}
    for r in sheet(wb, '뽑기상품'):
        if r['requiresAd']:
            out[r['kind']] = (int(r['pullCount']), int(r['dailyLimit']))
    return out


def setup_live(d1_only=False):
    wb = openpyxl.load_workbook(XLSX, data_only=True)
    C = {r['key']: float(r['value']) for r in sheet(wb, '상수')}
    st = {int(r['stage']): r for r in sheet(wb, '스테이지')}
    rows = {}
    for s, r in st.items():
        rows[s] = dict(waves=int(r['wavesPerStage']), bossTime=float(r['bossTime']), cb=int(r['enemyCountBase']),
                       cw=int(r['enemyCountPerWave']), cs=int(r['enemyCountPerStage']), cmax=int(r['enemyCountMax']),
                       gold=int(r['enemyGold']), bossGold=int(r['bossGold']), myc=int(r['bossMycelite']),
                       atk=float(r['enemyAttack']), bossAtk=float(r['bossAttack']), ehp=float(r['enemyHp']), bhp=float(r['bossHp']))
    end = max(rows)
    ts = end - 1 if end % 10 == 0 else end          # IdleProgress.cs:326 TailStage
    T0 = rows[ts]
    cache = {}

    def stage_fn(s):
        if s <= end:
            return rows.get(s)
        if s not in cache:
            n = s - ts
            r = dict(T0)
            r['ehp'] = T0['ehp'] * C['tailEnemyHpGrowth'] ** n
            r['bhp'] = T0['bhp'] * C['tailBossHpGrowth'] ** n * (C['tailWallMul'] if s % 10 == 0 else 1)
            r['atk'] = T0['atk'] * C['tailAttackGrowth'] ** n
            r['bossAtk'] = T0['bossAtk'] * C['tailAttackGrowth'] ** n
            r['gold'] = max(1, min(9e18, T0['gold'] * C['tailGoldGrowth'] ** n))
            r['bossGold'] = min(9e18, r['gold'] * C['bossGoldMul'])      # IdleProgress.BossGold: EnemyGold × bossGoldMul
            r['myc'] = T0['myc']
            cache[s] = r
        return cache[s]
    up = {}
    for r in sheet(wb, '업그레이드'):
        if r['valueMode'] == 'Power':
            up[r['stat']] = dict(vexp=float(r['valueExponent']), vdiv=float(r['valueDivisor']))
    kids = sorted([r for r in sheet(wb, '꼬마') if int(r['defaultSlot']) >= 0], key=lambda r: int(r['defaultSlot']))
    form = [float(r['attackMultiplier']) for r in kids]
    ag = ad_gacha(wb)
    pulls = {k: v[0] for k, v in ag.items()}
    const = dict(adGachaPulls=pulls.get('Weapon', 0))   # progress.py가 읽는 이름 (종류마다 같은 값 — 아래에서 확인)
    assert len(set(pulls.values())) == 1, pulls
    F.set_overrides({'upgrade': up, 'const': const, 'quest_stage_boss': True, 'formation': form, 'stage_fn': stage_fn})
    for r in sheet(wb, '던전'):
        dn = int(r['dungeon'])
        DG.DUNGEONS[dn].update(baseStage=int(r['baseStage']), stageStep=int(r['stageStep']), rewardKills=float(r['rewardKills']),
                               rewardMyc=float(r['rewardMyc']), rewardGrowth=float(r['rewardGrowth']))
        F.T.dungeon[dn] = int(r['unlockStage'])
    DG.SWEEP_MUL = C['dungeonSweepMul']
    if d1_only:
        DG.KINDS = {1}
    return dict(ad_gacha=ag, form=form, end=end, tail_stage=ts)


if __name__ == '__main__':
    base, days, ads = sys.argv[1], int(sys.argv[2]), float(sys.argv[3])
    scale = float(sys.argv[4]) if len(sys.argv) > 4 else 1.0
    extra = sys.argv[5:]
    tlarg = next((a for a in extra if a.startswith('tl=')), None)
    wt = 'wt' in extra
    d1 = 'd1' in extra
    if base != 'v3':
        raise SystemExit('라운드 4부터 base는 v3(머지된 라이브 표)만. 라운드 3 이전 래퍼는 r3/feed_r3.py')
    info = setup_live(d1)
    tl = json.load(open(os.path.join(HERE, tlarg[3:]), encoding='utf-8')) if tlarg else None
    CUR = {'A': 1.0, 'H': 1.0, 'DCC': 0.0, 'DCD': 0.0, 'G': 1.0, 'BM': 1.0}
    if wt:   # 세계수 효과 최대 합(표, run_cases max_totals)을 1일부터 (W4 상한)
        import tables, wt_port as W
        M = W.max_totals(tables.load())
        CUR.update(A=1 + M['Attack'], H=1 + M['Hp'], DCC=M['CritChance'], DCD=M['CritDamage'], G=1 + M['KillGold'], BM=1 + M['BossMycelite'])
    if tl or wt:   # 세계수 Mul 적용 지점: IdleProgress.cs:183-188 (FinalAttack·FinalHp·FinalHpRegen·FinalCrit*), IdleBattle.cs:399 (보스 균사석)·:599 (처치 골드)
        fa0, mh0, rg0 = B.Player.final_attack, B.Player.max_hp, B.Player.regen
        B.Player.final_attack = lambda self: fa0(self) * CUR['A']
        B.Player.max_hp = lambda self: mh0(self) * CUR['H']
        B.Player.regen = lambda self: rg0(self) * CUR['H']
        B.Player.crit_factor = lambda self: 1 + min(1, self.v('CritChance') + CUR['DCC']) * (self.v('CritDamage') + CUR['DCD'] - 1)
        B.Player.key = (lambda k0: lambda self: (k0(self), CUR['A'], CUR['H'], CUR['DCC'], CUR['DCD']))(B.Player.key)
        at0 = P.Sim.attempt

        def at(self):
            r = copy.copy(at0(self)); r.gold *= CUR['G']; return r
        P.Sim.attempt = at
        bm0 = F.boss_myc
        F.boss_myc = lambda s: bm0(s) * CUR['BM']
    sessions = [(m * scale, g) for m, g in [(30, 4.0), (20, 5.0), (40, 15.0)]]
    sim = P.Sim(pattern='daily', ads=ads, sessions=sessions)
    sim.dungeon = DG; sim.dungeon_after_play = True
    ag = info['ad_gacha']
    order = ['Weapon', 'Pet', 'Accessory', 'Character']
    out = dict(base=base, ads=ads, scale=scale, d1=d1, sessions=sim.sessions, ad_gacha=ag, form=info['form'], days=[])
    sim.quest_log = []
    for day in range(days):
        if tl and day > 0:
            e = tl[day - 1]
            CUR.update(A=1 + e['Attack'], H=1 + e['Hp'], DCC=e['CritChance'], DCD=e['CritDamage'], G=1 + e['KillGold'],
                       BM=1 + e.get('BossMycelite', 0))
        src0 = dict(sim.src); myc0 = dict(sim.myc_src)
        # ── progress.py:290-298 (광고 뽑기: 종류마다 하루 dailyLimit번 × 광고 비율, 공격 쪽부터) ──
        sim.day_counters()
        n_watch = int(round(sum(ag[k][1] for k in order) * sim.ads))
        for i in range(n_watch):
            k = order[i % len(order)]
            sim.first.setdefault('gacha', sim.wall)
            sim.gear.pull(k, ag[k][0]); sim.d_pulls += ag[k][0]; sim.d_ads += 1
        sim.sync_gear()
        sim.d_dun = {}
        sess = []
        for i, (mins, gap_h) in enumerate(sim.sessions):
            k0, b0, s0 = sim.d_kills, sim.d_boss, sim.stage   # 라운드 5: 창고 처치 = 보스 처치(= 돌파 수, battle.py:125-128) 제외 — IdleBattle.cs:393 if (!e.boss) AddHarvest
            if sim.dungeon: sim.run_dungeons()
            g = sim.play(mins * 60)
            if sim.dungeon and sim.dungeon_after_play: sim.run_dungeons()
            sim.adbox(mins * 60)
            q = sim.quests()
            sim.spend_myc()
            if i == len(sim.sessions) - 1: sim.quest_log.append((day + 1, sim.stage, q))
            off = g / (mins * 60) * min(gap_h, F.C('offlineCapHours')) * 3600 * F.C('offlineGoldRate')
            sim.gold += off; sim.src['offline'] += off
            if sim.watch('report'):
                sim.gold += off; sim.src['offline_ad'] += off
            sim.wall += gap_h * 3600
            sim.buy()
            sess.append(dict(mins=mins, gap=gap_h, kills=(sim.d_kills - k0) - (sim.stage - s0), kills_all=sim.d_kills - k0, bosses=sim.d_boss - b0,
                             stage0=s0, stage=sim.stage, eg=F.enemy_gold(sim.stage), bmyc=F.boss_myc(sim.stage)))
        gold = {k: sim.src[k] - src0[k] for k in sim.src}
        myc = {k: sim.myc_src[k] - myc0[k] for k in sim.myc_src}
        eg = F.enemy_gold(sim.stage)
        out['days'].append(dict(day=day + 1, stage=sim.stage, eg=eg, sessions=sess, kills=sim.d_kills, bosses=sim.d_boss,
                                gold=gold, gold_total=sum(gold.values()), gold_kills=sum(gold.values()) / eg,
                                myc=myc, myc_total=sum(myc.values()), pulls=sim.d_pulls, quests=sum(q),
                                cc=sim.p.v('CritChance'), cd=sim.p.v('CritDamage'), spd=sim.p.v('Speed'),
                                atk_lv=sim.p.lv['Attack'], gacha_lv=dict(sim.gear.level), dlevel=dict(sim.dlevel)))
        d = out['days'][-1]
        print(day + 1, sim.stage, 'kills', d['kills'], 'goldKE %.0f' % d['gold_kills'], 'myc %.0f' % d['myc_total'], 'pulls', d['pulls'],
              'off%% %.0f dun%% %.0f' % ((gold['offline'] + gold['offline_ad']) / d['gold_total'] * 100, gold['dungeon'] / d['gold_total'] * 100), flush=True)
    name = f'feed_v3_a{ads}' + ('' if scale == 1 else f'_t{scale}') + ('_d1' if d1 else '') + ('_wt' if wt else '') + \
           (('_tl' + os.path.basename(tlarg[3:])[9:-5]) if tlarg else '') + '.json'
    # 라운드 4 결과는 r4/ (창고 처치에 보스 포함), 라운드 5부터 r5/ (환경 변수 FEED_OUT으로 바꿀 수 있음)
    json.dump(out, open(os.path.join(HERE, os.environ.get('FEED_OUT', 'r5'), name), 'w', encoding='utf-8'), indent=0)
