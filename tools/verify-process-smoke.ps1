$ErrorActionPreference = 'Stop'
$projectDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$executablePath = Join-Path $projectDirectory 'Builds/WindowsDevelopment/YourParkCapture.exe'
if (-not (Test-Path -LiteralPath $executablePath)) { throw 'Build the Windows development player first.' }
$resultDirectory = Join-Path $projectDirectory 'TestResults'
New-Item -ItemType Directory -Path $resultDirectory -Force | Out-Null
$testRunId = [Guid]::NewGuid().ToString('N')
foreach ($phase in @('save', 'read-delete', 'check-deleted')) {
    $logPath = Join-Path $resultDirectory "process-$phase.log"
    $arguments = @('-batchmode', '-nographics', '--playerdata-smoke', $phase, $testRunId, '-logFile', "`"$logPath`"")
    $testPlayer = Start-Process -FilePath $executablePath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (-not $testPlayer.WaitForExit(45000)) {
        $testPlayer.Kill()
        throw "Process smoke test timed out: $phase"
    }
    $testPlayer.Refresh()
    $log = Get-Content -LiteralPath $logPath -Raw
    if ($testPlayer.ExitCode -ne 0 -or $log -notmatch "PLAYERDATA_PROCESS_SMOKE_PASS $phase" -or $log -match 'PLAYERDATA_PROCESS_SMOKE_FAIL') {
        throw "Process smoke test failed: $phase; inspect $logPath"
    }
    Write-Output "PASS $phase (process exit $($testPlayer.ExitCode))"
}
Write-Output "Synthetic data only. Run ID: $testRunId"
