# W4 되먹임 비교: 세계수 효과를 방치 진행에 넣었을 때 방치 스테이지가 며칠 앞당겨지는가 (wt-verify)
# 먼저: python run_cases.py (timeline_*.json) → python feed.py <base> 60 <ads> 1.0 tl=<timeline> (feed_*_tl*.json) → python feedback_cmp.py
import json, os, sys
sys.stdout.reconfigure(encoding='utf-8')
HERE = os.path.dirname(os.path.abspath(__file__))


def st(f):
    d = json.load(open(os.path.join(HERE, f), encoding='utf-8'))
    return {x['day']: x['stage'] for x in d['days']}


def day_of(S, s):
    for d in sorted(S):
        if S[d] >= s: return d
    return '>60'


PAIRS = [  # 라운드 3 (수치안 v3 표). 라운드 1·2 결과는 r1/·r2/
    ('v2 보통 · v3 효과 (검증 타임라인)', 'feed_v2_a0.5.json', 'feed_v2_a0.5_tlv2.json'),
    ('(라운드 2) v2 보통 · v2 효과 (이론 tl_v2.json 입력)', 'feed_v2_a0.5.json', 'r2/feed_v2_a0.5_tlthv2.json'),
    ('v2 보통 · 최대치 1일부터 (상한)', 'feed_v2_a0.5.json', 'feed_v2_a0.5_tlmax.json'),
    ('v2 무광고 · v3 효과', 'feed_v2_a0.0.json', 'feed_v2_a0.0_tlv2a0.json'),
    ('v2 무광고 · 최대치 1일부터', 'feed_v2_a0.0.json', 'feed_v2_a0.0_tlmax.json'),
    ('라이브 보통 · v3 효과 (검증 타임라인)', 'feed_live_a0.5.json', 'feed_live_a0.5_tllive.json'),
    ('(라운드 2) 라이브 보통 · v2 효과 (이론 tl_live.json 입력)', 'feed_live_a0.5.json', 'r2/feed_live_a0.5_tlthlive.json'),
    ('라이브 보통 · 최대치 1일부터', 'feed_live_a0.5.json', 'feed_live_a0.5_tlmax.json'),
    ('라이브 무광고 · v3 효과', 'feed_live_a0.0.json', 'feed_live_a0.0_tllivea0.json')]
for label, a, b in PAIRS:
    if not os.path.exists(os.path.join(HERE, b)):
        print(label, '— 없음'); continue
    A, B = st(a), st(b)
    print(label)
    print('   ' + ' · '.join(f"{d}일: {A[d]}→{B[d]} (기준 {day_of(A, B[d])}일, 앞당김 {(day_of(A, B[d]) - day_of(A, A[d])) if isinstance(day_of(A, B[d]), int) else '>' + str(60 - d)})" for d in (7, 14, 21, 30, 45, 60)))
