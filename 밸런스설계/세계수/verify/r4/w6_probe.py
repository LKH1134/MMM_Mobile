# W6 점검: 하루 뽑기 수·꼬마 레벨·뽑기 레벨, ★ 조건(레벨·단계·균사석) 중 무엇이 묶는가 (wt-verify)
import sys, os, json, math
sys.stdout.reconfigure(encoding='utf-8')
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.dirname(HERE))
import tables, wt_port as W
T = tables.load()
print('꼬마진화 needLevel/mycelite:', [(r['star'], r['needLevel'], r['mycelite']) for r in T['stars']], 'K1', T['const']['kidEvoLevelGradeMul'])
for f in sys.argv[1:]:
    fd = json.load(open(os.path.join(HERE, f), encoding='utf-8'))
    w = W.WT(T, fd).run()
    print(f, '하루 뽑기(1/7/14/30/45/60일):', [fd['days'][d - 1]['pulls'] for d in (1, 7, 14, 30, 45, 60)], '60일 합', sum(d['pulls'] for d in fd['days']))
    for d in (7, 14, 30, 45, 60):
        D = w.log['day'][d - 1]
        print(f'  {d}일 꼬마 뽑기 Lv {D["gacha_lv"]} 레벨', D['kid_lv'])
    for k, s in w.kids.items():
        print('   ', k, '등급', s['row']['grade'], 'evoStage', s['row']['evoStage'], '★', s['star'], '필요 Lv(★1~3)', [max(1, math.ceil(T['stars'][i]['needLevel'] * 0.7 ** s['row']['grade'] - 1e-4)) for i in (1, 2, 3)], '★ 날짜', w.log['star'].get(k))
