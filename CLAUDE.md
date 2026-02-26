# Project Rules

## Absolute Rules
- Never modify files outside the project directory
- Never delete files without explicit confirmation
- Never push to git without explicit instruction
- Always ask before making architectural changes
- When unsure → ask, don't guess

## Workflow

### Before Starting Any Task
1. Read the relevant skill file from `.claude/skills/` if available
2. If the task is complex (>30 min estimated), write a plan first and wait for approval
3. Break large tasks into small, verifiable steps

### While Working
- Make one logical change at a time
- Run tests after each significant change if tests exist
- Keep track of every file you touch

### After Completing Any Task
- Read `.claude/checklists/post-task.md` and verify every item
- List all files created or modified
- If something feels uncertain, flag it explicitly

---

## Agent Roles

When the task involves writing substantial code, run these agents in order after completing the work:

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

## Memory Documents

Keep these files up to date during long tasks:

| File | Purpose |
|------|---------|
| `.claude/memory/plan.md` | Current task plan and design decisions |
| `.claude/memory/context.md` | Why decisions were made, relevant background |
| `.claude/memory/progress.md` | Checklist of completed and remaining steps |

When context gets long or a new session starts → read these files first.
