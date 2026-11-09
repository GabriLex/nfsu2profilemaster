# NFSU2 Profiles Master (C# / .NET 8, WinForms)

by GabriLex. Garage editor / tuning / checksum / backup ZIP: funzioni basate su
[NFSU2-Savegame-Editor](https://github.com/hackch/NFSU2-Savegame-Editor) di **hackch** (MIT, vedi `LICENSE-hackch.txt`).

## Compilare
Serve il .NET 8 SDK (https://dotnet.microsoft.com/download) su Windows.

Piccolo (richiede .NET 8 Desktop Runtime sul PC di chi lo usa) - CONSIGLIATO, meno falsi positivi:
    dotnet publish -c Release -r win-x64 --self-contained false -o publish

Autonomo (funziona ovunque, ~150 MB, nessuna compressione):
    dotnet publish -c Release -r win-x64 --self-contained true -o publish

L'exe e' in .\publish\NFSU2ProfileMaster.exe
Icona: copia il tuo file come `icona.ico` accanto al .csproj prima di compilare.

## File slot (ESTRAI SLOT / APPLICA SLOT)
Nel Garage Editor: salva in un .json il record completo (auto, aspetto, tuning, stato slot) degli slot scelti,
e riapplicalo scegliendo per ogni voce lo slot di destinazione (anche slot diversi o 6-10 di un backup ZIP).
Formato: `nfsu2-slot-pack` v1. Non verificato in gioco: tieni un backup.

## Non ancora incluso (dal sito di hackch)
Personalizzazione estetica (vernice, vinili...) e Dyno richiedono di leggere GLOBALB.BUN del gioco.
Le funzioni sperimentali (carriera, scambio slot, auto traffico) sono escluse di proposito.
