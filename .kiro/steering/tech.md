# Tech Stack

## Platform

- **.NET 8** (`net8.0`), C# with `Nullable` and `ImplicitUsings` enabled across all projects.
- ASP.NET Core Web API (controllers, not minimal APIs).
- **Angular 20** client under `Frontend/requests-web/` (standalone components, Signals, OnPush).

## Libraries

- **Entity Framework Core 8** with the **InMemory** provider (`Microsoft.EntityFrameworkCore.InMemory`). Database name: `CandidateRequests`. No migrations; data is seeded on startup via `DbSeeder` (requests) and `UserSeeder` (default admin).
- **JWT bearer authentication** (`Microsoft.AspNetCore.Authentication.JwtBearer`, `System.IdentityModel.Tokens.Jwt`). Tokens are issued by `JwtTokenService` (Infrastructure) and validated via `TokenValidationParameters` configured in the API host.
- **Serilog** — structured logging to console.
- **Swashbuckle.AspNetCore** (Swagger) — enabled only in the Development environment; includes XML docs from the API and Application assemblies.
- **xUnit** (`xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`) for tests.

## Cross-Cutting Concerns

- `GlobalExceptionHandler` is a last-resort handler mapping unhandled exceptions to HTTP 500 `ProblemDetails` (no internals leaked).
- `[ApiController]` returns an RFC 7807 `ValidationProblemDetails` (HTTP 400) automatically on invalid model state; semantic search-criteria validation is handled by `RequestQueryValidator`.
- CORS policy `Frontend` allows the Angular client origin (read from `Cors:FrontendOrigin`, default `http://localhost:4200`), limited to GET/POST and `Content-Type`/`Authorization` headers.

## Configuration

- `Jwt`: `Issuer`, `Audience`, `SigningKey`, `ExpiryMinutes`.
- `Cors:FrontendOrigin`: allowed browser origin for the client.
- `Seed:Admin`: `Username` / `Password` for the seeded administrator (password stored only as a hash).

## Conventions

- Keep dependency direction inward: `Api → Application → Domain`, `Infrastructure → Application/Domain`. Domain has no outward dependencies.
- Register services through a per-layer `Binder` with `Use*` extension methods: `UseInfrastructure()` in `Requests.Infrastructure/Binder.cs` and `UseApplication()` in `Requests.Application/Binder.cs`, composed in `Program.cs`. Use `AddScoped` for repositories and application services.
- Controllers are thin and derive from `BaseController`; they take identity from the authenticated JWT claims (`CurrentUserId`, `IsAdministrator`), invoke the validator/service, and shape the HTTP response. Protected controllers are marked `[Authorize]`.
- Search criteria are sent in the request body via `POST /api/requests/search` (not the URL).
- Search-criteria validation and parsing is done by `RequestQueryValidator` (collects all errors, returns a validated `RequestQuery`); entity→DTO mapping is an explicit `Select` projection over `IQueryable` in `RequestService` so it stays translatable to a single data-store query.
- Cursors are decoded and validated in `RequestQueryValidator` before the query runs; cursor failures map to a controlled HTTP 400 via `CursorException` (`Malformed` / `SortMismatch`). The keyset seek in `RequestQueryBuilder` additionally guards its own `LastValue` parsing (defense-in-depth) so a bad cursor value can never escape as an unhandled exception — always prefer `TryParse`-and-throw-`CursorException` over a bare `Parse` on cursor-derived values.
- Prefer `sealed` classes for services, repositories, DTOs, and the `DbContext` (`RequestsDbContext`) — nothing in the solution is designed to be inherited.
- Use `record` types for DTOs (immutable).
- All I/O methods are `async` and accept a `CancellationToken` (default `default`).
- Business/authorization rules live in the Application layer (`RequestService`), not in controllers or repositories.
- Repository contracts (`I*Repository`) live in the Domain layer (`Interfaces/`); service contracts (`I*Service`, `ITokenService`) live in the Application layer. Repository implementations and `ITokenService` (`JwtTokenService`) live in Infrastructure; other service implementations live in the Application layer's `Implementations/` subfolder.

## Solution & IDE

- The backend is a standard C# solution. A solution file lives at `Backend/Requests.sln` and includes all four source projects plus the test project.
- Open `Backend/Requests.sln` in Visual Studio and press F5 to run the API (Visual Studio uses `Requests.Api` as the startup project automatically). Swagger is available at `/swagger` in Development.

## Common Commands

Prefer the solution; individual projects can still be targeted directly.

```bash
# Restore & build the whole solution
dotnet build Backend/Requests.sln

# Run the API (Development, Swagger at /swagger)
dotnet run --project Backend/src/Requests.Api/Requests.Api.csproj

# Run tests (single run, avoid watch mode)
dotnet test Backend/tests/Requests.Tests/Requests.Tests.csproj

# Frontend (Angular) — from Frontend/requests-web
npm install
npm start        # dev server at http://localhost:4200
```

Default dev URLs: `https://localhost:44301`, `http://localhost:60702`. The backend runs under IIS Express in Visual Studio; HTTPS uses port `44301` (in the IIS Express SSL-cert range), HTTP stays on `60702`.

Example flow (authenticate, then call a protected endpoint):

```bash
# 1. Log in to obtain a JWT
curl -X POST http://localhost:60702/api/auth/login \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"admin\",\"password\":\"admin\"}"

# 2. Call a protected endpoint with the token
curl -X POST http://localhost:60702/api/requests/search \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <token>" \
  -d "{}"
```
