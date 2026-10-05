# Project Structure

Clean Architecture, layered by responsibility. The backend is a standard C# solution
(`Backend/Requests.sln`) with source under `Backend/src/` and tests under `Backend/tests/`.
The Angular frontend lives under `Frontend/requests-web/`.

```
CandidateTest/
├── Backend/
│   ├── Requests.sln                    # C# solution — open this in Visual Studio, F5 to run the API
│   ├── src/
│   │   ├── Requests.Domain/            # Core entities, enums & repository contracts, no dependencies
│   │   │   ├── Entities/
│   │   │   │   ├── Request.cs
│   │   │   │   ├── RequestStatus.cs    # enum
│   │   │   │   ├── RequestType.cs      # enum
│   │   │   │   └── User.cs             # auth principal (username, IsAdministrator, PasswordHash)
│   │   │   └── Interfaces/             # IRequestRepository, IUserRepository (repository contracts)
│   │   │
│   │   ├── Requests.Application/       # Use cases, contracts, DTOs, query building, validation
│   │   │   ├── Binder.cs               # UseApplication() DI registration
│   │   │   ├── Requests/
│   │   │   │   ├── Interfaces/         # IRequestService
│   │   │   │   ├── Implementations/    # RequestService (business + authz), RequestQueryBuilder
│   │   │   │   ├── Models/             # RequestDto, RequestQuery, RequestSearchRequest, PagedResult, RequestStats, Sort* ...
│   │   │   │   ├── Pagination/         # Cursor, CursorCodec
│   │   │   │   └── Validation/         # RequestQueryValidator (validates + parses raw criteria)
│   │   │   └── Auth/
│   │   │       ├── Interfaces/         # IAuthService, ITokenService
│   │   │       ├── Implementations/    # AuthService, PasswordHasher
│   │   │       ├── Models/             # AuthModels (Register/Login requests, AuthResult)
│   │   │       └── Exceptions/         # AuthException (expected auth failures → 400/401)
│   │   │
│   │   ├── Requests.Infrastructure/    # EF Core, persistence, JWT issuing, DI wiring
│   │   │   ├── Binder.cs               # UseInfrastructure() DI registration (repos + token issuer)
│   │   │   ├── Persistence/
│   │   │   │   ├── RequestsDbContext.cs
│   │   │   │   └── DbSeeder.cs          # seeds in-memory requests
│   │   │   ├── Repositories/
│   │   │   │   ├── RequestRepository.cs
│   │   │   │   └── UserRepository.cs
│   │   │   └── Auth/
│   │   │       ├── JwtSettings.cs       # bound from the Jwt config section
│   │   │       └── JwtTokenService.cs   # ITokenService impl (issues signed JWTs)
│   │   │
│   │   └── Requests.Api/               # ASP.NET Core host
│   │       ├── Program.cs              # builder, DI, JWT auth, seeding, Serilog, CORS, Swagger, health
│   │       ├── ErrorHandling/GlobalExceptionHandler.cs
│   │       ├── Auth/
│   │       │   └── UserSeeder.cs        # seeds the default admin
│   │       ├── Controllers/
│   │       │   ├── Common/BaseController.cs      # reads identity from JWT claims
│   │       │   ├── Auth/AuthController.cs        # /api/auth/register, /api/auth/login
│   │       │   └── Requests/
│   │       │       ├── RequestSearchController.cs  # POST /api/requests/search
│   │       │       └── RequestStatsController.cs   # POST /api/requests/stats
│   │       └── Properties/launchSettings.json
│   │
│   └── tests/
│       └── Requests.Tests/             # xUnit tests
│
└── Frontend/
    └── requests-web/                   # Angular 20 app (standalone, Signals, OnPush)
```

## Layer Rules

- **Domain**: entities, enums, and repository contracts (`I*Repository` in `Interfaces/`). Never references other projects.
- **Application**: defines service interfaces (`I*Service`, `ITokenService`), DTOs, query building, validation, and services holding business/authorization rules. References Domain only.
- **Infrastructure**: implements the Domain repository interfaces and `ITokenService` (`JwtTokenService`), owns the `DbContext`, seeding, JWT issuing, and DI registration. References Application + Domain.
- **Api**: thin controllers that take identity from JWT claims, call application services, and shape HTTP responses. No business logic. Configures JWT bearer *validation* and seeds the default admin. References Application + Infrastructure.

## Naming & Placement

- One public type per file; file name matches the type.
- Namespaces mirror folder paths (e.g. `Requests.Application.Requests`).
- Group features into a folder per aggregate (e.g. `Requests/`, `Auth/`), each split into `Interfaces/`, `Implementations/`, `Models/`, and feature-specific subfolders (`Validation/`, `Pagination/`). Add new features as sibling folders following the same pattern.
- Controllers are grouped by feature under `Controllers/` (e.g. `Controllers/Auth/`, `Controllers/Requests/`); shared base lives in `Controllers/Common/`.
- Tests mirror the type under test (`RequestService` → `RequestServiceTests`) and use in-memory fakes for repositories.
