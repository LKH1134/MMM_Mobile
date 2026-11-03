#!/bin/bash
# 공속 3.0 봉인(speedMax=275) 조건만 다시 돌린다
cd "$(dirname "$0")/sim"
jobs=()
for sd in 1 2 3 4 5 6 7 8 9 10; do for nm in A B C Bads Cads; do jobs+=("name=$nm seed=$sd speedMax=275 tag=${nm}_s275_s$sd"); done; done
printf '%s\n' "${jobs[@]}" | xargs -P 8 -I{} bash -c 'a="{}"; t=$(echo "$a" | sed "s/.*tag=//"); timeout 900 dotnet bin/Release/net10.0/IdleSim.dll run $a stuck=20 > ../out/r_$t.txt 2>&1'
echo done
