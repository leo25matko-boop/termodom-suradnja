[CmdletBinding()]
param([string]$TaskName='prvi-zadatak', [string]$Destination=(Join-Path $env:USERPROFILE 'source/termodom-suradnja'))
$ErrorActionPreference='Stop'
if ($TaskName -notmatch '^[a-z0-9][a-z0-9-]{0,60}$') { throw 'Naziv zadatka: mala slova, znamenke i crtice.' }
$windowsIdentity=[Security.Principal.WindowsIdentity]::GetCurrent()
if (@($windowsIdentity.Groups | ForEach-Object {$_.Value}) -contains 'S-1-5-32-544') { throw 'Pokrenite u standardnoj zaposlenickoj Windows sesiji; filtrirani administratorski racun takodjer nije prikladan.' }
if ($env:GH_TOKEN -or $env:GITHUB_TOKEN -or $env:GIT_ASKPASS -or $env:CODEX_HOME) {
    throw 'Naslijedeni tokeni/putanje za autentifikaciju nisu dopusteni. Otvorite cistu zaposlenicku Windows sesiju.'
}
Get-Command git,gh -ErrorAction Stop | Out-Null
$identity = & gh api user --jq '{login:.login,id:.id}' | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or -not $identity.login) { throw 'Prvo gh auth login --web; prijavite svoj GitHub racun u sluzbenom prozoru.' }
if ($identity.login -eq 'leo25matko-boop') { throw 'Ovo je vlasnikov GitHub racun. Zaposlenik mora koristiti vlastiti racun.' }
$upstream='https://github.com/leo25matko-boop/termodom-suradnja.git'
if (Test-Path -LiteralPath $Destination) { throw 'Odrediste vec postoji; nista nije prepisano. Nastavite u postojecej kopiji.' }
& gh repo fork leo25matko-boop/termodom-suradnja --clone=false --remote=false
if ($LASTEXITCODE -ne 0) { throw 'Izrada/provjera zaposlenikova forka nije uspjela.' }
$fork = & gh api "repos/$($identity.login)/termodom-suradnja" | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or $fork.parent.full_name -ne 'leo25matko-boop/termodom-suradnja') { throw 'Odredisni repozitorij nije fork ovog projekta.' }
& git clone --single-branch --branch mail "https://github.com/$($identity.login)/termodom-suradnja.git" $Destination
if ($LASTEXITCODE -ne 0) { throw 'Kloniranje nije uspjelo.' }
& git -C $Destination remote add upstream $upstream
& git -C $Destination remote set-url --push upstream DISABLED
& git -C $Destination config credential.useHttpPath true
& git -C $Destination config user.name $identity.login
& git -C $Destination config user.email "$($identity.id)+$($identity.login)@users.noreply.github.com"
& git -C $Destination config push.default current
& git -C $Destination switch -c "zaposlenici/$($identity.login)/$TaskName"
if ($LASTEXITCODE -ne 0) { throw 'Grana nije pripremljena.' }
Write-Output "Spremno: $Destination"
Write-Output 'U Codexu se prijavite svojim racunom i otvorite samo tu mapu. Upute: docs/SURADNJA.md.'
