# Presjek kuće na m.php

Prikaz koristi postojeće odgovore `m.php?action=data` i `m.php?action=pumps`.
Grafikoni, povijest i poveznice na rasporede ostaju dostupni. Prikaz vremena
za Ozalj koristi [Open-Meteo](https://open-meteo.com/en/docs).

## Lokalna aplikacija sa stvarnim očitanjima

Raspakiraj cijelu mapu `web`. Uz Python 3.10 ili noviji dvaput klikni
`Pokreni-Web.cmd` na Windowsu ili pokreni `python run-local.py` iz ove mape.
Preglednik se otvara na [http://127.0.0.1:8740/](http://127.0.0.1:8740/).
Zaustavi aplikaciju s Ctrl+C u prozoru pokretača. Ako je port zauzet, pokreni
`python run-local.py --port 8741`.

Lokalni poslužitelj čita dva postojeća JSON odgovora izvornog `m.php`.
Za dohvat mjerenja i vremena treba internetska veza. Rasporedi, povijest i
grafikoni otvaraju se na izvornoj web adresi; lokalna aplikacija ne šalje
promjene grijanja.

## Demonstracija i ugradnja na hosting

Lokalni primjer s testnim podacima pokreće se iz ove mape naredbom
`python -m http.server 8732 --bind 127.0.0.1`. Otvori
`http://127.0.0.1:8732/preview.html`.

Vlasnik je potvrdio da teme `house/puffer/{top,midd,bottom}` pripadaju
puferu od 2.000 L. Za buduće tri sonde pufera od 1.000 L i mjerenje
toplinske pumpe prikazuje se crtica dok njihov izvor ne bude spojen.

Tipke −, + i postotak upravljaju povećanjem presjeka. Presjek se u uvećanom
prikazu može pomicati povlačenjem. Podržani su pokret s dva prsta, Ctrl uz
kotačić te tipke +, − i 0 kad je fokus na presjeku.

1. U hostingu preuzmi lokalnu kopiju stvarnog `m.php` i sačuvaj je kao izvornik.
2. Pokreni `python patch-m.py putanja/do/izvornog/m.php nova-m.php`. Alat
   izrađuje novu datoteku i provjerava poznate oznake predloška. Ako se izvor
   razlikuje, zadržava izvornik i javlja na kojoj oznaci treba prilagodba.
3. U istu mapu kao `m.php` prenesi `m-house.css`, `m-house.js`,
   `m-house-data.js` i `m-house-cutaway.webp`. Zatim postavi pregledanu
   `nova-m.php` na postojeću putanju `m.php`.
4. Otvori stranicu. Provjeri obje etaže, pet pumpi, vrijeme, grafikone i tablicu.
   Provjeri da oba postojeća GET odgovora i dalje vraćaju JSON. Za povratak
   prenesi sačuvanu izvornu kopiju na `m.php`.
