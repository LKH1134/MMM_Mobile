# 기준 8 확인 (manager#20): Equipment.cs:251-256 BaseOwnPercent·259 LevelMultiplier·262/266 Own/Equip 를 옮겨, 무기 배율 m 에서
# 장비 순서(등급·단계)대로 "다음 장비 Lv1 보유 > 이전 장비 최대 레벨 보유"가 지켜지는지(등급 경계) 본다. 표는 xlsx 직접 읽기.
import sys, re, zipfile
src = sys.argv[1]; ms = [float(x) for x in sys.argv[2:]] or [1.0, 1.75]
z = zipfile.ZipFile(src)
wb = z.read('xl/workbook.xml').decode('utf-8'); rels = z.read('xl/_rels/workbook.xml.rels').decode('utf-8')
rmap = {m.group(2): m.group(1) for m in re.finditer(r'Target="([^"]*)" Id="([^"]*)"', rels)}
paths = {m.group(1): rmap[m.group(2)].lstrip('/') for m in re.finditer(r'<sheet [^>]*?name="([^"]*)"[^>]*?r:id="([^"]*)"', wb)}
def sheet(n):
    x = z.read(paths[n]).decode('utf-8'); out = []
    for r in re.findall(r'<row [^>]*>(.*?)</row>', x, re.S):
        cells = {}
        for m in re.finditer(r'<c r="([A-Z]+)\d+"[^>]*>(.*?)</c>', r, re.S):
            t = re.search(r'<t[^>]*>(.*?)</t>', m.group(2), re.S) or re.search(r'<v>(.*?)</v>', m.group(2))
            cells[m.group(1)] = t.group(1) if t else ''
        out.append(cells)
    h = out[0]; return [{h[c]: r.get(c, '') for c in h} for r in out[3:] if r.get('A', '')]
C = {r['key']: float(r['value']) for r in sheet('상수')}
G = sorted((int(r['grade']), float(r['ownBase'])) for r in sheet('등급'))
own = [b for _, b in G]
E = [r for r in sheet('장비')]
print('장비 시트 열', list(E[0].keys())[:12])
for kind in sorted(set(r.get('kind', r.get('gachaKind', '')) for r in E)):
    items = sorted([r for r in E if r.get('kind', r.get('gachaKind', '')) == kind], key=lambda r: int(r['itemIndex']))
    size = {}
    for r in items: size[int(r['grade'])] = size.get(int(r['grade']), 0) + 1
    for m in ms:
        def base(r):
            g = int(r['grade']); f = own[g]; to = own[g + 1] if g + 1 < len(own) else f * C['equipTopGradeMul']
            return f * (to / f) ** (int(r['sub']) / max(1, size[g])) * m
        LM = lambda lv: 1 + C['equipLevelBonus'] * (lv - 1)
        mx = int(C['equipMaxLevel'])
        vals = [(int(r['itemIndex']), int(r['grade']), int(r['sub']), base(r)) for r in items]
        bad = []
        for (i1, g1, s1, b1), (i2, g2, s2, b2) in zip(vals, vals[1:]):
            # 등급 경계: 높은 등급 Lv1(보유) > 낮은 등급 최대 레벨(보유). 장착도 같은 비(× equipRatio)라 같다
            if g2 > g1 and not (b2 * LM(1) > b1 * LM(mx)): bad.append((i1, i2, b1 * LM(mx), b2))
        ratio = [round(b2 / b1, 4) for (_, g1, _, b1), (_, g2, _, b2) in zip(vals, vals[1:])]
        print('종류', kind, 'm', m, '장비', len(vals), 'Lv1 보유%', [round(v[3], 2) for v in vals], '이웃비', ratio,
              '최대Lv 배수', LM(mx), '등급 경계 위반', bad)
