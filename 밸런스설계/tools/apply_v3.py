# 이론 v3 (밸런스설계/이론.md 10.2 v3 표) 를 MainTable.xlsx에 넣는다. 사용: python apply_v3.py <출력.xlsx>
# 던전 새 열(C7·C8)은 보상 연결 방식이 정해진 뒤에 넣는다 — 여기서는 해금 스테이지만
import csv, re, sys, zipfile
from xml.sax.saxutils import escape

SRC = r'C:\Project\MMM_Mobile\Assets\Data\Tables\MainTable.xlsx'
CSV = r'C:\Project\MMM_Mobile\밸런스설계\theory\stage_proposal_v3.csv'
DST = sys.argv[1]
LAST_STAGE = 700

zin = zipfile.ZipFile(SRC)
order = zin.namelist()
files = {n: zin.read(n) for n in order}
wb = files['xl/workbook.xml'].decode('utf-8')
rels = files['xl/_rels/workbook.xml.rels'].decode('utf-8')


def sheet_path(name):
    rid = re.search(r'<sheet name="%s" sheetId="\d+" r:id="(rId\d+)"' % name, wb).group(1)
    t = re.search(r'Id="%s"[^>]*Target="([^"]+)"' % rid, rels) or re.search(r'Target="([^"]+)"[^>]*Id="%s"' % rid, rels)
    return 'xl/' + t.group(1).lstrip('/').replace('xl/', '', 1)


def num(v):
    v = float(v)
    if v == int(v) and abs(v) < 1e15: return str(int(v))
    return repr(v)


class Sheet:
    def __init__(self, name):
        self.path = sheet_path(name)
        self.s = files[self.path].decode('utf-8')

    def _cell(self, ref):
        m = re.search(r'<c r="%s"(?: [^>]*)?(?:/>|>.*?</c>)' % ref, self.s, flags=re.S)
        assert m, ref
        return m

    def style(self, ref):
        return re.search(r' s="(\d+)"', self._cell(ref).group(0)).group(1)

    def set_num(self, ref, v):
        m = self._cell(ref)
        self.s = self.s[:m.start()] + f'<c r="{ref}" s="{self.style(ref)}"><v>{num(v)}</v></c>' + self.s[m.end():]

    def set_str(self, ref, text):
        m = self._cell(ref)
        new = f'<c r="{ref}" s="{self.style(ref)}" t="inlineStr"><is><t xml:space="preserve">{escape(text)}</t></is></c>'
        self.s = self.s[:m.start()] + new + self.s[m.end():]

    def save(self):
        files[self.path] = self.s.encode('utf-8')


def row_of(sh, col, key):
    # key 열 값(숫자 index)이 key인 행 번호
    m = re.search(r'<c r="%s(\d+)"[^>]*><v>%s</v></c>' % (col, key), sh.s)
    assert m, (col, key)
    return int(m.group(1))


# ── 스테이지: 1~700 = stage_proposal_v3.csv, 701~1000 행 삭제 ──
COLS = {'wavesPerStage': 'G', 'bossTime': 'H', 'enemyCountBase': 'I', 'enemyCountPerWave': 'J', 'enemyCountPerStage': 'K',
        'enemyCountMax': 'L', 'enemyGold': 'M', 'bossGold': 'N', 'bossMycelite': 'O', 'enemyAttack': 'P', 'bossAttack': 'Q',
        'enemyHp': 'T', 'bossHp': 'U'}
st = Sheet('스테이지')
rows = list(csv.DictReader(open(CSV, encoding='utf-8')))
assert len(rows) == LAST_STAGE and rows[-1]['stage'] == str(LAST_STAGE)
for r in rows:
    ln = int(r['stage']) + 3
    assert f'<c r="C{ln}" s="6"><v>{r["stage"]}</v></c>' in st.s, ln
    for k, col in COLS.items(): st.set_num(f'{col}{ln}', r[k])
    # 메모 열(#enemyHpExample·#bossHp)은 옛 stageCoefficient 수식이었다 — 이제 체력 열 값 그대로
    st.set_num(f'R{ln}', r['enemyHp'])
    st.set_num(f'S{ln}', r['bossHp'])
last_row = LAST_STAGE + 3
st.s = re.sub(r'<row r="(\d+)"[^>]*>.*?</row>', lambda m: '' if int(m.group(1)) > last_row else m.group(0), st.s, flags=re.S)
st.s = re.sub(r'<dimension ref="A1:U\d+"/>', f'<dimension ref="A1:U{last_row}"/>', st.s)
assert '<f>' not in st.s and '<f ' not in st.s
st.save()
for n in order:
    if n.startswith('xl/tables/'):
        t = files[n].decode('utf-8')
        if 'name="StageRows"' in t:
            t = t.replace('ref="A3:S1003"', f'ref="A3:S{last_row}"')
            assert f'ref="A3:S{last_row}"' in t
            files[n] = t.encode('utf-8')

# ── 업그레이드 ──
up = Sheet('업그레이드')
for key, maxlv in (('4001', 29900), ('4002', 29900), ('4003', 19400)):
    ln = row_of(up, 'A', key)
    up.set_str(f'G{ln}', 'Power')
    up.set_num(f'I{ln}', 3.5)       # priceExponent
    up.set_num(f'K{ln}', maxlv)
    up.set_num(f'U{ln}', 3)         # valueExponent
    up.set_num(f'V{ln}', 10)        # valueDivisor
up.set_num(f'K{row_of(up, "A", "4004")}', 275)     # 공속 3.00/s 봉인 (기준 5)
up.s = up.s.replace('<formula1>"Add,Multiply"</formula1>', '<formula1>"Add,Multiply,Power"</formula1>')
# Lv 10 능력치 메모 수식: Power면 baseValue × (1 + 10 / valueDivisor)^valueExponent
up.s = up.s.replace('IF(G4="Multiply",E4*(1+F4*10),E4+F4*10)', 'IF(G4="Power",E4*(1+10/V4)^U4,IF(G4="Multiply",E4*(1+F4*10),E4+F4*10))')
up.save()

# ── 퀘스트 ──
qu = Sheet('퀘스트')
for key, target, gold, myc in (('9201', 300, 2000, 75), ('9202', 3, 3000, 75), ('9203', 20, 2000, 75), ('9204', 11, 3000, 75), ('9205', 3, 6000, 50)):
    ln = row_of(qu, 'A', key)
    qu.set_num(f'D{ln}', target); qu.set_num(f'E{ln}', gold); qu.set_num(f'F{ln}', myc)
qu.set_str(f'G{row_of(qu, "A", "9202")}', '보스 도전 (보스가 나오면 1)')
qu.save()

# ── 뽑기 ──
EXP = [20, 30, 20, 40, 60, 80, 100, 150, 200, 0]
ODDS = [(100, 0, 0, 0, 0), (70, 30, 0, 0, 0), (58, 30, 12, 0, 0), (55, 30, 15, 0, 0), (47, 30, 18, 5, 0),
        (43, 30, 20, 7, 0), (38.5, 30, 22, 8.5, 1), (33, 30, 25, 10.5, 1.5), (27.5, 30, 28, 12.5, 2), (21, 30, 30, 16, 3)]
ga = Sheet('뽑기')
for i in range(10):
    assert abs(sum(ODDS[i]) - 100) < 1e-9
    ln = row_of(ga, 'A', str(8201 + i))
    ga.set_num(f'D{ln}', EXP[i])
    for c, v in zip('EFGHI', ODDS[i]): ga.set_num(f'{c}{ln}', v)
ga.save()

# ── 던전 해금 ──
du = Sheet('던전')
for i, s in enumerate((5, 25, 150, 250, 400)): du.set_num(f'D{row_of(du, "A", str(9101 + i))}', s)
du.save()

# ── 등급 ownBase ──
gr = Sheet('등급')
for i, v in enumerate((2, 10, 50, 250, 1250)): gr.set_num(f'D{row_of(gr, "A", str(8101 + i))}', v)
gr.save()

# ── 꼬마 attackMultiplier ──
gg = Sheet('꼬마')
for i, v in enumerate((0.75, 0.75, 0.9, 1.08, 0.9, 1.30, 0.9, 1.08, 1.56)): gg.set_num(f'E{row_of(gg, "A", str(3001 + i))}', v)
gg.save()

# ── 상수: 값 바꾸기 + 새 상수 ──
co = Sheet('상수')
SET = {'equipLevelBonus': 0.006, 'equipTopGradeMul': 5, 'ggomaLevelBonus': 0.0015, 'offlineCapHours': 12,
       'offlineGoldRate': 0.5, 'adGachaPulls': 4, 'adBoxGoldKills': 4000, 'adBoxMyceliteBosses': 5}
for key, v in SET.items():
    m = re.search(r'<c r="B(\d+)"[^>]*>(?:<is><t[^>]*>%s</t></is>|<v>\d+</v>)</c>' % key, co.s)
    if not m or key not in m.group(0):
        # 공유 문자열 키일 수 있다 — 행마다 B열 글자를 풀어서 찾는다
        ss = re.findall(r'<si>(.*?)</si>', files['xl/sharedStrings.xml'].decode('utf-8'), flags=re.S)
        ss = [re.sub(r'<[^>]+>', '', x) for x in ss]
        ln = None
        for mm in re.finditer(r'<c r="B(\d+)"[^>]*t="s"[^>]*><v>(\d+)</v></c>', co.s):
            if ss[int(mm.group(2))] == key: ln = int(mm.group(1))
        assert ln, key
    else:
        ln = int(m.group(1))
    co.set_num(f'C{ln}', v)
NEW = [
    ('dungeonSweepMul', 0.5, '던전 소탕(지금 스테이지가 던전 레벨 기준 스테이지보다 낮을 때) 보상 = 마지막으로 깬 레벨 보상 × 이 값 (던전 보상 연결 뒤 사용)'),
    ('tailEnemyHpGrowth', 1.0089541, '스테이지 표 끝(700)을 넘으면 일반 적 체력 = 마지막 일반 스테이지(699) 값 × 이 값^(s − 699)'),
    ('tailBossHpGrowth', 1.0074414, '표 끝을 넘으면 보스 체력 = 699 값 × 이 값^(s − 699), 챕터(10의 배수)는 × tailWallMul'),
    ('tailAttackGrowth', 1.0035816, '표 끝을 넘으면 적·보스 공격 = 699 값 × 이 값^(s − 699)'),
    ('tailGoldGrowth', 1.0041914, '표 끝을 넘으면 적 처치 골드 = 699 값 × 이 값^(s − 699) (보스 골드 = × bossGoldMul)'),
    ('tailWallMul', 1.4, '표 끝을 넘은 챕터 보스(10의 배수 스테이지) 체력 배수'),
]
dim = re.search(r'<dimension ref="A1:D(\d+)"/>', co.s)
last = int(dim.group(1))
nxt = max(int(v) for v in re.findall(r'<c r="A\d+" s="\d+"><v>(9\d{3})</v></c>', co.s)) + 1
extra = ''
for k, (key, val, desc) in enumerate(NEW):
    assert f'>{key}<' not in co.s, key
    ln = last + 1 + k
    extra += (f'<row r="{ln}" spans="1:4" ht="21.95" customHeight="1"><c r="A{ln}" s="6"><v>{nxt + k}</v></c>'
              f'<c r="B{ln}" s="2" t="inlineStr"><is><t xml:space="preserve">{key}</t></is></c>'
              f'<c r="C{ln}" s="7"><v>{num(val)}</v></c>'
              f'<c r="D{ln}" s="2" t="inlineStr"><is><t xml:space="preserve">{escape(desc)}</t></is></c></row>')
co.s = co.s.replace('</sheetData>', extra + '</sheetData>').replace(dim.group(0), f'<dimension ref="A1:D{last + len(NEW)}"/>')
co.save()

# ── info 시트 행 수 ──
info = Sheet('info')
assert info.s.count(f'>{last - 3}행<') == 1
info.s = info.s.replace(f'>{last - 3}행<', f'>{last - 3 + len(NEW)}행<')
info.save()
sst = files['xl/sharedStrings.xml'].decode('utf-8')     # 스테이지 행 수는 공유 문자열에 있다
assert sst.count('>1000행<') == 1
files['xl/sharedStrings.xml'] = sst.replace('>1000행<', f'>{LAST_STAGE}행<').encode('utf-8')

# ── calcChain 지우고 열 때 다시 계산 (스테이지 수식 행을 없앴다) ──
order = [n for n in order if n != 'xl/calcChain.xml']
files['xl/_rels/workbook.xml.rels'] = re.sub(r'<Relationship Id="rId\d+" Type="[^"]+/calcChain" Target="calcChain.xml"/>', '', rels).encode('utf-8')
ct = files['[Content_Types].xml'].decode('utf-8')
files['[Content_Types].xml'] = ct.replace('<Override PartName="/xl/calcChain.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.calcChain+xml"/>', '').encode('utf-8')
files['xl/workbook.xml'] = wb.replace('<calcPr calcId="191029"/>', '<calcPr calcId="191029" fullCalcOnLoad="1"/>').encode('utf-8')
assert b'calcChain' not in files['xl/_rels/workbook.xml.rels'] and b'fullCalcOnLoad' in files['xl/workbook.xml']

with zipfile.ZipFile(DST, 'w', zipfile.ZIP_DEFLATED) as z:
    for n in order: z.writestr(n, files[n])
print('ok consts', nxt, '..', nxt + len(NEW) - 1, 'stage rows', last_row)
