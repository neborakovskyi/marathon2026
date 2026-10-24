# restaurant-backend-app — .NET 10 Lambdas

Restaurant booking, reservations, pre-orders, guest/group sessions, waiters, admin, reports.

This file is the single source of truth for backend work. Frontend lives in `frontend/restaurant-frontend-app/CLAUDE.md`. Do not copy these rules into the repo root.

**There is no local API host.** No Docker, no LocalStack, no `dotnet run` for the HTTP API. Unit tests run on the machine. Any runtime change requires a real `syndicate deploy`. Region for Terraform and Syndicate in this repo is `us-west-2`.

When a frontend model disagrees with an API response, this app wins: `docs/ApiRoutes.md` plus `dnapp/lambdas/api-handler/Routing/RouteRegistry.cs`.

---

## 1. Quick start

```bash
cd backend/restaurant-backend-app
dotnet test Tests/APIHandler.Tests.csproj
```

Deploy (after Syndicate config is filled in — see [local-setup-guide.md](local-setup-guide.md) and the root [README.md](../../README.md)):

```powershell
$env:SDCT_CONF = (Resolve-Path ".\.syndicate-config-restaurant-backend-app").Path
syndicate build
syndicate deploy --deploy_name dev
# incremental:
syndicate update
```

---

## 2. Lambdas

| Project | Trigger | Role |
|---|---|---|
| `dnapp/lambdas/api-handler` | API Gateway | All HTTP routes. `Function.FunctionHandler` matches method + path against `RouteRegistry`. |
| `dnapp/lambdas/reports-handler` | SQS | Consumes report-generation messages. |
| `dnapp/lambdas/report-sender` | scheduled / report pipeline | Builds and emails report files. |

`api-handler` is the default place for new product behaviour.

---

## 3. Code structure — api-handler

```
dnapp/lambdas/api-handler/
  Function.cs                 Lambda entry; OPTIONS CORS; 404 if no route
  Routing/RouteRegistry.cs    DI + every HTTP route (source of truth)
  Routing/RouteHandler.cs     `{param}` path matching
  Handlers/<Domain>/          IApiRequestHandler: parse request, call service, map status
  Services/<Domain>/          business rules
  Repositories/<Domain>/      DynamoDB via IDynamoDBContext + OverrideTableName
  Models/<Domain>/            request/response DTOs
  Entities/                   DynamoDB entities
  Exceptions/                 domain exceptions → HTTP codes in the handler
  Helpers/ResponseBuilder.cs  JSON + CORS headers
  Constants/ResourceNames.cs  env vars for tables, Cognito, region, indexes
Tests/Services/               xUnit + Moq + AutoFixture
```

**Where does a new endpoint go?**

| Piece | Location | Naming |
|---|---|---|
| Route | `Routing/RouteRegistry.cs` in the matching `Get*Routes()` list | `new Route(HttpMethods.GET, "/path/{id}")` |
| Handler | `Handlers/<Domain>/` | `GetThingHandler` implementing `IApiRequestHandler` |
| Service method | `Services/<Domain>/` + interface | `IThingService` |
| Persistence | `Repositories/<Domain>/` + interface | `IThingRepository` |
| DTO | `Models/<Domain>/` | `GetThingResponse.cs` |
| Entity | `Entities/` | DynamoDB document |
| Tests | `Tests/Services/` | `ThingServiceTests.cs` |

Handlers stay thin: path/query/body + auth claims, then one service call, then `ResponseBuilder` / typed exceptions. Do not put DynamoDB or Cognito calls in handlers.

Register new repositories and services in `RouteRegistry.BuildRoutes()` or they will not resolve.

---

## 4. Conventions

- **.NET 10**, nullable enabled, file-scoped namespaces, primary constructors where the rest of the domain already uses them.
- **camelCase JSON** via `ResponseBuilder`. Do not invent a second serializer settings object in a handler.
- **Table names and GSIs** come from `ResourceNames` / env vars in `lambda_config.json`. Do not hardcode production table names except where the existing config already inlines them (`groupReservationsTable`, `guestSessionTable`).
- **Auth:** Cognito `sub` from `request.RequestContext.Authorizer.Claims`. Missing claims → 401. Owner-only / waiter-only checks live in services (`ActionForbiddenException` → 403).
- **HTTP mapping in the handler, not a global filter:** 400 validation, 401 unauthorized, 403 forbidden, 404 not found, `ValidationException` → `CreateErrorCodeResponse`. Uncaught → 500 from `ExceptionHandler`.
- **CORS:** `Function` short-circuits `OPTIONS`. Success/error responses use `ResponseBuilder` headers. If you add a verb, include it there if clients will preflight it.
- **Path params** via `request.TryGetPathValue("name", out var value)` — the name must match `{name}` in the route template.

---

## 5. Testing

Tests cover services, not the Lambda host.

```bash
dotnet test Tests/APIHandler.Tests.csproj
```

- xUnit, Moq, AutoFixture (`InlineAutoData` where the existing tests already use it).
- Name: `MethodUnderTest_Scenario_ExpectedBehavior`.
- Mock every repository and AWS-facing dependency. Do not hit DynamoDB or Cognito from tests.
- Behaviour change or bug fix: a test that would fail before the change.
- New endpoint without a service-level test is incomplete unless the PR says why it is untestable.

---

## 6. API contract

1. Implement the handler + service + tests.
2. Register the route in `RouteRegistry.cs`.
3. Update `docs/ApiRoutes.md` in the same change (method, path, body, status codes).
4. Homework / group pre-order behaviour is specified in repo-root `homework-backend.md`. Do not silently change those response shapes.

Existing group-owner routes:

- `GET /guest/groups/{groupReservationId}/sessions`
- `GET /reservations/{reservationId}/guest-session-id`
- `POST /group-reservations/{groupReservationId}/submit`
- `DELETE /guest/groups/{groupReservationId}/sessions/{guestSessionId}`

---

## 7. Traps

1. **Route not registered** — handler exists but `RouteRegistry` was not updated → 404 in API Gateway.
2. **Path template mismatch** — `{groupReservationId}` in the route vs `TryGetPathValue("id")` in the handler.
3. **DI miss** — new service constructed in a handler constructor but never `AddSingleton` in `RouteRegistry`.
4. **Wrong table env var** — `ResourceNames` throws at runtime if the Lambda env is missing; keep `lambda_config.json` in sync with Syndicate aliases.
5. **Frontend region / URL** — current shared account API is `us-west-2` (`environment.development.ts`). Do not “fix” it to another region without checking the deployed API.
6. **Cannot validate HTTP locally** — `dotnet test` is not a substitute for a deploy when you change routing, auth, or env vars.

---

## 8. For AI agents

- Do not create commits, branches, tags, or push unless explicitly asked.
- Do not add a second `CLAUDE.md` for this app.
- Match neighbouring handlers in the same domain (auth extraction, exception mapping, `ResponseBuilder`).
- After behaviour changes, run `dotnet test Tests/APIHandler.Tests.csproj` and report the result, not an impression.
- Keep `docs/ApiRoutes.md` aligned with `RouteRegistry.cs`.
