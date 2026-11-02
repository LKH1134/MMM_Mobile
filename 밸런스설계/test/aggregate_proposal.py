# 사용: python 밸런스설계/test/aggregate_proposal.py > 밸런스설계/test/out/summary_P.txt  (run_proposal.sh 결과를 모은다)
import glob, re, os, csv, collections, statistics as st
D = os.path.join(os.path.dirname(__file__), 'out')
PREF = os.environ.get('PREF', 'P_')
MARKS = [100, 200, 300, 400]
CHAP = [10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 150, 200, 250, 300, 350, 400]
ODD = [14, 38, 57, 63, 187, 303]
groups = collections.defaultdict(list)
for f in sorted(glob.glob(f'{D}/r_{PREF}*_s[0-9]*.txt')):
    g = re.sub(r'_s\d+\.txt$', '', os.path.basename(f))[2:]
    groups[g].append(f)
def read(f): return open(f, 'rb').read().decode('cp949', 'replace')
def q(v, fmt='{:.2f}'):
    if not v: return '-'
    return f'{fmt.format(st.median(v))} [{fmt.format(min(v))}~{fmt.format(max(v))}]'
print('## 도달 (벽시계 일 중앙값 [최소~최대]) · 45일 때 스테이지')
print('그룹 | 시드 | 100 | 200 | 300 | 400 | 45일 스테이지')
for g, fs in groups.items():
    cells = []; fin = []
    mk = {m: [] for m in MARKS}
    for f in fs:
        t = read(f)
        for line in t.splitlines():
            r = re.match(r'^(\d+),([\d.]+),([\d.]+),([\d.]+)$', line)
            if r and int(r[1]) in mk: mk[int(r[1])].append(float(r[3]))
        e = re.search(r'최종 스테이지 (\d+)', t)
        if e: fin.append(int(e[1]))
    print(f'{g} | {len(fs)} | ' + ' | '.join((q(mk[m]) + f' ({len(mk[m])}/{len(fs)})') for m in MARKS) + ' | ' + q(fin, '{:.0f}'))
def stagerows(g): return [list(csv.DictReader(open(f))) for f in sorted(glob.glob(f'{D}/stage_{g}_s*.csv'))]
def spent(rows, s):
    for i, r in enumerate(rows[:-1]):
        if int(r['stage']) == s: return float(rows[i + 1]['playSec']) - float(r['playSec']), int(r['failBoss']) + int(r['defWave']) + int(r['defBoss'])
    return None
print('\n## 벽 크기: 그 스테이지에서 접속해 싸운 플레이 분 (중앙값 [최대], 실패 평균) — 시드 중 도달한 것만')
for g in groups:
    S = stagerows(g)
    if not S: continue
    print(f'### {g}')
    for title, lst in (('챕터 보스', CHAP), ('의도 밖 후보', ODD)):
        cells = []
        for s in lst:
            v = [spent(rows, s) for rows in S]; v = [x for x in v if x]
            if v: cells.append(f's{s}: {st.median(x[0] for x in v)/60:.1f}분 [{max(x[0] for x in v)/60:.1f}] 실패{st.mean(x[1] for x in v):.1f} ({len(v)})')
        print(title, '|', ' ; '.join(cells))
    # 진행 없는 날 (벽 날)
    zero = []
    for rows in S:
        day = collections.Counter(int(float(r['wallSec']) // 86400) for r in rows)
        last = int(float(rows[-1]['wallSec']) // 86400)
        zero.append(sum(1 for d in range(0, min(45, last + 1)) if day.get(d, 0) == 0))
    print('진행 0인 날(45일 중, 시드 중앙값 [최대]):', st.median(zero), max(zero))
print('\n## 재화 비중 (골드 45일 합 시드 중앙값 %, 균사석 하루 평균)')
for g, fs in groups.items():
    sh = collections.defaultdict(list); myc = collections.defaultdict(list); qd = []; eq = collections.defaultdict(list); ads = []; pulls = []; dun = []
    for f in fs:
        t = read(f)
        m = re.search(r'처치골드=(\S+) 앱밖=(\S+) 퀘스트골드=(\S+) 상자골드=(\S+) 던전골드=(\S+) \| 균사석 보스=(\d+) 퀘스트=(\d+) 상자=(\d+) 던전=(\d+) \| 광고 (\d+)회.*뽑기 (\d+)회 · 일수 (\d+)', t)
        if not m: continue
        gold = [float(x) for x in m.groups()[:5]]; tot = sum(gold) or 1
        for k, v in zip(['처치', '앱밖', '퀘스트', '상자', '던전'], gold): sh[k].append(100 * v / tot)
        days = int(m[12])
        for k, i in (('보스', 6), ('퀘스트', 7), ('상자', 8), ('던전', 9)): myc[k].append(int(m[i]) / days)
        ads.append(int(m[10]) / days); pulls.append(int(m[11]) / days)
        e = re.search(r'장비% 무기=(\d+) 방어구=(\d+) 펫=(\d+)', t)
        if e: [eq[k].append(int(e[i + 1])) for i, k in enumerate(['무기', '방어구', '펫'])]
        d = re.search(r'던전: 입장 (\d+)회 \(레벨 미달 실패 (\d+)회\)', t)
        if d: dun.append((int(d[1]), int(d[2])))
    if not sh: continue
    print(f'{g} | 골드 비중% ' + ' '.join(f'{k} {st.median(v):.1f}' for k, v in sh.items()) + ' | 균사석/일 ' + ' '.join(f'{k} {st.median(v):.1f}' for k, v in myc.items()) + f' | 광고 {st.median(ads):.1f}회/일 소환 {st.median(pulls):.0f}/일' + (f' | 던전 입장 {st.median(x[0] for x in dun):.0f} 실패 {st.median(x[1] for x in dun):.0f}' if dun else ''))
    print('   장비 분산(45일 말, 시드): ' + ' ; '.join(f'{k}% 중앙 {st.median(v):.0f} [{min(v)}~{max(v)}]' for k, v in eq.items()))
