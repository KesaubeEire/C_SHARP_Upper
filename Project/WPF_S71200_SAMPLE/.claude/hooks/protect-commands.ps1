# PreToolUse hook: gate destructive shell commands.
#
# Permission model: broad allow for safe commands, this hook escalates
# DESTRUCTIVE commands:
#   * ASK  — force a permission prompt for recoverable-but-destructive actions.
#   * DENY — hard-block the truly catastrophic (wiping / or ~).
#
# Reads the PreToolUse event JSON on stdin; emits the decision on stdout (exit 0).
# Fails OPEN on any parse problem.

$ErrorActionPreference = 'Stop'

try {
    $raw = [Console]::In.ReadToEnd()
    if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }
    $event = $raw | ConvertFrom-Json
}
catch { exit 0 }

$command = [string]$event.tool_input.command
if ([string]::IsNullOrWhiteSpace($command)) { exit 0 }

$norm = ($command -replace '\s+', ' ').Trim()

function Send-Decision {
    param([string]$Decision, [string]$Reason)
    $payload = [ordered]@{
        hookSpecificOutput = [ordered]@{
            hookEventName            = 'PreToolUse'
            permissionDecision       = $Decision
            permissionDecisionReason = $Reason
        }
    }
    $payload | ConvertTo-Json -Depth 5 -Compress
    exit 0
}

# --- DENY: catastrophic, never legitimate (checked first) ---
$denyPat = '(?i)\brm\s+-[a-z]*r[a-z]*f[a-z]*\s+(/|~|/\*|\$HOME)(\s|$)'
if ($norm -match $denyPat) {
    Send-Decision 'deny' 'Refusing rm -rf of / or ~. Run it yourself if you truly intend it.'
}

# --- ASK: destructive but recoverable — force a prompt ---
$pats = @(
    '(?i)\brm\s'
    '(?i)\brmdir\s'
    '(?i)\bRemove-Item\b'
    '(?i)\bgit\s+push\b[^|;&]*--force(?!-with-lease)'
    '(?i)\bgit\s+reset\s+--hard\b'
    '(?i)\bgit\s+clean\b[^|;&]*-[a-z]*f'
    '(?i)\bgit\s+add\b'
    '(?i)\bgit\s+commit\b'
    '(?i)\bgit\s+push\b'
)

$reasons = @(
    'file/directory deletion (rm)'
    'directory deletion (rmdir)'
    'file/directory deletion (Remove-Item)'
    'git push --force (rewrites shared history)'
    'git reset --hard (discards uncommitted work)'
    'git clean -f (deletes untracked files)'
    'git add (staging — confirm per governance)'
    'git commit (confirm per governance)'
    'git push (confirm per governance)'
)

for ($i = 0; $i -lt $pats.Length; $i++) {
    if ($norm -match $pats[$i]) {
        Send-Decision 'ask' "Confirm destructive action: $($reasons[$i])."
    }
}

exit 0
