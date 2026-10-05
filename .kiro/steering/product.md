# Product

A .NET 8 Web API (with an Angular client) for managing customer **Requests** (e.g. general, legal, payment, and appeal cases).

## Core Domain

- A `Request` has a number, a customer, an owner, an optional assignee, a status, a type, and audit timestamps.
- `RequestStatus`: New, InProgress, Completed, Cancelled.
- `RequestType`: General, Legal, Payment, Appeal.
- A `User` has a username, an administrator flag, and a salted password hash.

## Key Behavior

- **Search:** `POST /api/requests/search` — filter, sort, and page over requests (criteria in the JSON request body).
- **Stats:** `POST /api/requests/stats` — aggregate counts grouped by status and by type, powering the dashboard.
- **Auth:** `POST /api/auth/register` and `POST /api/auth/login` — on success return a signed JWT.
- Authorization is role-based and enforced in the application layer:
  - **Administrators** see all requests.
  - **Regular users** see only requests they own (`OwnerId`) or are assigned (`AssignedToUserId`).

## Identity & Authentication

- Callers authenticate with a **JWT bearer token** obtained from `/api/auth/login`, sent as `Authorization: Bearer <token>`.
- The token carries the user id (`sub`), username, and an `is_admin` claim. Because it is server-signed, the client cannot alter its role.
- A default administrator account is seeded on startup from the `Seed:Admin` configuration section (default `admin`/`admin`); regular users self-register via `/api/auth/register`.

This is a candidate/exercise project. Data is seeded in-memory at startup; there is no persistent database.
