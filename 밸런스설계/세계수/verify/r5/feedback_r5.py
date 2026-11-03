# W4 되먹임 (라운드 5, v4 표 = 9305 가지 6): 세계수 효과를 넣은 방치 진행이 효과 없는 기준선보다 며칠 앞서는가 (wt-verify)
# 앞당김(d) = (기준선이 그날 되먹임 스테이지에 처음 닿는 날) − (기준선이 그날 자기 스테이지에 처음 닿는 날)
import json, os, sys
sys.stdout.reconfigure(encoding='utf-8')
HERE = os.path.dirname(os.path.abspath(__file__))


def st(f): return {x['day']: x['stage'] for x in json.load(open(os.path.join(HERE, f), encoding='utf-8'))['days']}


def day_of(S, s):
    for d in sorted(S):
        if S[d] >= s: return d
    return None


PAIRS = [('보통 · v4 효과 (날짜별)', 'feed_v3_a0.5.json', 'feed_v3_a0.5_tlv4.json'),
         ('보통 · 최대치 1일부터 (상한)', 'feed_v3_a0.5.json', 'feed_v3_a0.5_wt.json'),
         ('무광고 · v4 효과', 'feed_v3_a0.0.json', 'feed_v3_a0.0_tlv4a0.json'),
         ('무광고 · 최대치 1일부터', 'feed_v3_a0.0.json', 'feed_v3_a0.0_wt.json'),
         ('광고 많이 · v4 효과', 'feed_v3_a1.0.json', 'feed_v3_a1.0_tlv4a1.json')]
for label, a, b in PAIRS:
    A, B = st(a), st(b)
    cells = []
    for d in (3, 7, 14, 21, 30, 45, 60):
        x = day_of(A, B[d])
        cells.append(f"{d}일 {A[d]}→{B[d]} ({'>' + str(60 - d) if x is None else x - day_of(A, A[d])})")
    print(label + ': ' + ' · '.join(cells))
    print('   최대 앞당김(1~60일):', max(((day_of(A, B[d]) or 61) - day_of(A, A[d]), d) for d in A))

print('\n같은 스테이지 도착일 차 (기준선 도착일 − 되먹임 도착일, 양수 = 세계수 효과가 앞당김). 챕터 단위 10 스테이지마다:')
for label, a, b in PAIRS:
    A, B = st(a), st(b)
    top = min(max(A.values()), max(B.values()))
    lead = [(day_of(A, s) - day_of(B, s), s) for s in range(10, top + 1, 10)]
    print(f'  {label}: 최대 {max(lead)} · 30일 전 최대 {max(l for l in lead if day_of(A, l[1]) <= 30)} · 평균 {sum(l[0] for l in lead) / len(lead):.2f}일')
