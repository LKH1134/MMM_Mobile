# 되먹임 비교: python fb_cmp.py <baseline.json> <run.json> ... → 날짜별 스테이지와 앞당김(일)
# 앞당김(d) = d − (기준선이 그 스테이지에 처음 닿는 날). 기준선이 60일 안에 못 닿으면 '>' 표시
import json, sys, os
sys.stdout.reconfigure(encoding='utf-8')
HERE = os.path.dirname(os.path.abspath(__file__))
def load(f): return {int(k): v for k, v in json.load(open(os.path.join(HERE, f))).items()}
def day_of(S, s):
    for d in sorted(S):
        if S[d] >= s: return d
    return None
A = load(sys.argv[1])
for f in sys.argv[2:]:
    Bs = load(f); parts = []
    for d in (7, 14, 21, 30, 45, 60):
        if d not in Bs: continue
        b = day_of(A, Bs[d])
        parts.append(f'{d}일 {A[d]}→{Bs[d]} ({"앞당김 " + str(b - d) if b else "기준선 60일 밖"})')
    print(f, ' · '.join(parts))
