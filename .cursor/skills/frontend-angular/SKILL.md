---
name: frontend-angular
description: >-
  Implements and verifies the restaurant Angular 22 SPA (standalone components,
  inject(), HTTP services, Karma/Jasmine). Use when working under
  frontend/restaurant-frontend-app, changing UI, routes, models, or frontend tests.
---

# Frontend Angular

Read `frontend/restaurant-frontend-app/CLAUDE.md` before editing. That file is the source of truth.

## Do

1. Follow **current** conventions, not the unfinished target list (signals/`OnPush`/zoneless only inside the task that owns them).
2. Put files where the CLAUDE.md decision table says. One interface file per concern. `inject()` not constructor DI.
3. Type HTTP calls. Surface errors with `toast.service.ts`. Unsubscribe or `takeUntilDestroyed()`.
4. When a model disagrees with the API, trust `backend/restaurant-backend-app/docs/ApiRoutes.md` and `RouteRegistry.cs`.

## Verify before claiming done

From `frontend/restaurant-frontend-app`:

```bash
npx tsc -p tsconfig.app.json --noEmit
npx tsc -p tsconfig.spec.json --noEmit
npm test -- --watch=false
```

Production bundle (needs a URL): `BACKEND_URL=https://example.com npm run build` must print `Bundle check passed`.

Use `npm ci`, not `npm install`. Do not add npm packages without asking. Do not commit `environment.production.ts`.
