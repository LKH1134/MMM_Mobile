# 진행 모델: 실제 공식 + 전투 시뮬(battle.attempt)로 "강화 → 도전 → 실패하면 반복(파밍)"을 돌리고
# 재화 원천(전투 골드·보스 균사석·앱 밖·광고 상자·일일 퀘스트·광고 뽑기·보고서 2배·던전)을 하루 일정에 넣는다.
# 난수 없음 → 다시 돌리면 같은 결과.
import math
import formulas as F
import battle as B

OFFENSE = ['Attack', 'Speed', 'CritChance', 'CritDamage', 'AdvancedAttack', 'NormalEnemyDamage']
DEFENSE = ['Hp', 'HpRegen']


# ── 장비 (기대값 근사) ──
class Gear:
    KINDS = ['Weapon', 'Pet', 'Accessory']

    def __init__(self):
        self.pulls = {k: 0 for k in self.KINDS + ['Character']}
        self.copies = {k: [0.0] * len(F.equip_items(k)) for k in self.KINDS}
        self.level = {k: 1 for k in self.KINDS + ['Character']}
        self.exp = {k: 0 for k in self.KINDS + ['Character']}

    def pull(self, kind, n):
        self.pulls[kind] += n
        if kind != 'Character':
            tbl = F.OV.get('gacha', F.T.gacha)
            odds = tbl[min(self.level[kind], len(tbl)) - 1]['odds']
            # Equipment.PickItems: 뽑힌 등급 안에서 같은 확률
            for i, (g, _, size) in enumerate(F.equip_items(kind)):
                self.copies[kind][i] += n * odds[g] / 100 / size
        e = self.exp[kind] + n
        lv = self.level[kind]
        rows = F.OV.get('gacha', F.T.gacha)
        while lv < len(rows) and e >= rows[lv - 1]['exp']:
            e -= rows[lv - 1]['exp']
            lv += 1
        self.level[kind], self.exp[kind] = lv, (0 if lv >= len(rows) else e)

    def percent(self, kind):
        tot, best = 0.0, 0.0
        bonus = F.C('equipLevelBonus')
        for i, n in enumerate(self.copies[kind]):
            base = F.equip_base(kind, i)
            if n >= 1:
                lv = F.level_from_copies(int(n))
                own = base * (1 + bonus * (lv - 1))
                best = max(best, own * F.C('equipRatio'))
            else:
                own = base * (1 - math.exp(-n))
            tot += own
        return tot + best

    def atk_pct(self):
        return self.percent('Weapon') + self.percent('Pet')

    def hp_pct(self):
        return self.percent('Accessory')


# ── 강화 고르기 ──
def can_buy(p, stat):
    u = F.U(stat)
    if u['unlockLv'] > 0 and p.lv[u['unlockStat']] < u['unlockLv']:
        return False
    if p.lv[stat] >= u['maxLv']:
        return False
    return p.lv[stat] < F.cap_level(stat, p.lv[u['unlockStat']])


def log_power(p, mode):
    ehp = p.max_hp() + 30 * p.regen()
    off = math.log(p.boss_dps()) + 0.3 * math.log(p.v('NormalEnemyDamage'))
    if mode == 'offense':
        return off
    if mode == 'defense':
        return math.log(ehp)
    return off + math.log(ehp)


def best_buy(p, mode):
    stats = OFFENSE if mode == 'offense' else DEFENSE if mode == 'defense' else OFFENSE + DEFENSE
    base = log_power(p, mode)
    best, best_eff = None, -1
    for st in stats:
        if not can_buy(p, st):
            continue
        c = F.cost(st, p.lv[st])
        p.lv[st] += 1
        gain = log_power(p, mode) - base
        p.lv[st] -= 1
        eff = gain / c
        if eff > best_eff:
            best, best_eff = st, eff
    return best


class Sim:
    """한 플레이어의 진행. pattern: 'continuous' 또는 'daily'. ads: 광고를 보는가"""

    def __init__(self, pattern='daily', ads=True, sessions=None, dungeon_gold_kills=0.0, log_every=None):
        self.p = B.Player()
        self.gear = Gear()
        self.stage = 1
        self.gold = 0.0
        self.myc = 0.0
        self.pattern = pattern
        self.ads = float(ads)        # 광고 기회 중 보는 비율 (기준선 0.5, 기준 6 개정)
        self.ad_acc = {}
        self.dungeon = None          # 던전 보상 함수 (dungeon.py), None이면 없음
        self.dlevel = {}
        # 하루 일정: (홈에서 플레이하는 분, 다음 접속까지 시간)
        self.sessions = sessions or [(30, 4.0), (20, 5.0), (40, 15.0)]
        self.dungeon_gold_kills = dungeon_gold_kills
        self.active = 0.0        # 누적 플레이 초
        self.wall = 0.0          # 누적 달력 초
        self.arrive = {1: (0.0, 0.0)}   # 스테이지 → (플레이 초, 달력 초)
        self.stage_info = {}     # 스테이지 → dict(attempts, fails, reasons, play)
        self.mode = 'balanced'
        self.cache = {}
        self.src = {k: 0.0 for k in ('battle', 'offline', 'offline_ad', 'adbox', 'quest', 'dungeon')}
        self.myc_src = {k: 0.0 for k in ('boss', 'quest', 'adbox', 'dungeon')}
        self.free_pulls = 0
        self.day_counters()
        self.adbox_elapsed = 0.0
        self.stuck = False
        self.first = {}          # D6: 처음 쓴 달력 시각(초) — dungeon, gacha, quest
        self.upg_1h = 0          # D6: 첫 1시간(플레이) 강화 횟수
        self.claimed = set()

    def day_counters(self):
        self.claimed = set()
        self.d_kills = self.d_stages = self.d_upg = self.d_pulls = self.d_ads = self.d_boss = 0

    def sync_gear(self):
        self.p.atk_pct, self.p.hp_pct = self.gear.atk_pct(), self.gear.hp_pct()

    def buy(self):
        n = 0
        while True:
            st = best_buy(self.p, self.mode)
            if st is None:
                break
            c = F.cost(st, self.p.lv[st])
            if self.gold < c:
                break
            self.gold -= c
            self.p.lv[st] += 1
            n += 1
            if self.active <= 3600:
                self.upg_1h += 1
        self.d_upg += n
        return n

    def spend_myc(self):
        kinds = Gear.KINDS
        while self.myc >= 100:
            k = kinds[sum(self.gear.pulls[x] for x in kinds) // 35 % 3]
            self.first.setdefault('gacha', self.wall)
            if self.myc >= 300:
                self.myc -= 300
                self.gear.pull(k, 35)
                self.d_pulls += 35
            else:
                self.myc -= 100
                self.gear.pull(k, 11)
                self.d_pulls += 11
        self.sync_gear()

    def attempt(self):
        key = (self.stage, self.p.key())
        r = self.cache.get(key)
        if r is None:
            r = B.attempt(self.stage, self.p)
            if len(self.cache) > 20000:
                self.cache.clear()
            self.cache[key] = r
        return r

    def info(self):
        return self.stage_info.setdefault(self.stage, dict(attempts=0, fails=0, reasons={}, play=0.0, buys=0))

    def play(self, seconds):
        """홈에서 seconds 동안 전투 (스테이지가 오른다). 번 전투 골드를 돌려준다"""
        left = seconds
        earned = 0.0
        while left > 0:
            inf = self.info()
            inf['buys'] += self.buy()
            r = self.attempt()
            if r.ok:
                k = 1
            else:
                mode = 'balanced' if r.reason == 'defeat' else 'offense'
                self.mode = mode
                st = best_buy(self.p, mode)
                if st is None or r.gold <= 0:
                    k = max(1, int(left // r.time))      # 살 것이 없거나 골드를 못 번다 → 남은 시간 내내 실패
                    if r.gold <= 0:
                        self.stuck = True
                else:
                    need = F.cost(st, self.p.lv[st]) - self.gold
                    k = max(1, math.ceil(need / r.gold))
                k = min(k, max(1, math.ceil(left / r.time)))
            dt = k * r.time
            left -= dt
            g = k * r.gold
            self.gold += g
            earned += g
            self.active += dt
            self.wall += dt
            self.d_kills += k * r.kills
            if r.ok or r.wave_reached > F.waves(self.stage):
                self.d_boss += k
            inf['attempts'] += k
            inf['play'] += dt
            if not r.ok:
                inf['fails'] += k
                inf['reasons'][r.reason] = inf['reasons'].get(r.reason, 0) + k
                continue
            self.mode = 'balanced'
            m = F.boss_myc(self.stage)
            self.myc += m
            self.myc_src['boss'] += m
            self.d_stages += 1
            self.stage += 1
            self.arrive.setdefault(self.stage, (self.active, self.wall))
            self.spend_myc()
        self.src['battle'] += earned
        return earned

    def watch(self, kind):
        """광고 기회 하나: 종류별로 ads 비율만큼 결정적으로 본다 (0.5면 번갈아)"""
        a = self.ad_acc.get(kind, 0.0) + self.ads
        if a >= 1 - 1e-9:
            self.ad_acc[kind] = a - 1
            self.d_ads += 1
            return True
        self.ad_acc[kind] = a
        return False

    def adbox(self, home_seconds):
        if self.pattern != 'daily' or self.ads <= 0:
            return
        self.adbox_elapsed += home_seconds
        n = int(self.adbox_elapsed // 3600)
        self.adbox_elapsed -= n * 3600
        for _ in range(n):
            if not self.watch('box'):
                continue
            self.n_box = getattr(self, 'n_box', 0) + 1
            g = F.enemy_gold(self.stage) * F.C('adBoxGoldKills')
            m = F.boss_myc(self.stage) * F.C('adBoxMyceliteBosses')
            self.gold += g; self.src['adbox'] += g
            self.myc += m; self.myc_src['adbox'] += m

    def quests(self):
        done = {'kill': self.d_kills, 'stage': self.d_boss if F.OV.get('quest_stage_boss') else self.d_stages, 'upgrade': self.d_upg,
                'gacha': self.d_pulls, 'ad': self.d_ads}
        qm = F.OV.get('quest_myc')
        qt = F.OV.get('quest_target')
        res = []
        qg = F.OV.get('quest_gold')      # D5: 퀘스트 골드 = 처치 골드(현재 스테이지) × goldKills (시트 열 그대로, 값만)
        for i, q in enumerate(F.T.quests):
            tgt = qt[i] if qt else q['target']
            ok = done[q['kind']] >= tgt
            res.append(ok)
            if not ok or i in self.claimed:     # 하루 한 번 (접속마다 확인, 깬 접속에 받는다)
                continue
            self.claimed.add(i)
            self.first.setdefault('quest', self.wall)
            gk = qg[i] if qg else q['goldKills']
            self.gold += F.enemy_gold(self.stage) * gk
            self.src['quest'] += F.enemy_gold(self.stage) * gk
            m = qm[i] if qm else q['myc']
            self.myc += m
            self.myc_src['quest'] += m
        return res

    def run_days(self, days, stop_stage=None):
        self.quest_log = []
        self.src_log = []
        for day in range(days):
            self.day_counters()
            n_free = int(round(4 * self.ads))     # 광고 뽑기 11회: 4종 중 n종 (공격 쪽부터)
            n_ad = int(F.OV.get('const', {}).get('adGachaPulls', 11))   # 광고 뽑기 1회 뽑기 수 (지금 11)
            for k in ['Weapon', 'Pet', 'Accessory', 'Character'][:n_free]:
                self.first.setdefault('gacha', self.wall)
                self.gear.pull(k, n_ad)
                self.d_pulls += n_ad
                self.d_ads += 1
            self.sync_gear()
            self.d_dun = {}
            for i, (mins, gap_h) in enumerate(self.sessions):
                if self.dungeon:
                    self.run_dungeons()      # 접속마다: 그날 남은 입장을 새로 열린 던전에도 (첫날 던전 맛보기, D6)
                g = self.play(mins * 60)
                if self.dungeon and getattr(self, 'dungeon_after_play', False):
                    self.run_dungeons()      # v3d: 이번 접속에서 새로 연 던전도 바로 입장 (첫날 첫 접속에 던전 1)
                self.adbox(mins * 60)
                q = self.quests()
                self.spend_myc()
                if i == len(self.sessions) - 1:
                    self.quest_log.append((day + 1, self.stage, q))
                # 앱 밖: 이번 접속의 초당 전투 골드 × min(비운 시간, 8시간), 정산 광고 2배
                off = g / (mins * 60) * min(gap_h, F.C('offlineCapHours')) * 3600 * F.OV.get('const', {}).get('offlineGoldRate', 1.0)
                self.gold += off; self.src['offline'] += off
                if self.watch('report'):
                    self.gold += off; self.src['offline_ad'] += off
                self.wall += gap_h * 3600
                self.buy()
            self.src_log.append(dict(self.src))      # 날마다 누적 골드 출처 (D8: 첫 30일 비중)
            if stop_stage and self.stage >= stop_stage:
                break
        return self

    def run_dungeons(self):
        """던전마다 무료 dungeonPerDay회 + 광고 입장(최대 dungeonAdPerDay, ads 비율). 자동 다음 레벨 도전: 깨면 레벨 +1"""
        free = int(F.C('dungeonPerDay'))
        ad_max = int(F.OV.get('const', {}).get('dungeonAdPerDay', 2))
        for dn, unlock in sorted(F.T.dungeon.items()):
            if self.stage < unlock or dn not in self.dungeon.KINDS:
                continue
            if dn in self.d_dun:
                continue
            self.d_dun[dn] = True
            self.first.setdefault('dungeon', self.wall)
            n = free + sum(1 for _ in range(ad_max) if self.watch('dungeon%d' % dn))
            self.n_dun = getattr(self, 'n_dun', 0) + n
            for _ in range(n):
                lv = self.dlevel.get(dn, 1)
                g, m, cleared = self.dungeon.run(dn, lv, self)
                self.gold += g; self.src['dungeon'] += g
                self.myc += m; self.myc_src['dungeon'] += m
                if cleared:
                    self.dlevel[dn] = lv + 1
        self.spend_myc()

    def run_continuous(self, hours, stop_stage=None, chunk=600):
        t = 0
        while t < hours * 3600:
            self.play(chunk)
            t += chunk
            if stop_stage and self.stage >= stop_stage:
                break
        return self


def first_arrival(sim, s):
    a = sim.arrive.get(s)
    return a
