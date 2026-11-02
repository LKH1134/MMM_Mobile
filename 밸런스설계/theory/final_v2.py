# v2: it75 고정점 날짜표 + 적 공격 강화만 보정(atk_hp_gear=False) 비교. python final_v2.py <이름> <atk_hp_gear 0|1> [farm] [dayfile] [days]
import sys, json, csv
import proposal as PR, progress as P, dungeon, export
name, gear = sys.argv[1], sys.argv[2] == '1'
farm = float(sys.argv[3]) if len(sys.argv) > 3 else 75
dayfile = sys.argv[4] if len(sys.argv) > 4 else 'day_it75.json'
days = int(sys.argv[5]) if len(sys.argv) > 5 else 45
tbl = {int(k): v for k, v in json.load(open(dayfile)).items()}
rows = PR.apply({'check_step': 20, 'day_table': tbl, 'atk_hp_gear': gear}, farm=farm, extras=True)
with open(f'stage_{name}.csv', 'w', newline='', encoding='utf-8') as f:
    w = csv.writer(f); w.writerow([c for c, _ in export.COLS])
    for s in sorted(rows):
        r = dict(rows[s], stage=s)
        w.writerow([('%.6g' % r[k]) if isinstance(r[k], float) else r[k] for _, k in export.COLS])
MARKS = [10, 20, 50, 100, 150, 200, 250, 300, 350, 400, 500, 700, 1000]
out = dict(name=name, runs={})
for ads in (0.0, 0.5, 1.0):
    sim = P.Sim(pattern='daily', ads=ads); sim.dungeon = dungeon; sim.run_days(days)
    st = sorted(sim.arrive); gaps = []
    for a, b in zip(st, st[1:]):
        inf = sim.stage_info.get(a, {})
        gaps.append((a, round((sim.arrive[b][0] - sim.arrive[a][0]) / 60, 1), inf.get('fails', 0)))
    off = [g for g in gaps if g[0] % 10 and g[1] >= 15]
    ch = [(g[0], g[1]) for g in gaps if not g[0] % 10]
    tot = sum(sim.src.values()) or 1
    out['runs'][ads] = dict(stage=sim.stage, arrive={s: round(sim.arrive[s][1] / 86400, 2) for s in MARKS if s in sim.arrive},
                            off_chapter=off, chapter=ch, gold_share={k: round(v / tot * 100, 1) for k, v in sim.src.items()},
                            quests=sim.quest_log[-5:], per_day=[(d, st_) for d, st_, _ in sim.quest_log],
                            zero_days=sum(1 for (d1, a1, _), (d2, a2, _) in zip(sim.quest_log, sim.quest_log[1:]) if a2 == a1))
    print(name, ads, sim.stage, out['runs'][ads]['arrive'], 'zero days', out['runs'][ads]['zero_days'], 'off>=15', len(off), off[:20], flush=True)
    print('   chapter', ch[:50], flush=True)
json.dump(out, open(f'out_var_{name}.json', 'w', encoding='utf-8'), indent=1, default=str)
print('done', name)
