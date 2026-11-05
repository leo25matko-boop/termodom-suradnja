# TermoDom Studio — osobni lokalni Codex klijent

Nativni WPF klijent za rad samo na pregledanoj kopiji TermoDoma. Stvarni službeni
Codex CLI pokreće se kao child proces kroz stdio JSON-RPC. Nije preglednik ChatGPT
stranice i ne otkriva druge razgovore, prijavu, privatne projekte ili memorije.
Ovo je osobni lokalni klijent; nije usluga koja dijeli vlasnikov račun zaposlenicima.

## Početak

1. Pokreni `TermoDom-Studio-Setup-1.0.0-x64.exe` i odaberi **Instaliraj za mene**.
2. Otvori Studio. Ako Windows Sandbox nije uključen, odaberi njegovo uključivanje
   i potvrdi službeni Windows UAC. Spremi rad i ponovno pokreni Windows ako to traži.
   Potrebni su Windows Pro/Enterprise/Education i hardverska virtualizacija.
3. Odaberi **Otvori izolirani Studio**. Otvara se zaseban Microsoft Windows Sandbox
   prozor u kojem radi nativna aplikacija. Taj vanjski prozor je vidljiv dio izolacije.
4. **Prijavi se kroz ChatGPT** otvara službenu OpenAI prijavu u pregledniku unutar
   Sandboxa. Ondje možeš odabrati svoj Google račun. Ne unosi lozinku ili kod u chat.
   Ovdje postoji nova odvojena prijava; postojeća sesija iz host Codexa se ne kopira.
5. Odaberi model, razmišljanje i ponuđenu brzinu. Popis dolazi iz `model/list`;
   dostupnost, ograničenja i potrošnja ovise o računu. Fast nije zajamčen za svaki
   model i može trošiti više limita. Nema zasebnog API ključa.
6. Upiši zadatak, Ctrl+Enter. Agent uređuje projekt i pokreće razvojne naredbe.
   Nove razgovore Studio čuva u svojoj indeksnoj datoteci i zasebnom CODEX_HOME.
7. Za Windows razvoj jednom odaberi **Terminal → Pripremi .NET SDK**. Preuzima
   službeni Microsoft SDK (285 MB), provjerava SHA512 i raspakira ga u podatke
   Sandboxa. Zatim **Windows testovi**, ili `./scripts/team/Dev.ps1 -Action Run`.
   Git i službeni Codex već su u paketu. Android JDK/SDK nisu u instalateru;
   agent ih može pripremiti unutar Sandboxa uz prihvaćanje potrebnih licenci.

## Projektni kontinuitet i Git

Početni paket sadrži pregledane izvorne datoteke ovog projekta i
`docs/KONTEKST-ZA-CODEX.md`; ne sadrži cjelokupni privatni izvorni razgovor,
osobnu solarnu poveznicu, prijave, potpisne ključeve ili build artefakte.
Vlasnik je dodatno odobrio prijenos upravo ovog izvornog projektnog razgovora.
Njegov privatni izvoz vidljivih korisničkih/asistentskih poruka i priloženih slika
pripremljen je zasebno u Studio podacima `Data/Imports/current-chat.json`.
Nije u repozitoriju, seed ZIP-u, Git bundleu ili javnom instalateru. Drugi razgovori,
skriveno rezoniranje, sistemske upute, auth store i tool logovi se ne izvoze.
Studio ga prikazuje kao nastavak i prije prvog zadatka unosi stvarne poruke i slike
u vlastitu Codex povijest preko `thread/inject_items`. Izvorni chat ostaje netaknut;
ovo je privatno prenesena snimka, ne dvosmjerna sinkronizacija s izvornim chatom.
Google/OpenAI prijava sama ne preuzima povijest. Na drugom osobnom računalu treba
zasebno privatno prenijeti taj izvoz; nemoj ga objavljivati na GitHubu niti dijeliti
zaposlenicima. Naknadne poruke iz izvornika nisu automatski u toj snimci.
Prvo pokretanje stvara lokalnu Git granu `studio` iz pregledanog projektnog bundlea.
Zadržava samo izdvojenu Git povijest TermoDoma i izvorni kod Studija. Git konfiguracija
i vjerodajnice s host računala ne ulaze u bundle. Kopija ima `upstream` prema javnom
`termodom-suradnja`, ali nema automatski push,
reset, pull ili produkcijsku objavu. Privatni GitHub pristup s hosta se ne nasljeđuje.

Datoteke u uredu i doma razmjenjuj kroz javni GitHub fork/PR uz svoje službene
GitHub prijave, ne kopiranjem Studijeva Data direktorija. Za objavu agent treba
stvoriti granu, provjeriti diff i poslati PR kroz vlastitu službenu GitHub prijavu
unutar Sandboxa. Ne force-pushati `mail`. Za nastavak postojećeg
Studijeva rada na drugom osobnom računalu najprije predaj izvorne izmjene kroz Git.
Ovaj instalater ne prepisuje već postojeću izoliranu kopiju ni razgovore.

Uređivač odbija putanje izvan projekta i promjenu na disku između učitavanja i
spremanja. Git kartica prikazuje stvarni diff i status; commit sprema lokalno sve
neignorirane datoteke nakon pregleda. Terminal pokreće stvarni PowerShell, prikazuje
izlaz i exit code, te podržava zaustavljanje procesa. Nije ugrađen puni IDE/debugger.

## Granice pristupa

Na hostu je samo pokretač. Radni Codex klijent se ne pokreće u normalnoj host sesiji.
Generirani `.wsb` mapira točno tri mape:

| Mapa | Pristup u Sandboxu |
| --- | --- |
| Instalirana aplikacija i alati | Samo čitanje |
| Zasebna kopija TermoDom projekta | Čitanje i pisanje |
| Zasebni Studio podaci (vlastita nova prijava i razgovori) | Čitanje i pisanje |

Host clipboard, mikrofon, kamera i pisači nisu proslijeđeni. Koristi se Protected
Client. Drugi host projekti, `.codex`, Google profil i privatne mape nisu mapirani.
`Full access` znači puni pristup dostupnom Sandbox okruženju i njegovim mapiranim
podacima. Agent ondje može promijeniti ili izbrisati kod i vlastite Studio podatke.
Ne znači puni pristup host računalu. Standardni način je `on-request` (agent može
zatražiti potvrdu), puni način `never`; oba ostaju u vanjskoj Windows izolaciji.
Sandbox se deklarira i protokolom `externalSandbox`, s omogućenom mrežom.

Mreža je dostupna i uključuje LAN. Ovo nije izolacija od svih mrežnih servisa;
nemoj dodavati privatne mrežne vjerodajnice ili dijeliti host mape putem SMB-a.
Windows Sandbox se oslanja na Microsoftovu virtualizaciju; ovo nije obećanje
otpornosti na svaki OS propust. Windows korisnik i dalje može otvoriti vlastite
datoteke izvan aplikacije. Zasebni CODEX_HOME sam za sebe nije granica prava.

Podaci su u `%LOCALAPPDATA%\TermoDomStudio\Isolated`. NTFS ACL pri stvaranju ostavlja
pristup trenutnom korisniku i SYSTEM-u. Auth je osjetljiv službeni Codex file store;
ne objavljivati Data mapu niti je prenositi zaposlenicima. Administrator sustava može
preuzeti pristup. Odjava je dostupna u Studiju. Sandboxov Google preglednik je
privremen i briše se zatvaranjem, dok zasebna Codex prijava ostaje u Studio podacima.

## Status provjere

Build i protokolske lokalne provjere obuhvaćaju stvarni Codex 0.166.0 initialize,
prazan odvojeni account/read, model/list, command/exec, uvoz sintetičkih poruka i
nastavak uvezenog razgovora kroz stdio. Te provjere
ne pozivaju model, ne prijavljuju vlasnika i nisu dokaz izolacije živog Sandboxa.
Na razvojnom kućnom računalu Sandbox je isključen. Potrebno je završiti njegovu
aktivaciju, službenu prijavu, prvi model zadatak, nastavak razgovora nakon restarta
i praktičnu provjeru nedostupnosti host mapa. Paket do tada treba smatrati
izgrađenim kandidatom za osobno isprobavanje, a ne potpuno provjerenom zamjenom Codexa.
Studio nije potpuna kopija svih vlasničkih funkcija Codex desktop aplikacije:
postojeći konektori, sve ekstenzije, koordinacija drugih chatova, automacije i
ugrađeni browser nisu preneseni samo prijavom. Ugrađen je stvarni Codex agent,
njegovi alati za kod, odabir modela/brzine, potvrde, vlastita povijest, terminal i Git.
Automatski računovi app/plugin konektori su isključeni kako nova prijava ne bi
agentu otvorila pristup drugim privatnim izvorima preko povezanog računa. Git remote
ostaje isti; dopuštenje za push/PR nije automatski preneseno iz host GitHub prijave.
Za zajamčeni pristup samo jednom repozitoriju treba zaseban projektno ograničen
razvojni pristup. Ne prijavljivati široki osobni GitHub token u ovo okruženje.

Službeni izvori:
- https://learn.chatgpt.com/docs/app-server
- https://learn.chatgpt.com/docs/sign-in-with-chatgpt
- https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-configure-using-wsb-file

## Izgradnja

`dotnet build studio/TermoDom.Studio.csproj`

`dotnet run --project studio/tests/Studio.Tests.csproj`

Opcionalna protokolska provjera: dodaj `-- --codex=<apsolutna službena codex.exe putanja>`.
Pokreće se samo s praznim privremenim CODEX_HOME i poznatom echo naredbom.
`--preview` otvara jasno označen pregled bez agenta, prijave, terminala ili spremanja.
`--inside` odbija rad izvan pripremljenog Windows Sandboxa; nije host full-access prečac.

`Build.ps1` pakira pregledani `HEAD` i njegov projektni Git bundle. Prima prethodno
provjereni službeni Codex npm paket 0.166.0-win32-x64, MinGit 2.56.0.4 x64 i JSON
manifest s `.dotnet.url`/`.dotnet.hash` iz Microsoftovih službenih release metapodataka.
Koristi svježu izlaznu mapu. Očuvati sve vendor notice/license datoteke. SDK se ne
ugrađuje u veliki instalater; preuzima se naknadno unutar Sandboxa.
