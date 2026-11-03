# V1: 보통 스테이지 s−1→s 체력·공격 비 ≤ 1.15 (챕터 보스와 그 다음 스테이지 제외).  python v1_check.py <csv> [from]
import csv, sys
rows = {int(r['stage']): r for r in csv.DictReader(open(sys.argv[1], encoding='utf-8'))}
lo = int(sys.argv[2]) if len(sys.argv) > 2 else 6
cols = [c for c in ('enemyHp', 'bossHp', 'enemyAttack', 'bossAttack') if c in next(iter(rows.values()))]
worst = {c: (0, 0) for c in cols}; bad = []
for s in sorted(rows):
    if s <= lo or s % 10 in (0, 1) or s - 1 not in rows:
        continue
    for c in cols:
        r = float(rows[s][c]) / float(rows[s - 1][c])
        if r > worst[c][0]: worst[c] = (r, s)
        if r > 1.15: bad.append((s, c, round(r, 3)))
print(sys.argv[1], 'worst', {c: (round(v[0], 3), v[1]) for c, v in worst.items()}, 'over 1.15:', len(bad), bad[:20])
