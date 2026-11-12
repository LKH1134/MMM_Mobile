# 라운드 8 (2026-11-12 포팅 현행화): 지금 표 그대로의 세계수 진행 재측정 + W1~W9 (wt-verify).
# python r8/feed_now.py 0.5 ; ... (feed 만들기, 아래 FEEDS)  →  python r8/run_r8.py > r8/cases_r8.log
import sys, os, json, re, copy
sys.stdout.reconfigure(encoding='utf-8')
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE); sys.path.insert(0, os.path.dirname(HERE))
import tables, wt_port as W
from wt_v8 import WT8
T = tables.load()
R = lambda x: None if x is None else round(x, 2)
VER = os.path.dirname(HERE)


def feed(f):
    p = os.path.join(HERE, f) if os.path.exists(os.path.join(HERE, f)) else os.path.join(VER, f)
    return json.load(open(p, encoding='utf-8'))


def run(f, T=T, **o): return WT8(T, feed(f), **o).run()
def reach(w): return ' / '.join(f"{R(w.log['reach'].get(s))}" for s in range(2, 9))
def r8(w): return R(w.log['reach'].get(8))


# ── 0. 표·코드 대조 ──
print('== 0. 표 (MainTable.xlsx) ==')
print('시트 있음? 호감도', '호감도' in T['sheets'], '· 의뢰', '의뢰' in T['sheets'], '· 퀘스트', '퀘스트' in T['sheets'])
gone = ['bagCapHours', 'treeRingBagBonus', 'kidBagShare', 'decoSetBagMul', 'giftFavoriteMul', 'giftMaxCount', 'requestAffinity', 'decoFirstAffinity',
        'kidEvoLevelGradeMul', 'treeQuestDaily']
print('지워진 상수가 남았나:', [k for k in gone if k in T['const']])
print('꼬마 열:', sorted(T['kids'][0].keys()))
print('꼬마진화:', [(r['star'], r['mycelite'], r['needLevel']) for r in T['stars']], '(#bagMul은 메모 열)')
print('세계수 열:', [k for k in T['tree'][0].keys()])
print('세계수:', [(r['stage'], r['evoExp'], r['evoGoldKills'], r['evoMycelite'], r['ringKills'], r['craftSlots'],
                (r['rootSlots'], r['trunkSlots'], r['branchSlots'], r['crownSlots']), r['preferIds']) for r in T['tree']])
print('가호:', [(r['stage'], r['effect'], r['value']) for r in T['bless']])
print('꼬마:', [(k['key'], k['initialOwned'], k['favoriteIds'], k['bonusEffect'], k['bonusValue'], k['evoStage']) for k in T['kids']])
print('퀘스트 (treeExp):', [(r['key'], r['period'], r['kind'], r['target'], r['treeExp']) for r in T['quests']])
sw, pools = W.species_weights(T)
print('창고 종 확률 (조각 수, 보스 조각 제외):', pools)
print('  식용 1001~1005 확률:', [round(sw[i], 4) for i in range(1001, 1006)], '· 버섯 1개 평균 value:',
      round(sum(sw[e['index']] * e['value'] for e in T['enemies']), 3))
M = W.max_totals(T)
print('효과 최대 합 (24개 다 놓음 — 칸 12라 닿을 수 없는 상한):', {k: round(v, 3) for k, v in M.items() if v})

# ── W9: 검증기 규칙 (GameBalanceValidator.cs:46-80 RequiredConsts, :337-394 ValidateWorldTree, :545-570 ValidateQuests) ──
print('\n== W9 회귀 ==')
src = open(os.path.join(tables.ROOT, 'Assets/Scripts/Mobile/Data/Editor/GameBalanceValidator.cs'), encoding='utf-8').read()
blk = src[src.index('RequiredConsts ='):src.index('};', src.index('RequiredConsts ='))]
req = re.findall(r'\("(\w+)", ([\d.]+), (true|false)\)', blk)
issues = []
for k, mn, integer in req:
    if k not in T['const']: issues.append(f'상수 {k} 없음')
    elif not T['const'][k] >= float(mn): issues.append(f'{k} < {mn}')
    elif integer == 'true' and T['const'][k] != int(T['const'][k]): issues.append(f'{k} 정수 아님')
known = {k for k, _, _ in req}
issues += [f'모르는 상수 {k}' for k in T['const'] if k not in known]
st = [r['stage'] for r in T['tree']]
if st != list(range(1, len(st) + 1)): issues.append('세계수 stage 불연속')
for r in T['tree']:
    if r['stage'] < len(st) and r['evoExp'] <= 0: issues.append(f'evoExp {r["stage"]} ≤ 0')
    for c in ('evoExp', 'evoGoldKills', 'evoMycelite', 'ringKills', 'craftSlots', 'rootSlots', 'trunkSlots', 'branchSlots', 'crownSlots'):
        if r[c] < 0: issues.append(f'{c} 음수')
    for i in r['preferIds']:
        if i not in {e['index'] for e in T['enemies']}: issues.append(f'preferIds {i}')
for r in T['bless']:
    if r['stage'] not in st: issues.append('가호 단계')
stars = [r['star'] for r in T['stars']]
if stars != list(range(len(stars))): issues.append('star 불연속')
if any(r['needLevel'] < 1 or r['mycelite'] < 0 for r in T['stars']): issues.append('꼬마진화 범위')
kidkeys = {k['key'] for k in T['kids']}
for d in T['decos']:
    if d['owner'] not in kidkeys or d['grade'] not in T['dgrades']: issues.append(f'장식 {d["key"]}')
for e in T['enemies']:
    if e['value'] < 0 or e['requestCount'] < 1: issues.append(f'적 {e["index"]}')
for k in T['kids']:
    if k['evoStage'] < 1: issues.append('evoStage')
    for i in k['favoriteIds']:
        if i not in {e['index'] for e in T['enemies']}: issues.append(f'favoriteIds {i}')
dq = [r for r in T['quests'] if r['period'] == 'daily']
for r in T['quests']:
    if r['period'] == 'weekly' and r['kind'] == 'daily' and r['target'] > len(dq) * 7: issues.append('주간 daily 목표')
    if r['treeExp'] < 0: issues.append('treeExp')
print('검증기 규칙 (세계수·퀘스트·상수):', issues or '문제 없음')
mono = {c: all(T['tree'][i + 1][c] >= T['tree'][i][c] for i in range(len(st) - 2)) for c in ('evoExp', 'evoGoldKills', 'evoMycelite', 'ringKills')}
mono.update({c: all(T['tree'][i + 1][c] >= T['tree'][i][c] for i in range(len(st) - 1)) for c in ('craftSlots', 'rootSlots', 'trunkSlots', 'branchSlots', 'crownSlots')})
print('단계마다 줄지 않음 (마지막 단계 0 제외):', mono)
print('꼬마진화 단조:', all(T['stars'][i + 1]['mycelite'] >= T['stars'][i]['mycelite'] and T['stars'][i + 1]['needLevel'] >= T['stars'][i]['needLevel'] for i in range(len(stars) - 1)))
print('장식등급 단조:', all(T['dgrades'][g + 1][c] >= T['dgrades'][g][c] for g in range(len(T['dgrades']) - 1) for c in ('goldKills', 'mycelite', 'hours')))
eg700 = T['stage'][700]['enemyGold']
print('long 넘침: 가장 큰 골드 비용 KillGold(ringKills 3000×1.35^9 / evoGold 50000) at 스테이지 700 = %.3g (long 9.2e18)' % (eg700 * max(3000 * 1.35 ** 9, 50000)))

FEEDS = [('보통', 'feed_now_a0.5.json'), ('무광고', 'feed_now_a0.0.json'), ('광고 많이', 'feed_now_a1.0.json'),
         ('플레이 ×0.5', 'feed_now_a0.5_t0.5.json'), ('무광고 ×0.5', 'feed_now_a0.0_t0.5.json'), ('플레이 ×2', 'feed_now_a0.5_t2.0.json'),
         ('(옛 feed 11-03) 보통', 'r6/feed_v3_a0.5.json'), ('(옛 feed) 무광고', 'r6/feed_v3_a0.0.json')]
print('\n== 방치 입력 요약 (하루) ==')
for label, f in FEEDS:
    F = feed(f); ds = F['days']
    def avg(k, a, b): return sum(d[k] for d in ds[a - 1:b]) / (b - a + 1)
    print(f'  [{label}] 처치 1-7 {avg("kills", 1, 7):.0f} · 8-30 {avg("kills", 8, 30):.0f} · 31-90 {avg("kills", 31, 90):.0f} | 꼬마 뽑기 '
          f'{avg("kid_pulls", 1, 7) if "kid_pulls" in ds[0] else avg("pulls", 1, 7) * 0.25:.1f} / {avg("kid_pulls", 31, 90) if "kid_pulls" in ds[0] else avg("pulls", 31, 90) * 0.25:.1f}'
          f' | 균사석 {avg("myc_total", 1, 7):.0f} / {avg("myc_total", 31, 90):.0f} | 광고 {ds[0].get("ads", "-")} | 스테이지 7일 {ds[6]["stage"]} 30일 {ds[29]["stage"]} 90일 {ds[-1]["stage"]}')

print('\n== W1 8단계 (일): 정책 × 퀘스트 ==')
for label, f in FEEDS:
    cells = []
    for pol in ('best', 'grade', 'firstall'):
        for q in (True, False):
            w = run(f, policy=pol, quests=q)
            cells.append(f"{pol}{'·퀘' if q else '·퀘X'} {r8(w)}")
    print(f'  [{label}] ' + ' | '.join(cells))

print('\n== 기준선 상세 (퀘스트 받음) ==')
for label, f in FEEDS[:6] + FEEDS[6:7]:
    for pol in (('best', 'grade') if label in ('보통', '무광고', '광고 많이') else ('best',)):
        w = run(f, policy=pol)
        L, m = w.log, w.m7
        stv = [L['reach'].get(s) for s in range(2, 9)]
        g = [stv[0]] + [b - a for a, b in zip(stv, stv[1:])] if None not in stv else None
        print(f"[{label} · {pol}] 2~8단계 {reach(w)} · 간격 {[R(x) for x in g] if g else None} · 비 {[R(b / a) for a, b in zip(g, g[1:])] if g else None}")
        print('    ★1/★2/★3:', {k: [R(x) for x in v] for k, v in L['star'].items()})
        print('    꼬마 얻은 날:', {k: R(v) for k, v in L['own_day'].items()}, '· 성장(방치 균사석):', {k: [R(x) for x in v] for k, v in L['grow'].items()})
        lv = {d['day']: d['kid_lv'] for d in L['day']}
        print('    꼬마 레벨 7/14/30/60일:', {dd: (min(lv[dd].values()), max(lv[dd].values())) for dd in (7, 14, 30, 60)})
        print('    단계 도달 때 (보유+제작 중 / 칸):', L['decos_at'], f"· 24개 제작 {R(L['all24'])}일")
        fill = {s: R(m['fill'][s] - L['reach'][s]) if s in m['fill'] and s in L['reach'] else None for s in range(2, 9)}
        print(f"    W7′ (a) 진화 → 칸 다 참(일) {fill} · (b) 접속 끝 보관함 최소 {m['store_min']} · 첫 배치 균사석 {m['first_myc']}")
        print(f"    8단계 세트 {m['sets_at8']} · 8단계 효과 {m['eff8']}")
        P = W.period_table(w)
        print('    구간', '/'.join(p['r'] for p in P))
        print('    골드 쓰기%', '/'.join(f"{p['g_sp']:.1f}" for p in P), '| 부탁 골드%', '/'.join(f"{p['g_req']:.2f}" for p in P),
              '| 균사석 쓰기/일', '/'.join(f"{p['m_sp']:.0f}" for p in P), '| 수입(주간·첫배치)/일', '/'.join(f"{p['m_inc']:.0f}" for p in P),
              '| 방치 균사석/일', '/'.join(f"{p['m_idle']:.0f}" for p in P), '| 순%', '/'.join(f"{p['m_net']:.1f}" for p in P))
        print('    공물 exp/일', '/'.join(f"{p['exp']:.0f}" for p in P), '| 퀘스트 exp/일', '/'.join(f"{p['exp_q']:.0f}" for p in P),
              '| 성장 균사석(방치)/일', '/'.join(f"{p['grow_myc']:.0f}" for p in P), '| 꼬마 뽑기/일', '/'.join(f"{p['kid_pulls']:.0f}" for p in P))
        print(f"    부탁 {L['req_done']}/{L['req_total']} · 주간 상자 {L['week_boxes']}/{len(w.feed['days']) // 7 + (1 if len(w.feed['days']) % 7 else 0)} · 퀘스트 받음/나옴",
              {k: f"{L['q_done'].get(k, 0)}/{v}" for k, v in L['q_shown'].items()})
        e8 = L['eff_at'].get(8)
        if e8: print('    8단계 도달 순간 효과:', {k: round(v, 3) for k, v in e8.items() if v})

print('\n== W7′(c)(d): 목표별 배치 (보통 · 퀘스트) ==')
union = set(); dates = {}
for obj in ('progress', 'gold', 'myc', 'craft', 'combat'):
    w = run('feed_now_a0.5.json', policy='best', obj=obj)
    dates[obj] = r8(w); union |= set(w.m7['placed8'] or w.placed)
    print(f'  {obj}: 8단계 {r8(w)}일 · 8단계 배치 {w.m7["placed8"]} · 세트 {w.m7["sets_at8"]}')
wg = run('feed_now_a0.5.json', policy='grade'); dates['grade'] = r8(wg); union |= set(wg.placed)
print(f'  grade: 8단계 {r8(wg)}일 · 배치 {sorted(wg.placed)}')
print('  8단계 차이 (최대 − 최소):', R(max(dates.values()) - min(dates.values())), dates)
print('  어느 목표에도 안 놓이는 장식 (d):', sorted(set(d['key'] for d in T['decos']) - union))

print('\n== 민감도 (보통 · best) ==')
for lab, o in (('세계수 균사석 몫 5%', dict(myc_share=0.05)), ('세계수 골드 몫 5%', dict(gold_share=0.05)), ('골드 몫 20%', dict(gold_share=0.20)),
               ('꼬마 성장 안 함', dict(grow=False)), ('부탁 안 함', dict(requests=False)), ('가속 씀', dict(rush=True)),
               ('광고 퀘스트 못 깸', dict(skip={'quest_ad'})), ('주간 퀘스트 안 함', dict(skip={'week_daily', 'week_stage', 'week_kill', 'week_dungeon'}))):
    w = run('feed_now_a0.5.json', **o)
    st3 = {k: R(v[2]) if len(v) > 2 else None for k, v in w.log['star'].items()}
    print(f'  [{lab}] 2~8단계 {reach(w)} · ★3 {st3}')
w = WT8(T, feed('feed_now_a0.5.json'))      # 상수 9079 설명대로 독성·약용이 창고에 안 들어간다면 (식용 확률은 그대로, 나머지 0)
w.sw = {i: (v if i <= 1010 else 0.0) for i, v in w.sw.items()}
w.run()
print(f'  [독성·약용이 창고에 안 들어감 (상수 9079 설명대로)] 2~8단계 {reach(w)}')
