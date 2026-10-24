# Agent and contributor map

This repo is the restaurant booking / pre-order platform (Angular SPA + .NET 10 Lambdas + Terraform). Read the file that owns the area you are changing. Do not copy those files into a second source of truth.

| Area | Source of truth |
|---|---|
| Frontend (Angular 22) | `frontend/restaurant-frontend-app/CLAUDE.md` |
| Backend (.NET Lambdas, Syndicate) | `backend/restaurant-backend-app/CLAUDE.md` |
| API contracts | `backend/restaurant-backend-app/docs/ApiRoutes.md` and `dnapp/lambdas/api-handler/Routing/RouteRegistry.cs` |
| Deploy / destroy AWS | root `README.md` |
| Backend local AWS/Syndicate setup | `backend/restaurant-backend-app/local-setup-guide.md` |
| Swagger export | `backend/restaurant-backend-app/docs/GenerateSwagger.md` |
| Group pre-order homework spec | `homework-backend.md` |
| Signals migration notes (frontend) | `signals-migration.md` |
| Cursor skills | `.cursor/skills/` |

## Layout

```
infra/terraform/                      shared AWS (S3, CloudFront, Cognito extras, SQS, IAM)
backend/restaurant-backend-app/       Syndicate app: api-handler, reports-handler, report-sender
frontend/restaurant-frontend-app/     Angular 22 SPA → S3 + CloudFront
```

## Working agreements

- Do not commit, branch, tag, or push unless the user asks.
- Backend cannot be hosted locally. Unit tests run with `dotnet test`. Runtime changes need `syndicate deploy`.
- Frontend `npm start` talks to the deployed API Gateway URL in `environment.development.ts`.
- When a frontend model disagrees with an API response, the backend route + handler win.
- One logical change per PR. Do not mix lint/format sweeps with behaviour changes.
