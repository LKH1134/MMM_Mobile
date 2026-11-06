#!/bin/bash
# run_one.sh <variant> <dll폴더 base|cur|w> <표 이름> <MYC_KINDS|-> <시나리오> <시드>
S=/c/Users/user/AppData/Local/Temp/claude/82d5963c662fda11/15bcc578-adcf-464f-8bdc-aab5a3c07b9e/scratchpad
v=$1; d=$2; t=$3; mk=$4; nm=$5; sd=$6
mkdir -p $S/out/$v
export VERIFY_OUT=$(cygpath -w $S/out/$v)
if [ "$mk" != "-" ]; then export MYC_KINDS=$mk; else unset MYC_KINDS; fi
timeout 900 dotnet $S/sim_$d/IdleSim.dll run name=$nm seed=$sd maxDays=120 stuck=20 tag=${nm}_s$sd table="$(cygpath -w $S/tables/$t.xlsx)" > $S/out/$v/r_${nm}_s$sd.txt 2>&1
