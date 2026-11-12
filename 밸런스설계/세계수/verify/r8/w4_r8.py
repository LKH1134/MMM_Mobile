# 라운드 8 W4: 처치 골드·보스 균사석 효과의 날짜별 크기와 방치 골드·균사석에서 닿는 몫 (wt-verify)
# 처치 골드 효과는 적 골드(IdleBattle.cs:749)에 곱해지고, 앱 밖 골드는 접속 중 초당 골드(IdleAway)에서 나오므로 전투+앱 밖 골드에 닿는다.
# 던전·퀘스트·광고 상자·룰렛 골드는 EnemyGold를 직접 써서 닿지 않는다(DungeonRewards.cs:39-43, DailyQuests.cs:110-114, Roulette.cs:109).
# 보스 균사석 효과는 보스 처치 균사석에만 (IdleBattle.cs:500).  python r8/w4_r8.py > r8/w4_r8.log
import sys, os, json
sys.stdout.reconfigure(encoding='utf-8')
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE); sys.path.insert(0, os.path.dirname(HERE))
import tables
from wt_v8 import WT8
T = tables.load()
for label, f in (('보통', 'feed_now_a0.5.json'), ('무광고', 'feed_now_a0.0.json'), ('광고 많이', 'feed_now_a1.0.json')):
    F = json.load(open(os.path.join(HERE, f), encoding='utf-8'))
    for pol in ('best', 'grade'):
        w = WT8(T, F, policy=pol).run()
        kg = [d['bonus']['KillGold'] for d in w.log['day']]; bmx = [d['bonus']['BossMycelite'] for d in w.log['day']]
        first = lambda arr, v: next((i + 1 for i, x in enumerate(arr) if x >= v - 1e-9), None)
        sh = [(D['gold']['battle'] + D['gold']['offline']) / D['gold_total'] for D in F['days']]
        bsh = [D['myc']['boss'] / D['myc_total'] for D in F['days']]
        gain = [k * s for k, s in zip(kg, sh)]       # 하루 골드 증가 비율 (그날 효과 × 닿는 몫)
        mg = [k * s for k, s in zip(bmx, bsh)]
        print(f'[{label} · {pol}] 처치 골드 효과 7/14/30/45/60일 {[round(kg[i - 1], 2) for i in (7, 14, 30, 45, 60)]} · 0.1/0.2/0.3 처음 {first(kg, .1)}/{first(kg, .2)}/{first(kg, .3)}일')
        print(f'    효과가 닿는 골드 몫(전투+앱 밖) 1-7 {sum(sh[:7]) / 7:.2f} · 8-60 {sum(sh[7:60]) / 53:.2f} → 하루 골드 증가 1-7 {sum(gain[:7]) / 7 * 100:.1f}% · 8-30 {sum(gain[7:30]) / 23 * 100:.1f}% · 31-60 {sum(gain[30:60]) / 30 * 100:.1f}%')
        print(f'    60일 누적 골드 증가 {sum(g * D["gold_total"] for g, D in zip(gain[:60], F["days"])) / sum(D["gold_total"] for D in F["days"][:60]) * 100:.1f}% (= 하루치 {sum(g * D["gold_total"] for g, D in zip(gain[:60], F["days"])) / (sum(D["gold_total"] for D in F["days"][:60]) / 60):.1f}일분)'
              f' · 보스 균사석 효과 60일 {bmx[59]:.2f} × 보스 몫 {sum(bsh[7:60]) / 53 * 100:.1f}% → 하루 균사석 +{sum(mg[7:60]) / 53 * 100:.2f}%')
# test 실측(실제 DLL)의 골드 출처로 다시: 전투+앱 밖 몫
print('\n(참고) test 실측 골드 출처 (khd1110 stab_B 43일 무광고): 처치 2.715e14 · 앱 밖 9.407e14 · 퀘스트 1.343e14 · 던전 7.609e14 → 전투+앱 밖 몫 %.2f'
      % ((2.715 + 9.407) / (2.715 + 9.407 + 1.343 + 7.609)))
print('(참고) test 실측 Bhalf 7일: 처치 1.099e13 · 앱 밖 5.433e13 · 퀘스트 9.828e12 · 상자 1.072e12 · 던전 0 → %.2f' % ((1.099 + 5.433) / (1.099 + 5.433 + 0.9828 + 0.1072)))

# 되먹임용 날짜별 효과 (best 정책, 하루 끝) → r8/timeline_now_a*.json (test/idle 쪽이 WorldTree.Bonus 대신 넣어 볼 값)
for a in ('0.5', '0.0', '1.0'):
    F = json.load(open(os.path.join(HERE, f'feed_now_a{a}.json'), encoding='utf-8'))
    w = WT8(T, F, policy='best').run()
    tl = [dict(day=d['day'], **{k: round(v, 4) for k, v in d['bonus'].items()}) for d in w.log['day']]
    json.dump(tl, open(os.path.join(HERE, f'timeline_now_a{a}.json'), 'w', encoding='utf-8'))
print('timeline_now_a{0.5,0.0,1.0}.json 저장')
