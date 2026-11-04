# 사용: python diff_tables.py <표A.xlsx> <표B.xlsx>  — 시트별 달라진 행 수와 앞 6행 (GameBalance.bytes를 .xlsx로 복사해서 쓴다)
import openpyxl, sys
a = openpyxl.load_workbook(sys.argv[1], data_only=True); b = openpyxl.load_workbook(sys.argv[2], data_only=True)
for n in a.sheetnames:
    if n not in b.sheetnames: print('missing', n); continue
    ra = [list(r) for r in a[n].iter_rows(values_only=True)]; rb = [list(r) for r in b[n].iter_rows(values_only=True)]
    d = [(i, x, y) for i, (x, y) in enumerate(zip(ra, rb)) if x != y]
    print(n, len(ra), len(rb), 'diff rows', len(d))
    for i, x, y in d[:6]: print('  ', i, x[:8], '->', y[:8])
