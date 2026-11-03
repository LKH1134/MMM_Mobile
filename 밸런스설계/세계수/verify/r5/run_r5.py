# 라운드 5 (이론 v4 = 세계수 9305 branchSlots 5 -> 6): 적용된 표로 W1·W5~W7 재계산, 같은 입력에 칸만 5로 되돌린 표와 비교 (wt-verify)
# 입력 r5/feed_v3_*.json = feed.py (라운드 5: 창고 처치에서 보스 처치 제외, IdleBattle.cs:393)
# W7 측정: (접속 끝) rush_all 끝 = 그 접속의 받기·놓기·제작이 끝난 시점 · (접속 중 최대) place_all 직후마다
import sys, json, os, copy
sys.stdout.reconfigure(encoding='utf-8')
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.dirname(HERE))
import tables, wt_port as W
from run_cases import line
T4 = tables.load()
T3 = copy.deepcopy(T4); T3['tree'][4]['branchSlots'] = 5
assert T4['tree'][4]['stage'] == 5 and T4['tree'][4]['branchSlots'] == 6
print('가지 칸 1~8:', [r['branchSlots'] for r in T4['tree']], '칸 합:', [sum(r[z] for z in W.ZONES.values()) for r in T4['tree']])
print('칸 단조 (자리별·합 모두 줄지 않음):', all(T4['tree'][i + 1][z] >= T4['tree'][i][z] for i in range(7) for z in W.ZONES.values()))


def run(T, f, **o):
    w = W.WT(T, json.load(open(os.path.join(HERE, f), encoding='utf-8')), **o)
    end, mid = {}, {}
    pa0, ra0 = w.place_all, w.rush_all
    def pa():
        pa0(); n = len(w.owned) - len(w.placed); mid[w.stage] = max(mid.get(w.stage, 0), n)
    def ra():
        ra0(); n = len(w.owned) - len(w.placed); end[w.stage] = max(end.get(w.stage, 0), n)
    w.place_all, w.rush_all = pa, ra
    w.run()
    return w, end, mid


FEEDS = [('보통', 'feed_v3_a0.5.json'), ('무광고', 'feed_v3_a0.0.json'), ('광고 많이', 'feed_v3_a1.0.json'),
         ('플레이 ×0.5', 'feed_v3_a0.5_t0.5.json'), ('플레이 ×2', 'feed_v3_a0.5_t2.0.json'),
         ('보통 · 던전 1만', 'feed_v3_a0.5_d1.json'), ('무광고 · 던전 1만', 'feed_v3_a0.0_d1.json')]
if __name__ == '__main__':
    for label, f in FEEDS:
        for tag, T in (('v4 표', T4), ('칸 5 (라운드 4 표)', T3)):
            w, end, mid = run(T, f)
            print(f'\n[{label} · {tag}]')
            line(f'{label} · {tag}', w)
            print(f'    W7 못 놓은 장식 최대 — 접속 끝 {end} · 접속 중 {mid}')
            L = w.log
            print(f"    진화 순간 보유/칸 {L['decos_at']} · 8단계 진화 순간 효과 {({k: round(v, 3) for k, v in L['eff_at'].get(8, {}).items() if v})}")
            if tag == 'v4 표' and label in ('보통', '무광고', '광고 많이'):
                for p in W.period_table(w):
                    print(f"      {p['r']}: 창고/일 {p['harvest']:.0f} 공물 경험치/일 {p['exp']:.0f} 선물 몫 {p['gift_share']:.1f}% · 골드 쓰기 {p['g_sp']:.1f}% 보따리 {p['g_bag']:.1f}% · 순균사석 {p['m_net']:.1f}%")
    for tag, T in (('v4', T4), ('v3slot', T3)):
        for lab, f in (('', 'feed_v3_a0.5.json'), ('a0', 'feed_v3_a0.0.json'), ('a1', 'feed_v3_a1.0.json')):
            w = W.WT(T, json.load(open(os.path.join(HERE, f), encoding='utf-8'))).run()
            tl = [dict(day=d['day'], **{k: d['bonus'][k] for k in ('Attack', 'Hp', 'CritChance', 'CritDamage', 'KillGold', 'BossMycelite', 'Harvest')}) for d in w.log['day']]
            json.dump(tl, open(os.path.join(HERE, f'timeline_{tag}{lab}.json'), 'w', encoding='utf-8'))
    a = json.load(open(os.path.join(HERE, 'timeline_v4.json'))); b = json.load(open(os.path.join(HERE, 'timeline_v3slot.json')))
    cmb = ('Attack', 'Hp', 'CritChance', 'CritDamage', 'KillGold', 'BossMycelite')
    print('\n전투 효과 타임라인 v4 vs 칸 5 (보통): 다른 날', [(x['day'], {k: (x[k], y[k]) for k in cmb if x[k] != y[k]}) for x, y in zip(a, b) if any(x[k] != y[k] for k in cmb)])
    print('효과 최대 합:', {k: round(v, 3) for k, v in W.max_totals(T4).items() if v})
