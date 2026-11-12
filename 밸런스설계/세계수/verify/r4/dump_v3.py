# 라운드 4: 머지된 라이브 표에서 방치 쪽 바뀐 시트를 덤프 (wt-verify, 읽기만)
import openpyxl, sys, os
sys.stdout.reconfigure(encoding='utf-8')
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '../../../..'))
wb = openpyxl.load_workbook(os.path.join(ROOT, 'Assets/Data/Tables/MainTable.xlsx'), data_only=True)
for n in ['던전', '꼬마', '등급']:
    for r in wb[n].iter_rows(values_only=True):
        print(n, r)
for r in wb['상수'].iter_rows(values_only=True):
    if r[0] is not None: print('상수', r[:3])
ws = wb['스테이지']
rows = list(ws.iter_rows(values_only=True))
print(rows[0]); print(len(rows) - 3, 'rows')
for r in rows[3:]:
    if r[0] is None: continue
    s = r[rows[0].index('stage')]
    if s in (1, 2, 5, 10, 50, 100, 150, 200, 300, 400, 500, 600, 699, 700): print(r)
