---
name: NimbosService Architecture Overview
description: Core architectural patterns, auth model, and domain structure of NimbosService
type: project
---

ASP.NET Core 10 / C# service backed by SQL Server (EF Core 10). Single-project layout: Controllers, Models, DTOs, Data, Middleware.

**Auth model:** Custom bearer token middleware (`DeviceTokenAuthMiddleware`) — the token IS the user's GUID. Not JWT. User is resolved from DB on every authenticated request and injected into `HttpContext.Items["CurrentUser"]`. Public routes (POST /users, POST /auth/google, POST /auth/apple, /swagger) are bypassed.

**Domain:** Habit tracking app with families. Users have Tasks (recurring habits + tomorrow-only extras), a Shield (fragments earned from daily completion), DailySnapshots, and optional Family membership (Parent/Child roles).

**Why:** Mobile app (iOS) using Google Sign-In and Apple Sign-In. Solo users (no SSO) are also supported via DeviceId.

**How to apply:** When reviewing auth issues, note the token-is-GUID pattern is a significant security weakness. Star/shield logic lives entirely in controllers — no service layer exists. All DB access is direct EF Core in controllers with no repository pattern.
