# W4 되먹임 (라운드 6, 의뢰 v5.1): 의뢰로 일찍 쌓인 세계수 효과를 방치 진행에 넣었을 때 (wt-verify) — 90일
import json, os, sys
sys.stdout.reconfigure(encoding='utf-8')
HERE = os.path.dirname(os.path.abspath(__file__))
def st(f): return {x['day']: x['stage'] for x in json.load(open(os.path.join(HERE, f), encoding='utf-8'))['days']}
def day_of(S, s):
    for d in sorted(S):
        if S[d] >= s: return d
    return None
PAIRS = [('보통 · 의뢰 효과 (날짜별)', 'feed_v3_a0.5.json', 'feed_v3_a0.5_tlq.json'),
         ('보통 · 최대치 1일부터 (상한)', 'feed_v3_a0.5.json', 'feed_v3_a0.5_wt.json'),
         ('무광고 · 의뢰 효과', 'feed_v3_a0.0.json', 'feed_v3_a0.0_tlqa0.json'),
         ('무광고 · 최대치 1일부터', 'feed_v3_a0.0.json', 'feed_v3_a0.0_wt.json'),
         ('광고 많이 · 의뢰 효과', 'feed_v3_a1.0.json', 'feed_v3_a1.0_tlqa1.json')]
for label, a, b in PAIRS:
    A, B = st(a), st(b)
    print(label + ': ' + ' · '.join(f"{d}일 {A[d]}→{B[d]}" for d in (7, 14, 21, 30, 45, 60, 75, 90)))
    top = min(max(A.values()), max(B.values()))
    lead = [(day_of(A, s) - day_of(B, s), s) for s in range(10, top + 1, 10)]
    print(f'   같은 스테이지 도착일 차: 최대 {max(lead)} · 30일 전 최대 {max(l for l in lead if day_of(A, l[1]) <= 30)} · 60일 전 최대 {max(l for l in lead if day_of(A, l[1]) <= 60)} · 평균 {sum(l[0] for l in lead) / len(lead):.2f}일 · 늦어진 최대 {min(lead)}')
