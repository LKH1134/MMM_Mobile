# 시드별 허들 상세 (manager#20, 11-05): 첫 1시간 5분↑ 대기 스테이지, 60분 허들 시작(시드별), s170·180 대기(분), 7일 안 60분↑ 대기 수
#   python pet_rerun/detail.py <out 루트> <안>...
import sys, os, csv, statistics as st
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from pet_analyze import one
root = sys.argv[1]
for v in sys.argv[2:]:
    for sc in ['B', 'Bhalf', 'Bads']:
        L, w, n60 = [], [], []
        for sd in range(1, 11):
            ps = os.path.join(root, v, 'stage_%s_s%d.csv' % (sc, sd))
            L.append(one(ps, ps.replace('stage_', 'snap_')))
            S = [(int(r['stage']), float(r['wallSec']), float(r['playSec'])) for r in csv.DictReader(open(ps))]
            dur = {a[0]: b[2] - a[2] for a, b in zip(S, S[1:])}
            w.append(round(max(dur.get(s, 0) for s in (170, 180)) / 60))
            n60.append(sum(1 for a, b in zip(S, S[1:]) if b[2] - a[2] >= 3600 and a[1] - 25200 < 7 * 86400))
        print(v, sc, '첫1시간 5분↑ 스테이지', sorted(set(s for x in L for s in x['w5st'])), '| H60 시드별', [x['H60'] for x in L],
              '| H60 벽시계일 중앙 %.1f' % st.median([x['H60day'] for x in L]), '| s170·180 최대 대기(분)', w, '| 7일 안 60분↑ 대기 수 중앙', st.median(n60))
