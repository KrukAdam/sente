# DBTool (Sente task)

Narzędzie CLI do pracy z bazą Firebird 5.0:
- `build-db` – tworzy nową pustą bazę i wykonuje skrypty (domains/tables/procedures)
- `update-db` – aktualizuje istniejącą bazę na podstawie skryptów
- `export-scripts` – eksportuje metadane (domains/tables/procedures) do `schema.json`

## Wymagania
- .NET 8 SDK
- Firebird 5.0 (uruchomiony serwer, domyślnie `127.0.0.1:3050`)
- IBExpert (opcjonalnie – do podglądu bazy)

## Konfiguracja (ENV)
Hasło nie jest trzymane w repo. Przed uruchomieniem ustaw zmienne środowiskowe:

- `FB_PASSWORD` (wymagane)
- opcjonalnie:
  - `FB_HOST` (domyślnie `127.0.0.1`)
  - `FB_PORT` (domyślnie `3050`)
  - `FB_USER` (domyślnie `SYSDBA`)

W Visual Studio: **Project Properties → Debug → Environment variables**.

## Skrypty
Skrypty SQL do budowy/update bazy są w:
`DBTool/Scripts/Firebird`

Wspierane elementy (zgodnie z zadaniem):
- domains
- tables (z kolumnami)
- procedures
