# MainTable.xlsx → 시트별 행 (python 표준 라이브러리만)
import zipfile, re, sys, xml.etree.ElementTree as ET
NS = {'m': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
RNS = '{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id'
PATH = 'Assets/Data/Tables/MainTable.xlsx'

def col_idx(ref):
    c = re.match(r'[A-Z]+', ref).group(0); n = 0
    for ch in c: n = n * 26 + ord(ch) - 64
    return n - 1

def load(path=PATH):
    z = zipfile.ZipFile(path)
    ss = []
    if 'xl/sharedStrings.xml' in z.namelist():
        for si in ET.fromstring(z.read('xl/sharedStrings.xml')).findall('m:si', NS):
            ss.append(''.join(t.text or '' for t in si.iter('{%s}t' % NS['m'])))
    wb = ET.fromstring(z.read('xl/workbook.xml'))
    rels = ET.fromstring(z.read('xl/_rels/workbook.xml.rels'))
    rmap = {r.get('Id'): r.get('Target') for r in rels}
    out = {}
    for sh in wb.find('m:sheets', NS):
        tgt = rmap[sh.get(RNS)].lstrip('/')
        if not tgt.startswith('xl/'): tgt = 'xl/' + tgt
        root = ET.fromstring(z.read(tgt)); rows = []
        for row in root.iter('{%s}row' % NS['m']):
            vals = {}
            for c in row.findall('m:c', NS):
                v = c.find('m:v', NS); t = c.get('t')
                if t == 'inlineStr':
                    val = ''.join(x.text or '' for x in c.iter('{%s}t' % NS['m']))
                elif v is None: continue
                elif t == 's': val = ss[int(v.text)]
                else: val = v.text
                vals[col_idx(c.get('r'))] = val
            if vals:
                rows.append([vals.get(i, '') for i in range(max(vals) + 1)])
        out[sh.get('name')] = rows
    return out

if __name__ == '__main__':
    d = load()
    want = sys.argv[1:] or list(d)
    for name in want:
        rows = d[name]
        print(f'=== {name} ({len(rows)} rows)')
        lim = int(__import__('os').environ.get('LIM', '40'))
        for r in rows[:lim]: print('\t'.join(r))
