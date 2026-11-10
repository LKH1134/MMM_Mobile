# 2026-11-10 도감 세트 보너스 문구를 String.xlsx에 더한다. 사용: python add_dex_strings.py <String.xlsx>
# 엑셀 XML을 직접 고친다 (inlineStr)
import re, sys, zipfile
from xml.sax.saxutils import escape

PATH = sys.argv[1]
ROWS = [
    ('dex.drop', '도감 +1 {0}', 'Book +1 {0}', '방치 보스를 잡고 도감 버섯을 얻음. {0}=종류 ★'),
    ('dex.set_done', '{0} 세트 완성!', '{0} Set Complete!', '도감 무리를 다 모음. {0}=종류 ★'),
    ('dex.set_header', '{0} 버섯 {1}/{2}', '{0} {1}/{2}', '세트 보너스 목록 제목. {0}=종류 {1}=모은 수 {2}=전체'),
    ('dex.set_from', '({0}부터)', '(from {0})', '세트 목록: 아직 안 나오는 무리의 등장 스테이지'),
    ('dex.set_active', '적용 중', 'Active', '세트 보너스 목록 아래: 켜진 효과 합'),
]

z = zipfile.ZipFile(PATH)
order = z.namelist()
files = {n: z.read(n) for n in order}
z.close()
s = files['xl/worksheets/sheet1.xml'].decode('utf-8')
r = max(int(n) for n in re.findall(r'<row r="(\d+)"', s))
out = ''
for row in ROWS:
    assert f'<t>{row[0]}</t>' not in s, row[0]
    r += 1
    out += f'<row r="{r}">' + ''.join(f'<c r="{c}{r}" t="inlineStr"><is><t>{escape(v)}</t></is></c>' for c, v in zip('ABCD', row)) + '</row>'
s = s.replace('</sheetData>', out + '</sheetData>')
s = re.sub(r'<dimension ref="A1:D\d+" />', f'<dimension ref="A1:D{r}" />', s)
files['xl/worksheets/sheet1.xml'] = s.encode('utf-8')
with zipfile.ZipFile(PATH, 'w', zipfile.ZIP_DEFLATED) as o:
    for n in order:
        o.writestr(n, files[n])
print('ok', r)
