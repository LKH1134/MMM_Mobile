# 이론 모델 실행: python model.py [c1|current|proposal|all] [days]
#   c1        기준 1 (스테이지 1~5 무강화) 검사
#   current   지금 밸런스(+공속 상한 275, 기준 5)로 진행 — 무광고/광고
#   proposal  수치안으로 진행 — 무광고/광고
# 결과는 theory/out_<이름>.json (이론.md 표의 원본). 난수 없음.
import json, math, os, sys, time
import formulas as F
import battle as B
import progress as P
import proposal as PR

OUT = os.path.dirname(__file__)
MARKS = [5, 6, 10, 20, 30, 40, 50, 60, 80, 100, 150, 184, 200, 250, 300, 400, 500, 700, 1000]


def c1(label):
    rows = []
    for s in range(1, 7):
        r = B.attempt(s, B.Player())
        rows.append(dict(stage=s, ok=r.ok, reason=r.reason, time=round(r.time, 1), boss_used=round(r.boss_used, 1),
                         min_hp=round(r.min_hp, 2), boss_hp=F.boss_hp(s), enemy_hp=F.enemy_hp(s, 1),
                         count=[F.enemy_count(s, w) for w in range(1, F.waves(s) + 1)], atk=F.enemy_attack(s)))
        print(label, rows[-1])
    return rows


def hurdles(sim, top=12):
    """스테이지별 머문 달력 시간(도착 간격) → 큰 순서. 원인 표시용 정보 포함"""
    st = sorted(sim.arrive)
    gaps = []
    for a, b in zip(st, st[1:]):
        if b != a + 1:
            continue
        dw = (sim.arrive[b][1] - sim.arrive[a][1]) / 3600
        dp = (sim.arrive[b][0] - sim.arrive[a][0]) / 60
        inf = sim.stage_info.get(a, {})
        gaps.append(dict(stage=a, wall_h=round(dw, 2), play_min=round(dp, 1), attempts=inf.get('attempts', 0),
                         reasons=inf.get('reasons', {})))
    return sorted(gaps, key=lambda g: -g['wall_h'])[:top]


def run(label, days, ads, dungeons=False):
    t0 = time.time()
    sim = P.Sim(pattern='daily', ads=ads)
    if dungeons:
        import dungeon
        sim.dungeon = dungeon
    sim.run_days(days)
    res = dict(label=label, ads=ads, days=days, stage=sim.stage,
               arrive={s: (round(sim.arrive[s][0] / 3600, 2), round(sim.arrive[s][1] / 86400, 2)) for s in MARKS if s in sim.arrive},
               per_day=[(d, s, q) for d, s, q in sim.quest_log],
               gold_src={k: v for k, v in sim.src.items()}, myc_src=sim.myc_src, pulls=sim.gear.pulls,
               levels=sim.p.lv, atk_pct=sim.p.atk_pct, hp_pct=sim.p.hp_pct, hurdles=hurdles(sim),
               dlevel=dict(sim.dlevel), secs=round(time.time() - t0, 1))
    tot = sum(sim.src.values()) or 1
    print(f"[{label} ads={ads}] stage {sim.stage} after {days}d ({res['secs']}s)")
    print('  arrive (play h, day):', res['arrive'])
    print('  stage by day:', [(d, s) for d, s, _ in sim.quest_log])
    print('  gold share %:', {k: round(v / tot * 100, 1) for k, v in sim.src.items()})
    print('  myc:', {k: round(v) for k, v in sim.myc_src.items()}, 'pulls', sim.gear.pulls)
    print('  quests last:', sim.quest_log[-1] if sim.quest_log else None)
    print('  dungeon levels:', sim.dlevel)
    print('  hurdles:', res['hurdles'][:8])
    return res


def save(name, obj):
    with open(os.path.join(OUT, f'out_{name}.json'), 'w', encoding='utf-8') as f:
        json.dump(obj, f, ensure_ascii=False, indent=1, default=str)


if __name__ == '__main__':
    what = sys.argv[1] if len(sys.argv) > 1 else 'all'
    days = int(sys.argv[2]) if len(sys.argv) > 2 else 30
    if what in ('c1', 'all'):
        PR.current(); a = c1('current')
        PR.apply(); b = c1('proposal')
        save('c1', dict(current=a, proposal=b))
    if what in ('current', 'all'):
        PR.current()
        save('current', [run('current', days, a) for a in (0.0, 0.5, 1.0)])
    if what in ('proposal', 'all'):
        PR.apply()
        save('proposal', [run('proposal', days, a, True) for a in (0.0, 0.25, 0.5, 1.0)])
