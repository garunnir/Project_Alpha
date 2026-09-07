# Agent Completion Gate (범용 완료 게이트)

**목적:** 구현·리팩터·이주·프리팹·런타임 경로 작업에서 **「패치함 / 컴파일됨」≠ 완료** 를 강제한다. 도메인·기능명에 묶지 않는 **공통** 게이트.

**관련 룰:** `.cursor/rules/agent-completion-gate.mdc` · `agent-scope.mdc` · `migration-parity.mdc`

---

## When (적용)

다음 **하나 이상**이면 이 게이트 **필수** (`CLAUDE.md` post-task skip과 **별개** — skip은 QA/Test 생략용, 완료 선언은 여기 기준):

| 트리거 | 예 |
|--------|-----|
| 변경 규모 | ≥3 파일, 또는 한 파일 net ≥40줄 |
| 계약·데이터 | 공개 API, 직렬화/저장 스키마, 새 타입·파이프라인 |
| 경로 교체 | 구/신 경로, 하이브리드, 기능 불변 리팩터 (`migration-parity`) |
| 에셋·에디터 | 프리팹/씬/Animator/MCP 메뉴·`Dist/MCP/*` |
| 동작·입력·표시 | 플레이어/NPC/UI/전투/상호작용 등 **런타임에서 보이는** 동작 |

**경량 예외 (게이트 생략 가능):** ≤2파일, net ≤40줄, typo/주석/포맷, handoff에 **완료 조건·검증이 이미 적힌** 좁은 패치만.

---

## 1. 시작 전 — Plan / Handoff (Agent 실행 전)

- [ ] **완료 조건(Done)** 이 측정 가능하게 적혀 있다 (체크 ≤5, “잘 됨” 금지)
- [ ] **범위 밖(하지 말 것)** 이 한 줄 이상 있다 (다른 도메인 혼입 방지)
- [ ] 이주·리팩터면 **변경 전 동작 계약** 또는 `migration-parity.md` §C 인벤토리가 있다
- [ ] 에디터 SSOT(Organize/Ensure/Patch 메뉴)가 있으면 **MCP/메뉴 실행**이 Plan에 포함됐다

**Plan 템플릿 (복붙):**

```text
## Done (완료 조건)
- [ ] …

## 하지 말 것
- …

## 검증
- [ ] 정적: …
- [ ] 런타임/Play: … (에이전트 불가 시 → Incomplete로 보고)
```

---

## 2. 구현 중

- [ ] **요청 범위만** — 승인 없이 다른 도메인·드라이브 리팩터 혼입 안 함
- [ ] 두 경로 공존 시 **경계 SSOT** (누가 입력·resolve·저장 담당) 코드 또는 문서에 명시
- [ ] Unity 에디터 작업은 **MCP 우선**; 실패 시 사용자에게 MCP 복구 요청 (수동 넘기기 금지, 명시 승인 없으면)

---

## 3. 완료 선언 전 — 정적 검증 (에이전트가 할 수 있는 것)

- [ ] **Unity 콘솔 Error 0** — `Unity_GetConsoleLogs`(errorCount) 또는 `Unity_ReadConsole` Types=Error. 컴파일 실패 시 Done 금지
- [ ] 변경 파일 목록·요약을 보고에 포함
- [ ] **컴파일만**으로 완료 선언하지 않음
- [ ] Plan **Done** 항목과 diff가 대응 (미충족 → Pending 명시)
- [ ] 삭제·이름 변경 시 **남은 참조** grep (구 타입·구 메뉴·구 입력 경로)
- [ ] 이주 목표와 반대 패턴 grep (예: “캐시/SSOT로 통일”인데 소비자마다 `GetComponent` 남음 → 미완 또는 문서화된 예외)
- [ ] 씬 dirty 시 **저장** (`SaveOpenScenes` / MCP). `MarkSceneDirty`만으로 끝내지 않음
- [ ] `migration-parity` 해당 시 `.claude/checklists/migration-parity.md` 통과 또는 미완·Revert·flag off 명시
- [ ] Dist UI 경로면 `.claude/checklists/dist-ui-gate.md` 통과

---

## 4. 완료 선언 전 — 런타임 검증

동작·입력·표시·저장·네트워크에 손댔으면:

- [ ] Play 또는 동등 수동 검증을 **Done**에 적은 항목까지 수행
- [ ] 에이전트가 Play/MCP 런타임을 **못 돌리면** → 상태는 **Incomplete** 또는 **「코드 반영 완료, Play 검증 Pending」** — **Done 금지**
- [ ] 예상 가능한 회귀를 사용자 재현 뺑뺑이로 메우지 않음 (계약 밖 버그만 사용자 확인 요청)

---

## 5. 보고 형식 (MUST — 셋 중 하나)

| 상태 | 사용 조건 |
|------|-----------|
| **Done** | §1 Done 전항 + §3 해당 항목 + §4(해당 시) 통과 |
| **Incomplete** | 정적/Play/MCP 중 막힌 것 + 다음 검증 한 줄 |
| **Partial** | 일부만 목표 달성 — **Pending 목록** + 기본 경로 켰다고 선언 안 함 |

**MUST NOT**

- 컴파일·grep 1회만 하고 **Done**
- Plan에 없는 범위를 Done에 끼워 넣기
- 하이브리드·미배선을 침묵하고 완료
- Play 검증 생략 후 “테스트해 보세요”만 하고 **Done**

---

## 6. 도메인 세부

기능별 체크 항목(조준 커서, 인벤 DnD 등)은 **도메인 문서·해당 Plan의 Done**에만 둔다. 이 파일은 **완료 선언 프로세스** SSOT.
