# How to use ARTR Pien

## Install from source

```powershell
dotnet build ARTR.Pien.sln -c Release
dotnet run --project src/ARTR.Pien.Cli -c Release -- --help
```

## Initialize configuration

```powershell
dotnet run --project src/ARTR.Pien.Cli -c Release -- init --website --force
```

Edit `pien.json` so `authorization.confirmed` is true only for targets you own or are authorized to test.

## Scan a local target

```powershell
dotnet run --project src/ARTR.Pien.Cli -c Release -- scan --target http://127.0.0.1:5088/ --format console,json --output artifacts/pien
```

## Validate / list / explain

```powershell
dotnet run --project src/ARTR.Pien.Cli -c Release -- validate
dotnet run --project src/ARTR.Pien.Cli -c Release -- list-checks
dotnet run --project src/ARTR.Pien.Cli -c Release -- explain PIEN-HTTP-001
```

Exit codes are 0–10 (`PienExitCode`).
