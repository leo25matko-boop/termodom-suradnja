# Rad kod kuće i u uredu

## Prijave i granice

Svaki zaposlenik koristi svoj standardni Windows profil, vlastiti ChatGPT/Codex
račun s dostupnim Codexom te vlastiti GitHub račun. Vlasnik nastavlja s vlastitim
računima kod kuće. Nije potrebno dijeliti API ključ, lozinku ili povijest razgovora.
Javni kod može preuzeti svatko; pristup drugim vlasnikovim privatnim repozitorijima
time se ne dodjeljuje. Prijava na GitHub potrebna je za fork, push i pull request.

Ne koristite vlasnikovu Windows sesiju, browser profil, zajednički ChatGPT račun,
Credential Manager, `.codex`, `.ssh`, `.aws`, kopirane cookieje ni tokene. Nemojte
uključiti vlasnikov Chrome sync, OneDrive ili mrežne diskove u zaposlenički profil.
Nema prijenosa ovoga razgovora. Novi Codex razgovor počinje u mapi projekta i
čita `README.md` i `AGENTS.md` kao projektne upute.

## Jednokratno u uredu

1. Vlasnik/IT provodi `URED-ADMIN.md` na uredskom računalu.
2. Zaposlenik se prijavi u svoj Windows profil, bez pokretanja kao administrator.
3. Instalira ili dobije Git, GitHub CLI, .NET 8 SDK i Codex iz službenih izvora.
   Za Android dodaje Android Studio, JDK 17 i SDK 36 / Build Tools 35.0.0.
4. U vlastitoj sesiji pokrene `gh auth login --web --hostname github.com --git-protocol https`
   i prijavi se u službenom prozoru. Zatim `gh auth setup-git`.
5. Na stranici repozitorija klikne **Fork**, ostavi naziv `termodom-suradnja`, zatim
   u vlastitoj radnoj mapi klonira **svoj fork**. Može koristiti priloženu skriptu:

```powershell
# Skriptu preuzeti iz ovog repozitorija ili vlasnikova pripremljenog paketa.
./scripts/team/Setup-Employee.ps1 -TaskName prikaz-kamere `
  -Destination "C:\TermoDomOffice\Workspaces\$env:USERNAME\termodom-suradnja"
```

Skripta ne kopira prijave i odbija vlasnikov GitHub račun. Ako fork već postoji,
zadržava se; ako lokalna mapa postoji, ništa se ne prepisuje. Alternativa ručno:

```powershell
git clone --single-branch --branch mail https://github.com/MOJ_LOGIN/termodom-suradnja.git
cd termodom-suradnja
git remote add upstream https://github.com/leo25matko-boop/termodom-suradnja.git
git remote set-url --push upstream DISABLED
git config credential.useHttpPath true
git switch -c zaposlenici/MOJ_LOGIN/prikaz-kamere
```

U Codexu odabrati **Sign in with ChatGPT**, vlastiti račun i **Open folder** za tu
radnu mapu. Za stariji Windows sandbox vlasnik/IT jednom odobri službeni **Set up
Agent sandbox**; redovni rad ostaje bez administratorskih ovlasti. Projektni
`config.toml` je početna postavka, nije zaštita od korisnika koji ga uređuje.
Sistemski `requirements.toml` i NTFS prava mora primijeniti administrator.

## Uobičajen rad zaposlenika

```powershell
git status
# Ako postoje izmjene, najprije ih commitati u vlastitu granu; ne resetirati ih.
git fetch upstream
git switch -c zaposlenici/MOJ_LOGIN/novi-zadatak upstream/mail
./scripts/team/Dev.ps1 -Action Run
# Zatražiti izmjenu od Codexa, pregledati diff i testirati:
./scripts/team/Dev.ps1 -Action Test
git diff
git add windows/MainWindow.cs # navesti samo namjeravane datoteke
git diff --cached
git commit -m "Opis izmjene"
git push -u origin HEAD
gh pr create --repo leo25matko-boop/termodom-suradnja --base mail `
  --head MOJ_LOGIN:zaposlenici/MOJ_LOGIN/novi-zadatak --web
```

Izbornik u službenom GitHub prozoru omogućuje provjeru opisa i predaju PR-a.
Ne spajati samostalno; vlasnik pregledava. Nakon dorade pushati istu granu.
Za dopunu postojeće grane: najprije commit rada, `git fetch upstream`, zatim
`git merge upstream/mail`. Riješiti konflikte i ponovno testirati. Nema force pusha.

Windows razvojna aplikacija je nativni WPF demo. U njemu ne radi stvarna prijava
kamere, ne učitavaju se osobne postavke elektrane i mutacije regulatora se odbijaju.
Android Debug ima lokalne testne HTML odgovore, demo elektranu i neaktivnu kameru.
Razvojni testovi nemaju vlasnikove produkcijske vjerodajnice. Kod sadrži javnu
adresu izvornog grijanja; razvojni demo je zaštita od slučajne radnje, a ne mrežna
autorizacija. Namjerno izmijenjen kod može kontaktirati javno dostupan servis.
Stvarnu kontrolu pristupa tom servisu mora provoditi njegov poslužitelj/mreža.

## Nastavak kod kuće

Otvoriti `termodom-suradnja`, ne roditeljsku mapu svih Codex projekata. Prvo
jednokratno kloniranje (ako kopija još ne postoji), poslije samo preuzimanje:

```powershell
git clone --single-branch --branch mail https://github.com/leo25matko-boop/termodom-suradnja.git
cd termodom-suradnja
git status
git switch mail
git pull --ff-only origin mail
./scripts/team/Dev.ps1 -Action Test
```

Prije `switch`/`pull` sačuvati postojeći rad u svojoj grani; ne koristiti `reset
--hard` ni `clean`. Vlasnik na GitHubu pregleda Files changed i CI, potvrdi PR i
spoji ga. Zatim oba računala rade `fetch`/`pull` prema gornjem postupku.
Vlasnikov vlastiti PR ne može dobiti vlastito GitHub odobrenje. Kao jedini
administrator može iznimno spojiti svoj pregledani PR; zaposlenici nemaju tu ovlast.

## Završna provjera na uredskom računalu

```powershell
./scripts/team/Verify-Employee.ps1 -ProjectPath (Get-Location).Path `
  -KnownPrivateRepositories 'VLASNIK/POSTOJECI-PRIVATNI-REPO'
```

Ime privatnog repozitorija vlasnik daje lokalno; ne spremati ga u javni repo.
Provjera otvara samo prava pristupa bez čitanja privatnog sadržaja, a GitHub
provjerava samo HTTP HEAD. Za uspjeh je potreban stvarni status 404, a mapa mora
vratiti Windows Access Denied (5); nedostajuća mapa ili mrežna greška nisu prolaz.

Zatim ručno potvrditi vlastitu Codex prijavu, aktivan sandbox i nemogućnost promjene
sistemskih zahtjeva. Zatražiti malu izmjenu, testirati, commitati i poslati stvarni
PR, koji vlasnik pregledava. Ovaj korak zahtijeva zaposleničke prijave; vlasnikova
sesija ili anonimno kloniranje ne mogu dokazati zaposleničke ovlasti.

## Službeni izvori

- https://learn.chatgpt.com/docs/windows/windows-sandbox
- https://learn.chatgpt.com/docs/enterprise/managed-configuration
- https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches
- https://learn.microsoft.com/en-us/windows/security/identity-protection/access-control/access-control
