import openpyxl, sys, os
sys.stdout.reconfigure(encoding='utf-8')
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '../../../..'))
wb = openpyxl.load_workbook(os.path.join(ROOT, 'Assets/Data/Tables/MainTable.xlsx'), data_only=True)
for r in wb['업그레이드'].iter_rows(values_only=True): print(r)
