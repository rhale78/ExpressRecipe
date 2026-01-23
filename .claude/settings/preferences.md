# User Preferences for Claude Code Sessions

## Token Efficiency (HIGH PRIORITY)
- **Ask questions** if something is unclear or if there's a way to save tokens/time
- **Use cached file contents** - don't re-read files already in context; say "using cached context" when doing so
- **Use git diff** to check if files changed before re-reading
- **Suggest file splits** when multiple classes/interfaces are in same file

## Code Organization
- **One class/interface per file** - always. Suggest splitting when violations found.

## Agent Usage
- **Prefer Explore/Task agents** for searches over multiple Glob/Grep calls
- **Accuracy over speed** - validate with Grep/Glob if agent results seem off
- Working software > fast delivery with mistakes

## Communication Style
- **Keep responses short** unless documentation/full summaries requested
- **Plans are fine** but ask questions if unclear
- **Proactively suggest** token-saving approaches

## Goal
Get work done efficiently without waiting for session resets. Time and tokens are valuable.
