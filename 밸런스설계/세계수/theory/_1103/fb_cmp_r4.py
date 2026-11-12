# 라운드 4 W4: 같은 스테이지에 닿는 날의 차이. python fb_cmp_r4.py > fb_cmp_r4.log
# 앞당김(d) = d − (기준선이 d일 스테이지에 처음 닿는 날). 전체 기간 최대·30일 전 최대, 하루 스테이지 증가 최대(폭주 확인)
import json, os, sys
sys.stdout.reconfigure(encoding='utf-8')
HERE = os.path.dirname(os.path.abspath(__file__))
load = lambda f: {int(k): v for k, v in json.load(open(os.path.join(HERE, f))).items()}


def day_of(S, s):
    return next((d for d in sorted(S) if S[d] >= s), None)


def cmp(base, run):
    A, Bs = load(base), load(run)
    adv = {}
    for d in sorted(Bs):
        b = day_of(A, Bs[d])
        if b is not None:
            adv[d] = d - b
    mx = max(adv.values()); mx30 = max(v for d, v in adv.items() if d <= 30)
    at = max(adv, key=lambda d: adv[d])
    inc = max(Bs[d] - Bs[d - 1] for d in sorted(Bs) if d > 1 and d - 1 in Bs)
    return mx, mx30, at, Bs[at], min(adv.values()), inc


for name, base, run in [('보통 · v4 날짜별 효과 (120일)', 'fb_v3_a0.5_120_none.json', 'fb_v3_a0.5_120_tl_tl4_v3.json'),
                        ('보통 · 지금 표 날짜별 효과 (120일)', 'fb_v3_a0.5_120_none.json', 'fb_v3_a0.5_120_tl_tl3_v3.json'),
                        ('보통 · 최대치를 1일부터 (상한)', 'fb_v3_a0.5_120_none.json', 'fb_v3_a0.5_90_c_A1.02_H1.02_CD0.03.json'),
                        ('무광고 · v4 날짜별', 'fb_v3_a0_90_none.json', 'fb_v3_a0_90_tl_tl4_v3_a0.json'),
                        ('무광고 · 상한', 'fb_v3_a0_90_none.json', 'fb_v3_a0_90_c_A1.02_H1.02_CD0.03.json'),
                        ('광고 많이 · v4 날짜별', 'fb_v3_a1_90_none.json', 'fb_v3_a1_90_tl_tl4_v3_a1.json')]:
    mx, mx30, at, st, mn, inc = cmp(base, run)
    A = load(base)
    inc0 = max(A[d] - A[d - 1] for d in sorted(A) if d > 1)
    print(f'{name}: 앞당김 최대 {mx}일 ({at}일, 스테이지 {st}), 30일 전 최대 {mx30}일, 가장 늦어짐 {mn}일 | 하루 스테이지 증가 최대 효과 {inc} / 기준선 {inc0}')

print()
print('60일 안 앞당김 최대 · 전체 기간 같은 날 스테이지 차이 최대 (챕터 = 10)')
for name, base, run in [('보통 · v4', 'fb_v3_a0.5_120_none.json', 'fb_v3_a0.5_120_tl_tl4_v3.json'),
                        ('보통 · 상한', 'fb_v3_a0.5_120_none.json', 'fb_v3_a0.5_90_c_A1.02_H1.02_CD0.03.json'),
                        ('무광고 · v4', 'fb_v3_a0_90_none.json', 'fb_v3_a0_90_tl_tl4_v3_a0.json'),
                        ('무광고 · 상한', 'fb_v3_a0_90_none.json', 'fb_v3_a0_90_c_A1.02_H1.02_CD0.03.json'),
                        ('광고 많이 · v4', 'fb_v3_a1_90_none.json', 'fb_v3_a1_90_tl_tl4_v3_a1.json')]:
    A, Bs = load(base), load(run)
    adv60 = max(d - day_of(A, Bs[d]) for d in Bs if d <= 60 and day_of(A, Bs[d]))
    dmax = max(Bs[d] - A[d] for d in Bs if d in A)
    dmin = min(Bs[d] - A[d] for d in Bs if d in A)
    print(f'{name}: 60일 안 앞당김 최대 {adv60}일, 같은 날 스테이지 차이 {dmin:+d} ~ {dmax:+d}')
