# 사용: python mkproj.py <소스 루트(Assets가 있는 곳)> <출력 폴더>
# 프로젝트 루트의 Assembly-CSharp(.-Editor).csproj를 복사해 Compile 경로를 소스 루트 기준 절대 경로로, 상대 HintPath를 현재 프로젝트 절대 경로로 바꾸고
# csproj에 없는 새 .cs를 Compile로 더한다 (루트에 없는 파일은 뺀다). 출력 폴더에 {이름}.csproj
import sys, re, os
BS = chr(92)
PROJ = 'C:' + BS + 'Project' + BS + 'MMM_Mobile'
src, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)

def all_cs(root, editor):
    r = []
    for d, _, fs in os.walk(os.path.join(root, 'Assets', 'Scripts')):
        for f in fs:
            if f.endswith('.cs'):
                p = os.path.join(d, f)
                isEd = (BS + 'Editor' + BS) in p
                if isEd == editor:
                    r.append(os.path.relpath(p, root))
    return r

for name, editor in (('Assembly-CSharp', False), ('Assembly-CSharp-Editor', True)):
    t = open(os.path.join(PROJ, name + '.csproj'), encoding='utf-8-sig').read()
    inc = set(m.lower() for m in re.findall(r'Compile Include="([^"]*)"', t))

    def fix(m):
        rel = m.group(1)
        if os.path.exists(os.path.join(src, rel)):
            return 'Compile Include="' + os.path.join(src, rel) + '"'
        return 'Compile Remove="' + rel + '"'
    t = re.sub(r'Compile Include="([^"]*)"', fix, t)
    t = re.sub(r'<Compile Remove="[^"]*" />', '', t)
    added = [r for r in all_cs(src, editor) if r.lower() not in inc and 'GoogleMobileAds' not in r]
    t = t.replace('</Project>', '  <ItemGroup>\n' + ''.join('    <Compile Include="' + os.path.join(src, r) + '" />\n' for r in added) + '  </ItemGroup>\n</Project>')
    t = re.sub(r'<HintPath>((?:Library|Assets)' + re.escape(BS) + r'[^<]*)</HintPath>', lambda m: '<HintPath>' + os.path.join(PROJ, m.group(1)) + '</HintPath>', t)
    t = re.sub(r'<BaseIntermediateOutputPath>[^<]*</BaseIntermediateOutputPath>', lambda m: '<BaseIntermediateOutputPath>' + out + BS + 'obj' + BS + '$(MSBuildProjectName)' + BS + '</BaseIntermediateOutputPath>', t)
    t = re.sub(r'<OutputPath>[^<]*</OutputPath>', lambda m: '<OutputPath>' + out + BS + 'bin' + BS + '</OutputPath>', t)
    def pr(m):
        n = m.group(1)
        if n == 'Assembly-CSharp':
            return '<Reference Include="Assembly-CSharp"><HintPath>' + out + BS + 'bin' + BS + 'Assembly-CSharp.dll</HintPath></Reference>'
        return '<Reference Include="' + n + '"><HintPath>' + PROJ + BS + 'Library' + BS + 'ScriptAssemblies' + BS + n + '.dll</HintPath></Reference>'
    t = re.sub(r'<ProjectReference Include="([^"]*)\.csproj" />', pr, t)
    open(os.path.join(out, name + '.csproj'), 'w', encoding='utf-8').write(t)
    print(name, '추가 Compile', added)
    print(' ProjectReference:', re.findall(r'<ProjectReference[^>]*>', t))
