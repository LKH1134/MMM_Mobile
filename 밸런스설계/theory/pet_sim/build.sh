#!/bin/bash
# test 시뮬(밸런스설계/test/sim, 실제 Assembly-CSharp.dll 호출)을 scratchpad로 복사해 theory 전용으로 빌드한다 (test 파일은 읽기만).
# 바꾸는 것 2곳: 출력 폴더 = 환경 변수 THEORY_OUT, 공격 장비 % × 환경 변수 WEAPON_MUL (손잡이 B 시험용, 기본 1 = 지금 코드)
SP='C:/Users/user/AppData/Local/Temp/claude/82d5963c662fda11/2c54f97f-d275-490e-a84e-6bcf329252d2/scratchpad'
SRC='C:/Project/MMM_Mobile/밸런스설계/test/sim'
rm -rf "$SP/simcopy"; mkdir -p "$SP/simcopy"
cp "$SRC"/*.cs "$SRC"/IdleSim.csproj "$SP/simcopy/"
cd "$SP/simcopy"
python -c "import re;p='Program.cs';s=open(p,encoding='utf-8').read();s=re.sub(r'static string Out = @\"[^\"]*\";','static string Out = System.Environment.GetEnvironmentVariable(\"THEORY_OUT\") ?? \"theory_out\";',s,count=1);open(p,'w',encoding='utf-8').write(s)"
sed -i 's|if (E(k).Stat == EquipStat.Attack) atkPct += sum; else hpPct += sum;|if (E(k).Stat == EquipStat.Attack) atkPct += sum * TheoryKnob.WMul; else hpPct += sum;|' Player.cs
cat > TheoryKnob.cs <<'CS'
static class TheoryKnob
{
    public static readonly double WMul = double.Parse(System.Environment.GetEnvironmentVariable("WEAPON_MUL") ?? "1", System.Globalization.CultureInfo.InvariantCulture);
}
CS
grep -c "THEORY_OUT" Program.cs; grep -c "TheoryKnob.WMul" Player.cs
dotnet build -c Release -o "$SP/simcopy/bin" 2>&1 | tail -3
