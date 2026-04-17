# Firebird DBTool CLI

A .NET 8 command-line tool for working with Firebird 5.0 databases.

It supports creating a new database from SQL scripts, updating an existing database, and exporting metadata into JSON format.

## Features

- `build-db` – creates a new empty database and executes SQL scripts
- `update-db` – updates an existing database based on SQL scripts
- `export-scripts` – exports metadata (`domains`, `tables`, `procedures`) to `schema.json`

## Tech Stack

- .NET 8
- Firebird 5.0
- CLI application
- SQL metadata export

## Requirements

- .NET 8 SDK
- Firebird 5.0 server running (default: `127.0.0.1:3050`)
- IBExpert (optional, for database inspection)

## Configuration

Database credentials are not stored in the repository.

Set the following environment variables before running the tool:

### Required
- `FB_PASSWORD`

### Optional
- `FB_HOST` (default: `127.0.0.1`)
- `FB_PORT` (default: `3050`)
- `FB_USER` (default: `SYSDBA`)

In Visual Studio:
`Project Properties -> Debug -> Environment variables`

## SQL Scripts

SQL scripts used for database creation and updates are located in:

`DBTool/Scripts/Firebird`

## Supported Objects

The tool currently supports:

- domains
- tables (including columns)
- procedures

## Usage

### Build a new database
```bash
dotnet run -- build-db
