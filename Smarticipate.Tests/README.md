# Smarticipate.Tests

Tests for the question types and teacher toolbox backend.

## Layout

- `Handlers/` — unit tests for the per-type question handlers. Pure in-memory logic, no database. Cover the single-channel invariant, option dedup and selection bounds, scale range and distribution, and free-text trim/empty.
- `Integration/` — API integration tests using `WebApplicationFactory<Program>`. They boot the real API over the full middleware pipeline and exercise the endpoints end to end: toolbox CRUD and ownership, per-type authoring validation, firing guards, per-type answering (validation, revise, dedup), aggregation, activations-by-session, and session end/restart/delete lifecycle.

## Running

```
dotnet test
```

Unit tests only (no database needed):

```
dotnet test --filter FullyQualifiedName!~Integration
```

## Integration test prerequisites

The integration tests run against a real PostgreSQL server (so jsonb, the unique dedup indexes, and foreign keys behave exactly as in production). There is no Docker dependency.

For each run, `ApiFactory` creates a fresh uniquely-named database (`smarticipate_it_<random>`), lets the app migrate and seed it on startup, and drops it on dispose.

Requirements:

- A reachable PostgreSQL server. By default the tests connect to `localhost:5432` as `postgres`/`postgres` with permission to create and drop databases.
- Override the connection via environment variables if your setup differs:
  - `QA_PGHOST` (default `localhost`)
  - `QA_PGPORT` (default `5432`)
  - `QA_PGUSER` (default `postgres`)
  - `QA_PGPASSWORD` (default `postgres`)

## Auth in integration tests

Authentication is driven by a header-based test scheme (`TestAuthHandler`): a request carrying `X-Test-User: <userId>` is authenticated as that user. Each test creates a real `AspNetUsers` row so the `OwnerUserId` and `Session.UserId` foreign keys hold and the owner-scoping checks (`not yours == 404`) are exercised for real. Requests without the header are anonymous, matching the student-facing endpoints.

## Note

A broader manual QA checklist for the whole feature (including the UI areas still to build) is kept alongside the design and implementation docs outside the repo.
