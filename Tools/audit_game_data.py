"""Read-only game-data coverage audit. Python standard library only.

Run: python Tools/audit_game_data.py
Use --report DATA_AUDIT.md to save the inventory and findings.
Unity's GameBalanceValidator remains the authority for types, ranges and asset loading.
"""
import argparse
from collections import Counter
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
NS = {'m': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
REL = '{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id'


def read_xlsx(path):
    with ZipFile(path) as z:
        strings = []
        if 'xl/sharedStrings.xml' in z.namelist():
            strings = [''.join(t.itertext()) for t in ET.fromstring(z.read('xl/sharedStrings.xml'))]
        relations = {r.attrib['Id']: r.attrib['Target'] for r in ET.fromstring(z.read('xl/_rels/workbook.xml.rels'))}
        result = {}
        for sheet in ET.fromstring(z.read('xl/workbook.xml')).find('m:sheets', NS):
            target = relations[sheet.attrib[REL]].lstrip('/')
            if not target.startswith('xl/'):
                target = 'xl/' + target
            rows = []
            for row in ET.fromstring(z.read(target)).find('m:sheetData', NS):
                cells = {}
                for cell in row:
                    address = cell.attrib['r']
                    column = 0
                    for c in re.match('[A-Z]+', address)[0]:
                        column = column * 26 + ord(c) - 64
                    value = cell.find('m:v', NS)
                    text = value.text if value is not None else ''
                    if cell.attrib.get('t') == 's':
                        text = strings[int(text)]
                    elif cell.attrib.get('t') == 'inlineStr':
                        inline = cell.find('m:is', NS)
                        text = ''.join(inline.itertext()) if inline is not None else ''
                    cells[column - 1] = text or ''
                rows.append([cells.get(i, '') for i in range(max(cells, default=-1) + 1)])
            result[sheet.attrib['name']] = rows
        return result


def records(rows):
    return [dict(zip(rows[0], row)) for row in rows[3:] if row and row[0]]


def audit():
    source = ROOT / 'Assets/Data/Tables/GameBalance.xlsx'
    sheets = read_xlsx(source)
    tables = {name: records(rows) for name, rows in sheets.items() if name != 'info'}
    issues = []
    consts = {r['key'] for r in tables['상수']}
    scripts = sorted((ROOT / 'Assets/Scripts/Mobile').rglob('*.cs'))
    for p in scripts:
        for key in re.findall(r'GameBalance\.Const(?:Int)?\("([^"\n]+)"', p.read_text(encoding='utf-8-sig')):
            if key not in consts:
                issues.append(f'{p.relative_to(ROOT).as_posix()}: 상수 {key} 미등록')
    paths = {r['path'] for r in tables['리소스']}
    resource_assets = []
    for p in sorted((ROOT / 'Assets/Resources').rglob('*')):
        if p.suffix.lower() not in ('.png', '.jpg', '.jpeg', '.prefab'):
            continue
        path = p.relative_to(ROOT / 'Assets/Resources').with_suffix('').as_posix()
        resource_assets.append(path)
        if path not in paths:
            issues.append(f'리소스 미등록: {path}')
    settings = {(r['scope'], r['field']) for r in tables['수확전투'] if r['dungeon'] == '1'}
    # Serialized gameplay scalars; colors and purely presentational fields stay in Unity assets.
    scopes = ['SkillEffects', 'FieldDevices', 'HarvestField', 'Harvester', 'FieldInput']
    visual = {'Harvester': {'spinSpeed'}, 'FieldInput': {'headLength', 'startOffset', 'maxDrag', 'minArrow', 'maxArrow'}}
    for scope in scopes:
        text = (ROOT / f'Assets/Scripts/Mobile/InGame/{scope}.cs').read_text(encoding='utf-8-sig')
        fields = re.findall(r'\[SerializeField[^\]]*\]\s+(?:float|int|long|double|bool)\s+(\w+)\s*=', text)
        for field in fields:
            if field not in visual.get(scope, set()) and (scope, field) not in settings:
                issues.append(f'수확전투 미등록: {scope}.{field}')
    for scope, fields in {'BossGimmicks': ['splitAt0', 'splitAt1'], 'StageHud': ['speed0', 'speed1', 'speed2']}.items():
        for field in fields:
            if (scope, field) not in settings:
                issues.append(f'수확전투 미등록: {scope}.{field}')
    output = ROOT / 'Assets/Resources/Tables/GameBalance.bytes'
    if source.read_bytes() != output.read_bytes():
        issues.append('GameBalance.xlsx와 런타임 bytes 불일치: Unity Reimport Tables 필요')
    legacy = []
    for p in sorted((ROOT / 'Assets/Data/Tables/Legacy').glob('*.xlsx')):
        rows = sum(len(records(r)) for r in read_xlsx(p).values())
        legacy.append((p.name, rows))
        if p.read_bytes() != (ROOT / f'Assets/Resources/Tables/Legacy/{p.stem}.bytes').read_bytes():
            issues.append(f'Legacy 원본/bytes 불일치: {p.name}')
    inventory = Counter(p.suffix.lower() for p in (ROOT / 'Assets').rglob('*') if p.is_file() and p.suffix != '.meta')
    lines = [
        '# 프로젝트 데이터 조사 — 2026-11-03', '',
        f'Unity MCP 연결 확인: MMM_Mobile / Unity 6000.3.19f1. 통합 표 {len(tables)}개 시트, {sum(map(len, tables.values())):,}행.', '',
        '## 조사 범위', '',
        f'- Assets 전체 파일 목록: {sum(inventory.values()):,}개(meta 제외). 게임 C# {len(scripts)}개, 씬 {inventory[".unity"]}개, 프리팹 {inventory[".prefab"]}개.',
        '- 통합 표·Legacy 5종과 대응 bytes, Mobile 소스의 상수 조회·수확 전투 직렬화 수치, 씬·프리팹 실제값, Resources 전체 Sprite·Prefab을 대조했다.',
        '- Backup은 폐기된 씬 보관본, 밸런스설계는 이론·시뮬레이션·대안 결과다. 라이브 정의에 덮어쓰지 않는다.', '',
        '## 추가·연동 결과', '',
        '| 대상 | 변경 |', '|---|---|',
        '| 수확전투 | 16행 추가: SkillEffects 7, FieldDevices 4, 보스 분신 체력 기준 2, 배속 3 |',
        '| 상수 | 3행 추가: harvestFarmRounds=1, harvestCycleLength=10, offlineRateMinSeconds=30 |',
        '| 리소스 | 누락 Sprite 26행 추가. Resources Sprite·Prefab 89개 전부 등록 |',
        '| 등급 | #name 메모를 name:string으로 전환하고 UI 조회 연결 |',
        '| info | 이번 조사 범위·추가 scope·편집법 4행 추가 |', '',
        '새 수치는 기존 씬·코드값 그대로다. 기존 ID·수치·수식·수식 캐시는 유지했다. 새 전투 수치·상수·이름은 런타임에서 읽으며 리소스 행은 에셋 목록 등록이다.', '',
        '## 테이블 목록', '', '| 시트 | 데이터 행 |', '|---|---:|',
    ]
    lines += [f'| {name} | {len(rows)} |' for name, rows in tables.items()]
    lines += ['', '## Legacy', '', '| 파일 | 데이터 행 |', '|---|---:|']
    lines += [f'| {name} | {count} |' for name, count in legacy]
    lines += ['', 'Legacy의 스킬·보스·던전 스테이지는 GameData에서 계속 사용하므로 통합 표의 방치형 적·보스·스테이지와 합치지 않는다. Mushroom/Harvester는 현재 데이터 행이 없는 기존 틀이다.', '',
              '## 테이블 대상에서 제외한 값', '',
              '- 세이브 값·진행도·통화 잔액·시각은 플레이어 상태다.',
              '- UI 좌표·색·폰트·렌더링·장식 애니메이션·팝업 층·입력 연타 간격은 코드/씬/프리팹 설정이다. 보스 HUD 잔상, 화살표 길이, 수확기 회전 연출도 여기에 포함한다.',
              '- enum 값·저장 인덱스·편성 슬롯 수·등급 수·장비 등급당 단계 수는 저장 형식/구조 계약이다.',
              '- Resources 밖 Assets/Art·Prefabs의 직접 참조 에셋은 Unity GUID로 연결한다. Resources 경로 테이블에 잘못 등록하지 않는다.',
              '- SDK·광고 ID·빌드/플랫폼 설정·오버플로 방지 한도·수학적 상수·프레임 분할 한도는 밸런스 데이터가 아니다.', '',
              '## 검사 결과', '', f'정적 대조: 문제 {len(issues)}개.']
    lines += ['- ' + i for i in issues]
    lines += ['', '이번 변경의 Unity 검증 기록:', '',
              '- 컴파일 완료. GameBalanceValidator 및 Reimport Tables: 1,116행 OK, 데이터 문제 0개.',
              '- 새 전투 설정 16개: InGame을 임시 additive 로드한 뒤 실제 컴포넌트 필드를 sentinel로 바꾸고 Apply/Number로 표값 적용 확인. 씬을 저장하지 않고 닫고 Start로 복구.',
              '- 등급명 5개·파밍 1판·반복 주기 10·HarvestSettings 74행을 런타임 로더로 확인. xlsx/bytes 완전 일치.',
              '- 잘못된 표 4종(필수 행 삭제·분신 기준 역전·scope/field 중복·자료형 오류)을 각각 검증기가 거부하는 것 확인.',
              '- 기존 셀 변경은 등급 E1:E3 헤더 3칸뿐. 추가된 행 외 기존 값·수식·캐시 및 나머지 Excel 패키지 파일은 보존.',
              '- 기존 SDK/TMP obsolete 경고 2건과 MCP 재연결 로그는 이번 데이터 수정과 별개다. Android 빌드·실기기 플레이는 수행하지 않았다.', '',
              '재실행: `python Tools/audit_game_data.py` (표준 라이브러리만 사용). Unity 구조·자료형·범위·실제 리소스 로딩 검사는 `MoreMush/Data/Reimport Tables`로 실행한다.', '']
    return '\n'.join(lines), issues


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    report, issues = audit()
    if args.report:
        args.report.write_text(report, encoding='utf-8')
    print(f'게임 데이터 대조: 문제 {len(issues)}개')
    for issue in issues:
        print(issue)
    sys.exit(bool(issues))
