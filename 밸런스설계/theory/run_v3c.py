# v3c: v3b(D1 목표일 고정) + D5(퀘스트·던전 골드 늘려 앱 밖 비중 ≤60%, 비용 baseCost × cost_mul로 속도 유지) + D6 첫날 지표
#   python run_v3c.py <이름> <wall_mul> <ads> <days> <quest_mul> <dungeon_mul> <cost_mul>
import sys, json, csv
import proposal as PR, progress as P, dungeon, export
import gen_stage as G
import formulas as F
from run_v3b import DAY

QUEST_GOLD_BASE = [200, 300, 200, 300, 600]     # 퀘스트 시트 goldKills (지금)


def setup(wall, quest_mul, dungeon_mul, cost_mul):
    for d in dungeon.DUNGEONS.values():
        d['rewardKills'] = d.setdefault('_rk', d['rewardKills']) * dungeon_mul
    rows = PR.apply({'check_step': 20, 'atk_hp_gear': False, 'v3': True, 'day_table': DAY, 'wall_mul': wall}, farm=PR.V3['farm'], extras=True)
    F.OV['quest_gold'] = [round(x * quest_mul) for x in QUEST_GOLD_BASE]
    F.OV['cost_mul'] = cost_mul
    return rows


def day1(ads):
    sim = P.Sim(pattern='daily', ads=ads); sim.dungeon = dungeon; sim.run_days(1)
    st = sorted(sim.arrive)
    longest = (0, 0.0)
    for a, b in zip(st, st[1:]):
        m = (sim.arrive[b][0] - sim.arrive[a][0]) / 60
        if m > longest[1]: longest = (a, round(m, 1))
    m = (sim.active - sim.arrive[st[-1]][0]) / 60
    if m > longest[1]: longest = (st[-1], round(m, 1))
    return dict(stage_24h=sim.stage, longest_wall_play_min=longest, play_min=round(sim.active / 60),
                first_use_h={k: round(v / 3600, 2) for k, v in sim.first.items()}, upgrades_first_hour=sim.upg_1h,
                arrive_h={s: round(sim.arrive[s][1] / 3600, 2) for s in (5, 6, 10, 20, 50, 100) if s in sim.arrive})


if __name__ == '__main__':
    name, wall, ads, days = sys.argv[1], float(sys.argv[2]), float(sys.argv[3]), int(sys.argv[4])
    qm, dm, cm = float(sys.argv[5]), float(sys.argv[6]), float(sys.argv[7])
    rows = setup(wall, qm, dm, cm)
    MARKS = [10, 50, 100, 150, 200, 250, 300, 400, 500, 600, 700, 800, 900, 1000]
    d1 = day1(ads)
    sim = P.Sim(pattern='daily', ads=ads); sim.dungeon = dungeon; sim.run_days(days)
    tot = sum(sim.src.values()) or 1
    per_day = [(d, s_) for d, s_, _ in sim.quest_log]

    def longest(lim):
        b = s = 0; p = None
        for d, st in per_day:
            if st > lim: break
            s = s + 1 if p == st else 0; b = max(b, s); p = st
        return b

    shares = {}
    for h in (60, days):
        pass
    st = sorted(sim.arrive)
    off = [(a, round((sim.arrive[b][0] - sim.arrive[a][0]) / 60, 1)) for a, b in zip(st, st[1:]) if a % 10 and (sim.arrive[b][0] - sim.arrive[a][0]) >= 900]
    out = dict(name=name, wall=wall, ads=ads, days=days, quest_mul=qm, dungeon_mul=dm, cost_mul=cm, day1=d1, stage=sim.stage,
               arrive={s: round(sim.arrive[s][1] / 86400, 2) for s in MARKS if s in sim.arrive}, target={s: round(DAY[s], 2) for s in MARKS},
               gold_share=shares,
               longest_zero={'700': longest(700), '900': longest(900), 'all': longest(10 ** 9)}, zero_days=sum(1 for a, b in zip(per_day, per_day[1:]) if a[1] == b[1]),
               off_chapter=off, dungeon=getattr(sim, 'dcount', {}), tail=G.PARAMS['_tail'], per_day=per_day)
    json.dump(out, open(f'out_v3c_{name}_{ads}_{days}.json', 'w', encoding='utf-8'), indent=1, default=str)
    if ads == 0.5:
        with open(f'stage_{name}.csv', 'w', newline='', encoding='utf-8') as f:
            w = csv.writer(f); w.writerow([c for c, _ in export.COLS])
            for s in sorted(rows):
                r = dict(rows[s], stage=s)
                w.writerow([('%.6g' % r[k]) if isinstance(r[k], float) else r[k] for _, k in export.COLS])
    print(name, 'ads', ads, 'days', days, 'stage', sim.stage, out['arrive'], 'share30', shares[30], 'share_end', shares[days], 'longest0', out['longest_zero'],
          'off>=15', len(off), 'dun', out['dungeon'], 'DAY1', d1, flush=True)
