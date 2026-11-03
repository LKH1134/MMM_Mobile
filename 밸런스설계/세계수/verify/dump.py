# 표 덤프 (검증용): 실제 GameBalance.xlsx를 openpyxl로 읽어 시트별 행을 출력
import openpyxl, sys, os
sys.stdout.reconfigure(encoding='utf-8')
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '../../..'))
wb = openpyxl.load_workbook(os.path.join(ROOT, 'Assets/Data/Tables/GameBalance.xlsx'), data_only=True)
want = sys.argv[1:] or wb.sheetnames
print(wb.sheetnames)
for n in want:
    ws = wb[n]
    print('==', n, ws.max_row, ws.max_column)
    for r in ws.iter_rows(values_only=True):
        if any(v is not None for v in r): print(r)
