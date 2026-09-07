# Project Rules

## Absolute Rules
- Never modify files outside the project directory
- Never delete files without explicit confirmation
- Never push to git without explicit instruction
- Always ask before making architectural changes
- When unsure → ask, don't guess

## Workflow

### Before Starting Any Task
1. Read the relevant skill file from `.claude/skills/` when creating or modifying Unity scripts — skip for docs-only, config, or single-line fixes
2. If the task is complex (>30 min estimated), write a plan first and wait for approval
3. Large tasks: break into **verifiable execution steps** (order/phasing — not “minimum diff” optimization). Plan/Handoff에 **Done·하지 말 것·검증** 블록 (`agent-completion-gate.mdc`)
4. **Scope:** match the **user's requested scope** — neither shrink for "minimum diff" nor expand for drive-by refactors. If your estimate **exceeds** what they asked, confirm before editing (`agent-scope.mdc` §Scope)

### While Working
- Make one logical change at a time
- Run tests after each significant change if tests exist
- Keep track of every file you touch
- **Unity MCP:** Editor-only work (`Dist/MCP/*`, scene save, animator/controller wiring) → run via Unity MCP first. If MCP is off, times out, or errors → **ask the user to fix MCP connection**, then retry; do **not** punt manual menu clicks or YAML/`git checkout` asset fixes to the user unless they explicitly approve (see `.cursor/rules/collaboration-unity.mdc` §Unity MCP).

### After Completing Any Task
- List all files created or modified
- If something feels uncertain, flag it explicitly
- **agent completion gate** (`.claude/checklists/agent-completion-gate.md` + `agent-completion-gate.mdc`): 구현·리팩터·이주·프리팹·런타임 동작 — **컴파일만 Done 금지**. 보고: Done / Incomplete / Partial
- **post-task checklist** (`.claude/checklists/post-task.md`):
  - **Skip** (token/속도 우선): ≤2 files, net ≤40 lines, typo/comment/format, handoff 구현만
  - **Required**: ≥3 files, public API·직렬화·새 타입/파이프라인, migration-parity 해당
  - **기능 불변 최적화·내부 교체**: `.claude/checklists/migration-parity.md` §C (기능 인벤토리 + 검증 게이트). 경량 예외는 §C와 동일(≤2파일·≤40줄·경로 불변)

---

## Agent Roles

**Token trade-off:** QA/Test/Review 3연속은 품질↑·토큰↑. 아래 기준으로만 실행.

When the task involves **substantial** code (≥3 files or new public API), run these agents in order after completing the work:

**Skip** QA/Test/Review when: ≤2 files, net ≤40 lines, 또는 handoff에 명시된 좁은 패치만.

### QA Agent
```
Review the following code strictly. List only problems — no praise needed.
Check for: bugs, null reference risks, missing error handling, security issues,
logic errors, and anything that could break in production.
Code: [paste code here]
```

### Test Agent
```
Write unit tests for the following code.
Requirements:
- Cover the happy path
- Cover at least 2 edge cases
- Cover at least 1 failure/error case
- Use the same language and test framework already in this project
Code: [paste code here]
```

### Review Agent (for PRs or final output)
```
You are a senior engineer doing a final review.
Check: naming conventions, code clarity, unnecessary complexity,
missing documentation, and consistency with the rest of the codebase.
Suggest improvements as inline comments.
Code: [paste code here]
```

---

## Component Documentation

When writing or modifying a Unity component, always include a header comment at the top of the file in this format:

```csharp
// ============================================================
// [ComponentName] — One-line summary of what this component does
// ============================================================
```

- Focus on "what it does", not "how it works"
- Required when creating a new component
- When modifying an existing component, add the header if missing or correct it if outdated

---

## Memory Documents

Keep these files up to date during long tasks:

| File | Purpose |
|------|---------|
| `.claude/memory/plan.md` | Current task plan and design decisions |
| `.claude/memory/context.md` | Why decisions were made, relevant background |
| `.claude/memory/progress.md` | Checklist of completed and remaining steps |

When context gets long or a new session starts → read these files first.

## Remaining work

SSOT: `docs/PLAN.md`.  
- “뭐 남았지?” → 그 문서의 남은 항목만 (도메인 Pending를 다시 긁지 않음).
- 구현 시 해당 항목 **지침** + 링크된 도메인 문서를 따른다. 끝나면 **항목 삭제** (완료 체크를 남기지 않음).

## Domain docs (LLM entry)

Index: `docs/README.md`. Stack: `docs/tech-stack.md`.  
Feature SSOT when touching that area — **read the Doc before editing related scripts**:

| Topic | Doc | Rule |
|-------|-----|------|
| Remaining work / impl guide | `docs/PLAN.md` | (구현되면 항목 삭제) |
| Game time / scale / day clock | `docs/time/TIME.md` | `.cursor/rules/game-time.mdc` |
| Weather / climate field | `docs/weather/WEATHER.md` | — |
| Map / TileMap / bake / visibility | `docs/map/SYSTEM.md` (+ `TILEMAP*.md`) | `.cursor/rules/map-system.mdc` |
| Inventory UI | `docs/inventory/INVENTORY_UI.md` | `.cursor/rules/inventory-ui.mdc` |
| Equipment / weapon visual folders | `docs/equipment/GEAR.md` · `WEAPON_VISUAL.md` | (Wear·Wield · anim/VFX 맵) |
| UI MVC / font | `docs/ui/UI_Scripts.md` | `.cursor/rules/ui-prefab-layout.mdc` · `ui-font.mdc` |
| UI hover placement | `docs/ui/HOVER.md` | (ContextMenu asm · keep-in-bounds) |
| Locomotion | `docs/locomotion/LOCOMOTION.md` | `.cursor/rules/locomotion.mdc` |
| Swim / Dive | `docs/locomotion/SWIM.md` | — |
| Vault / Fence vault | `docs/locomotion/VAULT.md` | — |
| Body / anatomy / climate | `docs/body/BODY.md` · 밸런스 `docs/body/TUNING.md` | — |
| Legacy (do not expand as SSOT) | `docs/legacy/LEGACY_README.md` | `.cursor/rules/legacy.mdc` |

Assets 트리의 `.md`는 stub다. 본문은 `docs/`만 진실원.
