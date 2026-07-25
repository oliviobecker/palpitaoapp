<#
.SYNOPSIS
    Scores every seeded round through the real API, in ascending round order.

.DESCRIPTION
    seed-demo-season.cs writes raw rows only. PredictionScores, RoundParticipantResults,
    Absences, Standings and GroupUsers.IsEliminated exist ONLY as output of
    RoundScoringService, reachable solely via POST /rounds/{id}/score. This script
    drives that endpoint.

    Order matters and is not cosmetic. The Flavio rule resolves its target from the
    LIVE Standings table at scoring time, so "the leader before round N" is only
    correct if rounds 1..N-1 have already been scored. Scoring out of order, skipping
    a round, or resuming above an unscored one all fail silently with plausible
    numbers -- so this script refuses to do any of them.

    Scoring is idempotent per round (the service wipes and rewrites that round's
    scores), which is why a retry after a timeout is safe.

.PARAMETER BaseUrl
    API root, no trailing slash, e.g. https://homolog.example.com/api

.PARAMETER FromRound
    Resume point. Every round below it must already be scored (checked).

.PARAMETER ToRound
    Last round to score. 0 (default) means all of them.

.PARAMETER DryRun
    Run all the checks and print the plan, but never POST.

.EXAMPLE
    ./scripts/rehearsal/score-season.ps1 -BaseUrl https://homolog.example.com/api `
        -Email admin@palpitao.local -Password '...'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BaseUrl,
    [Parameter(Mandatory)][string]$Email,
    [Parameter(Mandatory)][string]$Password,
    [string]$GroupSlug,
    [int]$FromRound = 1,
    [int]$ToRound = 0,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')

function Write-Summary([string]$Line) {
    if ($env:GITHUB_STEP_SUMMARY) { Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value $Line }
}

# --- 1. Reachability -------------------------------------------------------
# A wrong base URL should say "cannot reach /health", not produce a baffling 401
# five steps later. /health/db additionally reports pending migrations.
foreach ($probe in 'health', 'health/db') {
    try {
        $r = Invoke-RestMethod -Uri "$BaseUrl/$probe" -Method Get -TimeoutSec 30 -SkipHttpErrorCheck -StatusCodeVariable code
    } catch {
        throw "Cannot reach $BaseUrl/$probe -- check -BaseUrl. ($($_.Exception.Message))"
    }
    if ($code -ne 200) {
        throw "$BaseUrl/$probe returned HTTP $code$(if ($r) { ": $($r | ConvertTo-Json -Compress -Depth 4)" }). Refusing to score."
    }
}
Write-Host "API reachable at $BaseUrl" -ForegroundColor DarkGray

# --- 2. Authenticate -------------------------------------------------------
# The only call under the `auth` rate-limit policy (20/60s per IP); everything
# below is unthrottled, so we log in once and reuse the 12h token.
function Connect-Api {
    $body = @{ email = $Email; password = $Password } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri "$BaseUrl/auth/login" -Method Post -Body $body -ContentType 'application/json' `
        -TimeoutSec 30 -SkipHttpErrorCheck -StatusCodeVariable code
    if ($code -ne 200) { throw "Login failed for '$Email' (HTTP $code): $($res.message)" }
    return $res.token
}

$token = Connect-Api
$groups = Invoke-RestMethod -Uri "$BaseUrl/auth/my-groups" -Method Get -TimeoutSec 30 `
    -Headers @{ Authorization = "Bearer $token" }

$admin = @($groups | Where-Object { $_.role -eq 'GroupAdmin' -and (-not $GroupSlug -or $_.slug -eq $GroupSlug) })
if ($admin.Count -eq 0) {
    throw "'$Email' does not administer any group$(if ($GroupSlug) { " with slug '$GroupSlug'" }). Found: $(($groups | ForEach-Object { "$($_.slug)=$($_.role)" }) -join ', ')"
}
if ($admin.Count -gt 1) {
    throw "'$Email' administers $($admin.Count) groups; pass -GroupSlug to pick one: $(($admin.slug) -join ', ')"
}
$groupId = $admin[0].groupId
$headers = @{ Authorization = "Bearer $token"; 'X-Group-Id' = $groupId; 'Accept-Language' = 'en-US' }
Write-Host "Authenticated as $Email, group $($admin[0].slug) ($groupId)" -ForegroundColor DarkGray

# --- 3. Plan ---------------------------------------------------------------
$all = @(Invoke-RestMethod -Uri "$BaseUrl/rounds" -Method Get -Headers $headers -TimeoutSec 60) |
    Sort-Object number
if ($all.Count -eq 0) { throw "The group has no rounds. Run the seeder first." }

$targets = @($all | Where-Object {
    $_.number -ge $FromRound -and ($ToRound -eq 0 -or $_.number -le $ToRound) -and $_.matchCount -gt 0
})
if ($targets.Count -eq 0) { throw "No rounds with matches in range $FromRound..$(if ($ToRound) { $ToRound } else { 'end' })." }

# Rounds the seeder left empty (the by-hand rehearsal round) are skipped, not an error.
$skipped = @($all | Where-Object { $_.matchCount -eq 0 })
if ($skipped.Count -gt 0) {
    Write-Host "Skipping $($skipped.Count) round(s) with no matches: $(($skipped | ForEach-Object { "R$($_.number)" }) -join ', ')" -ForegroundColor DarkGray
}

# ScoreRoundInternalAsync requires Locked or Scored. Fail before starting rather
# than at round 27 with two thirds of the season already written.
$bad = @($targets | Where-Object { $_.status -notin @('Locked', 'Scored') })
if ($bad.Count -gt 0) {
    throw "These rounds are not Locked/Scored and cannot be scored: $(($bad | ForEach-Object { "R$($_.number)=$($_.status)" }) -join ', ')"
}

# --- 4. Resume guard -------------------------------------------------------
# Resuming above an unscored round leaves a hole in Standings, and
# GetLeadersBeforeRoundAsync would then read a leaderboard missing a prefix --
# wrong results, no error.
if ($FromRound -gt 1) {
    foreach ($r in $all | Where-Object { $_.number -lt $FromRound -and $_.matchCount -gt 0 }) {
        $res = Invoke-RestMethod -Uri "$BaseUrl/rounds/$($r.id)/results" -Method Get -Headers $headers `
            -TimeoutSec 60 -SkipHttpErrorCheck -StatusCodeVariable code
        if ($code -ne 200 -or @($res.participants).Count -eq 0) {
            throw "Refusing to resume at round $FromRound -- round $($r.number) has no results yet. Score from 1, or run the reset-scoring phase first."
        }
    }
    Write-Host "Resume guard: rounds 1..$($FromRound - 1) are already scored." -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "Scoring $($targets.Count) round(s): R$($targets[0].number)..R$($targets[-1].number)$(if ($DryRun) { '  [DRY RUN]' })" -ForegroundColor Cyan
if ($DryRun) { Write-Host "Dry run - nothing was posted."; exit 0 }

Write-Summary "## Rehearsal scoring"
Write-Summary ""
Write-Summary "| Round | Top scorer | Pts | Absent | Flavio |"
Write-Summary "|---|---|---:|---:|---:|"

# --- 5. Score, strictly serial, ascending ----------------------------------
$i = 0
foreach ($round in $targets) {
    $i++
    $label = "[$i/$($targets.Count)] R$($round.number)"
    $attempt = 0
    $result = $null

    while ($true) {
        $attempt++
        $res = Invoke-RestMethod -Uri "$BaseUrl/rounds/$($round.id)/score" -Method Post -Headers $headers `
            -TimeoutSec 180 -SkipHttpErrorCheck -StatusCodeVariable code

        if ($code -eq 200) { $result = $res; break }

        if ($code -eq 401 -and $attempt -eq 1) {
            Write-Host "$label token expired, re-authenticating..." -ForegroundColor DarkYellow
            $token = Connect-Api
            $headers['Authorization'] = "Bearer $token"
            continue
        }

        if ($code -ge 500 -and $attempt -le 3) {
            $wait = [Math]::Pow(2, $attempt)
            Write-Host "$label HTTP $code, retrying in ${wait}s (scoring is idempotent)..." -ForegroundColor DarkYellow
            Start-Sleep -Seconds $wait
            continue
        }

        # 4xx: stop dead. Skipping a round corrupts the leader computation for
        # every round after it.
        $hint = switch ($res.message) {
            { $_ -match 'mustBeLockedToScore' } { 'the round is not Locked/Scored' }
            { $_ -match 'noMatches' }           { 'the round has no matches' }
            { $_ -match 'allResultsRequired' }  { 'some match is missing a result' }
            { $_ -match 'adminOnly|headerMissing' } { 'wrong account or missing X-Group-Id' }
            default { $null }
        }
        throw "$label failed with HTTP $code - $($res.message)$(if ($hint) { " ($hint)" })`n  traceId: $($res.traceId)`n  Nothing after this round was scored."
    }

    $participants = @($result.participants)
    $top = $participants | Sort-Object -Property finalPoints -Descending | Select-Object -First 1
    $absent = @($participants | Where-Object { $_.wasAbsent }).Count
    $flavio = @($participants | Where-Object { $_.flavioRuleApplied })

    $note = if ($flavio.Count -gt 0) { "  FLAVIO: $(($flavio | ForEach-Object { "$($_.name) $($_.grossPoints)->$($_.finalPoints)" }) -join ', ')" } else { '' }
    Write-Host "$label ok - top $($top.name) ($($top.finalPoints) pts), $absent absent$note"
    Write-Summary "| $($round.number) | $($top.name) | $($top.finalPoints) | $absent | $($flavio.Count) |"

    # Not a rate limit (only auth/ocr are throttled) -- just keeps the standings
    # recompute from queueing behind itself.
    Start-Sleep -Milliseconds 200
}

# --- 6. Final standings ----------------------------------------------------
$season = Invoke-RestMethod -Uri "$BaseUrl/seasons/active" -Method Get -Headers $headers -TimeoutSec 30
$standings = @(Invoke-RestMethod -Uri "$BaseUrl/seasons/$($season.id)/standings" -Method Get -Headers $headers -TimeoutSec 60)

Write-Host ""
Write-Host "Final standings - $($season.name)" -ForegroundColor Cyan
$standings | Format-Table -AutoSize @{L='#';E={$_.position}}, @{L='Name';E={$_.name}},
    @{L='Pts';E={$_.totalPoints}}, @{L='Played';E={$_.playedRounds}},
    @{L='Abs';E={$_.absenceCount}}, @{L='Penalty';E={$_.penaltyPoints}},
    @{L='Out';E={if ($_.isEliminated) { 'yes' } else { '' }}} | Out-String | Write-Host

Write-Summary ""
Write-Summary "### Final standings - $($season.name)"
Write-Summary ""
Write-Summary "| # | Name | Pts | Played | Absences | Penalty | Eliminated |"
Write-Summary "|---:|---|---:|---:|---:|---:|---|"
foreach ($s in $standings) {
    Write-Summary "| $($s.position) | $($s.name) | $($s.totalPoints) | $($s.playedRounds) | $($s.absenceCount) | $($s.penaltyPoints) | $(if ($s.isEliminated) { 'yes' } else { '' }) |"
}

Write-Host "Scored $($targets.Count) round(s)." -ForegroundColor Green
