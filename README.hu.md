# WetWare Lab – biológiai neurális hálózat szimulátor és oktató munkaállomás

[English](README.md) · **Magyar**

![Hálózat oldal roham közben](docs/screenshots/network-seizure.png)

## Letöltés

A `WetWareLab.exe` a [legfrissebb kiadásból](https://github.com/SandsHUN/WetWareSimulation/releases/latest) tölthető le: egyetlen fájl, telepítés és .NET futtatókörnyezet nélkül fut (Windows 10/11, 64 bit). Mivel nincs digitálisan aláírva, első indításkor a SmartScreen figyelmeztethet: „További információ” → „Futtatás mindenképp”.

Modern, kétnyelvű (magyar / angol) WPF oktatóalkalmazás a biológiai számítás (wetware / neuromorf számítástechnika) szemléltetésére. A specifikáció: [Specifications/wetware_specifications.md](Specifications/wetware_specifications.md).

## Futtatás

```
dotnet run --project src/WetWare.App
dotnet test tests/WetWare.Core.Tests
```

Követelmény: Windows 10/11, .NET 8 SDK (vagy újabb). A `WetWare.slnx` megnyitásához Visual Studio 2022 17.13+ vagy .NET 9.0.200+ SDK kell.

### Önálló EXE készítése

Egyetlen, telepítés nélkül futtatható fájl (a .NET futtatókörnyezetet is tartalmazza, ~76 MB):

```
dotnet publish src/WetWare.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish/win-x64
```

Eredmény: `publish/win-x64/WetWareLab.exe` – bármely 64 bites Windows 10/11 gépre átmásolható.

## Modulok

| Oldal | Tartalom |
|---|---|
| Kezdőlap | Áttekintés, élő háttérhálózat, modulkártyák |
| Neuron labor | LIF neuron elektródával, oszcilloszkóp, élő egyenlet (hover-magyarázatokkal), fázissík, f–I görbe, lépésenkénti Bio-Debugger |
| Hálózat | 100 neuronos kisvilág-hálózat: raster plot, populációs ráta, hálózati gráf (kalcium-képalkotás-szerű felvillanások, terjedő tüskék), STDP súlymátrix, súlyeloszlás, metrikák (szinkronizációs index, átlagos tüzelés, erős szinapszisok), stimuláció- és lézeres abláció eszköz, matematikai labor, „Mi történik most?” narrátor |
| STDP labor | Bi & Poo párosítási protokoll, tanulási ablak, súlytörténet, animált pre/post idővonal |
| Neurális kódolás | Frekvencia-, latencia-, fázis- és populációs kód ugyanarra az ingerre |
| Látás | Retina (ON/OFF centrum–környék, DoG) → V1 (Gabor-orientáció), illúziók (Hermann-rács, Mach-sávok, szimultán kontraszt), rajzolás, képbetöltés, tüskeképzés |
| Felismerés | Felügyelet nélkül, STDP-vel tanuló tüskés WTA hálózat; kialakuló receptív mezők, animált felismerés |
| Kísérletek | Pavlovi kondicionálás, roham, homeosztázis, információkódolás |
| Fogalomtár | 38 kereshető fogalom, kétnyelvűen |

Minden oldalon: tooltipek, „Egyszerűen” (laikusoknak szóló hasonlatok) és „Útmutató” (javasolt kísérletek) panel; futás közbeni HU/EN nyelvváltás.

## Architektúra

- `src/WetWare.Core` – platformfüggetlen szimulációs mag (.NET 8): LIF neuron, kisvilág-hálózat késleltetett szinapszisokkal, nyomkövetéses STDP, Tsodyks–Markram rövid távú depresszió, Poisson-zaj, metrikák, forgatókönyvek, kódolás, látás, felismerés.
- `src/WetWare.App` – WPF kliens (MVVM, CommunityToolkit.Mvvm, ScottPlot 5, saját nagy teljesítményű vezérlők).
- `tests/WetWare.Core.Tests` – xUnit tesztek (analitikus tüzelési frekvencia, STDP, Pavlov-tanulás, roham-szinkronizáció, látás, felismerés).

**Eltérés a specifikációtól:** a Python/Brian 2 + ZeroMQ motor helyett az azonos egyenleteket egy beépített C# motor számolja (dt = 0,1 ms, Euler). Így az app telepítés nélkül, egyetlen programként fut; a képkockánkénti adatátadás (FrameClock) a PUB/SUB telemetria szerepét tölti be.

## Lokalizáció

A szövegek a `src/WetWare.App/Localization/Strings/hu.json` és `en.json` fájlokban vannak (azonos kulcsokkal). XAML-ben: `{loc:Tr Kulcs}`.

## Fejlesztői kapcsolók

`WetWareLab.exe --lang en --page Network --scenario Pavlov --snapshot out.png --delay 5000` – képernyőkép készítése automatizált vizuális ellenőrzéshez. Oldalkulcsok: `Home`, `Neuron`, `Network`, `Stdp`, `Coding`, `Vision`, `Recognition`, `Scenarios`, `Glossary`.


## Hivatkozások

A modellek forrásait lásd az [angol README](README.md#references) végén.

## Licenc

[MIT](LICENSE) © SandsHUN
