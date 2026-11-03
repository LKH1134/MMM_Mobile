# v6 수정안 비교 + 안 놓이는 장식 + W2·W3 요약: python v6_fix.py > v6_fix.log
import sys, copy
sys.dont_write_bytecode = True
sys.stdout.reconfigure(encoding='utf-8')
import wt_model as M, wt_tables as WT
from v6_run import run, FEEDS, line

def evo(ex7=None, ex6=None):
    T = WT.load_current()
    if ex6: T['tree'][5]['evoExp'] = ex6
    if ex7: T['tree'][6]['evoExp'] = ex7
    return T

print('## W1 수정안 (보통·무광고, 의뢰 있음/없음, pace)')
for k in ('보통', '무광고', '×0.5'):
    for q in (True, False):
        tag = '의뢰' if q else '의뢰 없음'
        line(f'{k} · {tag} · 지금 v6', run(FEEDS[k], 'pace', quests=q))
        line(f'{k} · {tag} · K4 세트 2개', run(FEEDS[k], 'pace', quests=q, set_rule=2))
        line(f'{k} · {tag} · evoExp 7단계 440000→410000', run(FEEDS[k], 'pace', quests=q, T=evo(410000)))

print('\n## 목표별로 한 번도 안 놓이는 장식 (보통, 의뢰, 90일)')
PROF = {'진행(pace)': {}, '보따리': dict(Harvest=2, BagGold=10, BagMycelite=10, CraftSpeed=1, BagHours=2),
        '제작': dict(Harvest=2, CraftSpeed=10, BagGold=1, BagMycelite=1), '전투': dict(Harvest=1, Hp=50, CritDamage=50, Attack=50)}
import v6_model as V
ever = {}
for name, w in PROF.items():
    tr = run(FEEDS['보통'], 'pace', weights=w)
    seen = set()
    orig = V.V6Tree.place_all
    print(f'- {name}: 8단계 {tr.log["stage_day"].get(8)} · 8단계에 놓인 것 {sorted(tr.placed)}')
    for k in tr.placed: ever.setdefault(k, set()).add(name)
never = [d['key'] for d in WT.load_current()['decos'] if d['key'] not in ever]
print('어느 목표에서도 8단계에 안 놓이는 장식:', never)

print('\n## W2·W3·W6 요약 (v6 pace, 의뢰)')
for k in ('보통', '무광고', '광고 많이'):
    print(M.summary(run(FEEDS[k], 'pace'), f'v6 · {k}'))
    print()
