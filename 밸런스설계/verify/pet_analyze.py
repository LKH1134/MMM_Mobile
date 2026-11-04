# verify 독립 분석 (manager#20, 11-04). test/theory 분석 스크립트를 보지 않고 sim 출력(stage_*.csv, snap_*.csv)만 읽는다.
#   python pet_analyze.py <out 루트> <이름>...   (이름마다 <루트>/<이름>/stage_{B|Bhalf|Bads}_s{1..10}.csv)
# 정의
#   D n      = snap 의 'Dn' 행 stage (벽시계 7시 시작 + n일)
#   1시간    = playSec ≤ 3600 에서 도달한 가장 큰 stage
#   대기(s)  = 스테이지 s 를 깨는 데 든 플레이 시간 = playSec(s+1) − playSec(s)
#   첫 1시간 5분↑ 대기 = playSec(s) < 3600 이고 대기(s) ≥ 300 인 s
#   허들 X분 시작 = 대기(s) ≥ X·60 인 첫 s
#   끝 = 마지막 stage 행, 중단 = 로그 r_*.txt 에 'stuck at'(20일 무진행)
import sys, os, csv, statistics as st, json

SC = ['B', 'Bhalf', 'Bads']
SCN = {'B': '무광고', 'Bhalf': '보통', 'Bads': '많이'}


def med(x):
    x = [v for v in x if v is not None]
    return st.median(x) if x else None


def one(path_stage, path_snap):
    rows = list(csv.DictReader(open(path_stage, encoding='utf-8')))
    S = [(int(r['stage']), float(r['wallSec']), float(r['playSec'])) for r in rows]
    snap = {r['label']: r for r in csv.DictReader(open(path_snap, encoding='utf-8'))}
    out = {}
    for d in (1, 3, 7, 14, 30, 60):
        r = snap.get('D%d' % d)
        out['D%d' % d] = int(r['stage']) if r else None
        out['atk_D%d' % d] = float(r['atkPct']) if r else None
    out['h1'] = max(s for s, w, p in S if p <= 3600)
    dur = {}
    for (s, w, p), (s2, w2, p2) in zip(S, S[1:]):
        dur[s] = p2 - p
    waits = [(s, dur[s]) for s, w, p in S if p < 3600 and s in dur and dur[s] >= 300]
    out['w5n'] = len(waits)
    out['w5min'] = sum(d for _, d in waits) / 60
    out['w5st'] = [s for s, _ in waits]
    for X in (5, 30, 60):
        hs = [s for s, w, p in S if s in dur and dur[s] >= X * 60]
        out['H%d' % X] = hs[0] if hs else None
        out['H%dday' % X] = next(((w - 25200) / 86400 for s, w, p in S if s == hs[0]), None) if hs else None
    out['end'] = S[-1][0]
    out['endDay'] = (S[-1][1] - 25200) / 86400
    pr = path_stage.replace('stage_', 'r_').replace('.csv', '.txt')
    out['stopped'] = os.path.exists(pr) and b'stuck at' in open(pr, 'rb').read()
    # 7일 안(벽시계) 30분↑ 대기 합, 최장
    in7 = [dur[s] for s, w, p in S if s in dur and w - 25200 < 7 * 86400]
    out['w30sum7'] = sum(d for d in in7 if d >= 1800) / 60
    out['max7'] = max(in7) / 60 if in7 else 0
    return out


def summarize(root, name):
    res = {}
    for sc in SC:
        L = []
        for sd in range(1, 11):
            ps = os.path.join(root, name, 'stage_%s_s%d.csv' % (sc, sd))
            pn = os.path.join(root, name, 'snap_%s_s%d.csv' % (sc, sd))
            if os.path.exists(ps) and os.path.exists(pn):
                L.append(one(ps, pn))
        if not L:
            continue
        agg = {k: med([x[k] for x in L]) for k in L[0] if not isinstance(L[0][k], (list, bool))}
        agg['n'] = len(L)
        agg['stopped'] = sum(x['stopped'] for x in L)
        agg['w5st'] = sorted(set(s for x in L for s in x['w5st']))
        agg['H60range'] = (min(x['H60'] or 0 for x in L), max(x['H60'] or 0 for x in L))
        agg['H30range'] = (min(x['H30'] or 0 for x in L), max(x['H30'] or 0 for x in L))
        res[sc] = agg
    return res


if __name__ == '__main__':
    root = sys.argv[1]
    allr = {}
    for name in sys.argv[2:]:
        allr[name] = summarize(root, name)
    hdr = ['1h', '5분↑회', '5분↑분', 'D1', 'D3', 'D7', 'D14', 'D30', 'D60', 'H5', 'H30', 'H60[범위]', 'H60일', '끝(중단)', '7일 30분↑합', '7일 최장', 'atk% D1/D7/D30']
    print('안 | 구간 | ' + ' | '.join(hdr))
    for name, res in allr.items():
        for sc, a in res.items():
            f = lambda v: '-' if v is None else ('%g' % round(v, 1))
            print(' | '.join([name, SCN[sc], f(a['h1']), f(a['w5n']), f(a['w5min']), f(a['D1']), f(a['D3']), f(a['D7']), f(a['D14']), f(a['D30']), f(a['D60']),
                              f(a['H5']), f(a['H30']), '%s[%d~%d]' % (f(a['H60']), *a['H60range']), f(a['H60day']), '%s(%d/%d)' % (f(a['end']), a['stopped'], a['n']),
                              f(a['w30sum7']), f(a['max7']), '%s/%s/%s' % (f(a['atk_D1']), f(a['atk_D7']), f(a['atk_D30']))]))
    json.dump(allr, open(os.path.join(root, 'summary_verify.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1, default=str)
