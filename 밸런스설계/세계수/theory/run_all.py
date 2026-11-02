# 이론.md 표의 원본을 한 번에 만든다: python run_all.py > run_all.log
# (먼저 feed 4개: python idle_feed.py live 60 / v2 60 / live 60 0.0 / v2 60 0.0)
import wt_model as M

CASES = [
    ('지금 표(= v1 적용됨) · 라이브 보통', 'feed_live.json', 'current', {}),
    ('지금 표(= v1 적용됨) · v2 보통', 'feed_v2.json', 'current', {}),
    ('v2 · 라이브 보통 (기준선)', 'feed_live.json', 'v2', {}),
    ('v2 · v2 보통 (민감도)', 'feed_v2.json', 'v2', {}),
    ('v2 · 라이브 무광고', 'feed_live_a0.json', 'v2', {}),
    ('v2 · v2 무광고', 'feed_v2_a0.json', 'v2', {}),
    ('v2 · v2 보통 · 골드 예산 5%', 'feed_v2.json', 'v2', dict(gold_frac=0.05)),
    ('v2 · v2 보통 · 균사석 예산 5%', 'feed_v2.json', 'v2', dict(myc_frac=0.05)),
    ('v2 · v2 보통 · 균사석 예산 0%', 'feed_v2.json', 'v2', dict(myc_frac=0.0)),
    ('v2 · v2 무광고 · 균사석 예산 0%', 'feed_v2_a0.json', 'v2', dict(myc_frac=0.0)),
    ('v2 · v2 보통 · 꼬마 뽑기 10%', 'feed_v2.json', 'v2', dict(char_frac=0.10)),
    ('v2 · v2 보통 · 꼬마 뽑기 50%', 'feed_v2.json', 'v2', dict(char_frac=0.50)),
    ('v2 · v2 보통 · 선물 안 함', 'feed_v2.json', 'v2', dict(gifts=0)),
    ('v2 · v2 보통 · 선물 접속당 2', 'feed_v2.json', 'v2', dict(gifts=2)),
    ('v2 · v2 보통 · 가속 씀', 'feed_v2.json', 'v2', dict(rush=1)),
    ('v2 · 라이브 보통 · K1 없음', 'feed_live.json', 'v2-noK1', {}),
    ('v2 · v2 보통 · K1 없음', 'feed_v2.json', 'v2-noK1', {}),
]

if __name__ == '__main__':
    for label, feed, which, kw in CASES:
        if which == 'v2-noK1':
            import json, os, wt_tables as WT
            T = WT.apply(WT.load_current(), WT.V2)
            T['kidEvoLevelGradeMul'] = None
            tr = M.Tree(T, json.load(open(os.path.join(M.HERE, feed)))['feed'], **kw).run()
        else:
            tr = M.run(feed, which, **kw)
        print(M.summary(tr, label))
        print()
