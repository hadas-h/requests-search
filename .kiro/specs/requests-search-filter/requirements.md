# Requirements Document

## Introduction

This document defines the requirements for the **Requests Search and Filtering** feature — an extension of the existing Requests list that adds search, filtering, sorting, and pagination across both the server (API) and the client (user interface).

The system exposes Requests search through `POST /api/requests/search` (criteria in the request body), authenticates callers with JWT bearer tokens issued by an auth endpoint, and enforces authorization in the Application layer using the identity carried by the token. The current implementation loads every record into memory (`GetAllAsync`) and filters in memory — an approach that does not scale to millions of records. This feature replaces that approach with a single query executed at the database level, while preserving the existing technology stack (.NET 8 / ASP.NET Core Web API / EF Core / Clean Architecture).

**Scope:**

- **Backend** — search by Request Number (partial match), filter by Status (single or multiple), creation date range, and Request Type; sorting; keyset-based pagination; invalid-input handling; and authorization enforced as part of the query. Filtering, authorization, sorting, and pagination are composed into a single `IQueryable` executed at the database level.
- **Frontend** — an Angular 20 app with two tabs: a search screen (filter form, sorting controls, results table, and loading / error / empty-state indicators) and a dashboard overview (charts, KPIs, progress, with drill-down into the search tab). The implementation uses standalone components, Signals, the new control flow (`@if`/`@for`/`@switch`), and OnPush change detection. Public open-source libraries are permitted (PrimeNG for UI components, Chart.js for charts, @ngx-translate for i18n); state is managed with Signals, without a dedicated state-management library. The UI ships in English (LTR), with all strings sourced from a translation file.

**Project structure:** two top-level folders — `Backend/` (server code) and `Frontend/` (the Angular application). The backend is a standard C# solution: a solution file (`Backend/Requests.sln`) groups all backend projects and the test project so the solution can be opened and run directly from Visual Studio.

This document covers the search and filtering feature only. The microservices architecture design and supporting documentation are handled as separate deliverables.

## Glossary

- **Request**: The request entity, with fields `Id (int)`, `RequestNumber (string)`, `CustomerId (int)`, `OwnerId (int)`, `AssignedToUserId (int?)`, `Status (RequestStatus)`, `RequestType (RequestType)`, `CreatedAt (DateTime)`, `UpdatedAt (DateTime)`.
- **RequestStatus**: Enumeration with values `New`, `InProgress`, `Completed`, `Cancelled`.
- **RequestType**: Enumeration with values `General`, `Legal`, `Payment`, `Appeal`.
- **Search_API**: The `POST /api/requests/search` endpoint, accepting search, filter, sort, and pagination criteria in the request body.
- **Request_Service**: The Application-layer service that holds the search, sorting, and authorization logic.
- **Request_Repository**: The Infrastructure-layer component exposing `IQueryable<Request>` for building a query at the database level.
- **Caller**: The party calling the Search_API. Its identity is carried by a signed JWT sent as `Authorization: Bearer <token>`; the token encodes the caller's user id (subject claim) and administrator role (`is_admin` claim).
- **Regular_User**: A Caller whose token `is_admin` claim is not `true`.
- **Administrator**: A Caller whose token `is_admin` claim is `true`.
- **Auth_API**: The `POST /api/auth/register` and `POST /api/auth/login` endpoints that create an account or sign in and return a signed JWT.
- **Token_Service**: The Api-layer component that issues signed JWTs containing the caller's id and role claims.
- **Authorized_Set**: The set of Requests a Caller may see — all Requests for an Administrator; only Requests where `OwnerId` or `AssignedToUserId` equals the Caller's id, for a Regular_User.
- **Query_Parameters**: The combined set of search, filter, sort, and pagination inputs supplied on the request.
- **Sort_Field**: The field results are ordered by — one of `RequestNumber`, `Status`, `RequestType`, `CreatedAt`.
- **Sort_Direction**: The ordering direction — ascending (`Asc`) or descending (`Desc`).
- **Keyset_Pagination**: A pagination strategy that uses a `WHERE` clause on the sort values of the last returned row (instead of `OFFSET`/`SKIP`), giving stable performance independent of page depth. When the Sort_Field is not unique, the predicate includes the tie-breaker: `SortField <compare> lastValue OR (SortField == lastValue AND Id <compare> lastId)`.
- **Cursor**: An opaque token (Base64-encoded) that encodes the sort values of the last row in a page — `(sortFieldValue, Id)` — together with the active Sort_Field and Sort_Direction. It is valid only for the Sort_Field and Sort_Direction under which it was created.
- **Composite_Index**: An index on a column combination aligned with the authorization scope and sort order (e.g. `(OwnerId, CreatedAt, Id)`), enabling Keyset_Pagination to run as an efficient seek on the default `CreatedAt` sort.
- **Page_Size**: The maximum number of Requests returned in a single page.
- **PagedResult**: The search response shape — a list of `RequestDto` items together with `nextCursor`.
- **Filter_Form**: The client-side filter form component.
- **Results_Table**: The component that displays the returned Requests.
- **Search_Page**: The smart container component that coordinates the Filter_Form, the Results_Table, the loading/error state, and the API calls.
- **Requests_Api_Client**: The client-side service responsible for HTTP calls to the Search_API.
- **Stats_API**: The `POST /api/requests/stats` endpoint, returning aggregate counts (total, by Status, by Request Type) over the Caller's Authorized_Set.
- **Dashboard**: The overview tab that visualizes the Stats_API response as charts, KPIs, and a progress indicator, and supports drill-down into the search tab.

## Requirements

### Requirement 1: Search by Request Number (partial match)

**User Story:** As a Caller, I want to search Requests by a partial Request Number, so that I can find Requests without knowing the exact number.

#### Acceptance Criteria

1. WHEN a Caller supplies a `requestNumber` value, THE Search_API SHALL return only Requests whose `RequestNumber` contains that value.
2. WHERE a `requestNumber` value is supplied, THE Request_Service SHALL match it case-insensitively within the `IQueryable`, using a comparison that is independent of the data-store collation so that results are consistent across providers.
3. WHEN a `requestNumber` value is absent, empty, or whitespace-only after trimming, THE Search_API SHALL apply no Request Number filter.

### Requirement 2: Filter by Status (single or multiple)

**User Story:** As a Caller, I want to filter Requests by one or more Statuses, so that I can focus on Requests in specific states.

#### Acceptance Criteria

1. WHEN a Caller supplies one `status` value, THE Search_API SHALL return only Requests whose `Status` equals that value.
2. WHEN a Caller supplies multiple `status` values, THE Search_API SHALL return only Requests whose `Status` matches any of the supplied values.
3. WHEN duplicate `status` values are supplied, THE Search_API SHALL treat them as a single distinct value.
4. WHEN no `status` value is supplied, THE Search_API SHALL apply no Status filter.
5. IF a supplied `status` value is not a defined RequestStatus, THEN THE Search_API SHALL return HTTP 400 identifying the invalid value.

### Requirement 3: Filter by creation date range

**User Story:** As a Caller, I want to filter Requests by a creation date range, so that I can view Requests created within a specific period.

#### Acceptance Criteria

1. WHEN a Caller supplies a `createdFrom` value, THE Search_API SHALL return only Requests whose `CreatedAt` is greater than or equal to that value.
2. WHEN a Caller supplies a `createdTo` value, THE Search_API SHALL return only Requests whose `CreatedAt` is less than or equal to that value.
3. WHEN both `createdFrom` and `createdTo` are supplied, THE Search_API SHALL return only Requests whose `CreatedAt` falls within the inclusive range.
4. WHEN no date range is supplied, THE Search_API SHALL apply no creation-date filter.
5. THE Search_API SHALL normalize supplied date values to UTC before comparison, so that filtering matches the UTC `CreatedAt` values consistently.
6. IF `createdFrom` is later than `createdTo`, THEN THE Search_API SHALL return HTTP 400 identifying the invalid range.
7. IF a supplied date value cannot be parsed as a valid date, THEN THE Search_API SHALL return HTTP 400 identifying the invalid value.

### Requirement 4: Filter by Request Type

**User Story:** As a Caller, I want to filter Requests by Request Type, so that I can view only Requests of a given type.

#### Acceptance Criteria

1. WHEN a Caller supplies a `requestType` value, THE Search_API SHALL return only Requests whose `RequestType` equals that value.
2. WHEN no `requestType` value is supplied, THE Search_API SHALL apply no Request Type filter.
3. IF a supplied `requestType` value is not a defined RequestType, THEN THE Search_API SHALL return HTTP 400 identifying the invalid value.

### Requirement 5: Sorting

**User Story:** As a Caller, I want to sort the results, so that I can view them in a meaningful order.

#### Acceptance Criteria

1. WHEN a Caller supplies a `sortBy` field and a `sortDirection`, THE Search_API SHALL return Requests ordered by that field in that direction, applied within the `IQueryable`.
2. THE Search_API SHALL support the Sort_Field values `RequestNumber`, `Status`, `RequestType`, and `CreatedAt`, in both ascending and descending Sort_Direction.
3. WHEN sorting by `Status` or `RequestType`, THE Search_API SHALL order Requests alphabetically by the value's name (the English label shown in the UI), rather than by the enum's numeric value, so that a newly added enum value sorts into its correct position with no code change.
4. WHEN no `sortBy` field is supplied, THE Search_API SHALL order Requests by `CreatedAt` descending as the default.
5. THE Request_Service SHALL apply `Id` as a secondary tie-breaker so that Requests with equal Sort_Field values have a stable, deterministic order.
6. IF a supplied `sortBy` field is not a supported Sort_Field, THEN THE Search_API SHALL return HTTP 400 identifying the invalid field.
7. IF a supplied `sortDirection` is neither ascending nor descending, THEN THE Search_API SHALL return HTTP 400 identifying the invalid direction.

### Requirement 6: Server-side authorization enforced within the query

**User Story:** As the system owner, I want authorization enforced within the database query, so that a Regular_User can never retrieve Requests outside their Authorized_Set, even at scale.

#### Acceptance Criteria

1. WHILE the Caller is a Regular_User, THE Request_Service SHALL restrict the query to the Authorized_Set (`OwnerId` or `AssignedToUserId` equal to the Caller's id) within the `IQueryable`, before applying filters, sorting, and pagination.
2. WHILE the Caller is an Administrator, THE Request_Service SHALL include all Requests as the candidate set.
3. THE Request_Service SHALL derive the Authorized_Set solely from the Caller identity carried by the authenticated JWT claims, never from any Query_Parameter or client-supplied header.
4. WHEN filters, sorting, and pagination are combined, THE Request_Service SHALL return only Requests within the Caller's Authorized_Set.
5. IF a request to the Search_API carries no valid JWT bearer token, THEN THE Search_API SHALL return HTTP 401 and SHALL NOT return any Requests.
6. THE Search_API SHALL take the Caller's id from the token subject claim and treat the Caller as an Administrator only when the token `is_admin` claim equals `true` (case-insensitive).

### Requirement 7: Keyset pagination and large-scale performance

**User Story:** As the system owner, I want search and paging to perform predictably over millions of Requests, so that response times remain stable regardless of dataset size.

#### Acceptance Criteria

1. THE Request_Service SHALL compose filtering, authorization, sorting, and pagination into a single `IQueryable` executed at the database level, without loading unfiltered Requests into memory.
2. THE Request_Service SHALL advance pages using Keyset_Pagination based on the active Sort_Field and the `Id` tie-breaker, rather than `OFFSET`/`SKIP`.
3. WHEN advancing past a Cursor whose active Sort_Field is not unique, THE Request_Service SHALL apply the composite seek predicate so that no Request is skipped or duplicated across pages.
4. WHEN a Caller supplies a `cursor` value, THE Search_API SHALL return the next Page_Size Requests following the position encoded by the Cursor under the active sort order.
5. WHEN a page is returned and further Requests remain, THE Search_API SHALL include a `nextCursor` referencing the position after the last returned Request; otherwise THE Search_API SHALL return a null `nextCursor`.
6. THE Search_API SHALL return at most Page_Size Requests per response, using a default Page_Size of 50 when none is supplied.
7. THE Backend SHALL define indexes that let Keyset_Pagination perform an index seek rather than a full scan: a Composite_Index aligned with the default sort and authorization scope (`(OwnerId, CreatedAt, Id)` and `(AssignedToUserId, CreatedAt, Id)`), and supporting single-column indexes for the `Status`, `RequestType`, and `RequestNumber` filters. Sorting by `Status`/`RequestType` orders by the enum **name** (R5.3) and is therefore a deliberate UX-over-index tradeoff that does not use the single-column index — documented as such rather than backed by a dedicated composite index.
8. IF a supplied `pageSize` value is not an integer between 1 and 200 inclusive, THEN THE Search_API SHALL return HTTP 400 identifying the invalid page size.
9. IF a supplied `cursor` value cannot be decoded, THEN THE Search_API SHALL return HTTP 400 identifying the invalid cursor.
10. IF a supplied `cursor` value was created under a Sort_Field or Sort_Direction different from the current request, THEN THE Search_API SHALL return HTTP 400 indicating the cursor does not match the current sort order.

### Requirement 8: Invalid-input handling and consistent error envelope

**User Story:** As a Caller, I want clear errors for invalid input, so that I can correct my search parameters.

#### Acceptance Criteria

1. THE Search_API SHALL validate all supplied Query_Parameters before executing the query.
2. WHEN any Query_Parameter is invalid, THE Search_API SHALL return HTTP 400 with a body describing each invalid parameter, and SHALL NOT return any Requests.
3. WHEN multiple parameters are invalid, THE Search_API SHALL report all of them in a single response.
4. IF an unexpected error occurs while executing the query, THEN THE Search_API SHALL return HTTP 500 with a message that excludes internal implementation details.

### Requirement 9: API response contract

**User Story:** As a frontend developer, I want a well-defined response contract, so that the search UI can render results and paging reliably.

#### Acceptance Criteria

1. WHEN the Search_API returns a successful result, THE Search_API SHALL return a PagedResult containing the matching RequestDto items and the `nextCursor`.
2. THE Search_API SHALL return each Request as a RequestDto containing `Id`, `RequestNumber`, `CustomerId`, `OwnerId`, `AssignedToUserId`, `Status`, `RequestType`, and `CreatedAt`.
3. THE Search_API SHALL project directly to RequestDto within the `IQueryable`, so that only the required columns are read from the data store.
4. WHEN no Requests match the supplied criteria, THE Search_API SHALL return HTTP 200 with an empty items list and a null `nextCursor`.
5. WHEN an empty request body is supplied, THE Search_API SHALL return the first page of Requests within the Caller's Authorized_Set.
6. THE Backend SHALL enable CORS for the Frontend origin, permitting the `POST` method (and `GET` for health/diagnostics) and the `Content-Type` and `Authorization` request headers, so that the browser-based client can call the Auth_API and the Search_API.
7. THE Search_API SHALL accept its search, filter, sort, and pagination criteria in the JSON request body of a `POST /api/requests/search` call, rather than in the URL query string.

### Requirement 10: Server-side query composition

**User Story:** As a developer, I want the query built from composable, conditionally-applied steps, so that the filter logic stays readable, testable, and index-friendly at scale.

#### Acceptance Criteria

1. THE Request_Service SHALL add a filter condition to the `IQueryable` only for each Query_Parameter that is supplied.
2. THE Request_Service SHALL apply authorization and filters before applying ordering and pagination.
3. WHEN determining whether more results exist, THE Request_Service SHALL fetch at most `Page_Size + 1` rows and SHALL NOT execute a separate unbounded count over the full data set for that purpose.
4. THE Request_Service SHALL execute all read queries with `AsNoTracking`, as the search path is read-only.
5. THE Request_Service SHALL keep the entire filter, authorization, ordering, and pagination expression translatable to a single data-store query, without client-side evaluation over unfiltered Requests.

### Requirement 11: Filter form (Filter_Form)

**User Story:** As a Caller, I want a filter form in the UI, so that I can enter search and filter criteria.

#### Acceptance Criteria

1. THE Filter_Form SHALL provide inputs for Request Number, Status (multi-select), Request Type, and a creation date range (from and to), implemented as an Angular reactive form.
2. WHEN a Caller submits the Filter_Form, THE Search_Page SHALL request Requests through the Requests_Api_Client using the entered criteria.
3. WHEN a Caller clears the Filter_Form, THE Search_Page SHALL request the unfiltered first page of Requests within the Caller's Authorized_Set.
4. WHERE multiple Status values are chosen, THE Filter_Form SHALL send all chosen values to the Search_API.
5. IF `createdFrom` is later than `createdTo`, THEN THE Filter_Form SHALL display a validation message and prevent submission.

### Requirement 12: Results table and sorting (Results_Table)

**User Story:** As a Caller, I want the results shown in a sortable table, so that I can review and order the Requests.

#### Acceptance Criteria

1. THE Results_Table SHALL display columns for `RequestNumber`, `Status`, `RequestType`, and `CreatedAt`.
2. THE Results_Table SHALL render the Status and RequestType values as human-readable labels via i18n keys, and SHALL render each Status with a distinct color indication.
3. WHEN a Caller activates a sortable column header, THE Search_Page SHALL request Requests sorted by the corresponding Sort_Field and Sort_Direction.
4. WHEN further Requests remain after the current page, THE Search_Page SHALL provide a control to load the next page using the `nextCursor`.
5. THE Results_Table SHALL be a presentational component using `OnPush` change detection and signal inputs.

### Requirement 13: State indicators (loading / error / empty)

**User Story:** As a Caller, I want clear loading, error, and empty-state indicators, so that I always understand the state of my search.

#### Acceptance Criteria

1. WHILE a search request is in progress, THE Search_Page SHALL display a loading indicator.
2. WHEN a search request completes with one or more Requests, THE Search_Page SHALL display the Results_Table and hide the loading, error, and empty-state indicators.
3. WHEN a search request completes with zero Requests, THE Search_Page SHALL display an empty-state indicator.
4. IF a search request fails, THEN THE Search_Page SHALL display an error indicator with a retry control.
5. THE Search_Page SHALL manage loading, error, and result state using Angular Signals.

### Requirement 14: Filter and sort options in the UI

**User Story:** As a Caller using the web application, I want the filter and sort controls to offer only valid choices, so that I cannot submit unsupported values.

#### Acceptance Criteria

1. THE Frontend SHALL present the RequestStatus values (New, InProgress, Completed, Cancelled) as the only selectable Status filter options.
2. THE Frontend SHALL present the RequestType values (General, Legal, Payment, Appeal) as the only selectable Request Type filter options.
3. THE Frontend SHALL present exactly the Sort_Field values supported by the Search_API as the available sort options.
4. WHEN the Caller has not chosen a sort field, THE Frontend SHALL reflect the default order (CreatedAt descending) in the sorting control.

### Requirement 15: Frontend technology stack and code-quality conventions

**User Story:** As a developer, I want the frontend built on modern Angular 20 primitives with clean conventions, so that the solution is self-contained, portable, and maintainable.

#### Acceptance Criteria

1. THE Frontend SHALL be implemented with Angular 20 using standalone components, Signals, the new control flow (`@if`/`@for`/`@switch`), and `OnPush` change detection.
2. THE Frontend SHALL separate a smart container (Search_Page) that owns state and orchestration from presentational components (Filter_Form, Results_Table) that communicate only via signal inputs and outputs.
3. THE Frontend SHALL manage query, result, and status state using Angular Signals, without a heavyweight state-management library.
4. WHERE UI components are needed, THE Frontend MAY use public open-source libraries (PrimeNG for UI components, @ngx-translate for i18n), and SHALL otherwise use standard Angular 20 and HTML/CSS.
5. THE Frontend SHALL NOT contain user-facing hardcoded strings; all displayed text SHALL come from i18n translation keys.
6. THE Frontend SHALL NOT contain hardcoded API URLs or business-rule numbers; endpoints SHALL come from environment configuration and enumerated values SHALL come from typed enums.
7. THE Frontend SHALL apply direction-agnostic styling using logical properties (such as `margin-inline-start`) rather than fixed left/right directions, so the layout works regardless of text direction. The UI ships in English (LTR).
8. THE Frontend SHALL use a custom application favicon representing the Requests product (not a framework default).

### Requirement 16: Automated tests and test execution

**User Story:** As a reviewer, I want automated tests covering the most meaningful cases, together with clear instructions to run them, so that I can verify correctness quickly and with confidence.

#### Acceptance Criteria

1. THE Backend SHALL include xUnit tests that verify authorization scoping (a Regular_User receives only owned or assigned Requests; an Administrator receives all; query parameters never widen the Authorized_Set).
2. THE Backend SHALL include tests that verify combined filtering (partial Request Number, one or more Statuses, date range, and Request Type applied together as a conjunction).
3. THE Backend SHALL include tests that verify keyset pagination correctness — sequential traversal of all pages returns every matching Request exactly once, with no duplicates or gaps, and a null `nextCursor` on the final page.
4. THE Backend SHALL include tests that verify cursor round-trip (encode then decode preserves values) and rejection of malformed or sort-mismatched cursors.
5. THE Backend SHALL include tests that verify invalid input handling returns HTTP 400 and reports all invalid parameters together.
6. THE Frontend SHALL include tests for the state store (state transitions across loading, success, error, and empty; next-page loading via the cursor) and for the filter form's date-range validation.
7. THE repository SHALL document, in the README, the exact commands to run the backend tests (`dotnet test`) and the frontend tests (`npm test`).
8. WHERE tests are provided, THE test suite SHALL prioritize meaningful, high-value cases over exhaustive coverage.

### Requirement 17: Observability

**User Story:** As an operator, I want the service to expose health and structured, traceable logs, so that I can monitor it and diagnose issues.

#### Acceptance Criteria

1. THE Backend SHALL expose a health endpoint (`/health`) that reports the service status.
2. THE Backend SHALL emit structured logs for each request.
3. WHEN the Search_API returns an error, THE Backend SHALL log the failure without logging sensitive data.
4. THE Backend SHALL expose an OpenAPI (Swagger) document describing the Search_API parameters, responses, and error shapes.

### Requirement 18: Frontend usability and accessibility

**User Story:** As a Caller using the web application, I want a responsive, accessible, and smooth search experience, so that the interface is pleasant and usable for everyone.

#### Acceptance Criteria

1. THE Frontend SHALL debounce filter input so that rapid successive changes result in a single search request.
2. WHILE a search request is in progress, THE Frontend SHALL present a non-blocking loading indication (such as a skeleton or spinner) without discarding the current layout.
3. THE Frontend SHALL provide accessible interactive elements, including labels and keyboard operability for the filter controls and the results table.
4. THE Frontend SHALL present the search screen responsively across common desktop and tablet widths.

### Requirement 19: Backend as a standard C# solution runnable from Visual Studio

**User Story:** As a developer, I want the backend to be a standard C# solution that opens and runs in Visual Studio, so that I can build, run, and debug it the usual way without extra setup.

#### Acceptance Criteria

1. THE Backend SHALL provide a Visual Studio solution file (`Backend/Requests.sln`) that includes all backend source projects (`Requests.Domain`, `Requests.Application`, `Requests.Infrastructure`, `Requests.Api`) and the test project (`Requests.Tests`).
2. WHEN the solution is opened in Visual Studio and run (F5), THE Backend SHALL launch the `Requests.Api` project under the IIS Express profile and open the browser directly at the Swagger UI (`/swagger`).
3. THE Backend SHALL expose HTTPS on a port within the IIS Express SSL-certificate range (`https://localhost:44301`) and HTTP on `http://localhost:60702`, so the HTTPS endpoint is served with a trusted local certificate and the HTTP port continues to match the Frontend's configured API base URL.
4. THE Backend SHALL build successfully via `dotnet build Backend/Requests.sln` with no errors.
5. THE Backend SHALL run its tests via `dotnet test` against the solution or the test project, with all tests passing.

### Requirement 20: JWT authentication

**User Story:** As the system owner, I want callers to authenticate with a signed token rather than trusting client-supplied identity, so that a caller cannot choose to be an administrator or impersonate another user.

#### Acceptance Criteria

1. THE Auth_API SHALL expose `POST /api/auth/register` accepting a username and password, creating an account and returning a signed JWT.
2. THE Auth_API SHALL expose `POST /api/auth/login` accepting a username and password, and SHALL return a signed JWT on success.
3. THE Backend SHALL store user passwords only as salted hashes (PBKDF2), never in plaintext.
4. IF a registration uses a username that already exists, THEN THE Auth_API SHALL return HTTP 400 identifying the conflict.
5. IF a login supplies an unknown username or an incorrect password, THEN THE Auth_API SHALL return HTTP 401 without revealing which field was wrong.
6. THE Token_Service SHALL issue a JWT whose subject claim is the user's id and whose `is_admin` claim reflects the user's administrator role, signed with a configured signing key, issuer, and audience.
7. THE Search_API SHALL require a valid JWT bearer token and SHALL reject requests without one with HTTP 401.
8. THE Backend SHALL seed a default administrator account on startup from configuration (`Seed:Admin`), so an administrator can always sign in.

### Requirement 21: Dashboard overview with aggregate stats and drill-down

**User Story:** As a Caller, I want a visual dashboard of my Requests by status and type, so that I can see the overall picture at a glance and jump straight to a filtered list.

#### Acceptance Criteria

1. THE Frontend SHALL present the Requests screen as two tabs: a search tab (the filter form, results table, and sorting) and an overview (dashboard) tab.
2. THE Backend SHALL expose `POST /api/requests/stats` that returns, for the caller's Authorized_Set, the total count and the counts grouped by Status and by Request Type, computed by a database-level aggregation.
3. THE stats endpoint SHALL include every Status and every Request Type in its result, using a count of zero for values with no matching Requests.
4. THE stats endpoint SHALL require a valid JWT bearer token and SHALL scope its counts to the caller's Authorized_Set (an Administrator sees all; a Regular_User only owned or assigned Requests).
5. THE dashboard SHALL load its data with a single call to the stats endpoint and SHALL derive all KPIs, percentages, and the progress indicator on the client without further API calls.
6. THE dashboard SHALL render a doughnut chart of Requests by Status and a doughnut chart by Request Type, each slice colored consistently with the status/type color language and labeled via i18n keys.
7. THE dashboard SHALL display KPI figures (total, open, closed, completion percentage) and a progress indicator of completed versus remaining Requests.
8. WHEN a Caller clicks a Status slice, THE Frontend SHALL switch to the search tab and filter the results by that Status; WHEN a Caller clicks a Request Type slice, THE Frontend SHALL switch to the search tab and filter by that Request Type.
