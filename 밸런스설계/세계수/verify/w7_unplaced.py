# W7: 접속마다 "가졌지만 칸이 없어 못 놓은 장식" 수 최대 (라운드 2)
import sys, json
sys.stdout.reconfigure(encoding='utf-8')
import tables, wt_port as W
T = tables.load()
for f in ['feed_live_a0.5.json', 'feed_v2_a0.5.json', 'feed_v2_a0.0.json', 'feed_v2_a1.0.json', 'feed_v2_a0.5_t2.0.json']:
    w = W.WT(T, json.load(open(f, encoding='utf-8')))
    rec = {}
    orig = w.place_all
    def pa(w=w, orig=orig, rec=rec):
        orig()
        n = len(w.owned) - len(w.placed)
        rec[w.stage] = max(rec.get(w.stage, 0), n)
    w.place_all = pa
    w.run()
    print(f, '단계별 최대 못 놓은 장식', rec, '24개 완성', round(w.log['all24'] or -1, 1), '다 놓음', round(w.log['placed24'] or -1, 1))
