# TermoDom — suradnja

Izdvojeni izvorni Android i Windows projekt iz razgovora o TermoDomu.
Ovaj repozitorij je **javan**, prema vlasnikovu izboru. Preuzimanje ne zahtijeva
prijavu. Za rad u Codexu i slanje promjena svaki zaposlenik koristi vlastiti
ChatGPT/Codex i GitHub račun. Osobni razgovori se ne dijele Gitom.

**[Kratke upute za kuću i ured](docs/SURADNJA.md)** ·
**[Uredski računi i provjera izolacije](docs/URED-ADMIN.md)** ·
**[Kontekst i početna poruka za zaposlenikov Codex](docs/KONTEKST-ZA-CODEX.md)**

Projekt: `https://github.com/leo25matko-boop/termodom-suradnja`

- Izvornik ostaje pod kontrolom `leo25matko-boop`; glavna grana je `mail`.
- Zaposlenik radi u svojem forku, u grani `zaposlenici/<login>/<zadatak>`.
- Promjene dolaze kroz pull request; vlasnik pregledava i spaja.
- Nema administratorskih ni write ovlasti zaposlenika na izvorniku.
- Windows Debug i Android Debug koriste testne podatke i blokiraju povezivanje
  sa stvarnim uređajima. Produkcijski računi i ključevi nisu potrebni za razvoj.
- Ova kopija nema povijest, grane ili kod drugih projekata iz starog repozitorija.

## Brzo pokretanje na Windowsu

Potrebni su Git i .NET 8 SDK x64 iz službenih izvora. U PowerShellu:

```powershell
git clone --single-branch --branch mail https://github.com/leo25matko-boop/termodom-suradnja.git
cd termodom-suradnja
./scripts/team/Dev.ps1 -Action Test
./scripts/team/Dev.ps1 -Action Run
```

Za zaposlenika koji predaje promjene upotrijebite svoj fork prema uputama.
Obično kloniranje izvornika omogućuje čitanje i lokalni rad, ne izravni push.

Android zahtijeva JDK 17, Android SDK 36 i Build Tools 35.0.0. Otvorite `android/`
u Android Studiju, odaberite Debug ili pokrenite `Dev.ps1 -Action AndroidTest`.
Debug aplikacija ima zaseban paket `.debug` i automatski lokalni razvojni ključ;
vlasnikov potpisni ključ nije dostupan zaposlenicima.

Stari produkcijski instalateri ostaju u [izvornom izdanju 2.0.0](https://github.com/leo25matko-boop/termo/releases/tag/v2.0.0).
Ovaj repozitorij je razvojni; javnost izvora nije jamstvo podrške svakom modelu
kamere. Nativne funkcije i ograničenja opisani su u [NATIVE-2.0.md](NATIVE-2.0.md).
