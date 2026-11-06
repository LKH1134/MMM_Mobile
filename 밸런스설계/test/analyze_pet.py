# 사용: python 밸런스설계/test/analyze_pet.py > 밸런스설계/test/out/summary_pet.txt
# out/stage_pet_{base|cur}_{B|Bhalf|Bads}_s{1..10}.csv, out/snap_pet_*.csv, out/pet/r_*.txt 를 모아 전/후 비교 표(시드 중앙값[최소~최대])를 낸다
import csv, glob, os, re, statistics as st, bisect
D = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out')
SC = [('B', '무광고'), ('Bhalf', '보통(half)'), ('Bads', '많이(전부)')]
VS = [('base', '펫 있음(7b17e68)'), ('cur', '펫 삭제 후')]
SEEDS = range(1, 11)

def load_stage(v, nm, s):
    f = f'{D}/stage_pet_{v}_{nm}_s{s}.csv'
    rows = list(csv.DictReader(open(f)))
    return [(int(r['stage']), float(r['wallSec']), float(r['playSec'])) for r in rows]

def med(v, f='{:.0f}'):
    v = [x for x in v if x is not None]
    if not v: return '-'
    return f.format(st.median(v)) + ' [' + f.format(min(v)) + '~' + f.format(max(v)) + ']'

def stage_at(rows, key, t):   # key 1 = wall, 2 = play
    ts = [r[key] for r in rows]
    i = bisect.bisect_right(ts, t) - 1
    return rows[i][0] if i >= 0 else 1

def waits(rows, maxplay=None):   # (stage, 대기 play초): 스테이지 s에 도달해서 s+1에 도달하기까지(마지막은 끝 시각까지가 아니라 기록 없음 → 제외)
    out = []
    for i in range(len(rows) - 1):
        out.append((rows[i][0], rows[i + 1][2] - rows[i][2], rows[i][2]))
    return out

def first_hour(rows):
    # 플레이 시간 0~3600초 구간에서 5분 이상 대기: (스테이지, 초). 마지막 미완 구간은 1시간 끝까지의 대기를 쓴다
    res = []
    for i, r in enumerate(rows):
        if r[2] >= 3600: break
        nxt = rows[i + 1][2] if i + 1 < len(rows) else 1e18
        w = min(nxt, 3600) - r[2]
        if w >= 300: res.append((r[0], w))
    return res

out = []
P = out.append
data = {}
for nm, _ in SC:
    for v, _ in VS:
        data[(v, nm)] = [load_stage(v, nm, s) for s in SEEDS]

P('## 1. 도달 스테이지 (벽시계 n일 경과 시점, 시드 10개 중앙값[최소~최대])')
P('시나리오 | 빌드 | 10분(플레이) | 30분(플레이) | 1시간(플레이) | D1 | D3 | D7 | D30 | D60 | 최종(끝 시점)')
for nm, nl in SC:
    for v, vl in VS:
        R = data[(v, nm)]
        cells = []
        for pm in (10, 30, 60): cells.append(med([stage_at(r, 2, pm * 60) for r in R]))
        for d in (1, 3, 7, 30, 60): cells.append(med([stage_at(r, 1, d * 86400) for r in R]))
        cells.append(med([r[-1][0] for r in R]))
        P(f'{nl} | {vl} | ' + ' | '.join(cells))

P('\n## 2. 첫 1시간(플레이) 분 단위 스테이지 (중앙값)')
P('시나리오 | 빌드 | ' + ' | '.join(f'{m}분' for m in range(5, 61, 5)))
for nm, nl in SC:
    for v, vl in VS:
        R = data[(v, nm)]
        P(f'{nl} | {vl} | ' + ' | '.join(med([stage_at(r, 2, m * 60) for r in R]).split(' ')[0] for m in range(5, 61, 5)))

P('\n## 3. 첫 1시간(플레이) 5분 이상 대기 (시드 10개 중 대기가 있는 시드 수 · 대기 시작 스테이지 목록 · 최장 대기 초)')
P('시나리오 | 빌드 | 대기 있는 시드 | 시드당 대기 횟수 중앙[범위] | 시드당 대기 합계(분) 중앙 | 스테이지(시드별, 대기초) | 1분당 평균 스테이지 상승(시드 중앙)')
for nm, nl in SC:
    for v, vl in VS:
        R = data[(v, nm)]
        fh = [first_hour(r) for r in R]
        n = sum(1 for x in fh if x)
        lst = '; '.join(f's{i+1}:' + ','.join(f'{s}({w:.0f}s)' for s, w in x) for i, x in enumerate(fh) if x) or '-'
        rate = st.median(stage_at(r, 2, 3600) / 60 for r in R)
        P(f'{nl} | {vl} | {n}/10 | {med([len(x) for x in fh])} | {st.median(sum(w for _, w in x) / 60 for x in fh):.0f} | {lst} | {rate:.1f}')

P('\n## 4. 허들 시작 스테이지 (스테이지 1개를 깨는 데 플레이 시간이 처음 문턱을 넘는 스테이지, 시드 중앙값[최소~최대] / 그 시점 벽시계 일)')
P('시나리오 | 빌드 | ≥5분 | ≥30분 | ≥60분 | 120일 안 최장 한 스테이지 대기(분)')
for nm, nl in SC:
    for v, vl in VS:
        R = data[(v, nm)]
        cells = []
        for th in (300, 1800, 3600):
            ss = []; dd = []
            for r in R:
                f = next(((s, i) for i, (s, w, t) in enumerate(waits(r)) if w >= th), None)
                if f: ss.append(f[0]); dd.append(r[f[1]][1] / 86400)
                else: ss.append(None)
            miss = sum(1 for x in ss if x is None)
            cells.append(med(ss) + (f' (없음 {miss})' if miss else '') + (' / D' + med([d for d in dd], '{:.1f}') if dd else ''))
        cells.append(med([max(w for _, w, _ in waits(r)) / 60 for r in R], '{:.0f}'))
        P(f'{nl} | {vl} | ' + ' | '.join(cells))

# 스냅샷
def load_snap(v, nm, s):
    rows = list(csv.DictReader(open(f'{D}/snap_pet_{v}_{nm}_s{s}.csv')))
    return {r['label']: r for r in rows}
LABS = ['0.5h', '1h', '4h', 'D1', 'D3', 'D7', 'D14', 'D30', 'D60']
P('\n## 5. 공격 배율·장비% 추이 (스냅샷 시점 = 벽시계, 시드 중앙값). 공격배율% = IdleProgress.AttackMultiplier의 (배율-1)×100 (펫 있음: 무기+펫, 삭제 후: 무기)')
P('시나리오 | 빌드 | 항목 | ' + ' | '.join(LABS))
for nm, nl in SC:
    for v, vl in VS:
        S = [load_snap(v, nm, s) for s in SEEDS]
        keys = ['eqWeapon', 'eqAccessory'] + (['eqPet'] if v == 'base' else []) + ['atkPct']
        names = {'eqWeapon': '무기%', 'eqAccessory': '방어구%', 'eqPet': '펫%', 'atkPct': '공격배율%'}
        for k in keys:
            vals = []
            for lab in LABS:
                x = [float(s[lab][k]) for s in S if lab in s]
                vals.append(f'{st.median(x):.0f}' if x else '-')
            P(f'{nl} | {vl} | {names[k]} | ' + ' | '.join(vals))
        for k, kn in (('stage', '스테이지'), ('pulls', '누적 뽑기(회)'), ('mycHeld', '보유 균사석'), ('mycGainedTotal', '누적 균사석 획득(보스·퀘스트·상자·던전)')):
            vals = []
            for lab in LABS:
                x = [float(s[lab][k]) for s in S if lab in s]
                vals.append(f'{st.median(x):.0f}' if x else '-')
            P(f'{nl} | {vl} | {kn} | ' + ' | '.join(vals))
        for k, kn in (('gLvW', '뽑기Lv 무기'), ('gLvA', '뽑기Lv 방어구'), ('gLvC', '뽑기Lv 꼬마')):
            vals = []
            for lab in LABS:
                x = [float(s[lab][k]) for s in S if lab in s]
                vals.append(f'{st.median(x):.0f}' if x else '-')
            P(f'{nl} | {vl} | {kn} | ' + ' | '.join(vals))
P('\n## 6. 끝 시점(최대 120일, 20일 무진행이면 중단): 마지막 스테이지 · 마지막 도달 벽시계 일 (시드 중앙값[범위])')
P('시나리오 | 빌드 | 마지막 스테이지 | 마지막 스테이지에 닿은 벽시계 일 | 중단(20일 무진행) 시드 수')
for nm, nl in SC:
    for v, vl in VS:
        R = data[(v, nm)]
        stuck = 0
        for s in SEEDS:
            tx = open(f'{D}/pet/r_pet_{v}_{nm}_s{s}.txt', 'rb').read().decode('cp949', 'replace')
            if '[끝] stuck' in tx: stuck += 1
        P(f'{nl} | {vl} | {med([r[-1][0] for r in R])} | {med([r[-1][1] / 86400 for r in R], "{:.1f}")} | {stuck}/10')
print('\n'.join(out))
