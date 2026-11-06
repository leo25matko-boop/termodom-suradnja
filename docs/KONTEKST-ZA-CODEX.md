# Kontekst aplikacije za zajednički rad u Codexu

Ovo su zajedničke projektne upute za zasebne zaposleničke Codex račune. Nisu izvoz
vlasnikova razgovora ili prijave. Aktualno stanje uvijek provjeriti u Git kodu,
pull requestovima i testovima; osobna memorija drugog računa nije prenesena.

## Što radimo

TermoDom je nativna Android (Java Views/Canvas) i Windows (WPF/.NET 8) aplikacija
za grijanje, prikaz elektrane i podržane Xiaomi Mi 360 kamere. Dizajn je miran,
taman, moderan, s tirkiznim i jantarnim naglascima. Nema WebView/Electron sučelja,
emulatora, izmišljene žive slike ni lažno uspješnog spremanja.

Grijanje zadržava izvorne temperature i statuse, dvije etaže, po 24 satna klizača
rasporeda, šest postavki, deset izvora grafikona i CSV. Spremanje u produkciji
zahtijeva korisničku potvrdu i neovisno ponovno čitanje. Mutirajući zahtjevi se
ne ponavljaju automatski. Neispravan ili nedostupan izvor prikazuje grešku.

FusionSolar koristi korisnikovu lokalno unesenu dijeljenu kiosk poveznicu.
Osobni token nije u ovom repozitoriju. Prikaz je čitanje proizvodnje i grafikona;
upravljanje inverterom, nedostupna potrošnja i baterija ostaju u pripremi.

Xiaomi koristi postojeći Xiaomi račun, regiju Europa gdje je kamera registrirana,
šifriranu lokalnu sesiju i nativni video. Podrška ovisi o stvarnom modelu i mreži.
Kontrole dolaze iz modelove MIoT specifikacije i aktiviraju se samo nakon uspješnog
čitanja. PTZ je ograničen na provjerene protokole/modela, ne pretpostavlja se za
svaku Mi 360. Vlasnikova konkretna fizička kamera nije bila dostupna za provjeru.
Dvosmjerni razgovor i reprodukcija SD/cloud arhive još nisu implementirani.
Detalji: `NATIVE-2.0.md` i `scripts/CAMERA-ENGINE.md`.

## Sigurno razvojno okruženje

- Windows Debug automatski koristi lokalne fixture odgovore; `--demo` to omogućuje
  i u Release buildu. Nema učitavanja vlasnikove spremljene elektrane ili prijave.
- Android Debug ima zaseban `.debug` paket, lokalne HTML fixture podatke, testnu
  elektranu i neaktivne kontrole kamere. Produkcijski ključevi nisu potrebni.
- Razvojni način odbija mutacije regulatora. Ponašanje spremanja testirati mock
  transportom u unit testovima, bez naredbi stvarnim uređajima.
- Produkcijski Release zadržava izvorno povezivanje. Privatne račune i potpisne
  ključeve za stvarne instalatere unosi samo vlasnik u svojoj sesiji.

## Kako zaposlenik radi

Otvori samo klon ovog repozitorija, pročitaj `AGENTS.md`, radi u svojoj grani u
vlastitom forku, testiraj, pregledaj diff i pošalji pull request na `mail`.
Vlasnik pregledava prije spajanja. Nema administratorskog pristupa izvorniku ili
vlasnikovim drugim privatnim repozitorijima. Računi i razgovori ostaju odvojeni.

Primjer prve poruke u vlastitom Codex razgovoru:

> Nastavljamo razvoj aplikacije TermoDom iz ovog repozitorija. Pročitaj AGENTS.md,
> docs/KONTEKST-ZA-CODEX.md i docs/SURADNJA.md, pregledaj Git status i sačuvaj
> postojeće izmjene. Koristi razvojni demo, bez stvarnih računa ili naredbi
> grijanju/kameri. Moj zadatak je: [opiši izmjenu]. Implementiraj i testiraj,
> pa pripremi commit i pull request vlasniku. Ne spajaj promjene samostalno.

Nakon svakog dovršenog zadatka opis i provjeru spremiti u PR. Ako se promijene
trajne odluke, ažurirati ovaj dokument u istom PR-u. Tako ih vlasnikov i svaki
zaposlenički Codex mogu pročitati bez pristupa privatnim razgovorima.

## Što radi dijeljenje razgovora

Codexov Share je nepromjenjiva snimka za čitanje, ne zajednički uređivi izvorni
chat i ne prijenos lokalne radne mape. Primatelj može u podržanom klijentu priložiti
snimku kao kontekst novom razgovoru. Osobni share link može otvoriti svatko tko ga
dobije; prije dijeljenja treba pregledati poruke, slike, putanje i poveznice.
Ovaj projektni dokument pripremljen je umjesto javnog izvoza izvorne privatne
povijesti. Ne sadrži osobnu kiosk poveznicu, lozinke ni tokene.

Izmjene Codexa ulaze u **kod ove aplikacije** kroz fork i PR. Nakon spajanja oba
računala povlače isti kod. Već instalirani EXE/APK ne mijenja se samim razgovorom
ili Git commitom: vlasnik mora izgraditi, provjeriti i objaviti novu instalaciju.
Automatska ZIP provjera postojećeg Windows izdanja čita originalni repozitorij
`leo25matko-boop/termo`; objava instalatera ostaje zaseban vlasnikov korak.

Službena dokumentacija:
https://learn.chatgpt.com/docs/use-chatgpt#share-a-read-only-snapshot-of-a-codex-thread
https://learn.chatgpt.com/docs/projects

## Osobni TermoDom Studio

`studio/` sadrži novi nativni WPF osobni klijent službenog Codex app-servera.
Koristi zaseban Windows Sandbox, samo pregledani projekt i vlastiti CODEX_HOME.
Vlasnik se prijavljuje kroz službeni OpenAI prozor unutar tog Sandboxa; prethodna
ChatGPT/Codex povijest drugih projekata i host prijave nisu preneseni. Vlasnik je
naknadno zatražio prijenos samo ovog izvornog projektnog razgovora: njegov privatni
izvoz ostaje u Studio podacima izvan javnog repozitorija i instalatera. Modeli,
razmišljanje i brzine dolaze iz stvarnog model/list odgovora. Full access vrijedi
samo unutar vanjske Windows izolacije. Detalji, upute i točna ograničenja provjere
su u `studio/README.md`. Ovo nije način dijeljenja vlasnikova računa zaposlenicima.
Studio 1.1 dodaje projektne i Git radnje, pregled koda u istom razgovoru, sažimanje,
pretragu i privatni izvoz, pohranu izbora modela, skills/MCP postavke te rezervnu
službenu prijavu kodom. GitHub PR i novi repozitorij otvaraju službene obrasce;
ovlasti za GitHub i dodatne MCP usluge zahtijevaju zasebno korisničko povezivanje.
Sve funkcije službenog Codex desktop klijenta nisu prenesene. Izolacija i
ograničenje na jedan projekt/razgovor ostaju isti.
