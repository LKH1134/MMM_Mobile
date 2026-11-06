#!/bin/bash
# 펫 삭제 전후(11-04) 측정: 기준선(7b17e68 dll+표) vs 작업 트리(펫 삭제 후 dll+표). 무광고 B / 보통 Bhalf / 많이 Bads, 시드 1~10, 120일
# 준비: build/mkproj.py 로 csproj 생성 → dotnet build (b_base, b_cur) → sim/bin/base 에 b_base dll을 덮어쓴 복사본. 표는 각 커밋의 GameBalance.bytes
cd "$(dirname "$0")"
mkdir -p out/pet
for sd in 1 2 3 4 5 6 7 8 9 10; do for nm in B Bhalf Bads; do for v in base cur; do echo "$v $nm $sd"; done; done; done | xargs -P 8 -L1 bash run_pet_one.sh
echo done
