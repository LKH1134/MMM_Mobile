# v3d: v3b(D1 목표일 고정·700 이후 지수) + D8(첫 30일 앱 밖 ≤60%: 퀘스트·던전 골드) + D9(첫 7일 하루별 표)
#   python run_v3d.py <이름> <ads> <days> [설정 json 경로]
#   설정 키 (없으면 기본값 CFG):
#     wall: 챕터 보스 배수, quest_gold: 퀘스트 시트 goldKills 5개, dungeon_mul: 던전 rewardKills 배수,
#     sweep: 소탕 배수, cost_mul: 강화 비용(price 열) 배수, unlock: 던전 unlockStage(=baseStage) 5개,
#     gacha_exp: 뽑기 expToNext 9개 (10번째 0)
#   결과: out_v3d_<이름>_<ads>.json (+ ads 0.5면 stage_<이름>.csv)
# 난수 없음 — 같은 설정이면 같은 결과.
import sys, json, csv, math
import proposal as PR, progress as P, dungeon, export
import gen_stage as G
import formulas as F
from run_v3b import DAY

CFG = dict(wall=1.4, quest_gold=[200, 300, 200, 300, 600], dungeon_mul=1.0, sweep=0.5, cost_mul=1.0,
           unlock=[5, 15, 30, 80, 150], step=3, gacha_exp=[g['exp'] for g in PR.GACHA_V3][:9])
GRADES = ['일반', '고급', '희귀', '영웅', '전설']
UPG_KO = {'Speed': '공속', 'CritChance': '치명 확률', 'CritDamage': '치명 피해', 'AdvancedAttack': '상급 공격력',
          'NormalEnemyDamage': '일반 적 피해', 'HpRegen': '체력 재생'}


def target_stage(d):
    """D1 목표 곡선(run_v3b.DAY)에서 d일에 있어야 할 스테이지"""
    best = 1
    for s, dd in DAY.items():
        if dd <= d:
            best = max(best, s)
    return best


def setup(cfg):
    for i, d in dungeon.DUNGEONS.items():
        d['rewardKills'] = d.setdefault('_rk', d['rewardKills']) * cfg['dungeon_mul']
        d['baseStage'] = cfg.get('base', cfg['unlock'])[i - 1]
        d['stageStep'] = cfg['step']
    dungeon.SWEEP_MUL = cfg['sweep']
    for i, u in enumerate(cfg['unlock']):
        F.T.dungeon[i + 1] = u
    for i, e in enumerate(cfg['gacha_exp']):
        PR.GACHA_V3[i]['exp'] = e
    rows = PR.apply({'check_step': 20, 'atk_hp_gear': False, 'v3': True, 'day_table': DAY, 'wall_mul': cfg['wall']},
                    farm=PR.V3['farm'], extras=True)
    shape = cfg.get('gold_shape')
    if shape:      # D9: 스테이지 시트 enemyGold·bossGold × f(s) (로그 보간, 표 끝까지 마지막 값) — 첫날은 그대로, 2일차 이후 속도를 목표 곡선에
        def f(x):
            if x <= shape[0][0]:
                return shape[0][1]
            for (a, fa), (b, fb) in zip(shape, shape[1:]):
                if x <= b:
                    return math.exp(math.log(fa) + (math.log(fb) - math.log(fa)) * (x - a) / (b - a))
            return shape[-1][1]
        bad = 0
        for x in sorted(rows):
            rows[x]['gold'] = max(1, round(rows[x]['gold'] * f(x)))
            if x > 1 and rows[x]['gold'] <= rows[x - 1]['gold']:
                rows[x]['gold'] = rows[x - 1]['gold'] + 1; bad += 1
            rows[x]['bossGold'] = rows[x]['gold'] * 20
        G.PARAMS['_shape_mono_fix'] = bad
    F.OV['quest_gold'] = list(cfg['quest_gold'])
    F.OV['cost_mul'] = cfg['cost_mul']
    return rows


def grade_open(level):
    odds = PR.GACHA_V3[min(level, len(PR.GACHA_V3)) - 1]['odds']
    return max(i for i, o in enumerate(odds) if o > 0)


def exp_copies(sim, g):
    return sum(sum(sim.gear.copies[k][g * 3:(g + 1) * 3]) for k in P.Gear.KINDS)


def stage_play_in_day(sim, a0, a1):
    """[a0, a1) 플레이 초 안에서 스테이지마다 머문 플레이 분 → (가장 긴 스테이지, 분)"""
    st = sorted(sim.arrive)
    best = (sim.stage, 0.0)
    for i, s in enumerate(st):
        lo = sim.arrive[s][0]
        hi = sim.arrive[st[i + 1]][0] if i + 1 < len(st) else a1
        m = (min(hi, a1) - max(lo, a0)) / 60
        if m > best[1]:
            best = (s, round(m, 1))
    return best


def calendar_wall(sim, s):
    """스테이지 s에 머문 달력 일 (아직이면 지금까지)"""
    a = sim.arrive.get(s)
    b = sim.arrive.get(s + 1)
    if a is None:
        return 0.0
    return round(((b[1] if b else sim.wall) - a[1]) / 86400, 2)


def day_table(sim, days=8):
    rows = []
    upg_open = {st: False for st in UPG_KO}
    gl = {k: 1 for k in P.Gear.KINDS}
    seen_grade = 0
    seen_lv = 1
    first_epic = first_legend = False
    for d in range(days):
        s0, a0 = sim.stage, sim.active
        src0, myc0 = dict(sim.src), dict(sim.myc_src)
        box0, dun0, q0 = getattr(sim, 'n_box', 0), getattr(sim, 'n_dun', 0), sum(sim.myc_src.values())
        cnt0 = dict(getattr(sim, 'dcount', {'try': 0, 'fail': 0, 'sweep': 0}))
        sim.run_days(1)
        new = []
        for i, u in enumerate(F.T.dungeon.values()):
            if s0 < u <= sim.stage or (d == 0 and u <= sim.stage):
                new.append(f'던전{i + 1}(s{u})')
        for k in P.Gear.KINDS:
            if sim.gear.level[k] != gl[k]:
                gl[k] = sim.gear.level[k]
        lvmin, lvmax = min(gl.values()), max(gl.values())
        top = grade_open(lvmax)
        if top > seen_grade:
            new.append(f'뽑기 Lv{lvmax}: {GRADES[top]} 등급')
            seen_grade = top
        elif lvmax > seen_lv:
            new.append(f'뽑기 Lv{lvmax}')
        seen_lv = lvmax
        for st in UPG_KO:
            if not upg_open[st] and P.can_buy(sim.p, st) or (not upg_open[st] and sim.p.lv[st] > 0):
                upg_open[st] = True
                new.append(f'강화 {UPG_KO[st]}')
        if not first_epic and exp_copies(sim, 3) >= math.log(2):
            first_epic = True; new.append('첫 영웅(기대 50%)')
        if not first_legend and exp_copies(sim, 4) >= math.log(2):
            first_legend = True; new.append('첫 전설(기대 50%)')
        ws, wm = stage_play_in_day(sim, a0, sim.active)
        dsrc = {k: sim.src[k] - src0[k] for k in sim.src}
        tot = sum(dsrc.values()) or 1
        dmyc = {k: round(sim.myc_src[k] - myc0[k]) for k in sim.myc_src}
        cnt = getattr(sim, 'dcount', cnt0)
        q = sim.quest_log[-1][2]
        rows.append(dict(day=d, stage=sim.stage, target=target_stage(d + 1), gained=sim.stage - s0,
                         wall_stage=ws, wall_play_min=wm, wall_cal_days=calendar_wall(sim, ws), new=new,
                         quests=sum(q), dungeon_entries=getattr(sim, 'n_dun', 0) - dun0,
                         dungeon_clear=cnt['try'] - cnt0.get('try', 0) - (cnt['fail'] - cnt0.get('fail', 0)),
                         boxes=getattr(sim, 'n_box', 0) - box0,
                         gacha_level=f'{lvmin}~{lvmax}', pulls=sum(sim.gear.pulls.values()), d_lv_stage={k: dungeon.ref_stage(k, v) for k, v in sim.dlevel.items()},
                         gold_pct={k: round(v / tot * 100, 1) for k, v in dsrc.items()}, myc=dmyc,
                         dlevel=dict(sim.dlevel)))
    return rows


if __name__ == '__main__':
    name, ads, days = sys.argv[1], float(sys.argv[2]), int(sys.argv[3])
    cfg = dict(CFG)
    if len(sys.argv) > 4:
        cfg.update(json.load(open(sys.argv[4], encoding='utf-8')))
    rows = setup(cfg)
    sim = P.Sim(pattern='daily', ads=ads); sim.dungeon = dungeon; sim.dungeon_after_play = True
    stage_by_day, src_by_day = [], []
    table = day_table(sim, 8)
    for r in table:
        stage_by_day.append(r['stage'])
    # day_table은 run_days(1)을 8번 불렀다 — src 누적은 sim.src에 그대로
    src_by_day = [None] * 7 + [dict(sim.src)]
    for d in range(8, days):
        sim.run_days(1)
        stage_by_day.append(sim.stage)
        src_by_day.append(dict(sim.src))

    def share(day):
        s = src_by_day[min(day, len(src_by_day)) - 1]
        t = sum(s.values()) or 1
        out = {k: round(v / t * 100, 1) for k, v in s.items()}
        out['offline_all'] = round(out['offline'] + out['offline_ad'], 1)
        return out

    def longest_zero(lim):
        b = run_ = 0
        for a, c in zip(stage_by_day, stage_by_day[1:]):
            if a > lim:
                break
            run_ = run_ + 1 if a == c else 0
            b = max(b, run_)
        return b

    MARKS = [10, 50, 100, 150, 200, 250, 300, 400, 500, 600, 700, 800, 900, 1000]
    dev = [(d + 1, s, target_stage(d + 1), round(s / max(1, target_stage(d + 1)) - 1, 3)) for d, s in enumerate(stage_by_day[:60])]
    out = dict(name=name, ads=ads, days=days, cfg=cfg, table=table, stage=sim.stage, first_use_h={k: round(v / 3600, 2) for k, v in sim.first.items()},
               shape_mono_fix=G.PARAMS.get('_shape_mono_fix'),
               arrive={s: round(sim.arrive[s][1] / 86400, 2) for s in MARKS if s in sim.arrive},
               target={s: round(DAY[s], 2) for s in MARKS},
               share30=share(30) if days >= 30 else None, share_end=share(days),
               dev_max=max((abs(x[3]), x) for x in dev if x[0] <= min(days, 60) and x[2] <= 700),
               dev7=[x for x in dev if x[0] <= 8],
               longest_zero_900=longest_zero(900), longest_zero_all=longest_zero(10 ** 9),
               myc_per_day={k: round(v / days, 1) for k, v in sim.myc_src.items()},
               dungeon=getattr(sim, 'dcount', {}), dlevel=dict(sim.dlevel), tail=G.PARAMS['_tail'],
               stage_by_day=stage_by_day)
    json.dump(out, open(f'out_v3d_{name}_{ads}.json', 'w', encoding='utf-8'), indent=1, ensure_ascii=False, default=str)
    if ads == 0.5:
        with open(f'stage_{name}.csv', 'w', newline='', encoding='utf-8') as f:
            w = csv.writer(f); w.writerow([c for c, _ in export.COLS])
            for s in sorted(rows):
                r = dict(rows[s], stage=s)
                w.writerow([('%.6g' % r[k]) if isinstance(r[k], float) else r[k] for _, k in export.COLS])
    print(name, ads, 'stage', sim.stage, 'arrive', out['arrive'], 'share30', out['share30'], 'share_end', out['share_end'],
          'dev_max', out['dev_max'], 'first', out['first_use_h'], 'share300', out['share_end'] if days >= 300 else '', 'zero900', out['longest_zero_900'], 'dun', out['dungeon'], flush=True)
    for r in table:
        print(' D%d s%d(목표 %d) +%d 벽 s%s %s분/%s일 | %s | 퀘 %d 던전 %d(클 %d) 상자 %d 뽑기Lv %s | 골드 %s | 균사석 %s' % (
            r['day'], r['stage'], r['target'], r['gained'], r['wall_stage'], r['wall_play_min'], r['wall_cal_days'],
            ', '.join(r['new']), r['quests'], r['dungeon_entries'], r['dungeon_clear'], r['boxes'], r['gacha_level'] + ' 누적%d뽑 던전s%s' % (r['pulls'], r['d_lv_stage']),
            {k: v for k, v in r['gold_pct'].items() if v >= 1}, {k: v for k, v in r['myc'].items() if v}), flush=True)
