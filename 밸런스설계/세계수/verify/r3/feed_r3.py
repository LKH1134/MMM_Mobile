# 방치 입력 (wt-verify 독립 래퍼). KHD 방치 모델(밸런스설계/theory/progress.py Sim, 읽기만)을 그대로 돌리며
# 접속마다 처치 수·스테이지·적 처치 골드, 하루마다 골드(원천별)·균사석·뽑기 수를 기록한다.
# run_days 루프는 progress.py:266-294를 그대로 옮기고 기록만 더했다.
# python feed.py <live|v2> <days> <ads> → feed_<base>_a<ads>.json
import sys, os, json
sys.stdout.reconfigure(encoding='utf-8')
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.abspath(os.path.join(HERE, '../../theory')))
import formulas as F, progress as P, proposal as PR

base, days, ads = sys.argv[1], int(sys.argv[2]), float(sys.argv[3])
scale = float(sys.argv[4]) if len(sys.argv) > 4 else 1.0     # 접속 시간 배수 (플레이 시간 민감도)
wt = len(sys.argv) > 5 and sys.argv[5] == 'wt'               # 세계수 효과 최대치를 1일부터 되먹임 (W4 상한)
tl = json.load(open(os.path.join(HERE, sys.argv[5][3:]), encoding='utf-8')) if len(sys.argv) > 5 and sys.argv[5].startswith('tl=') else None
CUR = {'A': 1.0, 'H': 1.0, 'DCC': 0.0, 'DCD': 0.0, 'G': 1.0}   # 날마다 바뀌는 세계수 효과 (tl = wt_port 하루 끝 효과, 전날 값 사용)
use_dungeon = False
if base == 'live':
    PR.current()                         # 지금 표 + 공속 275 (KHD current, 던전 없음 = model.py run 기본)
elif base == 'v2':
    tbl = {int(k): v for k, v in json.load(open(os.path.join(HERE, '../../theory/day_v2b.json'))).items()}
    PR.apply({'check_step': 20, 'day_table': tbl, 'atk_hp_gear': False}, farm=150, extras=True)   # final_v2.py와 같은 설정, D1 배수 150
    use_dungeon = True
else:
    raise SystemExit('base?')

if wt:   # 수치안 v1 최대 합: 공격 0.31 · 체력 0.31 · 치명 +0.09 · 치명 피해 +0.5 · 처치 골드 0.25 (wt_port.py totals와 같은 값)
    import battle as B, copy
    A, H, DCC, DCD, G = 1.31, 1.31, 0.09, 0.5, 1.25
    fa0, mh0, rg0 = B.Player.final_attack, B.Player.max_hp, B.Player.regen
    B.Player.final_attack = lambda self: fa0(self) * A
    B.Player.max_hp = lambda self: mh0(self) * H
    B.Player.regen = lambda self: rg0(self) * H
    B.Player.crit_factor = lambda self: 1 + min(1, self.v('CritChance') + DCC) * (self.v('CritDamage') + DCD - 1)
    at0 = P.Sim.attempt
    def at(self):
        r = copy.copy(at0(self)); r.gold *= G; return r
    P.Sim.attempt = at
if tl:   # 현실 타임라인: wt_port가 낸 날짜별 효과를 되먹임
    import battle as B, copy
    fa0, mh0, rg0 = B.Player.final_attack, B.Player.max_hp, B.Player.regen
    B.Player.final_attack = lambda self: fa0(self) * CUR['A']
    B.Player.max_hp = lambda self: mh0(self) * CUR['H']
    B.Player.regen = lambda self: rg0(self) * CUR['H']
    B.Player.crit_factor = lambda self: 1 + min(1, self.v('CritChance') + CUR['DCC']) * (self.v('CritDamage') + CUR['DCD'] - 1)
    at0 = P.Sim.attempt
    def at(self):
        key = (self.stage, self.p.key(), CUR['A'], CUR['H'], CUR['DCC'], CUR['DCD'])
        r = self.cache.get(key)
        if r is None:
            r = B.attempt(self.stage, self.p); self.cache[key] = r
        r = copy.copy(r); r.gold *= CUR['G']; return r
    P.Sim.attempt = at
sessions = [(m * scale, g) for m, g in [(30, 4.0), (20, 5.0), (40, 15.0)]]
sim = P.Sim(pattern='daily', ads=ads, sessions=sessions)
if use_dungeon:
    import dungeon; sim.dungeon = dungeon
out = dict(base=base, ads=ads, sessions=sim.sessions, days=[])
for day in range(days):
    if tl and day > 0:
        e = tl[day - 1]
        CUR.update(A=1 + e['Attack'], H=1 + e['Hp'], DCC=e['CritChance'], DCD=e['CritDamage'], G=1 + e['KillGold'])
    src0 = dict(sim.src); myc0 = dict(sim.myc_src)
    sim.day_counters()
    n_free = int(round(4 * sim.ads))
    n_ad = int(F.OV.get('const', {}).get('adGachaPulls', 11))
    for k in ['Weapon', 'Pet', 'Accessory', 'Character'][:n_free]:
        sim.gear.pull(k, n_ad); sim.d_pulls += n_ad; sim.d_ads += 1
    sim.sync_gear()
    if sim.dungeon: sim.run_dungeons()
    sess = []
    for i, (mins, gap_h) in enumerate(sim.sessions):
        k0 = sim.d_kills; b0 = sim.d_boss; s0 = sim.stage
        g = sim.play(mins * 60)
        sim.adbox(mins * 60)
        if i == len(sim.sessions) - 1:
            sim.quests(); sim.spend_myc()
        off = g / (mins * 60) * min(gap_h, F.C('offlineCapHours')) * 3600 * F.OV.get('const', {}).get('offlineGoldRate', 1.0)
        sim.gold += off; sim.src['offline'] += off
        if sim.watch('report'):
            sim.gold += off; sim.src['offline_ad'] += off
        sim.wall += gap_h * 3600
        sim.buy()
        sess.append(dict(mins=mins, gap=gap_h, kills=sim.d_kills - k0, bosses=sim.d_boss - b0,
                         stage0=s0, stage=sim.stage, eg=F.enemy_gold(sim.stage), bmyc=F.boss_myc(sim.stage)))
    gold = {k: sim.src[k] - src0[k] for k in sim.src}
    myc = {k: sim.myc_src[k] - myc0[k] for k in sim.myc_src}
    eg = F.enemy_gold(sim.stage)
    out['days'].append(dict(day=day + 1, stage=sim.stage, eg=eg, sessions=sess, kills=sim.d_kills, bosses=sim.d_boss,
                            gold=gold, gold_total=sum(gold.values()), gold_kills=sum(gold.values()) / eg,
                            myc=myc, myc_total=sum(myc.values()), pulls=sim.d_pulls,
                            cc=sim.p.v('CritChance'), cd=sim.p.v('CritDamage'), spd=sim.p.v('Speed')))
    d = out['days'][-1]
    print(day + 1, sim.stage, 'kills', d['kills'], 'goldKE %.0f' % d['gold_kills'], 'myc %.0f' % d['myc_total'], 'pulls', d['pulls'], flush=True)
json.dump(out, open(os.path.join(HERE, f'feed_{base}_a{ads}' + ('' if scale == 1 else f'_t{scale}') + ('_wt' if wt else '') + (('_tl' + sys.argv[5][12:-5]) if tl else '') + '.json'), 'w', encoding='utf-8'), indent=0)
