import re, sys, os
sw = sys.argv[1]
os.chdir(os.path.join(sw, 'simsrc'))
p = 'Program.cs'; s = open(p, encoding='utf-8').read()
s2 = re.sub(r'static string Out = @"[^"]*";', 'static string Out = System.Environment.GetEnvironmentVariable("VERIFY_OUT") ?? "verify_out";', s, count=1)
assert s2 != s; open(p, 'w', encoding='utf-8').write(s2)
p = 'Player.cs'; s = open(p, encoding='utf-8').read()
a = '            Pull(Kinds[nextKind], count);\n            nextKind = (nextKind + 1) % Kinds.Length;'
s = s.replace('\r\n', '\n')
assert a in s
s = s.replace(a, '            var mk = VerifyKnob.MycKinds ?? Kinds;   // verify: 균사석 뽑기 종류 제한 (MYC_KINDS=0 -> 무기만)\n            Pull(mk[nextKind % mk.Length], count);\n            nextKind = (nextKind + 1) % mk.Length;')
open(p, 'w', encoding='utf-8').write(s)
open('VerifyKnob.cs', 'w', encoding='utf-8').write('''static class VerifyKnob
{
    public static readonly int[] MycKinds = string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("MYC_KINDS")) ? null
        : System.Array.ConvertAll(System.Environment.GetEnvironmentVariable("MYC_KINDS").Split(','), int.Parse);
}
''')
p = 'IdleSim.csproj'; s = open(p, encoding='utf-8').read()
old = os.path.join('C:' + os.sep, 'Users', 'user', 'AppData', 'Local', 'Temp', 'claude', '82d5963c662fda11', '2c54f97f-d275-490e-a84e-6bcf329252d2', 'scratchpad', 'b_cur', 'bin')
new = os.path.join(sw, 'b_w', 'bin')
assert old in s, old
s = s.replace(old, new)
open(p, 'w', encoding='utf-8').write(s)
print('ok')
