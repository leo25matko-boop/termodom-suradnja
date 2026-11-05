# Postavljanje drugog, uredskog računala

Ovo nije izvršeno na kućnom računalu. Vlasnik/IT mora biti fizički ili službeno
udaljeno prijavljen na UREDSKO računalo. Koristiti odvojeni administratorski račun
čiju lozinku zaposlenici nemaju. Zaposlenici ne smiju raditi u vlasnikovoj sesiji.

1. Windows Settings → Accounts → Other users: dodati svakom zaposleniku njegov
   račun. Change account type → **Standard User**. Koristiti njihovu vlastitu
   prijavu/Windows Hello ili lokalnu lozinku unesenu u službenom Windows prozoru.
   Skripte ne stvaraju zajedničke lozinke, ne isključuju UAC i ne mijenjaju vlasnikov
   račun. Postojeći standardni zaposlenički računi mogu se ponovno koristiti.
2. Provjeriti naziv računala (`hostname`) i korisnike (`Get-LocalUser`); zapisati
   naziv kućnog računala radi zaštite od slučajnog izvršenja na njemu.
3. Instalirati razvojne alate iz službenih izvora. Primjeri s Windows Package
   Managerom: `winget install --id Git.Git -e`, `GitHub.cli`,
   `Microsoft.DotNet.SDK.8`. Codex instalirati sa službene OpenAI stranice/storea,
   ne kopiranjem vlasnikova korisničkog profila. Android Studio dodati po potrebi.
4. Preuzeti ovu provjerenu kopiju projekta u vlasnikovu/IT mapu. Pregledati skriptu
   prije elevacije. Dodijeljene privatne putanje moraju biti precizne; ne navoditi
   korijen diska, Windows ili Program Files. Napraviti plan bez promjena:

```powershell
# Administrator u Windows PowerShellu x64 na UREDSKOM računalu.
$office = $env:COMPUTERNAME
./scripts/team/Provision-Office.ps1 -ExpectedOfficeComputer $office `
  -HomeComputer 'NAZIV-KUCNOG-PC' -OwnerAccount 'VLASNIK' `
  -EmployeeAccounts 'ana','ivan' -ProtectedPaths 'C:\Users\VLASNIK','D:\Privatno' `
  -ConfirmOffice -InstallCodexPolicy -WhatIf
```

Zamijeniti primjere stvarnim lokalnim računima i postojećim privatnim mapama.
Provjeriti plan, zatim ponoviti istu naredbu bez `-WhatIf`. Ako je sistemska Codex
mapa već prisutna, skripta odbija automatsku instalaciju politike. U tom slučaju
ponoviti bez `-InstallCodexPolicy`, a administrator ručno spaja postojeće i
priložene zahtjeve bez slabljenja postojećih ograničenja.

Skripta priprema `C:\TermoDomOffice`: zaposlenici imaju čitanje zajedničkih alata,
a izmjene samo u vlastitoj mapi `Workspaces\IME`. Vlasnik, SYSTEM i administratori
zadržavaju kontrolu. Grupa zaposlenika dobiva izričitu rekurzivnu zabranu pristupa
navedenim privatnim mapama. ACL metapodaci za povrat spremaju se u zaštićeni `Admin`.
Ne čita sadržaj privatnih datoteka i ne slijedi junction/symlink ciljeve.
Ako icacls prijavi nepotpunu primjenu, računalo još nije spremno za predaju.

Sistemski zahtjevi nalaze se u `%ProgramData%\OpenAI\Codex\requirements.toml`.
Odaberite podržani Codex >= 0.138.0 i provjerite stvarnu primjenu: dopušten je samo
read-only/workspace pristup i on-request odobravanje, bez full access profila.
Običan projektni `.codex/config.toml` sam po sebi nije nepromjenjiva politika.

5. U svakoj zaposleničkoj sesiji zaposlenik se službeno prijavljuje svojim
   ChatGPT/Codex računom. Po potrebi administrator jednom odobri Windows sandbox
   kroz **Set up Agent sandbox**. Codex nakon toga pokreće zaposlenik bez elevacije.
   Noviji službeni klijenti mogu koristiti MXC gdje je dostupan; priložena politika
   dozvoljava i naslijeđeni elevated sandbox, ne slabiji unelevated fallback.
   CLI na kućnom računalu nema podnaredbu `sandbox setup`; ne koristiti napamet
   noviju naredbu na starijem klijentu. Setup kroz službeni prozor ostaje točan put.
6. Zaposlenik slijedi `SURADNJA.md`. Koristiti GitHub fork, bez dodavanja zaposlenika
   administratorima, organizacijskim timovima ili vlasnikovim privatnim repozitorijima.
7. Provesti `Verify-Employee.ps1`, demo pokretanje i stvarni PR iz zaposleničke prijave.

## Važne granice

Codex sandbox ograničava Codexove alate; ne mijenja ovlasti čovjeka koji sam otvori
Explorer, terminal ili drugi program. Tu zaštitu daju standardni Windows račun,
NTFS prava, odvojene prijave i nedostupni vlasnikovi ključevi. To nije kiosk: alati
i obične javne Windows mape ostaju dostupni. Administrator uređaja može promijeniti
dozvole; zaposlenici zato ne smiju imati administratorske vjerodajnice.

Nije dovoljno samo zaključati projekt unutar Codexa dok zaposlenik ostaje u
vlasnikovoj Windows ili ChatGPT sesiji. Ako privatni podaci postoje izvan navedenih
mapa, u zajedničkoj mapi, mrežnom disku ili sinkroniziranom browseru, potrebna je
zasebna provjera tih lokacija. Nemojte kopirati vlasnikove prijave kao prečac.

Povrat NTFS promjene: administrator čita lokalni `Admin\acl-rollback.json` i za
svaku stavku izvršava `icacls PARENT /restore BACKUP /C /L`. Grupu/račune ne brisati
dok postoji njihov rad; uklanjati pristup tek nakon spremanja i predaje izmjena.
