# 방치 진행 → 세계수 입력 (하루 단위). KHD 방치 모델(밸런스설계/theory)을 읽기 전용으로 import해 돌리고,
# 접속마다 처치 수·스테이지·처치 골드·골드 수입(원천별)·균사석 수입·뽑기 수를 기록한다. 그 폴더에는 아무것도 쓰지 않는다.
#   python idle_feed.py live 60      → feed_live.json   (라이브 표 = 지금 + 공속 275 봉인, 보통 광고 0.5)
#   python idle_feed.py v2 60        → feed_v2.json     (KHD 수치안 v2 + D1 배수 150, day_v2b.json, 보통 0.5)
#   python idle_feed.py live 60 0.0  → feed_live_a0.json (무광고 민감도)
# 난수 없음 → 다시 돌리면 같은 결과.
import json, os, sys, time
HERE = os.path.dirname(os.path.abspath(__file__))
KHD = os.path.abspath(os.path.join(HERE, '..', '..', 'theory'))
sys.path.insert(0, KHD)
import formulas as F
import progress as P
import proposal as PR


class FeedSim(P.Sim):
    """KHD Sim + 접속별 기록. 게임 규칙은 건드리지 않는다"""

    def run_days(self, days, stop_stage=None):
        self.quest_log = []
        self.feed = []
        for day in range(days):
            self.day_counters()
            g0 = dict(self.src); m0 = dict(self.myc_src)
            pulls0 = sum(self.gear.pulls.values())
            n_free = int(round(4 * self.ads))
            n_ad = int(F.OV.get('const', {}).get('adGachaPulls', 11))
            for k in ['Weapon', 'Pet', 'Accessory', 'Character'][:n_free]:
                self.gear.pull(k, n_ad)
                self.d_pulls += n_ad
                self.d_ads += 1
            self.sync_gear()
            if self.dungeon:
                self.run_dungeons()
            sess = []
            for i, (mins, gap_h) in enumerate(self.sessions):
                s_start = self.stage
                k0, st0 = self.d_kills, self.d_stages
                g = self.play(mins * 60)
                self.adbox(mins * 60)
                if i == len(self.sessions) - 1:
                    self.quest_log.append((day + 1, self.stage, self.quests()))
                    self.spend_myc()
                off = g / (mins * 60) * min(gap_h, F.C('offlineCapHours')) * 3600 * F.OV.get('const', {}).get('offlineGoldRate', 1.0)
                self.gold += off; self.src['offline'] += off
                if self.watch('report'):
                    self.gold += off; self.src['offline_ad'] += off
                # 처치 수(보스 제외: 보스 처치 = 스테이지 돌파 수)
                kills = (self.d_kills - k0) - (self.d_stages - st0)
                sess.append(dict(mins=mins, gap_h=gap_h, stage_start=s_start, stage_end=self.stage,
                                 eg=F.enemy_gold(self.stage), kills=kills, battle_gold=g))
                self.wall += gap_h * 3600
                self.buy()
            gold = {k: self.src[k] - g0[k] for k in self.src}
            myc = {k: self.myc_src[k] - m0[k] for k in self.myc_src}
            self.feed.append(dict(day=day + 1, stage=self.stage, eg=F.enemy_gold(self.stage),
                                  boss_myc=F.boss_myc(self.stage), sessions=sess, gold=gold, myc=myc,
                                  pulls=sum(self.gear.pulls.values()) - pulls0))
            if stop_stage and self.stage >= stop_stage:
                break
        return self


def setup(which):
    if which == 'live':
        PR.current()
        return None
    if which == 'v2':
        import dungeon
        tbl = {int(k): v for k, v in json.load(open(os.path.join(KHD, 'day_v2b.json'))).items()}
        PR.apply({'check_step': 20, 'day_table': tbl, 'atk_hp_gear': False}, farm=150, extras=True)
        return dungeon
    raise SystemExit('live | v2')


if __name__ == '__main__':
    which = sys.argv[1]
    days = int(sys.argv[2]) if len(sys.argv) > 2 else 60
    ads = float(sys.argv[3]) if len(sys.argv) > 3 else 0.5
    t0 = time.time()
    dmod = setup(which)
    sim = FeedSim(pattern='daily', ads=ads)
    sim.dungeon = dmod
    sim.run_days(days)
    name = f'feed_{which}' + ('' if ads == 0.5 else f'_a{ads:g}') + '.json'
    json.dump(dict(which=which, ads=ads, days=days, feed=sim.feed), open(os.path.join(HERE, name), 'w', encoding='utf-8'), indent=0)
    print(which, ads, 'stage', sim.stage, 'secs', round(time.time() - t0, 1), '→', name)
