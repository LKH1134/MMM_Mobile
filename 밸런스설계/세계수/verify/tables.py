# 실제 GameBalance.xlsx 세계수 관련 시트 읽기 (wt-verify). 열 이름은 GameBalanceValidator.cs:16-32 스키마와 같다.
import openpyxl, os
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '../../..'))
XLSX = os.path.join(ROOT, 'Assets/Data/Tables/GameBalance.xlsx')


def _rows(ws):
    it = ws.iter_rows(values_only=True)
    head = next(it); next(it); next(it)          # 이름 · 타입 · 설명
    out = []
    for r in it:
        if r[0] is None:
            continue
        out.append({h: v for h, v in zip(head, r) if h and not str(h).startswith('#')})
    return out


def _ids(v):
    if v is None or v == '':
        return []
    return [int(x) for x in str(v).split(',')]


def load(path=XLSX):
    wb = openpyxl.load_workbook(path, data_only=True)
    T = {}
    T['tree'] = sorted(_rows(wb['세계수']), key=lambda r: r['stage'])
    for r in T['tree']:
        r['preferIds'] = _ids(r['preferIds'])
    T['bless'] = _rows(wb['가호'])
    T['kids'] = _rows(wb['꼬마'])
    for r in T['kids']:
        r['favoriteIds'] = _ids(r['favoriteIds'])
    T['stars'] = sorted(_rows(wb['꼬마진화']), key=lambda r: r['star'])
    T['aff'] = sorted(_rows(wb['호감도']), key=lambda r: r['level'])
    T['decos'] = _rows(wb['장식'])
    T['dgrades'] = {r['grade']: r for r in _rows(wb['장식등급'])}
    T['enemies'] = _rows(wb['적'])
    T['const'] = {r['key']: r['value'] for r in _rows(wb['상수'])}
    T['gacha'] = sorted(_rows(wb['뽑기']), key=lambda r: r['level'])
    return T
