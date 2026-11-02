# -*- coding: utf-8 -*-
"""test 실측(stage_P_*.csv)에서 '진행 0인 날'을 벽 단위(연속 일수)로 묶어 P3(2주차 이후 벽 1~3일, 벽 N+1 ≈ 1.5~2×N)와 비교.
wallSec = 그 스테이지에 도착한 벽시계 초 (테스트.md 12-1)."""
import csv
import glob
import os
import statistics as S
import sys

OUT = os.path.join(os.path.dirname(__file__), '..', 'test', 'out')
DAYS = 45


def day_max(path):
    arr = [0] * DAYS
    stages = []
    for r in csv.DictReader(open(path)):
        s, t = int(r['stage']), float(r['wallSec'])
        stages.append((s, t))
        d = int(t // 86400)
        if d < DAYS:
            arr[d] = max(arr[d], s)
    for d in range(1, DAYS):
        arr[d] = max(arr[d], arr[d - 1])
    return arr, stages


def streaks(arr):
    out, run, start = [], 0, None
    for d in range(1, DAYS):
        if arr[d] == arr[d - 1]:
            run += 1
            start = d if run == 1 else start
        else:
            if run:
                out.append((start, run, arr[d - 1]))
            run = 0
    if run:
        out.append((start, run, arr[DAYS - 1]))
    return out


if __name__ == '__main__':
    grp = sys.argv[1] if len(sys.argv) > 1 else 'B_half'
    zero, longest, walls = [], [], []
    for p in sorted(glob.glob(os.path.join(OUT, f'stage_P_{grp}_s*.csv'))):
        arr, _ = day_max(p)
        st = streaks(arr)
        zero.append(sum(x[1] for x in st))
        longest.append(max((x[1] for x in st), default=0))
        walls.append(st)
    print(grp, '진행 0인 날 (시드별)', zero, '중앙', S.median(zero), '| 가장 긴 연속 (시드별)', longest)
    # 주차별 벽 길이 (진행 0 연속 일수, 시작일 기준)
    for wk, (a, b) in enumerate(((1, 7), (7, 14), (14, 28), (28, 45)), 1):
        ls = [x[1] for st in walls for x in st if a <= x[0] < b]
        stuck = sorted({x[2] for st in walls for x in st if a <= x[0] < b})
        if ls:
            print(f'  {a}~{b}일 시작 벽: {len(ls)}개, 길이 중앙 {S.median(ls)}일, 최대 {max(ls)}일, 막힌 스테이지 예 {stuck[:12]}')
