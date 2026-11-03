#!/usr/bin/env bash
# 관리자 감시: 요청·답·"끝:"·산출물 생성만 새로 생긴 줄을 알린다
cd "$(dirname "$0")"
snap() { { grep -H -E '^- \[( |x)\] |^지금: 끝' 현황/*.md 2>/dev/null; ls 조사.md 이론.md 검증.md 테스트.md 2>/dev/null | sed 's/^/산출물: /'; } | sort; }
prev=$(snap)    # 시작 시점 것은 알리지 않는다
while true; do
  sleep 20
  cur=$(snap)
  comm -13 <(echo "$prev") <(echo "$cur")
  prev=$cur
done
