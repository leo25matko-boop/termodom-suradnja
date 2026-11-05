[CmdletBinding()]
param([string]$ProjectPath=(Get-Location).Path,[string]$OfficePolicy='C:\TermoDomOffice\office-policy.json',[string[]]$KnownPrivateRepositories=@())
$ErrorActionPreference='Stop'
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
$administratorSid='S-1-5-32-544'
if (@($identity.Groups | ForEach-Object {$_.Value}) -contains $administratorSid) { throw 'Ovo nije standardni zaposlenicki token (ukljucuje i filtrirane administratorske tokene).' }
if($env:GH_TOKEN -or $env:GITHUB_TOKEN -or $env:CODEX_HOME -or $env:GIT_ASKPASS) { throw 'Naslijedeni token ili vlasnikova konfiguracija mora se razdvojiti.' }
$policy=Get-Content -LiteralPath $OfficePolicy -Raw | ConvertFrom-Json
if($policy.provisionedComputer -ne $env:COMPUTERNAME) { throw 'Ovo nije racunalo navedeno u uredskom planu.' }
if($policy.expectedEmployees -notcontains $env:USERNAME) { throw 'Ova Windows sesija nije planirani zaposlenicki racun.' }
$project=(Resolve-Path -LiteralPath $ProjectPath).Path
$ownRoot="C:\TermoDomOffice\Workspaces\$env:USERNAME\"
if(-not $project.StartsWith($ownRoot,[StringComparison]::OrdinalIgnoreCase)) { throw 'Projekt mora biti u vlastitoj zaposlenickoj radnoj mapi.' }

if(-not ('TermoDomAccessProbe' -as [type])) {
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class TermoDomAccessProbe {
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
  static extern IntPtr CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
  [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
  public static int ReadAccessError(string path) {
    var h=CreateFile(path,0x80000000,7,IntPtr.Zero,3,0x02000000,IntPtr.Zero);
    if(h==new IntPtr(-1))return Marshal.GetLastWin32Error();
    CloseHandle(h);return 0; // No ReadFile call: no private content is read.
  }
}
'@
}
$result=@()
foreach($path in $policy.protectedPaths) {
    $errorCode=[TermoDomAccessProbe]::ReadAccessError($path)
    $result += [pscustomobject]@{check='protected-folder-read';path=$path;win32Error=$errorCode;passed=($errorCode -eq 5)}
    if($errorCode -ne 5) { throw 'Pristup nije dokazano odbijen. Nije procitan sadrzaj; administrator mora provjeriti dozvole.' }
}
foreach($folder in @('C:\TermoDomOffice\Admin','C:\TermoDomOffice\OwnerPrivate')) {
    if([TermoDomAccessProbe]::ReadAccessError($folder) -ne 5) { throw 'Vlasnikova uredska mapa nije dokazano zasticena.' }
}
Push-Location -LiteralPath $project
try {
    $login=& gh api user --jq .login
    if($LASTEXITCODE -ne 0 -or -not $login -or $login -eq $policy.ownerGitHub) { throw 'Potrebna je zaposlenikova vlastita GitHub prijava.' }
    $push=& gh api "repos/$($policy.repository)" --jq .permissions.push
    if($LASTEXITCODE -ne 0 -or $push -ne 'false') { throw 'Ocekivan je read/fork pristup izvorniku, bez izravnog write/admin pristupa.' }
    foreach($repository in $KnownPrivateRepositories) {
        if($repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') { throw 'Neispravan naziv repozitorija.' }
        # HEAD only: no private files or response bodies are downloaded.
        $headers=& gh api --method HEAD --include "repos/$repository" 2>$null
        if(($headers -join "`n") -notmatch 'HTTP/\S+ 404') { throw 'Pristup privatnom repozitoriju nije dokazano odbijen; status treba provjeriti vlasnik.' }
        $result += [pscustomobject]@{check='private-repository-head';passed=$true}
    }
    $origin=& git remote get-url origin
    if($origin -notmatch [regex]::Escape("github.com/$login/termodom-suradnja")) { throw 'Origin mora biti zaposlenikov fork.' }
    $branch=& git branch --show-current
    if($branch -notlike "zaposlenici/$login/*") { throw 'Otvorite vlastitu granu zaposlenici/<login>/<zadatak>.' }
    $probe=Join-Path $project ('.office-write-probe-'+[guid]::NewGuid()+'.tmp')
    try { [IO.File]::WriteAllText($probe,'synthetic workspace probe'); if([IO.File]::ReadAllText($probe) -ne 'synthetic workspace probe') { throw 'Readback failed' } } finally { if(Test-Path -LiteralPath $probe) { Remove-Item -LiteralPath $probe } }
    & (Join-Path $PSScriptRoot 'Dev.ps1') -Action Test
    if($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw 'Provjera projekta nije prosla.' }
    $result += [pscustomobject]@{check='workspace-write-build-core-tests';passed=$true}
    $result += [pscustomobject]@{check='Codex-own-sign-in-sandbox-and-PR';passed=$null;required='Potvrditi u vlastitom Codexu; izmjena, commit, push u fork i stvarni PR uz vlasnikov pregled.'}
    New-Item -ItemType Directory -Path '.evidence' -Force | Out-Null
    $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath '.evidence/employee-verification.json' -Encoding UTF8
    $result | Format-Table -AutoSize
} finally { Pop-Location }
