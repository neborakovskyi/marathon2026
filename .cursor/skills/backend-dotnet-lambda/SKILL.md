---
name: backend-dotnet-lambda
description: >-
  Implements restaurant backend HTTP APIs as .NET 10 Lambda handlers (RouteRegistry,
  services, DynamoDB repositories, xUnit). Use when working under
  backend/restaurant-backend-app, adding routes, guest/group pre-order, or API tests.
---

# Backend .NET Lambda

Read `backend/restaurant-backend-app/CLAUDE.md` before editing. That file is the source of truth.

There is no local API process. Tests: `dotnet test Tests/APIHandler.Tests.csproj`. Runtime needs `syndicate deploy`.

## New endpoint checklist

1. DTO in `Models/<Domain>/`
2. Repository method if persistence changes
3. Service method with domain exceptions
4. Thin `IApiRequestHandler` using `ResponseBuilder` and `TryGetPathValue`
5. Register DI + `RouteHandler` in `Routing/RouteRegistry.cs`
6. Tests in `Tests/Services/`
7. Update `docs/ApiRoutes.md`

Group pre-order homework shapes: repo-root `homework-backend.md`. Do not change those payloads without being asked.

## Status mapping

| Situation | Response |
|---|---|
| Missing/invalid input | 400 |
| Missing Cognito `sub` | 401 |
| `ActionForbiddenException` | 403 |
| Not found exceptions | 404 |
| `ValidationException` | `CreateErrorCodeResponse` |
| Uncaught | 500 via `ExceptionHandler` |

Do not call DynamoDB or Cognito from handlers or from tests.
