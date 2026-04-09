# User Testing Guide

This document covers manual user testing for the `squad` CLI. It assumes you are running from the repo root with:

```bash
alias squad="dotnet run --project src/Squad.Cli --"
```

---

## Setup

Before testing, create a temporary working directory:

```bash
mkdir /tmp/squad-test && cd /tmp/squad-test
```

---

## 1. Help and Discovery

### 1.1 Top-level help

```bash
squad --help
```

Expected: lists all commands with descriptions. Confirm `personal`, `plugin` branches appear at the bottom.

### 1.2 Version

```bash
squad --version
```

Expected: prints `0.2.0`.

### 1.3 Subcommand help

```bash
squad personal --help
squad plugin --help
squad plugin marketplace --help
```

Expected: each prints its own usage and subcommands.

---

## 2. Health Checks

### 2.1 Doctor with no squad

```bash
cd /tmp/squad-test
squad doctor
```

Expected: reports `.squad/ directory exists` check as failing with a hint to run `squad init`.

### 2.2 Doctor with a squad present

```bash
mkdir -p .squad
# Minimal team.md
cat > .squad/team.md <<'EOF'
# Squad Team — Test Team

## Members

| Name    | Role    | Skills | Status |
|---------|---------|--------|--------|
| builder | Builder | build  | Active |
EOF
squad doctor
```

Expected: `.squad/ directory exists` passes. Other checks may warn or fail depending on what files are present — that is fine.

---

## 3. Roles

```bash
squad roles
```

Expected: prints 20 built-in roles grouped by category (Software Development, Business & Operations, etc.) with emoji, short name, title, and tagline for each.

---

## 4. Economy

### 4.1 No squad

```bash
cd /tmp/squad-test-empty && mkdir /tmp/squad-test-empty && cd /tmp/squad-test-empty
squad economy
```

Expected: `✗ No squad found.`

### 4.2 With squad present

```bash
cd /tmp/squad-test
squad economy on
squad economy off
squad economy
```

Expected: `on`/`off` toggle the setting; bare `economy` shows current state.

---

## 5. Build

### 5.1 No config

```bash
cd /tmp/squad-test
squad build
```

Expected: error — `squad.config.json` not found.

### 5.2 With config

```bash
cat > /tmp/squad-test/squad.config.json <<'EOF'
{
  "version": "1.0",
  "team": { "name": "test-team" },
  "agents": [
    { "name": "builder", "role": "backend" }
  ],
  "routing": {
    "defaultAgent": "builder",
    "rules": []
  }
}
EOF
cd /tmp/squad-test
squad build
```

Expected: generates `.squad/` markdown files and reports the count written.

---

## 6. Migrate

### 6.1 Detect mode (no squad)

```bash
cd /tmp/squad-test-empty
squad migrate
```

Expected: reports mode `none` and suggests `squad init`.

### 6.2 --to markdown

```bash
cd /tmp/squad-test
squad migrate --to markdown --dry-run
```
Expected: `[DRY RUN] Would run squad build to generate .squad/ markdown.`

Actual <font bold color="red">✗</font>: `Error: Could not find color or style 'DRY'.`

### 6.3 --from ai-team

```bash
mkdir /tmp/squad-legacy && cd /tmp/squad-legacy
mkdir .ai-team
squad migrate --from ai-team --dry-run
```

Expected: `[DRY RUN] Would rename .ai-team/ → .squad/`

Actual <font bold color="red">✗</font>: `Error: Could not find color or style 'DRY'.`

```bash
squad migrate --from ai-team
ls
```

Expected: `.squad/` now exists, `.ai-team/` is gone.

### 6.4 --from ai-team when .squad already exists

```bash
mkdir /tmp/squad-legacy2 && cd /tmp/squad-legacy2
mkdir .ai-team && mkdir .squad
squad migrate --from ai-team
```

Expected: `✗` error message stating `.squad/ already exists`.

---

## 7. Export and Import

### 7.1 Export

```bash
cd /tmp/squad-test
squad export
```

Expected: writes `squad-export.json` and confirms the path.

### 7.2 Import round-trip

```bash
cp squad-export.json /tmp/squad-export-copy.json
rm -rf .squad squad.config.json
squad import /tmp/squad-export-copy.json
```

Expected: restores squad state from the export file.

---

## 8. Copilot

```bash
cd /tmp/squad-test
squad copilot add
squad copilot remove
```

Expected: adds/removes `@copilot` from `.squad/team.md` roster and confirms the change.

---

## 9. Personal Squad

### 9.1 Init

```bash
squad personal init
```

Expected: creates `~/.squad-personal/` (or configured path) with a starter `decisions.md`.

### 9.2 Add and list

```bash
squad personal add my-agent
squad personal list
```

Expected: `my-agent` appears in the list.

### 9.3 Remove

```bash
squad personal remove my-agent
squad personal list
```

Expected: `my-agent` no longer appears.

---

## 10. Plugin Marketplace

### 10.1 Add marketplace

```bash
squad plugin marketplace add owner/repo
```

Expected: registers `owner/repo` and confirms.

### 10.2 List

```bash
squad plugin marketplace list
```

Expected: `owner/repo` appears.

### 10.3 Browse

```bash
squad plugin marketplace browse owner/repo
```

Expected: fetches and lists plugins from that marketplace (requires `gh` CLI and network access).

### 10.4 Remove

```bash
squad plugin marketplace remove owner/repo
squad plugin marketplace list
```

Expected: marketplace no longer listed.

---

## 11. Upstream

```bash
squad upstream add /path/to/other-squad local
squad upstream list
squad upstream sync
squad upstream remove other-squad
```

Expected: each step confirms the action. `sync` pulls updated manifests from all registered upstreams.

---

## 12. Discover and Delegate

### 12.1 Discover with no upstreams

```bash
cd /tmp/squad-test
squad discover
```

Expected: `No squads discovered.`

### 12.2 Discover with an upstream

Set up a second local squad with a manifest, add it as an upstream, then:

```bash
squad discover
```

Expected: table listing the discovered squad with its name and accepted work types.

### 12.3 Delegate

```bash
squad delegate other-team "Implement OAuth login"
```

Expected: creates a cross-squad GitHub issue (requires `gh` CLI authenticated). Reports the created issue URL.

### 12.4 Delegate to unknown squad

```bash
squad delegate nonexistent "Some task"
```

Expected: `✗ Squad "nonexistent" not found.` with a list of known squads (or none discovered message).

---

## 13. Watch

```bash
cd /tmp/squad-test
squad watch --interval 1
```

Expected: starts polling loop, prints `Ralph — Watch Mode`, and polls every 1 minute. Press Ctrl+C to stop — should print `Ralph — Watch stopped.`

### Invalid interval

```bash
squad watch --interval 0
```

Expected: `✗ --interval must be a positive number of minutes.`

---

## 14. Extract

### 14.1 No consult mode

```bash
cd /tmp/squad-test
squad extract
```

Expected: `✗ Not in consult mode.`

### 14.2 Dry run with staged learnings

```bash
mkdir -p /tmp/squad-consult/.squad/extract
cat > /tmp/squad-consult/.squad/config.json <<'EOF'
{"version":1,"consultMode":true,"sourceSquad":"/tmp/personal-squad"}
EOF
cat > /tmp/squad-consult/.squad/extract/pattern.md <<'EOF'
## Always use async/await

Prefer async I/O throughout — never block on `.Result` or `.Wait()`.
EOF
cd /tmp/squad-consult
squad extract --dry-run
```

Expected: lists `pattern.md` with a content preview, writes nothing.

### 14.3 Extract with --yes

```bash
mkdir -p /tmp/personal-squad
echo "# Decisions" > /tmp/personal-squad/decisions.md
squad extract --yes
```

Expected: merges `pattern.md` into `/tmp/personal-squad/decisions.md` and reports `1 learning(s) merged.`

### 14.4 Copyleft license block

```bash
echo "GNU GENERAL PUBLIC LICENSE Version 3" > /tmp/squad-consult/LICENSE
squad extract --yes
```

Expected: `🚫 License: GPL-3.0 — Extraction blocked. Use --accept-risks to override.`

```bash
squad extract --yes --accept-risks
```

Expected: extraction proceeds.

---

## 15. Link and Init-Remote

```bash
squad link /path/to/team-root
squad init-remote /path/to/team-root
```

Expected: each writes the appropriate config and confirms. `link` connects this project to a shared team root. `init-remote` writes `.squad/config.json` for remote mode.

---

## 16. Streams and Schedule

```bash
squad streams list
squad schedule list
```

Expected: shows current state (empty lists are fine). Neither command should crash.

---

## Error Paths to Confirm

| Scenario | Command | Expected |
|---|---|---|
| Missing `gh` CLI | `squad delegate x "desc"` | `✗ gh CLI not found.` |
| Bad import file | `squad import /nonexistent.json` | error with path |
| Build on empty `.squad/` | `squad build` (no config) | `✗` with suggestion |
| Migrate to sdk with no `.squad/` | `squad migrate --to sdk` | `✗ No squad found.` |
| Watch interval zero | `squad watch --interval 0` | `✗` validation message |
