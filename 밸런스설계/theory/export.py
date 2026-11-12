# 수치안 스테이지 시트를 CSV로 (관리자가 MainTable.xlsx 스테이지 시트에 붙여 넣는다). python export.py
import csv, os
import proposal as PR

COLS = [('stage', 'stage'), ('enemyHp', 'ehp'), ('bossHp', 'bhp'), ('wavesPerStage', 'waves'), ('bossTime', 'bossTime'),
        ('enemyCountBase', 'cb'), ('enemyCountPerWave', 'cw'), ('enemyCountPerStage', 'cs'), ('enemyCountMax', 'cmax'),
        ('enemyGold', 'gold'), ('bossGold', 'bossGold'), ('bossMycelite', 'myc'), ('enemyAttack', 'atk'), ('bossAttack', 'bossAtk')]

if __name__ == '__main__':
    rows = PR.apply()
    path = os.path.join(os.path.dirname(__file__), 'stage_proposal.csv')
    with open(path, 'w', newline='', encoding='utf-8') as f:
        w = csv.writer(f)
        w.writerow([c for c, _ in COLS])
        for s in sorted(rows):
            r = dict(rows[s], stage=s)
            w.writerow([('%.6g' % r[k]) if isinstance(r[k], float) else r[k] for _, k in COLS])
    print('wrote', path, len(rows), 'rows')
