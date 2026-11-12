# 손잡이 A(표만): 스테이지 시트 enemyHp·bossHp × k(s) 를 넣은 MainTable.xlsx 사본을 만든다 (Assets 표는 건드리지 않는다).
#   python pet_table.py <출력.xlsx> <점 json: [[s, k], ...]>
#   k(s): 점 사이 로그 보간, 첫 점 앞은 1, 끝 점 뒤는 끝 값. s ≤ 5 는 늘 1 (기준 1 무강화 구간).
#   값은 원래 셀 값 × k 를 유효숫자 6자리로 (표의 다른 셀·서식은 그대로).
# 적용 목록(이론.md 13절)의 값은 이 스크립트 --list 출력과 같다.
import sys, os, json, math, re, zipfile, shutil
import xml.etree.ElementTree as ET

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
SRC = os.path.join(ROOT, 'Assets/Data/Tables/MainTable.xlsx')
NS = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'
RID = '{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id'


def kfun(points):
    def f(s):
        if s <= 5:
            return 1.0
        if s <= points[0][0]:
            return points[0][1] if s > 5 else 1.0
        for (a, ka), (b, kb) in zip(points, points[1:]):
            if s <= b:
                return math.exp(math.log(ka) + (math.log(kb) - math.log(ka)) * (s - a) / (b - a))
        return points[-1][1]
    return f


def stage_sheet_path(z):
    wb = ET.fromstring(z.read('xl/workbook.xml'))
    rels = ET.fromstring(z.read('xl/_rels/workbook.xml.rels'))
    rmap = {r.get('Id'): r.get('Target') for r in rels}
    for sh in wb.find('{%s}sheets' % NS):
        if sh.get('name') == '스테이지':
            t = rmap[sh.get(RID)].lstrip('/')
            return t if t.startswith('xl/') else 'xl/' + t


def shared_strings(z):
    if 'xl/sharedStrings.xml' not in z.namelist():
        return []
    root = ET.fromstring(z.read('xl/sharedStrings.xml'))
    return [''.join(t.text or '' for t in si.iter('{%s}t' % NS)) for si in root.findall('{%s}si' % NS)]


def patch(src, dst, f):
    z = zipfile.ZipFile(src)
    path = stage_sheet_path(z)
    ss = shared_strings(z)
    xml = z.read(path).decode('utf-8')
    # 머리 줄(1행)에서 enemyHp·bossHp·stage 열 글자 찾기
    head = re.search(r'<row [^>]*r="1"[^>]*>(.*?)</row>', xml, re.S).group(1)
    cols = {}
    for m in re.finditer(r'<c r="([A-Z]+)1"([^>]*)>(.*?)</c>', head, re.S):
        v = re.search(r'<v>(.*?)</v>', m.group(3)) or re.search(r'<t[^>]*>(.*?)</t>', m.group(3))
        txt = v.group(1) if v else ''
        if 't="s"' in m.group(2):
            txt = ss[int(txt)]
        cols[txt] = m.group(1)
    ce, cb, cs = cols['enemyHp'], cols['bossHp'], cols['stage']
    changed = []

    def row_fn(m):
        body = m.group(0)
        rn = int(m.group(1))
        if rn < 4:
            return body
        sm = re.search(r'<c r="%s%d"[^>]*>(?:<f>.*?</f>)?<v>(.*?)</v></c>' % (cs, rn), body)
        if not sm:
            return body
        s = int(float(sm.group(1)))
        k = f(s)
        if abs(k - 1) < 1e-12:
            return body
        for col in (ce, cb):
            def cell(cm):
                old = float(cm.group(2))
                new = float('%.6g' % (old * k))
                changed.append((s, col, old, new))
                return cm.group(1) + ('%.6g' % new) + cm.group(3)
            body = re.sub(r'(<c r="%s%d"[^>]*><v>)([^<]*)(</v></c>)' % (col, rn), cell, body, count=1)
        return body

    xml2 = re.sub(r'<row [^>]*?r="(\d+)"[^>]*>.*?</row>', row_fn, xml, flags=re.S)
    with zipfile.ZipFile(dst, 'w', zipfile.ZIP_DEFLATED) as out:
        for it in z.infolist():
            data = xml2.encode('utf-8') if it.filename == path else z.read(it.filename)
            out.writestr(it, data)
    return changed


if __name__ == '__main__':
    dst, pts = sys.argv[1], json.loads(sys.argv[2])
    ch = patch(SRC, dst, kfun(pts))
    print('바뀐 셀', len(ch), '스테이지', min(c[0] for c in ch) if ch else '-', '~', max(c[0] for c in ch) if ch else '-')
    if '--list' in sys.argv:
        for s, col, o, n in ch:
            print(s, col, o, n)
