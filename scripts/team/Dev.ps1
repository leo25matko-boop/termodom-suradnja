[CmdletBinding()]
param([ValidateSet('Build','Test','Run','AndroidTest','AndroidBuild')][string]$Action='Run')
$ErrorActionPreference='Stop'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
Push-Location -LiteralPath $projectRoot
try {
    if ($Action -in @('AndroidTest','AndroidBuild')) {
        if (-not $env:JAVA_HOME -or -not (Test-Path -LiteralPath (Join-Path $env:JAVA_HOME 'bin/java.exe'))) {
            throw 'Postavite JAVA_HOME na JDK 17 (Java 8 nije dovoljna). Upute: docs/SURADNJA.md.'
        }
        Push-Location android
        try {
            $task = if ($Action -eq 'AndroidTest') { ':app:testDebugUnitTest' } else { ':app:assembleDebug' }
            & ./gradlew.bat $task --no-daemon
            if ($LASTEXITCODE -ne 0) { throw 'Android razvojna provjera nije uspjela.' }
        } finally { Pop-Location }
        return
    }
    & dotnet restore windows/TermoDom.Windows.csproj --configfile windows/NuGet.Config
    if ($LASTEXITCODE -ne 0) { throw 'Dohvat razvojnih NuGet paketa nije uspio.' }
    & dotnet build windows/TermoDom.Windows.csproj -c Debug --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Windows razvojna kompilacija nije uspjela. Potreban je .NET 8 SDK x64.' }
    $app = Join-Path $projectRoot 'windows/bin/Debug/net8.0-windows/TermoDom.exe'
    if (-not (Test-Path -LiteralPath $app)) { throw 'Razvojni EXE nedostaje.' }
    if ($Action -eq 'Test') {
        $run = Start-Process -FilePath $app -ArgumentList '--self-test' -WindowStyle Hidden -PassThru
        if (-not $run.WaitForExit(60000)) { $run.Kill(); throw 'Testovi su prekoračili vrijeme.' }
        if ($run.ExitCode -ne 0) { throw 'Nativni testovi nisu prošli.' }
        Get-Content -LiteralPath (Join-Path (Split-Path $app) 'core-tests.txt')
    } elseif ($Action -eq 'Run') {
        # Visible by design: this is the developer's requested interactive app.
        Start-Process -FilePath $app -ArgumentList '--demo' | Out-Null
    }
} finally { Pop-Location }
