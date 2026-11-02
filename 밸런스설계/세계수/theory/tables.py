# 이론.md 표 조각: python tables.py
import json, os
import wt_model as M, wt_tables as WT
KEYS = ['Attack', 'Hp', 'CritChance', 'CritDamage', 'KillGold', 'BossMycelite', 'Speed', 'Harvest', 'BagGold', 'BagMycelite', 'BagHours', 'CraftSpeed']
for feed, which in [('feed_live.json', 'current'), ('feed_live.json', 'v3'), ('feed_v2.json', 'v3'), ('feed_live_a0.json', 'v3'), ('feed_v2_a0.json', 'v3')]:
    tr = M.run(feed, which)
    D = {r['day']: r for r in tr.log['daily']}
    print(f'### {feed} {which}')
    print('| 단계 | 도달 일 | 방치 스테이지 | 하루 처치 | 공격 | 체력 | 치명 확률 | 치명 피해 | 처치 골드 | 보스 균사석 | 공속 | 버섯 획득 | 보따리 배율 | 보따리 골드/일 (% 하루 골드) | 보따리 균사석/일 |')
    print('|' + '---|' * 15)
    for s, d in sorted(tr.log['stage_day'].items()):
        day = min(int(d) + 1, len(D))
        r = D[day]
        b = r['bonus']
        print(f"| {s} | {d} | {r['idle_stage']} | {r['kills']} | {b['Attack']:.3f} | {b['Hp']:.3f} | {b['CritChance']:.2f} | {b['CritDamage']:.2f} | {b['KillGold']:.2f} | {b['BossMycelite']:.2f} | {b['Speed']:.2f} | {b['Harvest']:.2f} | ×{r['bag_mult']:.2f} | {r['g_bag']:.0f} ({r['g_bag']/r['gold_ke']*100:.1f}%) | {r['m_bag']:.1f} |")
    tot = lambda k, a, b_: sum(D[i][k] for i in range(a, b_ + 1) if i in D)
    print('누적 60일: 골드 쓰기 KE', round(tot('g_spend', 1, 60)), '방치 골드 KE', round(tot('gold_ke', 1, 60)), '균사석 쓰기', round(tot('m_spend', 1, 60)),
          '세계수 균사석 수입', round(tot('m_bag', 1, 60) + tot('m_week', 1, 60) + tot('m_first', 1, 60)), '방치 균사석', round(tot('myc', 1, 60)))
    print()
