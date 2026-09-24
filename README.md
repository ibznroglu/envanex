# Envanex

> Modular ERP core — inventory, purchasing and sales. Built with .NET 10.

**Status:** in development. Live demo and screenshots land with PR 7.

Envanex implements the parts of an ERP where correctness actually matters: an append-only stock
ledger, moving-average inventory costing, purchase and sales order state machines, and an
integration surface for legacy clients.

## Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 (LTS) |
| UI | Blazor Web App (Server interactive) + Radzen Blazor Components |
| API | ASP.NET Core REST + OpenAPI + Scalar, `DevExtreme.AspNet.Data` grid protocol |
| Integration | SoapCore (XML web service) |
| Background | .NET Worker Service, hosted as a Windows Service |
| Data | EF Core + SQL Server 2022 |
| Tests | xUnit, Testcontainers |

## Running locally

Prerequisites: Docker Desktop and the .NET 10 SDK. The commands are for Git Bash.

```bash
cp .env.example .env    # then set MSSQL_SA_PASSWORD in .env; the file explains the rules
docker compose up -d    # SQL Server 2022, bound to 127.0.0.1:1433 only

# Use 127.0.0.1, not localhost: the port is bound to IPv4 loopback, and Windows resolves localhost to ::1 first.
CS="Server=127.0.0.1,1433;Database=EnvanexDev;User Id=sa;Password=<the password from .env>;TrustServerCertificate=True"
dotnet user-secrets set "ConnectionStrings:EnvanexDb" "$CS" --project src/Envanex.Web
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 64)" --project src/Envanex.Web

export ENVANEX_CONNECTION_STRING="$CS"   # the dotnet ef commands read this, not user-secrets
dotnet ef database update -p src/Envanex.Infrastructure -s src/Envanex.Web --context EnvanexDbContext
dotnet ef database update -p src/Envanex.Infrastructure -s src/Envanex.Web --context EnvanexIdentityDbContext

dotnet run --project src/Envanex.Web --launch-profile http
```

The read-only demo account is off by default. To try it locally:
1. In user-secrets, set `Demo:Enabled` to `true`.
2. Set a `Demo:Password` that meets the Identity password policy. The app refuses to start otherwise.
3. Sign in as `demo@envanex.local`.

## Documentation

- Architecture decisions: [`docs/adr/`](docs/adr/)
- Implementation plans and research: [`thoughts/shared/`](thoughts/shared/)

## License

MIT
