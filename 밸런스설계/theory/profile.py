# 수치안 허들 프로필: 구간별 가장 긴 대기(달력)·플레이·시도. python profile.py [ads]
import sys, json
import proposal as PR, progress as P, dungeon
PR.apply()
out = {}
for ads in [float(a) for a in (sys.argv[1:] or ['0.5'])]:
    sim = P.Sim(pattern='daily', ads=ads); sim.dungeon = dungeon; sim.run_days(45)
    st = sorted(sim.arrive); rows = []
    for a, b in zip(st, st[1:]):
        inf = sim.stage_info.get(a, {})
        rows.append((a, (sim.arrive[b][1] - sim.arrive[a][1]) / 3600, (sim.arrive[b][0] - sim.arrive[a][0]) / 60, inf.get('fails', 0), inf.get('reasons', {})))
    bands = [(1, 5), (6, 9), (10, 10), (11, 19), (20, 20), (21, 49), (50, 50), (51, 99), (100, 100), (101, 199), (200, 200), (201, 299), (300, 300), (301, 400)]
    prof = []
    for lo, hi in bands:
        sel = [r for r in rows if lo <= r[0] <= hi]
        if not sel: continue
        m = max(sel, key=lambda r: r[2])
        prof.append(dict(band=f'{lo}-{hi}', max_play_min=round(m[2], 1), at=m[0], fails=m[3], wall_h=round(m[1], 1),
                         sum_play_h=round(sum(r[2] for r in sel) / 60, 2), reasons=m[4]))
    walls = [dict(stage=r[0], play_min=round(r[2], 1), fails=r[3]) for r in rows if r[0] % 10 == 0]
    out[ads] = dict(profile=prof, walls=walls)
    print('ads', ads)
    for p in prof: print(' ', p)
    print('  chapter walls play_min:', [(w['stage'], w['play_min']) for w in walls][:45])
json.dump(out, open('out_profile.json', 'w'), indent=1)
