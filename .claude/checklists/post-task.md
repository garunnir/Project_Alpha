# Post-Task Checklist

Before reporting completion, verify ALL of the following:

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

If any item fails → fix it before reporting done.
