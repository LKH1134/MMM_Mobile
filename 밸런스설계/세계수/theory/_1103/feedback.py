# W4 되먹임: 세계수 전투 효과를 KHD 방치 Sim에 넣고 하루 끝 스테이지를 기록한다 (KHD 폴더·verify 폴더는 읽기만).
# 넣는 곳은 게임과 같다: 공격(FinalAttack)·체력/재생(FinalHp·FinalHpRegen)·치명 확률(Min 1)·치명 피해(더하기)·
# 처치 골드(전투 골드 → 앱 밖 골드도 접속 중 속도를 따라감, IdleAway.cs:66-101)·보스 균사석(IdleBattle.cs:392, 기대값).
#   python feedback.py <live|v2> <days> <ads> const A=1.05,H=1.05,G=1.02,CD=0.1,CC=0,BM=1.1   (1일부터 최대치 = 상한)
#   python feedback.py <live|v2> <days> <ads> tl=<timeline.json>                              (wt_model 날짜별 효과, 전날 값)
#   python feedback.py <live|v2> <days> <ads> none                                            (기준선)
#   python feedback.py v3 <days> <ads> ...   (라운드 4: 머지된 라이브 표, idle_feed.setup_live)
# → fb_<base>_a<ads>_<tag>.json : {day: stage}
import copy, json, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
KHD = os.path.abspath(os.path.join(HERE, '..', '..', 'theory'))
sys.path.insert(0, KHD)
import formulas as F, progress as P, proposal as PR, battle as B

CUR = dict(A=1.0, H=1.0, CC=0.0, CD=0.0, G=1.0, BM=1.0)


def install():
    fa0, mh0, rg0 = B.Player.final_attack, B.Player.max_hp, B.Player.regen
    B.Player.final_attack = lambda self: fa0(self) * CUR['A']
    B.Player.max_hp = lambda self: mh0(self) * CUR['H']
    B.Player.regen = lambda self: rg0(self) * CUR['H']
    B.Player.crit_factor = lambda self: 1 + min(1, self.v('CritChance') + CUR['CC']) * (self.v('CritDamage') + CUR['CD'] - 1)

    def attempt(self):
        key = (self.stage, self.p.key(), CUR['A'], CUR['H'], CUR['CC'], CUR['CD'])
        r = self.cache.get(key)
        if r is None:
            if len(self.cache) > 20000:
                self.cache.clear()
            r = B.attempt(self.stage, self.p)
            self.cache[key] = r
        r = copy.copy(r)
        r.gold *= CUR['G']
        return r
    P.Sim.attempt = attempt
    play0 = P.Sim.play

    def play(self, seconds):
        b0 = self.myc_src['boss']
        g = play0(self, seconds)
        extra = (self.myc_src['boss'] - b0) * (CUR['BM'] - 1)
        self.myc += extra; self.myc_src['boss'] += extra
        self.spend_myc()
        return g
    P.Sim.play = play


class FB(P.Sim):
    def run_days(self, days, tl=None, ad=None):
        self.stage_by_day = {}
        if ad is not None:
            import idle_feed as IF
            orig = lambda self_, n: IF.FeedSim.run_days(self_, n, ad)
        else:
            orig = P.Sim.run_days
        for day in range(days):
            if tl and day > 0:
                e = tl[day - 1]
                CUR.update(A=1 + e.get('Attack', 0), H=1 + e.get('Hp', 0), CC=e.get('CritChance', 0), CD=e.get('CritDamage', 0),
                           G=1 + e.get('KillGold', 0), BM=1 + e.get('BossMycelite', 0))
            orig(self, 1)
            self.stage_by_day[day + 1] = self.stage
        return self


if __name__ == '__main__':
    base, days, ads, spec = sys.argv[1], int(sys.argv[2]), float(sys.argv[3]), sys.argv[4]
    dmod = None
    ad = None
    if base == 'v3':      # 라운드 4: 머지된 라이브 표 (idle_feed.setup_live — 1~700 시트 + 꼬리, 뽑기상품 광고 행, 던전 시트)
        import idle_feed as IF
        info = IF.setup_live()
        dmod, ad = IF.DG, info['ad']
    elif base == 'live':
        PR.current()
    else:
        import dungeon
        tbl = {int(k): v for k, v in json.load(open(os.path.join(KHD, 'day_v2b.json'))).items()}
        PR.apply({'check_step': 20, 'day_table': tbl, 'atk_hp_gear': False}, farm=150, extras=True)
        dmod = dungeon
    tl, tag = None, 'none'
    if spec != 'none':
        install()
    if spec.startswith('const'):
        for kv in sys.argv[5].split(','):
            k, v = kv.split('=')
            CUR[k] = float(v)
        tag = 'c_' + sys.argv[5].replace('=', '').replace(',', '_')
    elif spec.startswith('tl='):
        tl = json.load(open(os.path.join(HERE, spec[3:]), encoding='utf-8'))
        tag = 'tl_' + spec[3:-5]
    sim = FB(pattern='daily', ads=ads)
    sim.dungeon = dmod
    sim.run_days(days, tl, ad)
    out = os.path.join(HERE, f'fb_{base}_a{ads:g}_{days}_{tag}.json')
    json.dump(sim.stage_by_day, open(out, 'w'), indent=0)
    print(base, ads, tag, {d: sim.stage_by_day[d] for d in (7, 14, 21, 30, 45, 60, 90, 120) if d in sim.stage_by_day})
