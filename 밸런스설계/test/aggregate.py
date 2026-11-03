# 사용: python 밸런스설계/test/aggregate.py > 밸런스설계/test/out/summary.txt
# out/r_<그룹>_s<시드>.txt (시뮬 로그, cp949) 를 모아 그룹별 시드 중앙값[최소~최대]를 낸다
import glob, re, collections, statistics as st, csv, os
D = os.path.join(os.path.dirname(__file__), 'out')
MARKS = [10, 50, 100, 200, 300, 500, 1000]
groups = collections.defaultdict(list)
for f in sorted(glob.glob(D + '/r_*.txt')):
    g = re.sub(r'_s\d+\.txt$', '', os.path.basename(f))[2:]
    groups[g].append(f)

def parse(f):
    t = open(f, 'rb').read().decode('cp949', 'replace')
    m = {}
    for line in t.splitlines():
        r = re.match(r'^(\d+),([\d.]+),([\d.]+),([\d.]+)$', line)
        if r: m[int(r[1])] = (float(r[3]), float(r[4]), float(r[2]))   # wallDays, playHours, wallHours
        elif re.match(r'^(\d+),', line) and '(' not in line: pass
    end = re.search(r'\[.*?\] (goal|maxDays|stuck[^·]*) · 최종 스테이지 (\d+)', t)
    stg = int(end[2]) if end else -1
    return m, stg, t

def fmt(v, unit):
    return f'{v:.2f}{unit}' if v < 100 else f'{v:.0f}{unit}'
def stat(vals, unit):
    if not vals: return '-'
    return f'{fmt(st.median(vals), unit)} [{fmt(min(vals), unit)}~{fmt(max(vals), unit)}]'

print('그룹 | 시드수 | ' + ' | '.join(f'스테이지{m}(벽시계일 / 플레이시간)' for m in MARKS) + ' | 막힘(시드중 미도달)')
for g, fs in groups.items():
    rows = [parse(f) for f in fs]
    cells = []
    for m in MARKS:
        d = [r[0][m][0] for r in rows if m in r[0]]
        p = [r[0][m][1] for r in rows if m in r[0]]
        miss = len(rows) - len(d)
        cells.append(stat(d, '일') + ' / ' + stat(p, 'h') + (f' (미도달 {miss})' if miss else ''))
    stuck = [r[1] for r in rows if r[1] < 1000]
    print(f'{g} | {len(rows)} | ' + ' | '.join(cells) + ' | ' + (f'최종 {sorted(stuck)}' if stuck else '-'))

# 허들/원인: 그룹별 스테이지 구간 소요(플레이 초) 중앙값, 실패 수
def stage_rows(tagprefix):
    out = []
    for f in sorted(glob.glob(f'{D}/stage_{tagprefix}_s*.csv')):
        out.append(list(csv.DictReader(open(f))))
    return out

print('\n## 구간별 스테이지당 소요 플레이 초 (중앙값, 시드 간) · 실패 합 · 진입 시 보스비율/생존비율 중앙값')
for g in ['A_tbl', 'A_s275', 'B_tbl', 'B_s275', 'C_tbl', 'C_s275', 'Bhalf_tbl', 'Bhalf_s275', 'Chalf_tbl', 'Chalf_s275', 'Bhalf_tbl_equipgame']:
    S = stage_rows(g)
    if not S: continue
    print(f'### {g} ({len(S)}시드)')
    bins = [(1, 5), (6, 10), (11, 20), (21, 50), (51, 100), (101, 200), (201, 300), (301, 500), (501, 700), (701, 800), (801, 900), (901, 1000)]
    print('구간 | 스테이지당 중앙 플레이초 | 최대 스테이지 소요초 | 평균 실패(보스시간/웨이브전멸/보스전멸) | 보스비율 | 생존비율')
    for a, b in bins:
        per = []; mx = []; fb = []; fw = []; fd = []; br = []; sr = []
        for rows in S:
            ts = [float(r['playSec']) for r in rows]
            ids = [i for i in range(len(rows)) if a <= int(rows[i]['stage']) <= b and i + 1 < len(rows)]
            if not ids: continue
            d = [ts[i + 1] - ts[i] for i in ids]
            per.append(st.mean(d)); mx.append(max(d))
            fb.append(sum(int(rows[i]['failBoss']) for i in ids) / len(ids))
            fw.append(sum(int(rows[i]['defWave']) for i in ids) / len(ids))
            fd.append(sum(int(rows[i]['defBoss']) for i in ids) / len(ids))
            br.append(st.median(float(rows[i]['bossRatio']) for i in ids)); sr.append(st.median(float(rows[i]['survRatio']) for i in ids))
        if per: print(f'{a}~{b} | {st.median(per):.0f} | {st.median(mx):.0f} | {st.median(fb):.2f}/{st.median(fw):.2f}/{st.median(fd):.2f} | {st.median(br):.2f} | {st.median(sr):.1f}')

print('\n## 재화·광고 합계 줄 (그룹 첫 시드)')
for g, fs in groups.items():
    t = parse(fs[0])[2]
    for line in t.splitlines():
        if line.startswith('재화 합계') or line.startswith('최종 강화'): print(g, '|', line)

print('\n## 시간 허들: 스테이지당 소요(10스테이지 이동평균 플레이초)가 처음 문턱을 넘는 스테이지 (시드 중앙값[최소~최대]) / 그 시점 누적 플레이 시간')
TH = [120, 300, 600, 1800, 3600]
for g in ['A_tbl', 'A_s275', 'A_tbl_eff', 'A_tbl_cost', 'B_tbl', 'B_s275', 'C_tbl', 'C_s275', 'Bads_tbl', 'Bhalf_tbl', 'Bhalf_s275', 'Chalf_tbl', 'Chalf_s275', 'Bhalf_tbl_equipgame']:
    S = stage_rows(g)
    if not S: continue
    cells = []
    for th in TH:
        res = []; tt = []
        for rows in S:
            ts = [float(r['playSec']) for r in rows]
            d = [ts[i + 1] - ts[i] for i in range(len(ts) - 1)]
            hit = None
            for i in range(10, len(d)):
                if st.mean(d[i - 10:i]) >= th and i > 30: hit = i; break   # 초반 스테이지 2~20 전멸 구간은 따로 본다 (i>30)
            if hit: res.append(hit + 1); tt.append(ts[hit] / 3600)
        cells.append(f'>= {th}초: ' + (f'스테이지 {st.median(res):.0f} [{min(res)}~{max(res)}] ({st.median(tt):.1f}h, {len(res)}/{len(S)}시드)' if res else '없음(끝까지 문턱 미만)'))
    print(g, '|', ' | '.join(cells))

print('\n## 초반(스테이지 1~20) 전멸 구간: 누적 플레이 시간으로 스테이지 N 도달 (중앙값, A_tbl)')
S = stage_rows('A_tbl')
for n in [2, 3, 5, 10, 15, 20, 21, 30]:
    v = [float(rows[n - 1]['playSec']) for rows in S]
    print(f'스테이지 {n}: {st.median(v):.0f}초 [{min(v):.0f}~{max(v):.0f}]')

print('\n## 가장 큰 스테이지 소요 (A_tbl 시드별 상위 3)')
for rows in S[:5]:
    ts = [float(r['playSec']) for r in rows]
    d = sorted(((ts[i + 1] - ts[i], i + 1) for i in range(len(ts) - 1)), reverse=True)[:3]
    print([f'{int(a)}초@{b}' for a, b in d])
