# V5: 1~5 한 바퀴 골드(퀘스트·앱 밖·광고 없이)로 s6을 깨는가 + s6에 필요한 최소 골드(탐욕 구매).  python v5_check.py [v3]
import sys
import proposal as PR, progress as P, battle as B, formulas as F
v3 = len(sys.argv) > 1 and sys.argv[1] == 'v3'
PR.apply({'check_step': 20, 'atk_hp_gear': False, 'v3': v3} if v3 else {'check_step': 20, 'atk_hp_gear': False}, farm=150 if v3 else 75, extras=True)


def spend(p, gold, mode='balanced'):
    while True:
        st = P.best_buy(p, mode)
        if st is None or F.cost(st, p.lv[st]) > gold:
            return gold
        gold -= F.cost(st, p.lv[st]); p.lv[st] += 1


def s6_with(gold):
    p = B.Player(); spend(p, gold)
    return B.attempt(6, p)


p = B.Player(); g = 0.0
for s in range(1, 6):
    r = B.attempt(s, p); g += r.gold
    print('s', s, r.reason, round(r.time, 1), 'gold', round(r.gold))
lo, hi = 0.0, g * 20
for _ in range(40):
    mid = (lo + hi) / 2
    if s6_with(mid).ok: hi = mid
    else: lo = mid
r = s6_with(g)
print('one pass s1-5 gold', round(g), 's6 need', round(hi), 'ratio', round(g / hi, 2), 's6 with pass gold:', r.reason, 'min_hp', round(r.min_hp, 2))
