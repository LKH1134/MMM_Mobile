# 2026-11-11 번역 누락 점검: 광고 상태 문구·시작 화면 버전을 String.xlsx에 더한다. 사용: python add_loc_fix_strings.py <String.xlsx>
# 엑셀 XML을 직접 고친다 (inlineStr)
import re, sys, zipfile
from xml.sax.saxutils import escape

PATH = sys.argv[1]
ROWS = [
    ('ad.status_consent_failed', '광고 동의 정보 확인 실패', "Couldn't check ad consent", '광고 상자 창 상태 줄 (AdService). SDK 오류 내용은 로그에만'),
    ('ad.status_form_failed', '광고 동의 양식 실패', "Couldn't show the consent form", '광고 상자 창 상태 줄'),
    ('ad.status_sdk_ready', '광고 SDK 준비 완료', 'Ad SDK ready', '광고 상자 창 상태 줄'),
    ('ad.status_privacy_failed', '광고 개인정보 설정 실패', "Couldn't open ad privacy settings", '광고 상자 창 상태 줄'),
    ('ad.status_loading', '광고 불러오는 중...', 'Loading ad...', '광고 상자 창 상태 줄'),
    ('ad.status_load_failed', '광고 로드 실패', "Couldn't load the ad", '광고 상자 창 상태 줄'),
    ('ad.status_ready', '광고 준비됨', 'Ad ready', '광고 상자 창 상태 줄'),
    ('ad.status_not_ready', '아직 광고가 준비되지 않았습니다', "The ad isn't ready yet", '광고 상자 창 상태 줄'),
    ('ad.status_closed', '광고 닫힘', 'Ad closed', '광고 상자 창 상태 줄'),
    ('ad.status_show_failed', '광고 표시 실패', "Couldn't show the ad", '광고 상자 창 상태 줄'),
    ('ad.status_rewarded', '보상 지급', 'Reward granted', '광고 상자 창 상태 줄'),
    ('start.version', '버전 {0}', 'Version {0}', '시작 화면 아래 (VersionText). {0}=앱 버전'),
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
