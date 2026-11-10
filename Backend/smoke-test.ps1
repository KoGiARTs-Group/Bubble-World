# BubbleWorld Backend smoke test
$base = 'http://localhost:3000/api/v1'
$ErrorActionPreference = 'Continue'
$results = @()

function Check($name, $cond, $detail = '') {
    $script:results += [PSCustomObject]@{ Test = $name; Result = $(if ($cond) { 'PASS' } else { 'FAIL' }); Detail = $detail }
}

# 1. Health
try {
    $h = Invoke-RestMethod "http://localhost:3000/health" -TimeoutSec 5
    Check 'Health endpoint' ($h.status -eq 'ok' -and $h.database -eq 'ok')
} catch { Check 'Health endpoint' $false $_.Exception.Message }

# 2. Register
$email = "smoke_$(Get-Random)@test.com"
try {
    $r = Invoke-RestMethod -Method Post "$base/auth/register" -ContentType 'application/json' `
        -Body (@{ email = $email; password = 'Test1234!'; displayName = 'SmokeTester' } | ConvertTo-Json) -TimeoutSec 10
    Check 'Register' ($r.status -eq 'success') $email
    $token = $r.data.tokens.accessToken
} catch { Check 'Register' $false $_.Exception.Message }

if (-not $token) { $results | Format-Table; exit 1 }
$headers = @{ Authorization = "Bearer $token" }

# 3. Create world
try {
    $w = Invoke-RestMethod -Method Post "$base/worlds" -Headers $headers -ContentType 'application/json' `
        -Body (@{ name = 'SmokeWorld'; environmentType = 'Forest' } | ConvertTo-Json) -TimeoutSec 10
    Check 'Create world' ($w.status -eq 'success')
    $worldId = $w.data.world.id
} catch { Check 'Create world' $false $_.Exception.Message }

if (-not $worldId) { $results | Format-Table; exit 1 }

# 4. GET world state (fallback build from DB)
try {
    $s = Invoke-RestMethod "$base/worlds/$worldId/state" -Headers $headers -TimeoutSec 10
    Check 'GET state (DB fallback)' ($s.status -eq 'success')
} catch { Check 'GET state (DB fallback)' $false $_.Exception.Message }

# 5. PUT world state
$state = @{
    saveVersion = '2.1'
    worldName = 'SmokeWorld'
    environmentType = 'Forest'
    cells = @{}
    soulform = @{ name = 'SmokeSoul' }
    dayCount = 1
    currentSeason = 0
    architectPoints = 100
    weatherCondition = 'Clear'
    tileCompositions = @{}
    tileStates = @{}
    entities = @()
    bubbles = @()
} | ConvertTo-Json -Depth 6
try {
    $p = Invoke-RestMethod -Method Put "$base/worlds/$worldId/state" -Headers $headers -ContentType 'application/json' -Body $state -TimeoutSec 10
    Check 'PUT state' ($p.status -eq 'success')
} catch { Check 'PUT state' $false $_.Exception.Message }

# 6. Entity spawn
try {
    $e = Invoke-RestMethod -Method Post "$base/worlds/$worldId/entities" -Headers $headers -ContentType 'application/json' `
        -Body (@{ entityType = 'Animal'; species = 'Deer'; gridX = 10; gridY = 0; gridZ = 10 } | ConvertTo-Json) -TimeoutSec 10
    Check 'Spawn entity' ($e.status -eq 'success')
    $entityId = $e.data.entity.id
} catch { Check 'Spawn entity' $false $_.Exception.Message }

# 7. Entity list + move + delete
if ($entityId) {
    try {
        $list = Invoke-RestMethod "$base/worlds/$worldId/entities" -Headers $headers -TimeoutSec 10
        Check 'List entities' ($list.status -eq 'success' -and $list.data.entities.Count -ge 1)
    } catch { Check 'List entities' $false $_.Exception.Message }
    try {
        $mv = Invoke-RestMethod -Method Patch "$base/worlds/$worldId/entities/$entityId" -Headers $headers -ContentType 'application/json' `
            -Body (@{ gridX = 11; gridZ = 9 } | ConvertTo-Json) -TimeoutSec 10
        Check 'Move entity' ($mv.status -eq 'success')
    } catch { Check 'Move entity' $false $_.Exception.Message }
    try {
        $dl = Invoke-RestMethod -Method Delete "$base/worlds/$worldId/entities/$entityId" -Headers $headers -TimeoutSec 10
        Check 'Delete entity' ($dl.status -eq 'success')
    } catch { Check 'Delete entity' $false $_.Exception.Message }
}

# 8. Simulation tick
try {
    $t = Invoke-RestMethod -Method Post "$base/worlds/$worldId/tick" -Headers $headers -ContentType 'application/json' `
        -Body (@{ ticks = 1 } | ConvertTo-Json) -TimeoutSec 15
    Check 'Simulation tick' ($t.status -eq 'success')
} catch { Check 'Simulation tick' $false $_.Exception.Message }

# 9. Soulform brain (mock mode, no Bedrock keys)
try {
    $b = Invoke-RestMethod -Method Post "$base/game-data/$worldId/soulform/brain" -Headers $headers -ContentType 'application/json' `
        -Body (@{ mode = 'chat'; playerMessage = 'Hello there!' } | ConvertTo-Json) -TimeoutSec 20
    Check 'Soulform brain chat' ($b.status -eq 'success' -and $b.data.brainResponse.currentThought)
    $thought = $b.data.brainResponse.currentThought
} catch { Check 'Soulform brain chat' $false $_.Exception.Message; $thought = '' }

# 10. Companion chat (AP charged)
try {
    $c = Invoke-RestMethod -Method Post "$base/companion/$worldId/chat" -Headers $headers -ContentType 'application/json' `
        -Body (@{ text = 'How are you doing?' } | ConvertTo-Json) -TimeoutSec 20
    Check 'Companion chat' ($c.status -eq 'success' -and $c.data.soulformMessage.text)
} catch { Check 'Companion chat' $false $_.Exception.Message }

# 11. Cross-user auth check (register second user, expect 403 on world)
$email2 = "smoke2_$(Get-Random)@test.com"
try {
    $r2 = Invoke-RestMethod -Method Post "$base/auth/register" -ContentType 'application/json' `
        -Body (@{ email = $email2; password = 'Test1234!'; displayName = 'Intruder' } | ConvertTo-Json) -TimeoutSec 10
    $h2 = @{ Authorization = "Bearer $($r2.data.tokens.accessToken)" }
    $denied = $false
    try {
        Invoke-RestMethod "$base/worlds/$worldId/state" -Headers $h2 -TimeoutSec 10 | Out-Null
    } catch {
        $denied = ($_.Exception.Response.StatusCode.value__ -eq 403 -or $_.Exception.Response.StatusCode.value__ -eq 404)
    }
    Check 'Cross-player access denied' $denied
} catch { Check 'Cross-player access denied' $false $_.Exception.Message }

# 12. Unauthenticated request rejected
try {
    $unauth = $false
    try { Invoke-RestMethod "$base/worlds" -TimeoutSec 5 | Out-Null }
    catch { $unauth = ($_.Exception.Response.StatusCode.value__ -eq 401) }
    Check 'Unauthenticated rejected' $unauth
} catch { Check 'Unauthenticated rejected' $false $_.Exception.Message }

Write-Host ''
$results | Format-Table -AutoSize
$failed = @($results | Where-Object Result -eq 'FAIL').Count
Write-Host "TOTAL: $($results.Count) | PASS: $($results.Count - $failed) | FAIL: $failed"
if ($thought) { Write-Host "Brain said: $thought" }
