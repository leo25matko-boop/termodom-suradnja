[CmdletBinding(SupportsShouldProcess=$true)]
param(
    [Parameter(Mandatory)][string]$ExpectedOfficeComputer,
    [Parameter(Mandatory)][string]$HomeComputer,
    [Parameter(Mandatory)][string]$OwnerAccount,
    [Parameter(Mandatory)][string[]]$EmployeeAccounts,
    [Parameter(Mandatory)][string[]]$ProtectedPaths,
    [switch]$ConfirmOffice,
    [switch]$InstallCodexPolicy
)
$ErrorActionPreference='Stop'
if (-not $ConfirmOffice -or $env:COMPUTERNAME -ne $ExpectedOfficeComputer -or $env:COMPUTERNAME -eq $HomeComputer) {
    throw 'ODBIJENO: potvrdite tocno UREDSKO racunalo. Ova skripta ne smije mijenjati kucno racunalo.'
}
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
$principal=[Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Potrebna je vlasnikova administratorska Windows sesija u uredu.' }
$owner=Get-LocalUser -Name $OwnerAccount -ErrorAction Stop
if ($EmployeeAccounts.Count -eq 0 -or $EmployeeAccounts -contains $OwnerAccount) { throw 'Vlasnik ne smije biti zaposlenik u ovoj konfiguraciji.' }
$admins=@(Get-LocalGroupMember -SID 'S-1-5-32-544' | ForEach-Object { $_.SID.Value })
$employees=@(foreach($name in $EmployeeAccounts) {
    $user=Get-LocalUser -Name $name -ErrorAction Stop
    if (-not $user.Enabled -or $admins -contains $user.SID.Value) { throw "Racun $name mora biti omogucen STANDARDNI korisnik. Uredite ga kroz Windows Settings > Accounts." }
    $user
})
$resolved=@(foreach($path in $ProtectedPaths) {
    $item=Get-Item -LiteralPath $path -Force
    if (-not $item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Zasticena putanja mora biti stvarna mapa, bez junction/symlink preusmjeravanja.' }
    $full=$item.FullName.TrimEnd('\')
    $forbidden=@('C:','C:\Users','C:\Windows','C:\Program Files','C:\Program Files (x86)','C:\ProgramData','C:\TermoDomOffice')
    if ($full -in $forbidden -or $full.Length -le 3 -or $full -like 'C:\Windows\*' -or $full -like 'C:\Program Files*') { throw "Neprikladna zasticena putanja: $full" }
    foreach($user in $employees) { if ($full -eq "C:\Users\$($user.Name)" -or $full.StartsWith("C:\Users\$($user.Name)\",[StringComparison]::OrdinalIgnoreCase)) { throw 'Ne smije se blokirati zaposlenikov vlastiti profil.' } }
    $full
})
$managed=Join-Path $env:ProgramData 'OpenAI/Codex/requirements.toml'
if ($InstallCodexPolicy -and (Test-Path -LiteralPath (Split-Path $managed))) { throw 'Postoji sistemska Codex mapa. Nije prepisana; administrator mora prvo pregledati i spojiti upravljana pravila.' }
$root='C:\TermoDomOffice'
if(Test-Path -LiteralPath $root) { throw 'Uredska mapa vec postoji. Nista nije prepisano; pregledajte postojeci raspored prije ponovnog postavljanja.' }
if ($WhatIfPreference) {
    [pscustomobject]@{Computer=$env:COMPUTERNAME;Employees=$EmployeeAccounts;ProtectedPaths=$resolved;WorkspaceRoot='C:\TermoDomOffice\Workspaces';InstallPolicy=[bool]$InstallCodexPolicy} | ConvertTo-Json -Depth 4
    return
}
if (-not $PSCmdlet.ShouldProcess($env:COMPUTERNAME,'Uredski standardni racuni, NTFS dozvole i opcionalna Codex politika')) { return }
$groupName='TermoDomDevelopers'
$group=Get-LocalGroup -Name $groupName -ErrorAction SilentlyContinue
if (-not $group) { $group=New-LocalGroup -Name $groupName -Description 'TermoDom office contributors; no administrator rights' }
$existing=@(Get-LocalGroupMember -Group $groupName | ForEach-Object { $_.SID.Value })
if ($existing -contains $owner.SID.Value) { throw 'Vlasnik je u zaposlenickoj grupi. Nista se ne smije blokirati dok se to ne razdvoji.' }
$employeeSids=@($employees | ForEach-Object {$_.SID.Value})
if(@($existing | Where-Object {$_ -notin $employeeSids}).Count) { throw 'Postojeca grupa sadrzi druge korisnike. Administrator mora prvo provjeriti grupu; ne mijenjamo njihova prava.' }
foreach($user in $employees) { if ($existing -notcontains $user.SID.Value) { Add-LocalGroupMember -Group $groupName -Member $user.SID } }

function Set-PrivateAcl([string]$Path,[string]$UserSid,[bool]$Modify) {
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
    $acl=[Security.AccessControl.DirectorySecurity]::new()
    $acl.SetAccessRuleProtection($true,$false)
    $inherit=[Security.AccessControl.InheritanceFlags]'ContainerInherit,ObjectInherit'
    foreach($sid in @('S-1-5-18','S-1-5-32-544',$owner.SID.Value)) {
        $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($sid),'FullControl',$inherit,'None','Allow'))
    }
    if($UserSid) { $rights=if($Modify){'Modify'}else{'ReadAndExecute'}; $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($UserSid),$rights,$inherit,'None','Allow')) }
    Set-Acl -LiteralPath $Path -AclObject $acl
}
Set-PrivateAcl $root $group.SID.Value $false
Set-PrivateAcl "$root\Workspaces" $group.SID.Value $false
Set-PrivateAcl "$root\Tools" $group.SID.Value $false
Set-PrivateAcl "$root\OwnerPrivate" '' $false
Set-PrivateAcl "$root\Admin" '' $false
foreach($user in $employees) { Set-PrivateAcl "$root\Workspaces\$($user.Name)" $user.SID.Value $true }

# Back up ACL metadata only. icacls /L affects links themselves, not link targets.
$rollbacks=@()
$index=0
foreach($path in $resolved) {
    $index++
    $backup="$root\Admin\acl-before-$index.txt"
    & icacls.exe $path /save $backup /T /C /L /Q | Out-Null
    if($LASTEXITCODE -ne 0) { throw 'NTFS kopija dozvola nije potpuna. Zastita nije primijenjena na ovu putanju.' }
    $rollbacks+=@{parent=(Split-Path $path -Parent);backup=$backup;path=$path}
    $rollbacks | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$root\Admin\acl-rollback.json" -Encoding UTF8
    & icacls.exe $path /deny "*$($group.SID.Value):(OI)(CI)(F)" /T /C /L /Q | Out-Null
    if($LASTEXITCODE -ne 0) { throw 'Neke dozvole nisu primijenjene. Ne predavati racunalo dok se to ne rijesi i provjeri.' }
}
@{repository='leo25matko-boop/termodom-suradnja';ownerGitHub='leo25matko-boop';protectedPaths=$resolved;expectedEmployees=$EmployeeAccounts;provisionedComputer=$env:COMPUTERNAME} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath "$root\office-policy.json" -Encoding UTF8
if($InstallCodexPolicy) {
    $template=Join-Path $PSScriptRoot '../../config/codex-requirements.example.toml'
    $text=Get-Content -LiteralPath $template -Raw
    foreach($path in $resolved) { if($path.Contains("'")) { throw 'Putanja nije podrzana u TOML predlosku.' } }
    $text+="`r`n[permissions.filesystem]`r`ndeny_read = ["+ (($resolved | ForEach-Object { "'$_'" }) -join ', ')+"]`r`n"
    New-Item -ItemType Directory -Path (Split-Path $managed) -Force | Out-Null
    [IO.File]::WriteAllText($managed,$text,[Text.UTF8Encoding]::new($false))
    # Users may read the policy; only administrators/SYSTEM/owner may edit it.
    Set-PrivateAcl (Split-Path $managed) 'S-1-5-32-545' $false
}
Write-Output 'Pripremljeno. Sada u svakoj zaposlenickoj sesiji provedite Verify-Employee.ps1 i sluzbenu Codex sandbox postavu.'
Write-Output 'Osobne prijave i vjerodajnice nisu kopirane, pregledane ni odjavljene.'
