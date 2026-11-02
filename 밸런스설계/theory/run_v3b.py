# v3b: 스테이지 생성 기준일을 D1 '빠름'(priceScale ×150) 목표 곡선에 고정 (측정 도착일로 갱신하지 않는다 — 되먹임 끊기).
#   보통 목표: 300 = 4.3일, 400 = 9.6, 500 = 14.9, 700 = 34.4, 1000 = 274 (이론.md 6.6 표, 그 사이 로그 보간)
#   300 이전: v3 반복에서 잰 보통 도착(100 = 0.43, 200 = 2.13)을 선형 보간 (D1 표에 없는 구간 — 가정)
#   python run_v3b.py <이름> <wall_mul> <ads> <days>  → out_v3b_<이름>_<ads>.json (+ ads 0.5면 stage_<이름>.csv)
import sys, json, math, csv
import proposal as PR, progress as P, dungeon, export
import gen_stage as G
import formulas as F

ANCHOR_LIN = [(6, 0.0), (100, 0.43), (200, 2.13), (300, 4.3)]
ANCHOR_LOG = [(300, 4.3), (400, 9.6), (500, 14.9), (700, 34.4), (1000, 274.0)]


def d1_day(s):
    if s <= 300:
        for (a, da), (b, db) in zip(ANCHOR_LIN, ANCHOR_LIN[1:]):
            if s <= b:
                return max(0.0, da + (db - da) * (s - a) / (b - a))
    for (a, da), (b, db) in zip(ANCHOR_LOG, ANCHOR_LOG[1:]):
        if s <= b:
            return math.exp(math.log(da) + (math.log(db) - math.log(da)) * (s - a) / (b - a))
    return 274.0


DAY = {s: d1_day(s) for s in range(1, 1001)}

if __name__ == '__main__':
    name, wall, ads, days = sys.argv[1], float(sys.argv[2]), float(sys.argv[3]), int(sys.argv[4])
    MARKS = [10, 50, 100, 150, 200, 250, 300, 400, 500, 600, 700, 800, 900, 1000]
    rows = PR.apply({'check_step': 20, 'atk_hp_gear': False, 'v3': True, 'day_table': DAY, 'wall_mul': wall}, farm=PR.V3['farm'], extras=True)
    fn = F.OV['stage_fn']
    out = dict(name=name, wall_mul=wall, ads=ads, days=days, tail=G.PARAMS['_tail'])
    for key in ('bhp', 'ehp', 'gold'):
        s = 1
        while s < 50000 and fn(s)[key] < 9e18:
            s += 1
        out['first_9e18_' + key] = s
    if ads == 0.5:
        with open(f'stage_{name}.csv', 'w', newline='', encoding='utf-8') as f:
            w = csv.writer(f); w.writerow([c for c, _ in export.COLS])
            for s in sorted(rows):
                r = dict(rows[s], stage=s)
                w.writerow([('%.6g' % r[k]) if isinstance(r[k], float) else r[k] for _, k in export.COLS])
    sim = P.Sim(pattern='daily', ads=ads); sim.dungeon = dungeon; sim.run_days(days)
    st = sorted(sim.arrive); gaps = []
    for a, b in zip(st, st[1:]):
        inf = sim.stage_info.get(a, {})
        gaps.append((a, round((sim.arrive[b][0] - sim.arrive[a][0]) / 60, 1), inf.get('fails', 0)))
    off = [g for g in gaps if g[0] % 10 and g[1] >= 15]
    tot = sum(sim.src.values()) or 1
    per_day = [(d, s_) for d, s_, _ in sim.quest_log]
    streak = best = 0; prev = None; best_at = None
    for d, s_ in per_day:
        streak = streak + 1 if prev is not None and s_ == prev else 0
        if streak > best: best, best_at = streak, (d, s_)
        prev = s_
    # 벽(같은 스테이지에 1일 넘게)
    walls = []
    for a, b in zip(st, st[1:]):
        dd = (sim.arrive[b][1] - sim.arrive[a][1]) / 86400
        if dd >= 1.0:
            walls.append((a, round(dd, 2)))
    out.update(stage=sim.stage, arrive={s: round(sim.arrive[s][1] / 86400, 2) for s in MARKS if s in sim.arrive},
               target={s: round(DAY[s], 2) for s in MARKS if s in DAY},
               off_chapter=off, walls_ge_1day=walls, gold_share={k: round(v / tot * 100, 1) for k, v in sim.src.items()},
               myc_per_day={k: round(v / days, 1) for k, v in sim.myc_src.items()},
               zero_days=sum(1 for a, b in zip(per_day, per_day[1:]) if a[1] == b[1]), longest_zero=best, longest_zero_at=best_at,
               dungeon=getattr(sim, 'dcount', {}), dlevel=dict(sim.dlevel), per_day=per_day)
    json.dump(out, open(f'out_v3b_{name}_{ads}.json', 'w', encoding='utf-8'), indent=1, default=str)
    print(name, 'wall', wall, 'ads', ads, 'stage', sim.stage, out['arrive'], 'zero', out['zero_days'], 'longest', best, best_at,
          'walls>=1d', len(walls), walls[-8:], 'off>=15', len(off), 'gold', out['gold_share'], 'dun', out['dungeon'],
          '9e18', out['first_9e18_bhp'], 'tail', {k: round(v, 5) for k, v in out['tail'].items()}, flush=True)
