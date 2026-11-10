# Device pairing smoke test (Unity client flow: shared x-api-key, no JWT).
# Covers: create/claim/cancel on /api/v1/pairing/device/* + /pairing/linked.
$base = 'http://localhost:3000/api/v1'
$ErrorActionPreference = 'Continue'
$results = @()

function Check($name, $cond, $detail = '') {
    $script:results += [PSCustomObject]@{ Test = $name; Result = $(if ($cond) { 'PASS' } else { 'FAIL' }); Detail = $detail }
}

$key = @((Get-Content .env) -match '^BRAIN_API_KEY=')[0] -replace '^BRAIN_API_KEY=', ''
$hk = @{ 'x-api-key' = $key }

# 1. Create device session (shared key)
$s = $null
try {
    $s = Invoke-RestMethod -Method Post "$base/pairing/device/session" -Headers $hk `
        -ContentType 'application/json' -Body (@{ worldKey = 'default' } | ConvertTo-Json) -TimeoutSec 10
    Check 'Device session create' ($s.status -eq 'success' -and $s.data.shortCode.Length -eq 6 -and $s.data.deepLink -like 'bubbleworld://pair*') $s.data.shortCode
} catch { Check 'Device session create' $false $_.Exception.Message }

if (-not $s) { $results | Format-Table; exit 1 }

# 2. Missing key rejected
try {
    Invoke-RestMethod -Method Post "$base/pairing/device/session" -ContentType 'application/json' -Body '{}' -TimeoutSec 10
    Check 'Missing key rejected' $false
} catch { Check 'Missing key rejected' ($_.Exception.Response.StatusCode.value__ -eq 401) }

# 3. Wrong key rejected
try {
    Invoke-RestMethod -Method Post "$base/pairing/device/session" -Headers @{ 'x-api-key' = 'not-the-key' } `
        -ContentType 'application/json' -Body '{}' -TimeoutSec 10
    Check 'Wrong key rejected' $false
} catch { Check 'Wrong key rejected' ($_.Exception.Response.StatusCode.value__ -eq 401) }

# 4. Register a (mobile) account
$email = "pair_$(Get-Random)@test.com"
$token = $null
try {
    $r = Invoke-RestMethod -Method Post "$base/auth/register" -ContentType 'application/json' `
        -Body (@{ email = $email; password = 'Test1234!'; displayName = 'Pairer' } | ConvertTo-Json) -TimeoutSec 10
    $token = $r.data.tokens.accessToken
    Check 'Register claimer' ($r.status -eq 'success') $email
} catch { Check 'Register claimer' $false $_.Exception.Message }

if (-not $token) { $results | Format-Table; exit 1 }
$headers = @{ Authorization = "Bearer $token" }

# 5. Claim the device session by short code -> gets the worldKey
try {
    $c = Invoke-RestMethod -Method Post "$base/pairing/claim" -Headers $headers `
        -ContentType 'application/json' -Body (@{ shortCode = $s.data.shortCode } | ConvertTo-Json) -TimeoutSec 10
    Check 'Claim returns worldKey' ($c.status -eq 'success' -and $c.data.worldKey -eq 'default' -and $c.data.role -eq 'viewer') ($c.data | ConvertTo-Json -Compress)
} catch { Check 'Claim returns worldKey' $false $_.Exception.Message }

# 6. Single use: second claim fails
try {
    Invoke-RestMethod -Method Post "$base/pairing/claim" -Headers $headers `
        -ContentType 'application/json' -Body (@{ shortCode = $s.data.shortCode } | ConvertTo-Json) -TimeoutSec 10
    Check 'Single use (re-claim rejected)' $false
} catch { Check 'Single use (re-claim rejected)' ($_.Exception.Response.StatusCode.value__ -eq 404) }

# 7. Linked worlds list contains the claimed key
try {
    $l = Invoke-RestMethod "$base/pairing/linked" -Headers $headers -TimeoutSec 10
    Check 'Linked worlds lists key' ($l.data.worldKeys -contains 'default') ($l.data.worldKeys -join ',')
} catch { Check 'Linked worlds lists key' $false $_.Exception.Message }

# 8. Cancel a pending session, then double-cancel fails
try {
    $s2 = Invoke-RestMethod -Method Post "$base/pairing/device/session" -Headers $hk `
        -ContentType 'application/json' -Body (@{ worldKey = 'default' } | ConvertTo-Json) -TimeoutSec 10
    Invoke-RestMethod -Method Delete "$base/pairing/device/session/$($s2.data.sessionId)?worldKey=default" `
        -Headers $hk -TimeoutSec 10 | Out-Null
    Check 'Device session cancel' $true $s2.data.sessionId
} catch { Check 'Device session cancel' $false $_.Exception.Message; $s2 = $null }

if ($s2) {
    try {
        Invoke-RestMethod -Method Delete "$base/pairing/device/session/$($s2.data.sessionId)?worldKey=default" `
            -Headers $hk -TimeoutSec 10
        Check 'Double cancel rejected' $false
    } catch { Check 'Double cancel rejected' ($_.Exception.Response.StatusCode.value__ -eq 404) }

    # Cancelled code cannot be claimed
    try {
        Invoke-RestMethod -Method Post "$base/pairing/claim" -Headers $headers `
            -ContentType 'application/json' -Body (@{ shortCode = $s2.data.shortCode } | ConvertTo-Json) -TimeoutSec 10
        Check 'Cancelled code unclaimable' $false
    } catch { Check 'Cancelled code unclaimable' ($_.Exception.Response.StatusCode.value__ -eq 404) }
}

# 9. Cancel with wrong worldKey fails
try {
    $s3 = Invoke-RestMethod -Method Post "$base/pairing/device/session" -Headers $hk `
        -ContentType 'application/json' -Body (@{ worldKey = 'default' } | ConvertTo-Json) -TimeoutSec 10
    Invoke-RestMethod -Method Delete "$base/pairing/device/session/$($s3.data.sessionId)?worldKey=other" `
        -Headers $hk -TimeoutSec 10
    Check 'Wrong worldKey cancel rejected' $false
} catch { Check 'Wrong worldKey cancel rejected' ($_.Exception.Response.StatusCode.value__ -eq 404); $s3 = $null }

$pass = ($results | Where-Object { $_.Result -eq 'PASS' }).Count
Write-Host "`nDevice pairing: $pass/$($results.Count) PASS" -ForegroundColor $(if ($pass -eq $results.Count) { 'Green' } else { 'Red' })
$results | Format-Table -AutoSize
if ($pass -ne $results.Count) { exit 1 }
