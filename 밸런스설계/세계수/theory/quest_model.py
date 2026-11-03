# 의뢰 v5 모델 (WorldTree.Quests.cs). wt_model.Tree(quests=QuestBook(...))로 붙인다.
#   일일: daily 행 중 treeQuestDaily개 — 게임은 무작위, 모델은 결정적으로 돌림(day % 4번째를 뺌 = 각 행이 3/4 날에 나옴)
#   주간: weekly 행 전부, 7일 묶음(1일 = 월요일 가정; start_dow로 첫 주를 줄일 수 있음)
#   진행은 라이브 접속만: kill = 보스 뺀 처치, species = kill/10 (지정 적 1종, 10종 균등), stage = 보스 처치(돌파),
#   boss = 보스 등장(대안 K3), combo = 던전 1 판 중 한 번이라도 목표 이상이면 (combo_p = 하루에 한 번 닿을 확률, 기대값으로 exp × p)
#   daily = 일일 의뢰 받은 수. 받기 = 진행이 찬 그 접속 (exp → D.exp, 공물 앞)
class QuestBook:
    def __init__(self, daily, weekly, n_daily=3, combo_p=1.0, start_dow=0, stage_kind='stage'):
        self.daily, self.weekly = daily, weekly          # [(kind, target, exp)]
        self.n, self.combo_p, self.start_dow, self.stage_kind = n_daily, combo_p, start_dow, stage_kind
        self.week = None
        self.log = []

    def _prog(self, kind, s):
        if kind == 'kill':
            return s['kills']
        if kind == 'species':
            return s['kills'] / 10
        if kind == 'stage':
            return s['boss_tries'] if self.stage_kind == 'boss' else s['stages']
        if kind == 'boss':
            return s['boss_tries']
        return 0

    def new_day(self, tree, dayrec):
        d = dayrec['day']
        k = len(self.daily)
        skip = (d % k) if self.n < k else None
        self.today = [dict(q=q, prog=0.0, done=False) for i, q in enumerate(self.daily) if i != skip][:self.n]
        self.first_sess = True
        wk = (d - 1 + self.start_dow) // 7
        if wk != self.week:
            self.week = wk
            self.wq = [dict(q=q, prog=0.0, done=False) for q in self.weekly]
        self.day_exp = 0.0

    def session(self, tree, s):
        ex = 0.0
        n_done = 0
        for e in self.today:
            kind, target, exp = e['q']
            if e['done']:
                continue
            if kind == 'combo':
                if self.first_sess:      # 던전 1은 그날 첫 접속에 돈다 (progress.run_dungeons)
                    e['done'] = True; ex += exp * self.combo_p; n_done += self.combo_p
                continue
            e['prog'] += self._prog(kind, s)
            if e['prog'] >= target:
                e['done'] = True; ex += exp; n_done += 1
        self.first_sess = False
        for e in self.wq:
            kind, target, exp = e['q']
            if e['done']:
                continue
            e['prog'] += n_done if kind == 'daily' else self._prog(kind, s)
            if e['prog'] >= target - 1e-9:
                e['done'] = True; ex += exp
        self.day_exp += ex
        return ex
