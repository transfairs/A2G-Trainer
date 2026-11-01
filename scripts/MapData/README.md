# Kartendaten für „Verein → Landkarte“

Erzeugt `Resources/Maps/DEU.map`, `GBR.map`, `FRA.map`, `ITA.map` und `ESP.map`: die Länder
der fünf Ligen mit Nachbarländern und inneren Grenzen, im Pixelraster (440 × 320) der
Landkarten des Dateneditors, in dem auch die Vereinspositionen gespeichert sind.

## Quellen

- [Natural Earth](https://www.naturalearthdata.com/) 10m, Version 5.1.2 (gemeinfrei):
  `ne_10m_admin_0_countries.geojson`, `ne_10m_admin_1_states_provinces.geojson`,
  `ne_10m_populated_places.geojson` aus
  `https://raw.githubusercontent.com/nvkelso/natural-earth-vector/v5.1.2/geojson/`
- Aus dem Spiel (wird nicht weitergegeben): `DATA.DEF/LandXxxx.sav` (Kartenposition jedes
  Vereins), `DATA.DEF/StadXxxx.sav` (Ort des Stadions) und die Landkarten-JPEGs in `Editor.exe`
  (Ressourcentyp `JPEGS`, 318 Spanien, 319 Deutschland, 320 England, 321 Frankreich, 323 Italien).

## Ablauf

1. Die Stadionorte werden unter den Orten von Natural Earth gesucht (alle Namensvarianten,
   ohne Akzente). Eine affine Abbildung von Länge (mal Kosinus der mittleren Breite) und
   Breite auf die Kartenposition wird per Ausgleichsrechnung bestimmt, Ausreißer fallen weg.
2. Mit dieser Abbildung als Start wird sie so verschoben, gedreht und skaliert, dass das Land
   die braune Landfläche der Editorkarte möglichst genau deckt (Nelder-Mead). Die Vereine
   wurden auf dieser Karte platziert, so liegen sie auch im Trainer an derselben Stelle.
3. Ebenen: Nachbarland (Fläche, Grenzen), Land (Fläche), Außengrenze und Küste, Grenzen der
   ersten Ebene (Bundesländer, Regionen Englands, Régions, Regioni, Comunidades) und der
   zweiten Ebene (Counties/Unitary Authorities, Départements, Province, Provincias). Eine
   Kante gehört zur ersten Ebene, wenn auf ihren beiden Seiten verschiedene Regionen liegen.
4. Zuschnitt auf das Bild plus 24 Pixel Rand, Douglas-Peucker mit 0,2 Pixel.

England umfasst wie die Editorkarte nur England; Wales, Schottland und Irland sind Nachbarn.
Frankreich ohne Überseegebiete, Spanien ohne Kanaren.

## Aufruf

```
dotnet run -c Release -- "C:\Spiele\Anstoss 2 Gold" <Ordner mit den GeoJSON-Dateien> ..\..\Resources\Maps [<Vorschau-Ordner>]
```

Mit Vorschau-Ordner entsteht je Land ein PNG: die Karte über der Editorkarte, dazu jeder für
die Abbildung genutzte Ort (rot: Vereinsposition, blau: berechnet).

## Dateiformat

Little Endian: `"A2GM"`, Version (Byte, 1), Breite und Höhe (Int16), Anzahl Ebenen (Byte); je
Ebene Art (Byte: 1 Nachbarland, 2 Nachbargrenze, 3 Land, 4 Außengrenze, 5 erste Ebene,
6 zweite Ebene) und Anzahl Formen (Int32); je Form Anzahl Punkte (UInt16) und Punkte als
Int16-Paare in 1/16 Pixel. Flächen sind geschlossene Ringe, Grenzen offene Linien.
