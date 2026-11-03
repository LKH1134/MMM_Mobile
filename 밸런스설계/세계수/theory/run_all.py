# 라운드 4 (KHD 방치 v3 머지 뒤) 이론.md 표의 원본: python run_all.py > run_all_r4.log
# (먼저 방치 입력: python idle_feed.py v3 90 / v3 90 0.0 / v3 90 1.0 / v3 90 0.5 0.5 / v3 60 0.5 2 / v3 60 0.5 1 d1 / v3 60 0.0 1 d1 / v3 120)
import sys
sys.dont_write_bytecode = True
sys.stdout.reconfigure(encoding='utf-8')
import json, os
import wt_model as M, wt_tables as WT

CASES = [
    ('지금 표(v3) · 보통 (기준선)', 'feed_v3_90.json', 'current', {}),
    ('v4 · 보통', 'feed_v3_90.json', 'v4', {}),
    ('지금 · 무광고', 'feed_v3_a0_90.json', 'current', {}),
    ('v4 · 무광고', 'feed_v3_a0_90.json', 'v4', {}),
    ('지금 · 광고 많이', 'feed_v3_a1_90.json', 'current', {}),
    ('v4 · 광고 많이', 'feed_v3_a1_90.json', 'v4', {}),
    ('v4 · 플레이 ×0.5', 'feed_v3_t0.5_90.json', 'v4', {}),
    ('v4 · 플레이 ×2', 'feed_v3_t2.json', 'v4', {}),
    ('v4 · 보통 · 던전 1만', 'feed_v3_d1.json', 'v4', {}),
    ('v4 · 무광고 · 던전 1만', 'feed_v3_a0_d1.json', 'v4', {}),
    ('v4 · 보통 · 골드 예산 5%', 'feed_v3_90.json', 'v4', dict(gold_frac=0.05)),
    ('v4 · 보통 · 균사석 예산 5%', 'feed_v3_90.json', 'v4', dict(myc_frac=0.05)),
    ('v4 · 보통 · 균사석 예산 0%', 'feed_v3_90.json', 'v4', dict(myc_frac=0.0)),
    ('v4 · 보통 · 꼬마 뽑기 10%', 'feed_v3_90.json', 'v4', dict(char_frac=0.10)),
    ('v4 · 보통 · 꼬마 뽑기 50%', 'feed_v3_90.json', 'v4', dict(char_frac=0.50)),
    ('v4 · 보통 · 선물 안 함', 'feed_v3_90.json', 'v4', dict(gifts=0)),
    ('v4 · 보통 · 가속 씀', 'feed_v3_90.json', 'v4', dict(rush=1)),
    ('v4 · 보통 · K1 없음', 'feed_v3_90.json', 'v4-noK1', {}),
]

if __name__ == '__main__':
    for label, feed, which, kw in CASES:
        if which == 'v4-noK1':
            T = WT.apply(WT.load_current(), WT.V4)
            T['kidEvoLevelGradeMul'] = None
            tr = M.Tree(T, json.load(open(os.path.join(M.HERE, feed), encoding='utf-8'))['feed'], **kw).run()
        else:
            tr = M.run(feed, which, **kw)
        print(M.summary(tr, label))
        print()
