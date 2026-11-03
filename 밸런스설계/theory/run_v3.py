# v3: D1~D4 + V1~V6. python run_v3.py <이름> <iters> [days]
#  iterate.py와 같은 고정점 반복(보통 r=0.5)에 v3 묶음(farm 150, 표 1~700 + 지수 꼬리, 뽑기 레벨 해금, 초반 골드 ×2, 이동 평균, 던전 상한·소탕)
import sys, json, math, csv
import proposal as PR, progress as P, dungeon, export
import gen_stage as G
import formulas as F

name, iters = sys.argv[1], int(sys.argv[2])
days = int(sys.argv[3]) if len(sys.argv) > 3 else 60
farm = PR.V3['farm']
MARKS = [5, 6, 10, 20, 50, 100, 150, 200, 250, 300, 350, 400, 500, 600, 700, 800, 1000]
tbl = None
try:
    tbl = {int(k): v for k, v in json.load(open(f'day_{name}.json')).items()}
    print('resume day table', flush=True)
except Exception:
    pass


def params():
    p = {'check_step': 20, 'atk_hp_gear': False, 'v3': True}
    if tbl:
        p['day_table'] = tbl
    return p


for it in range(iters):
    rows = PR.apply(params(), farm=farm, extras=True)
    sim = P.Sim(pattern='daily', ads=0.5); sim.dungeon = dungeon; sim.run_days(days)
    new = {s: max(0.0, sim.arrive[s][1] / 86400) for s in sim.arrive if s <= G.F.T.tableMax}
    if tbl:
        new = {s: math.sqrt(max(G.target_day(s), 1e-3) * max(v, 1e-3)) if v > 0 else v for s, v in new.items()}
    print(name, 'iter', it, 'stage', sim.stage, {s: round(sim.arrive[s][1] / 86400, 2) for s in MARKS if s in sim.arrive}, flush=True)
    tbl = new
    json.dump({str(k): v for k, v in tbl.items()}, open(f'day_{name}.json', 'w'))

rows = PR.apply(params(), farm=farm, extras=True)
with open(f'stage_{name}.csv', 'w', newline='', encoding='utf-8') as f:
    w = csv.writer(f); w.writerow([c for c, _ in export.COLS])
    for s in sorted(rows):
        r = dict(rows[s], stage=s)
        w.writerow([('%.6g' % r[k]) if isinstance(r[k], float) else r[k] for _, k in export.COLS])
fn = F.OV['stage_fn']
out = dict(name=name, farm=farm, tail={k: v for k, v in G.PARAMS['_tail'].items()}, runs={})
# 9e18 넘는 첫 스테이지 (보스 체력·적 체력·골드)
for key in ('bhp', 'ehp', 'gold'):
    s = 1
    while s < 20000 and fn(s)[key] < 9e18:
        s += 1
    out['first_9e18_' + key] = s
for ads in (0.0, 0.25, 0.5, 1.0):
    sim = P.Sim(pattern='daily', ads=ads); sim.dungeon = dungeon; sim.run_days(days)
    st = sorted(sim.arrive); gaps = []
    for a, b in zip(st, st[1:]):
        inf = sim.stage_info.get(a, {})
        gaps.append((a, round((sim.arrive[b][0] - sim.arrive[a][0]) / 60, 1), inf.get('fails', 0)))
    off = [g for g in gaps if g[0] % 10 and g[1] >= 15]
    ch = [(g[0], g[1]) for g in gaps if not g[0] % 10]
    tot = sum(sim.src.values()) or 1
    per_day = [(d, s_) for d, s_, _ in sim.quest_log]
    streak = best = 0; prev = None
    for d, s_ in per_day:
        streak = streak + 1 if prev is not None and s_ == prev else 0
        best = max(best, streak); prev = s_
    out['runs'][ads] = dict(stage=sim.stage, arrive={s: round(sim.arrive[s][1] / 86400, 2) for s in MARKS if s in sim.arrive},
                            off_chapter=off, chapter=ch, gold_share={k: round(v / tot * 100, 1) for k, v in sim.src.items()},
                            myc_per_day={k: round(v / days, 1) for k, v in sim.myc_src.items()},
                            per_day=per_day, zero_days=sum(1 for a, b in zip(per_day, per_day[1:]) if a[1] == b[1]),
                            longest_zero=best, dungeon=getattr(sim, 'dcount', {}), dlevel=dict(sim.dlevel))
    print(name, 'final', ads, sim.stage, out['runs'][ads]['arrive'], 'zero', out['runs'][ads]['zero_days'], 'longest', best,
          'off>=15', len(off), off[:15], 'gold', out['runs'][ads]['gold_share'], 'dun', out['runs'][ads]['dungeon'], flush=True)
json.dump(out, open(f'out_var_{name}.json', 'w', encoding='utf-8'), indent=1, default=str)
print('done', name, flush=True)
