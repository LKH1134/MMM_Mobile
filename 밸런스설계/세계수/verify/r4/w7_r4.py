# W7 (라운드 4): 접속마다 "가졌지만 칸이 없어 못 놓은 장식" 수 최대 — w7_unplaced.py와 같은 방법, 입력만 v3 머지 표
import sys, json, os
sys.stdout.reconfigure(encoding='utf-8')
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.dirname(HERE))
import tables, wt_port as W
T = tables.load()
for f in ['feed_v3_a0.5.json', 'feed_v3_a0.0.json', 'feed_v3_a1.0.json', 'feed_v3_a0.5_t2.0.json', 'feed_v3_a0.5_t0.5.json', 'feed_v3_a0.0_d1.json']:
    w = W.WT(T, json.load(open(os.path.join(HERE, f), encoding='utf-8')))
    rec = {}
    orig = w.place_all
    def pa(w=w, orig=orig, rec=rec):
        orig()
        n = len(w.owned) - len(w.placed)
        rec[w.stage] = max(rec.get(w.stage, 0), n)
    w.place_all = pa
    w.run()
    print(f, '단계별 최대 못 놓은 장식', rec, '24개 완성', round(w.log['all24'] or -1, 1), '다 놓음', round(w.log['placed24'] or -1, 1))
