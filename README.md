# Firebird DBTool CLI

A .NET 8 command-line tool for building, updating, and exporting Firebird 5.0 database schema.

This project was created as a practical CLI tool for working with SQL-based schema definitions and Firebird metadata. It supports creating a new database from scripts, updating an existing database, and exporting schema metadata to JSON.

## Features

- Build a new database from SQL scripts
- Update an existing database
- Export schema metadata to `schema.json`
- Transactional update flow with rollback on failure
- Retry execution for dependent procedures
- Basic schema diff support for missing domains, tables, and columns

## Supported Objects

The tool currently supports:

- domains
- tables
- columns
- procedures

## Tech Stack

- .NET 8
- C#
- Firebird 5.0
- CLI application

## Requirements

- .NET 8 SDK
- Firebird 5.0 server
- `FB_PASSWORD` environment variable

## Configuration

Database credentials are not stored in the repository.

Required environment variable:

- `FB_PASSWORD`

Optional environment variables:

- `FB_HOST`
- `FB_PORT`
- `FB_USER`

In Visual Studio:
`Project Properties -> Debug -> Environment variables`

## SQL Scripts

SQL scripts used for database creation and update are located in:

`DBTool/Scripts/Firebird`

## Commands

### Build a new database

```bash
dotnet run -- build-db --db-dir "<database_directory>" --scripts-dir "<scripts_directory>"
```

### Update an existing database

```bash
dotnet run -- update-db --connection-string "<connection_string>" --scripts-dir "<scripts_directory>"
```

### Export schema metadata

```bash
dotnet run -- export-scripts --connection-string "<connection_string>" --output-dir "<output_directory>"
```

```md
## Export Output

The export command generates `schema.json` with schema metadata for supported objects.
```
## Improvements After Review Feedback

The project was improved after technical review feedback.

Implemented improvements:

- added transactional update flow with rollback support
- added retry handling for dependent procedures
- improved update flow with basic schema diff support
- improved execution safety for build and update scenarios

## Current Limitations

This is not a full migration framework.

The current update flow focuses on practical support for:

- missing domains
- missing tables
- missing columns
- dependent procedures

## Project Goal

The goal of this project is to demonstrate practical backend-oriented problem solving in a .NET CLI application, including:

- database metadata handling
- safe schema updates
- transactional execution
- structured command-line workflows
- iterative improvement based on technical feedback
