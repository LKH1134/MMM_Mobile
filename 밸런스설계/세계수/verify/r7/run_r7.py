# 라운드 7 (기획서 Ver02 칸 + 9307 evoExp 41만 + 9711 O1): W1~W10 · W7′ (wt-verify). python r7/run_r7.py > r7/cases_r7.log
import sys, os, json, copy
sys.stdout.reconfigure(encoding='utf-8')
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE); sys.path.insert(0, os.path.dirname(HERE))
import tables, wt_port as W
from wt_v6 import WT6
T = tables.load()
R = lambda x: None if x is None else round(x, 2)
F6 = os.path.join(os.path.dirname(HERE), 'r6')


def feed(f): return json.load(open(os.path.join(F6, f), encoding='utf-8'))


def run(f, T=T, **o):
    return WT6(T, feed(f), **o).run()


def reach(w): return ' / '.join(f"{R(w.log['reach'].get(s))}" for s in range(2, 9))


def r8(w): return R(w.log['reach'].get(8))


print('칸 (뿌리/줄기/가지/수관):', [(r['rootSlots'], r['trunkSlots'], r['branchSlots'], r['crownSlots']) for r in T['tree']])
print('자리별 단조:', all(T['tree'][i + 1][c] >= T['tree'][i][c] for i in range(7) for c in W.ZONES.values()),
      '· 자리 열리는 단계:', {z: next(r['stage'] for r in T['tree'] if r[c] > 0) for z, c in W.ZONES.items()})
print('evoExp:', [r['evoExp'] for r in T['tree']], '· 9711:', [(d['key'], d['effect'], d['value']) for d in T['decos'] if d['index'] == 9711])
print('효과 최대 합 (24개 다 놓았을 때 = 이제 불가능한 상한):', {k: round(v, 3) for k, v in W.max_totals(T).items() if v})

FEEDS = [('보통', 'feed_v3_a0.5.json'), ('무광고', 'feed_v3_a0.0.json'), ('광고 많이', 'feed_v3_a1.0.json'), ('플레이 ×0.5', 'feed_v3_a0.5_t0.5.json')]
print('\n== W1 8단계 (일): 정책 × 의뢰 ==')
for label, f in FEEDS:
    cells = []
    for pol in ('best', 'grade', 'firstall'):
        for q in (True, False):
            w = run(f, policy=pol, quests=q)
            cells.append(f"{pol}{'·의뢰' if q else '·의뢰X'} {r8(w)}")
    print(f'  [{label}] ' + ' | '.join(cells))

print('\n== 기준선 상세 (best = 진행 최선 교체, 의뢰 함) ==')
for label, f in FEEDS[:3]:
    for pol in ('best', 'grade'):
        w = run(f, policy=pol)
        L, m = w.log, w.m7
        st = [L['reach'].get(s) for s in range(2, 9)]
        g = [st[0]] + [b - a for a, b in zip(st, st[1:])] if None not in st else None
        st3 = {k: R(v[2]) if len(v) > 2 else None for k, v in L['star'].items()}
        print(f"[{label} · {pol}] 2~8단계 {reach(w)} · 간격 비 {[R(b / a) for a, b in zip(g, g[1:])] if g else None}")
        print(f"    ★3 {st3} · 호감 최대 {R(min(L['affmax'].values()))}~{R(max(L['affmax'].values()))}일 · 첫 배치 균사석 {m['first_myc']}")
        fill = {s: R(m['fill'][s] - L['reach'][s]) if s in m['fill'] and s in L['reach'] else None for s in range(2, 9)}
        print(f"    W7′(a) 진화 → 칸 다 참 (일): {fill} · (b) 접속 끝 보관함 최소 {m['store_min']} · (e) 24개 제작 {R(L['all24'])}일 (7단계 {R(L['reach'].get(7))})")
        print(f"    8단계 세트 {m['sets_at8']} · 8단계 효과 {m['eff8']}")
        P = W.period_table(w)
        print('    순균사석%', '/'.join(f"{p['m_net']:.1f}" for p in P), '| 골드 쓰기%', '/'.join(f"{p['g_sp']:.1f}" for p in P),
              '| 보따리%', '/'.join(f"{p['g_bag']:.1f}" for p in P), '| 공물 경험치/일', '/'.join(f"{p['exp']:.0f}" for p in P),
              '| 보따리 넘침 h/일', '/'.join(f"{p['bag_full']:.1f}" for p in P))
        ql = w.qlog
        print('    의뢰 일일 깬/나온:', {k: f"{ql['daily_done'].get(k, 0)}/{ql['daily_shown'][k]}" for k in ql['daily_shown']}, '주간 깬 주', ql['weekly_done'])

print('\n== W7′(c)(d): 목표별 배치 (보통 · 의뢰) ==')
union = set(); dates = {}
for obj in ('progress', 'bag', 'craft', 'combat'):
    w = run('feed_v3_a0.5.json', policy='best', obj=obj)
    dates[obj] = r8(w)
    union |= set(w.placed)
    print(f'  {obj}: 8단계 {r8(w)}일 · 8단계 배치 {sorted(w.placed)} · 세트 {w.m7["sets_at8"]}')
wg = run('feed_v3_a0.5.json', policy='grade'); dates['grade'] = r8(wg); union |= set(wg.placed)
print(f'  grade: 8단계 {r8(wg)}일 · 배치 {sorted(wg.placed)}')
print('  8단계 차이 (최대 − 최소):', R(max(dates.values()) - min(dates.values())), dates)
print('  어느 목표에도 안 놓이는 장식 (d):', sorted(set(d['key'] for d in T['decos']) - union))
# O1 전(9711 BagGold 0.02)으로 (d) 비교
T0 = copy.deepcopy(T)
for d in T0['decos']:
    if d['index'] == 9711: d['effect'], d['value'] = 'BagGold', 0.02
u0 = set()
for obj in ('progress', 'bag', 'craft', 'combat'):
    u0 |= set(run('feed_v3_a0.5.json', T=T0, policy='best', obj=obj).placed)
u0 |= set(run('feed_v3_a0.5.json', T=T0, policy='grade').placed)
print('  (참고) O1 전 표면 안 놓이는 장식:', sorted(set(d['key'] for d in T['decos']) - u0))
# 9307 evoExp 44만이었으면
T1 = copy.deepcopy(T); T1['tree'][6]['evoExp'] = 440000
print('\n(참고) 9307 evoExp 440,000이면 8단계: 보통 best 의뢰 %s · 의뢰X %s · 무광고 의뢰X %s · grade 의뢰X %s' % (
    r8(run('feed_v3_a0.5.json', T=T1)), r8(run('feed_v3_a0.5.json', T=T1, quests=False)), r8(run('feed_v3_a0.0.json', T=T1, quests=False)),
    r8(run('feed_v3_a0.5.json', T=T1, policy='grade', quests=False))))
