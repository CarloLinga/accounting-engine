# Accounting Engine

A headless accounting engine built with **Clean Architecture** (.NET 9 / ASP.NET Core Web API).
It exposes an HTTP API that other applications (Sales Invoicing, Payments, etc.) can call to
automatically generate double-entry accounting postings.

## Solution layout

| Project                    | Responsibility                                                                |
|----------------------------|-------------------------------------------------------------------------------|
| `AccountingEngine.Core`    | Domain entities, enums, and pure business logic (no external dependencies).   |
| `AccountingEngine.Application` | Services, DTOs, interfaces (use cases). Depends only on Core + EF abstractions. |
| `AccountingEngine.Infrastructure` | EF Core persistence (PostgreSQL/Npgsql), mappings, migrations.             |
| `AccountingEngine.Api`     | Controllers, DI wiring, OpenAPI/Scalar docs.                                  |
| `AccountingEngine.Tests`   | xUnit tests: SQLite service tests (no Docker) + Postgres integration tests (Testcontainers). |

## Running the API

1. Start a PostgreSQL instance (e.g. local or via Docker).
2. Configure `ConnectionStrings:DefaultConnection` in `src/AccountingEngine.Api/appsettings.json`
   (this file is gitignored; copy `appsettings.Example.json` as a starting point).
3. Apply the database schema:

   ```bash
   dotnet ef database update --project src/AccountingEngine.Infrastructure --startup-project src/AccountingEngine.Api
   ```

4. Run the API:

   ```bash
   dotnet run --project src/AccountingEngine.Api
   ```

   Open `http://localhost:5255/scalar/v1` for interactive API docs.

## Core concepts

- **Ledger integrity**: every journal entry must balance (total Debits = total Credits), each line
  posts to exactly one side (a DB check constraint enforces Debit XOR Credit), and references are unique.
- **Accounts** are identified by unique codes and typed (`Asset`, `Liability`, `Equity`, `Revenue`, `Expense`).
- **Source Rules** describe how a business event maps to journal lines. An automated rule is a
  template of Debit/Credit lines keyed by `AmountType`; a manual-rule (header-only) acts as a
  validated category for manual postings.

## Integrating an external app (e.g. Sales Invoicing)

Configure the rule once:

```
POST /api/source-rules
{
  "sourceType": "SALES_INVOICE",
  "description": "Sales invoice with VAT",
  "ruleLines": [
    { "accountCode": "130-01", "entryType": "Debit",  "amountType": "TOTAL_AMOUNT", "sequence": 1 },
    { "accountCode": "410-01", "entryType": "Credit", "amountType": "BASE_AMOUNT",  "sequence": 2 },
    { "accountCode": "232-01", "entryType": "Credit", "amountType": "TAX_AMOUNT",   "sequence": 3 }
  ]
}
```

Then post each transaction through the automated path with the amounts the rule expects:

```
POST /api/journals/source
{
  "sourceType": "SALES_INVOICE",
  "reference": "INV-2026-0001",
  "postedAt": "2026-09-09T10:00:00Z",
  "amounts": {
    "BASE_AMOUNT": 100.00,
    "TAX_AMOUNT": 12.00,
    "TOTAL_AMOUNT": 112.00
  }
}
```

The engine validates the rule, resolves the accounts, checks double-entry balance, and persists
the journal entry with its lines.

## Reports

### Trial Balance

```
GET /api/trial-balance?asOf=2026-12-31&fiscalYear=2026&includeInactiveAccounts=false
```

Postings dated **strictly before** the fiscal year start (always Jan 1, 00:00 UTC) are classified as
**opening balances**; postings from the period start through `asOf` are **in-period activity**.
Each account row reports opening/period/closing Debit, Credit and a balance normalized to the
account type's natural side (Asset/Expense → Debit − Credit; Liability/Equity/Revenue → Credit − Debit).
The report also returns `totalDebits`, `totalCredits` and `isBalanced`.

> Tip for imports: post opening-balance entries one minute before the fiscal year
> (e.g. `2025-12-31T23:59:00Z` for FY 2026) so they land in the opening-balance bucket.

## Testing

Service-level tests run against SQLite and do **not** require Docker:

```bash
dotnet test tests/AccountingEngine.Tests --filter "FullyQualifiedName!~AccountServiceTests"
```

Postgres integration tests use Testcontainers (requires Docker) and mirror the same service behaviors
against a real PostgreSQL instance:

```bash
dotnet test tests/AccountingEngine.Tests
```

## API surface

| Method | Route                          | Description                                    |
|--------|--------------------------------|------------------------------------------------|
| POST   | `/api/accounts`                | Create an account (unique code).              |
| GET    | `/api/accounts`                | Search accounts (by code/name).               |
| GET    | `/api/accounts/{code}`         | Get an account by code.                       |
| PUT    | `/api/accounts/{code}`         | Update an account.                            |
| PATCH  | `/api/accounts/{code}/active`  | Activate/deactivate an account.               |
| DELETE | `/api/accounts/{code}`         | Delete an account (blocked if posted to).     |
| GET    | `/api/source-rules`            | List source rules.                            |
| GET    | `/api/source-rules/amount-types` | Amount types for rule lines (used + engine defaults). |
| GET    | `/api/source-rules/{id}`       | Get a source rule by stable Id.               |
| POST   | `/api/source-rules`            | Create a source rule.                         |
| PUT    | `/api/source-rules/{id}`       | Update a source rule by Id (supports renames).|
| PUT    | `/api/source-rules/{sourceType}` | Legacy update by code (prefer PUT by Id).   |
| DELETE | `/api/source-rules/{id}`       | Delete a source rule by Id.                   |
| DELETE | `/api/source-rules/{sourceType}` | Legacy delete by code (prefer DELETE by Id).|
| PATCH  | `/api/source-rules/{sourceType}/status` | Activate/deactivate a rule.          |
| POST   | `/api/journals`                | Post a manual journal entry.                  |
| POST   | `/api/journals/source`         | Post a journal entry from a source rule.      |
| GET    | `/api/journals`                | List journal entries (date/source filters).   |
| GET    | `/api/journals/{id}`           | Get a journal entry by id.                    |
| GET    | `/api/journals/reference/{reference}` | Get a journal entry by reference.       |
| GET    | `/api/trial-balance`           | Trial Balance report for a fiscal year.       |
