# 메인 일일·주간 퀘스트의 세계수 경험치 (2026-11-12 현행화).
# 11-10에 세계수 의뢰(WorldTree.Quests.cs · 의뢰 시트)가 메인 퀘스트(Quest/DailyQuests.cs · 퀘스트 시트)로 합쳐졌다.
#   - 일일 행은 **모두** 매일 나온다(옛 의뢰는 3개 무작위). 주간 행도 모두. 보상 treeExp → WorldTree.AddQuestExp(Growth.cs:201-206) → D.exp
#   - 일일: 한국 00시 초기화, 주간: 월요일 한국 00시 (1일 = 월요일 가정, start_dow로 바꿈)
#   - 진행은 앱을 켠 동안만 센다(DailyQuests.cs:15). 종류별 진행원 (코드 줄):
#       kill    = 처치(IdleBattle.OnEnemyKilled :490, 보스 포함) → feed 처치 + 돌파 수
#       stage   = 보스 도전(IdleBattle.cs:443, 벽에서 막혀도 1) → feed boss_tries
#       upgrade = 강화(IdleProgress.cs:321) → 접속마다 강화하므로 첫 접속에 20 (가정)
#       gacha   = 뽑기 수(GachaPanel.cs:161) → 그날 뽑기 ≥ 11이면 첫 접속 (feed pulls)
#       ad      = 광고(AdService.cs:184) → 광고 비율 > 0이면 첫 접속에 3 (광고 뽑기·정산 2배·상자, 가정). 무광고는 못 깸
#       dungeon = 던전 판(IdleBattle.Dungeon.cs:225 · HarvestGame.cs:176) → feed dungeon_runs 수
#       daily   = 일일 퀘스트 받은 수(DailyQuests.Claim :164, 주간만)
#   - 받기는 진행이 찬 그 접속(일괄 수령, ClaimAll은 일일 먼저 → 주간 daily가 같은 번에 찰 수 있음)
class MainQuestBook:
    def __init__(self, quests, ads, start_dow=0, enabled=True, only=None):
        """quests = wt_tables T['quests'], ads = 광고 비율(0이면 광고 퀘스트 못 깸), only = 이 key만 (민감도)"""
        rows = [q for q in quests if only is None or q['key'] in only]
        self.daily = [q for q in rows if q['period'] == 'daily']
        self.weekly = [q for q in rows if q['period'] == 'weekly']
        self.ads, self.start_dow, self.enabled = ads, start_dow, enabled
        self.week = None
        self.log = []        # (day, 일일 받은 수, 주간 받은 key들)

    def _prog(self, kind, s, first, dayrec):
        if kind == 'kill':
            return s['kills'] + s.get('stages', 0)
        if kind == 'stage':
            return s['boss_tries']
        if kind == 'dungeon':
            return len(s.get('dungeon_runs') or {})
        if kind == 'upgrade':
            return 10 ** 9 if first else 0
        if kind == 'gacha':
            return dayrec['pulls'] if first else 0
        if kind == 'ad':
            return 10 ** 9 if (first and self.ads > 0) else 0
        return 0

    def new_day(self, tree, dayrec):
        d = dayrec['day']
        self.dayrec = dayrec
        self.today = [dict(q=q, prog=0.0, done=False) for q in self.daily]
        self.first = True
        wk = (d - 1 + self.start_dow) // 7
        if wk != self.week:
            self.week = wk
            self.wq = [dict(q=q, prog=0.0, done=False) for q in self.weekly]
        self.n_daily = 0

    def session(self, tree, s):
        """이 접속에서 받은 (세계수 경험치, 주간 골드 KE, 주간 균사석). 일일 행의 골드·균사석은 feed(방치 퀘스트 수입)에 이미 들어 있어
        돌려주지 않는다 — 주간 행은 11-10에 새로 생겨 feed에 없으므로 방치 수입에 더한다"""
        if not self.enabled:
            return 0.0, 0.0, 0.0
        ex = g = m = 0.0
        n_done = 0
        for e in self.today:
            if e['done']:
                continue
            e['prog'] += self._prog(e['q']['kind'], s, self.first, self.dayrec)
            if e['prog'] >= e['q']['target']:
                e['done'] = True; n_done += 1
                ex += e['q']['treeExp']
        self.n_daily += n_done
        for e in self.wq:
            if e['done']:
                continue
            k = e['q']['kind']
            e['prog'] += n_done if k == 'daily' else self._prog(k, s, self.first, self.dayrec)
            if e['prog'] >= e['q']['target'] - 1e-9:
                e['done'] = True
                ex += e['q']['treeExp']; g += e['q']['goldKills']; m += e['q']['mycelite']
                self.log.append((self.dayrec['day'], 'w', e['q']['key']))
        self.first = False
        return ex, g, m
