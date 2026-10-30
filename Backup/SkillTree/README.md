# SkillTree 백업 (2026-10-30)

모바일 방치형 전환으로 쓰지 않게 된 스킬트리 씬과 그 화면 전용 코드·리소스.
`Assets/` 밖에 두어 Unity가 읽거나 컴파일하지 않는다. `.meta`를 같이 두었으므로
되살릴 때는 `Assets/` 아래 같은 경로로 그대로 복사하면 GUID 연결이 유지된다.

되살릴 때 함께 할 것:
- Build Settings에 `Assets/Scenes/SkillTree.unity` 다시 추가
- `BottomBar.prefab` 세계수 자리 버튼의 `SceneButton.scene`을 `SkillTree`로 (제거 전 값)
- WorldTree 씬 `WorldTreeScreen.backScene`을 `SkillTree`로 (제거 전 값), Back 버튼은 원래 꺼져 있었음

남겨 둔 것 (다른 코드가 씀): `SkillProgress`(StageProgress의 미셀라이트 정산),
`SkillData`/`GameData.Skills`/`TableImporter`, 스킬 테이블.
