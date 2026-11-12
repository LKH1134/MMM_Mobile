# 의뢰 v5: 하루·주간 진행량 (라이브 플레이만 = feed 접속 기록). python quest_probe.py > quest_probe.log
#   kill = 보스 뺀 처치 (WorldTree.AddHarvest 안 QuestKill), species = kill / 10 (일반 적 그림 10종 균등, IdleBattle.Spawn),
#   stage = 보스 처치(돌파) 수 (IdleBattle.cs:405 지금 코드), boss = 보스 등장(도전) 수 (IdleBattle.cs:346, 대안),
#   던전 1 판 = 입장 2 + 광고 2 × 광고 비율 (던전 1 해금 = 스테이지 5, 첫날 첫 접속)
import json, os, sys
sys.stdout.reconfigure(encoding='utf-8')
HERE = os.path.dirname(os.path.abspath(__file__))
FEEDS = [('보통', 'feed_v3_90.json', 0.5), ('무광고', 'feed_v3_a0_90.json', 0.0), ('광고 많이', 'feed_v3_a1_90.json', 1.0),
         ('플레이 ×0.5', 'feed_v3_t0.5_90.json', 0.5), ('플레이 ×2', 'feed_v3_t2.json', 0.5)]


def pct(xs, q):
    s = sorted(xs); return s[min(len(s) - 1, int(q * len(s)))]


for label, f, ads in FEEDS:
    D = json.load(open(os.path.join(HERE, f), encoding='utf-8'))['feed']
    rows = []
    for d in D:
        S = d['sessions']
        rows.append(dict(day=d['day'], kill=sum(s['kills'] for s in S), stage=sum(s['stages'] for s in S),
                         boss=sum(s['boss_tries'] for s in S), s1=S[0]['kills'], st1=S[0]['stages'], b1=S[0]['boss_tries']))
    print(f'## {label} ({f}), 던전 1 하루 {2 + round(2 * ads)}판')
    for lo, hi in [(1, 7), (8, 14), (15, 30), (31, 45), (46, 60), (61, 90)]:
        R = [r for r in rows if lo <= r['day'] <= hi]
        if not R:
            continue
        k = [r['kill'] for r in R]; st = [r['stage'] for r in R]; b = [r['boss'] for r in R]
        print(f'd{lo}-{hi}: kill 최소 {min(k)} 중앙 {pct(k, .5)} | species ≈ kill/10 최소 {min(k)//10} | '
              f'stage 최소 {min(st)} 중앙 {pct(st, .5)} 최대 {max(st)} (3 미만 날 {sum(1 for x in st if x < 3)}/{len(st)}, 1 미만 {sum(1 for x in st if x < 1)}) | '
              f'boss 도전 최소 {min(b)} 중앙 {pct(b, .5)} | 첫 접속(30분) kill {min(r["s1"] for r in R)} stage {min(r["st1"] for r in R)} boss {min(r["b1"] for r in R)}')
    for w in range(0, 84, 7):
        R = rows[w:w + 7]
        if len(R) < 7:
            break
        print(f'  주 {w // 7 + 1} (d{w + 1}-{w + 7}): kill {sum(r["kill"] for r in R)} stage {sum(r["stage"] for r in R)} boss {sum(r["boss"] for r in R)}', end='')
    print('\n')
