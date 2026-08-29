# Envanta

> Modular ERP core — inventory, purchasing and sales. Built with .NET 10.

**Status:** in development. Live demo and screenshots land with PR 6.

Envanta implements the parts of an ERP where correctness actually matters: an append-only stock
ledger, moving-average inventory costing, purchase and sales order state machines, and an
integration surface for legacy clients.

## Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 10 (LTS) |
| UI | Blazor Web App (Server interactive) + Radzen Blazor Components |
| API | ASP.NET Core REST + Swagger, `DevExtreme.AspNet.Data` grid protocol |
| Integration | SoapCore (XML web service) |
| Background | .NET Worker Service, hosted as a Windows Service |
| Data | EF Core + SQL Server 2022 |
| Tests | xUnit, Testcontainers |

## Running locally

```bash
docker compose up -d
dotnet ef database update -p src/Envanta.Infrastructure -s src/Envanta.Web
dotnet run --project src/Envanta.Web
```

## Documentation

- Architecture decisions: [`docs/adr/`](docs/adr/)
- Implementation plans and research: [`thoughts/shared/`](thoughts/shared/)

## License

MIT
