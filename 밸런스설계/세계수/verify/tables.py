# 실제 MainTable.xlsx 세계수 관련 시트 읽기 (wt-verify). 열 이름은 GameBalanceValidator.cs:15-33 스키마와 같다.
# 2026-11-12 현행화: 호감도 시트·꼬마 grade 열·의뢰 시트가 없다. 세계수 경험치 퀘스트는 '퀘스트' 시트 treeExp 열(메인 일일·주간).
# '#'로 시작하는 열(메모, 예: 꼬마진화 #bagMul)은 게임이 읽지 않으므로 버린다.
import openpyxl, os
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '../../..'))
XLSX = os.path.join(ROOT, 'Assets/Data/Tables/MainTable.xlsx')


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
    T = {'sheets': wb.sheetnames}
    T['tree'] = sorted(_rows(wb['세계수']), key=lambda r: r['stage'])
    for r in T['tree']:
        r['preferIds'] = _ids(r['preferIds'])
    T['bless'] = _rows(wb['가호'])
    T['kids'] = _rows(wb['꼬마'])                 # 시트 순서 = Formation.Ids 순서 (Formation.cs:34-57)
    for r in T['kids']:
        r['favoriteIds'] = _ids(r['favoriteIds'])
    T['stars'] = sorted(_rows(wb['꼬마진화']), key=lambda r: r['star'])
    T['decos'] = _rows(wb['장식'])
    T['dgrades'] = {r['grade']: r for r in _rows(wb['장식등급'])}
    T['enemies'] = _rows(wb['적'])
    T['const'] = {r['key']: r['value'] for r in _rows(wb['상수'])}
    T['quests'] = _rows(wb['퀘스트'])
    T['stage'] = {r['stage']: r for r in _rows(wb['스테이지'])}
    T['dungeons'] = _rows(wb['던전'])
    T['attend'] = _rows(wb['출석'])
    T['roulette'] = _rows(wb['룰렛'])
    T['gacha_products'] = _rows(wb['뽑기상품'])
    T['bosses'] = _rows(wb['보스'])
    T['resources'] = {r['index']: r for r in _rows(wb['리소스'])}
    return T
