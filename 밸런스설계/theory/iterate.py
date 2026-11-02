# 장비 기준 날짜를 실제 도착 일수로 맞추는 고정점 반복 (보통 구간 r=0.5 기준).
# python iterate.py <이름> <farm> <iters> [days]  → day_<이름>.json, 마지막 표 stage_<이름>.csv, out_var_<이름>.json
import sys, json, math, csv
import proposal as PR, progress as P, dungeon, export
import gen_stage as G

name, farm, iters = sys.argv[1], float(sys.argv[2]), int(sys.argv[3])
days = int(sys.argv[4]) if len(sys.argv) > 4 else 45
MARKS = [10, 20, 50, 100, 150, 200, 250, 300, 350, 400, 500, 700, 1000]
tbl = None
for it in range(iters + 1):
    params = {'check_step': 20, 'atk_hp_gear': False}
    if tbl:
        params['day_table'] = tbl
    rows = PR.apply(params, farm=farm, extras=True)
    sim = P.Sim(pattern='daily', ads=0.5); sim.dungeon = dungeon; sim.run_days(days)
    new = {s: max(0.0, sim.arrive[s][1] / 86400) for s in sim.arrive}
    # 반복 사이 흔들림을 줄이려 로그 평균 (처음은 그대로)
    if tbl:
        merged = {}
        for s in new:
            old = G.target_day(s)
            merged[s] = math.sqrt(max(old, 1e-3) * max(new[s], 1e-3)) if old > 0 and new[s] > 0 else new[s]
        new = merged
    print(name, 'iter', it, 'stage', sim.stage, {s: round(sim.arrive[s][1] / 86400, 2) for s in MARKS if s in sim.arrive}, flush=True)
    tbl = new
json.dump({str(k): v for k, v in tbl.items()}, open(f'day_{name}.json', 'w'))
with open(f'stage_{name}.csv', 'w', newline='', encoding='utf-8') as f:
    w = csv.writer(f); w.writerow([c for c, _ in export.COLS])
    for s in sorted(rows):
        r = dict(rows[s], stage=s)
        w.writerow([('%.6g' % r[k]) if isinstance(r[k], float) else r[k] for _, k in export.COLS])
out = dict(name=name, farm=farm, runs={})
for ads in (0.0, 0.5, 1.0):
    sim = P.Sim(pattern='daily', ads=ads); sim.dungeon = dungeon; sim.run_days(days)
    st = sorted(sim.arrive); gaps = []
    for a, b in zip(st, st[1:]):
        inf = sim.stage_info.get(a, {})
        gaps.append((a, round((sim.arrive[b][0] - sim.arrive[a][0]) / 60, 1), inf.get('fails', 0)))
    off = [g for g in gaps if g[0] % 10 and g[1] >= 15]
    ch = [g for g in gaps if not g[0] % 10]
    tot = sum(sim.src.values()) or 1
    out['runs'][ads] = dict(stage=sim.stage, arrive={s: round(sim.arrive[s][1] / 86400, 2) for s in MARKS if s in sim.arrive},
                            off_chapter=off, chapter=ch, gold_share={k: round(v / tot * 100, 1) for k, v in sim.src.items()},
                            myc_per_day={k: round(v / days) for k, v in sim.myc_src.items()})
    print(name, 'final', ads, sim.stage, out['runs'][ads]['arrive'], 'off-chapter>=15min', off[:25], flush=True)
    print('   chapter', [(c[0], c[1]) for c in ch][:50], flush=True)
json.dump(out, open(f'out_var_{name}.json', 'w', encoding='utf-8'), indent=1)
print('done', name)
