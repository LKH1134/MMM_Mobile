# 라운드 8 방치 입력 (wt-verify, 2026-11-12): 지금 표(MainTable.xlsx)로 만든 근사 feed.
# 11-03 feed(r6/feed_v3_*, KHD theory/progress.py)는 지금 방치 표와 맞지 않아(스테이지 표·던전·펫 삭제·광고 뽑기 35회·출석 등) 다시 만든다.
# KHD theory 래퍼(../feed.py)는 지금 표에서 죽는다(상수 dungeonSweepMul 없음) → 방치 진행 모델을 새로 짜지 않고 아래처럼 근사한다.
#
# 1) 스테이지 곡선 = test 실측(실제 DLL 시뮬, 11-10 12:02 빌드) 밸런스설계/test/out/stage_stab_{B,Bads}.csv 의 (wallSec → stage).
#    B = 무광고, Bads = 광고 전부, 보통 = 두 곡선의 스테이지별 벽시계 평균(가정). 시뮬 접속은 7·13·20시 1시간씩(하루 3h).
#    여기 접속은 7:00 30분 · 12:00 20분 · 19:00 40분(하루 90분, 세계수 기준선). 스테이지 곡선은 벽(보스 시간 초과)에서 앱 밖 골드로 넘는
#    구간이 대부분이라 접속 시간에 덜 민감하다고 보고 그대로 쓴다(가정 F1).
# 2) 처치: 접속 안에서 오른 스테이지의 적 수(IdleProgress.EnemyCount, IdleProgress.cs:471-475 × wavesPerStage) + 남은 시간은 벽 재도전.
#    벽 재도전 1번 = 일반 적 waves 전부 + 보스 시간 초과, 시간 = 실측 벽 스테이지 (playSec 차 / (attempts+1)) 중앙값.
#    보스 등장(퀘스트 stage) = 오른 스테이지 수 + 재도전 수 (IdleBattle.cs:443).
# 3) 골드(절대): 전투(적 골드 × 처치 + 보스 골드 × 오른 스테이지) + 앱 밖(직전 접속 초당 골드 × min(공백, offlineCapHours) × offlineGoldRate ×
#    (1 + 광고율: 정산 2배)) + 퀘스트(goldKills × eg, 광고 퀘스트는 광고 ≥ 3일 때) + 광고 상자 + 던전(DungeonRewards.cs:39-43) + 룰렛(Roulette.cs:108-109).
# 4) 균사석: 퀘스트 + 보스(bossMycelite) + 던전 + 광고 상자 + 룰렛 + 출석(1~6일). 가이드 퀘스트(일회성)는 뺐다(가정 F2).
# 5) 뽑기: 균사석 (1 − 세계수 몫 0.10)으로 35회(300) 뽑기, 무기·방어구·꼬마 셋을 돌아가며(test 시뮬과 같음) → 꼬마 = 1/3.
#    광고 뽑기 35회: 보통 = 무기·방어구(test Bhalf와 같음), 광고 많이 = 셋 다 → 꼬마 +35/일.
# python r8/feed_now.py <a: 0|0.5|1> [시간배수] [days]   → r8/feed_now_a<a>[_t<배수>].json
import sys, os, json, csv, statistics
sys.stdout.reconfigure(encoding='utf-8')
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, os.path.dirname(HERE))
import tables
T = tables.load()
C = T['const']
OUT = os.path.abspath(os.path.join(HERE, '../../../test/out'))


def curve(tag):
    rows = list(csv.DictReader(open(os.path.join(OUT, f'stage_{tag}.csv'), encoding='utf-8')))
    return [(int(r['stage']), float(r['wallSec']), float(r['playSec']), int(r['attempts'])) for r in rows]


def enemies(s):
    r = T['stage'][min(s, max(T['stage']))]
    return sum(min(r['enemyCountMax'], max(r['enemyCountBase'], r['enemyCountBase'] + r['enemyCountPerWave'] * (w - 1) + r['enemyCountPerStage'] * (s - 1)))
               for w in range(1, r['wavesPerStage'] + 1))


def eg(s): return T['stage'][min(s, max(T['stage']))]['enemyGold']
def bg(s): return T['stage'][min(s, max(T['stage']))]['bossGold']
def bm(s): return T['stage'][min(s, max(T['stage']))]['bossMycelite']


CB, CA = curve('stab_B'), curve('stab_Bads')
# 벽 재도전 1번 시간 (실측 중앙값)
att = []
for cv in (CB, CA):
    for (s0, w0, p0, a0), (s1, w1, p1, a1) in zip(cv, cv[1:]):
        if a0 >= 5: att.append((p1 - p0) / (a0 + 1))
T_ATT = statistics.median(att)
# 벽이 아닌 스테이지 1개를 깨는 실측 시간 (enemies 대비 초당 처치)
clr = {}
for cv in (CB, CA):
    for (s0, w0, p0, a0), (s1, w1, p1, a1) in zip(cv, cv[1:]):
        if a0 == 0: clr.setdefault(s0, []).append(p1 - p0)
CLR = {s: statistics.median(v) for s, v in clr.items()}


def clear_time(s): return CLR.get(s, enemies(s) / 3.0 + 10)


def stage_at(day_h, a):
    """벽시계 시간(시간) → 스테이지. 보통 = 두 곡선 벽시계 평균"""
    def by(cv):
        return {s: w / 3600 for s, w, _, _ in cv}
    b, c = by(CB), by(CA)
    if a <= 0: m = b
    elif a >= 1: m = c
    else:
        m = {s: (b[s] + c[s]) / 2 for s in b if s in c}
        top = max(m); m.update({s: v for s, v in c.items() if s > top and v >= m[top]})
    st = 1
    for s, h in sorted(m.items()):
        if h <= day_h: st = max(st, s)
    return st


def dungeon_day(stage, a):
    """던전 하루: 입장 = dungeonPerDay + dungeonAdPerDay × a, 레벨 = 기준 스테이지가 방치 스테이지 이하인 최고 (DungeonRewards.cs:5-43)"""
    gold = myc = 0.0; n = 0; adn = 0
    for d in T['dungeons']:
        if stage < d['unlockStage']: continue
        k = C['dungeonPerDay'] + C['dungeonAdPerDay'] * a
        n += k; adn += C['dungeonAdPerDay'] * a
        if d['mode'] == 'BossLevel':          # 보스 토벌: 한 판 보스 3마리 근사 (test Driver.BossLvKills)
            mul = sum(1 + d['rewardGrowth'] * i for i in range(3)); L = 1
        else:
            L = 1 + max(0, (stage - d['baseStage']) // max(1, d['stageStep']))
            mul = 1 + d['rewardGrowth'] * (L - 1)
        gold += k * eg(stage) * d['rewardKills'] * (mul if d['mode'] in ('BossLevel', 'BossRush') else 1)
        myc += k * d['rewardMyc'] * mul
    return gold, myc, n, adn


def build(a, scale=1.0, days=90, tree_myc=0.10):
    sess_t = [(7.0, 30), (12.0, 20), (19.0, 40)]          # (시작 시각, 분)
    qd = [r for r in T['quests'] if r['period'] == 'daily']
    qw = [r for r in T['quests'] if r['period'] == 'weekly']
    roul_g = sum(r['amount'] * r['weight'] for r in T['roulette'] if r['kind'] == 'Gold') / sum(r['weight'] for r in T['roulette'])
    roul_m = sum(r['amount'] * r['weight'] for r in T['roulette'] if r['kind'] == 'Mycelite') / sum(r['weight'] for r in T['roulette'])
    attend = {r['day']: r['amount'] for r in T['attend'] if r['kind'] == 'Mycelite'}
    ad_kinds = 0 if a <= 0 else (3 if a >= 1 else 2)
    out = dict(base='now', ads=a, scale=scale, sessions=[(m * scale, None) for _, m in sess_t], t_attempt=T_ATT, days=[])
    stage = 1; rate_prev = None
    for d in range(days):
        sess = []; battle_g = 0.0; off_g = 0.0; boss_myc = 0.0; kills_d = 0
        for i, (h0, m) in enumerate(sess_t):
            mins = m * scale
            end_h = d * 24 + h0 + mins / 60
            s1 = max(stage, stage_at(end_h, a))
            play = mins * 60; kills = 0; bosses = 0; t = 0.0; s = stage; g0 = battle_g
            while s < s1 and t + clear_time(s) <= play + 1e-9:
                kills += enemies(s); bosses += 1; t += clear_time(s); battle_g += enemies(s) * eg(s) + bg(s); boss_myc += bm(s); s += 1
            if s < s1:                     # 곡선만큼 못 깼으면 남은 스테이지는 다음 접속으로 (드묾: 첫날)
                pass
            farm = max(0.0, play - t); n_att = farm / T_ATT
            fk = n_att * enemies(s); kills += fk; bosses += n_att; battle_g += fk * eg(s)
            rate = (battle_g - g0) / play
            gap_h = ((sess_t[i + 1][0] if i + 1 < len(sess_t) else 24 + sess_t[0][0]) - h0 - mins / 60)
            off = rate * min(gap_h, C['offlineCapHours']) * 3600 * C['offlineGoldRate'] * (1 + a)
            off_g += off
            sess.append(dict(mins=mins, gap=gap_h, kills=kills, kills_all=kills + (s - stage), bosses=bosses, dun=0, stage0=stage, stage=s, eg=eg(s),
                             g_battle=battle_g - g0))
            stage = s; kills_d += kills
        dg, dm, dn, dadn = dungeon_day(stage, a)
        sess[0]['dun'] = dn                                  # 던전은 첫 접속에 몰아서 (퀘스트 진행만 쓰임)
        home_h = sum(x['mins'] for x in sess) / 60 * 0.7     # test Driver home 0.7
        boxes = home_h / C['adBoxIntervalHours'] * a
        box_g = boxes * C['adBoxGoldChance'] * C['adBoxGoldKills'] * eg(stage)
        box_m = boxes * (1 - C['adBoxGoldChance']) * C['adBoxMycelite']
        ads = 3 * a + ad_kinds + boxes + dadn                 # 정산 2배 + 광고 뽑기 + 상자 + 던전 광고
        q_ok = [r for r in qd if not (r['kind'] == 'ad' and ads < r['target'])]
        q_g = sum(r['goldKills'] for r in q_ok) * eg(stage) + sum(r['goldKills'] for r in qw) * eg(stage) / 7
        q_m = sum(r['mycelite'] for r in q_ok) + sum(r['mycelite'] for r in qw) / 7
        r_g = 3 * roul_g * eg(stage); r_m = 3 * roul_m
        gold = dict(battle=battle_g, offline=off_g, quest=q_g, adbox=box_g, dungeon=dg, roulette=r_g)
        myc = dict(boss=boss_myc, quest=q_m, adbox=box_m, dungeon=dm, roulette=r_m, attend=attend.get(d + 1, 0))
        myc_total = sum(myc.values())
        pulls_m = myc_total * (1 - tree_myc) / 300 * 35
        pulls = pulls_m + 35 * ad_kinds
        kid = pulls_m / 3 + (35 if ad_kinds >= 3 else 0)
        out['days'].append(dict(day=d + 1, stage=stage, eg=eg(stage), sessions=sess, kills=kills_d, gold=gold, gold_total=sum(gold.values()),
                                gold_kills=sum(gold.values()) / eg(stage), myc=myc, myc_total=myc_total, pulls=pulls, kid_pulls=kid, ads=ads))
    return out


if __name__ == '__main__':
    a = float(sys.argv[1]); scale = float(sys.argv[2]) if len(sys.argv) > 2 else 1.0
    days = int(sys.argv[3]) if len(sys.argv) > 3 else 90
    f = build(a, scale, days)
    name = f'feed_now_a{a}' + ('' if scale == 1 else f'_t{scale}') + '.json'
    json.dump(f, open(os.path.join(HERE, name), 'w', encoding='utf-8'), indent=0)
    print(name, 'T_ATT %.1fs' % T_ATT)
    for D in f['days']:
        if D['day'] in (1, 2, 3, 7, 14, 21, 30, 45, 60, 90):
            g = D['gold']
            print(D['day'], 'stage', D['stage'], 'kills %.0f' % D['kills'], 'goldKE %.0f' % D['gold_kills'],
                  'off%% %.0f dun%% %.0f' % (g['offline'] / D['gold_total'] * 100, g['dungeon'] / D['gold_total'] * 100),
                  'myc %.0f' % D['myc_total'], 'pulls %.0f kid %.1f ads %.1f' % (D['pulls'], D['kid_pulls'], D['ads']),
                  'bosses %.0f' % sum(s['bosses'] for s in D['sessions']))
