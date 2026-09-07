# Post-Task Checklist

Before reporting completion, verify ALL of the following.

**범용 완료 게이트 (구현·리팩터·이주·프리팹·런타임 동작):** `.claude/checklists/agent-completion-gate.md` — 경량 예외는 그 파일 §When. 보고 상태는 **Done / Incomplete / Partial** 중 하나.

## Code Quality
- [ ] No hardcoded values that should be constants or config
- [ ] No dead code or commented-out blocks left behind
- [ ] No TODO/FIXME left unaddressed unless explicitly agreed

## Error Handling
- [ ] All exceptions are caught or explicitly allowed to propagate
- [ ] Null references are guarded (null checks, ?., ?? operators)
- [ ] Edge cases handled (empty lists, zero values, missing files)

## Security
- [ ] No secrets, API keys, or passwords in code
- [ ] User inputs are validated before use
- [ ] No sensitive data written to logs

## Reporting
- [ ] List every file that was created or modified
- [ ] Summarize what changed and why
- [ ] Flag anything uncertain or requiring human review

## Migration / hybrid path switch — if this task did that
- [ ] `.claude/checklists/migration-parity.md` 계약을 기본 경로로 켜기 전에 통과했거나, 미완이면 구경로·Revert·flag off·Pending을 명시했다
- [ ] 예상 가능 공백을 사용자 재현 뺑뺑이로 메우지 않았다 (룰: `migration-parity.mdc`)

## Dist UI — if this task created/edited UI paths
- [ ] `.claude/checklists/dist-ui-gate.md` 통과 (`ui-font` · `ui-prefab-layout`)
- [ ] 검증 에이전트를 쓰면 Fail 시 Incomplete

## Scene — if this task dirtied an open Unity scene
- [ ] Saved (`manage_scene save` / `SaveOpenScenes`). MarkSceneDirty-only is not done

If any item fails → fix it before reporting done.
