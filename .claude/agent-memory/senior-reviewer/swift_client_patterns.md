---
name: Swift Client Patterns and Recurring Issues
description: Patterns, anti-patterns, and architectural notes from NimbosApp Swift client reviews
type: project
---

**Architecture:**
- `NimbusAppApp` owns all top-level `@StateObject` instances (HabitViewModel, FamilyViewModel, DailyRefreshViewModel, NotificationViewModel). Views receive them as `@ObservedObject`.
- Routing is driven by two `@AppStorage` keys: `nimbus_onboardingComplete` (Bool) and `nimbus_role` (String). Both must be set before routing fires.
- `UserDefaults.standard` is used as the sole local persistence layer (no CoreData, no Keychain for non-secrets). The auth token is stored in UserDefaults, not Keychain.

**Recurring risk areas:**
1. UserDefaults race: role key and onboardingComplete key are written sequentially in sign-in VMs. SwiftUI `@AppStorage` observes changes synchronously on the main thread, so write order matters. Writing role first, then onboardingComplete, is the correct pattern. FIXED in d33407a for sign-in VMs (Google + Apple).
2. `onChange(of: isOnboardingComplete)` in NimbusAppApp triggers `habitViewModel.reload()` → `GET /users/me` → `applyUserDTO` → writes role to UserDefaults. PARTIALLY FIXED in d33407a: `applyUserDTO` now protects against server "solo" downgrading a non-solo local role, but a child user who calls `createFamily` (directly or via FamilyViewModel.loadFamily) could get silently promoted to parent in the DB.
3. Sign-out wipes UserDefaults keys and calls `viewModel.reset()` + `familyViewModel.reset()`. FIXED in d33407a.
4. Apple Sign-In `authorizationControllerDelegate` callback is NOT `@MainActor`. The `Task { }` inside it dispatches to an unstructured task. The class is `@MainActor` but the delegate callback is called on an arbitrary thread; the Task hop re-enters `@MainActor` via `await`. Safe in practice but fragile.
5. Token stored in UserDefaults (not Keychain). Accessible to iCloud backup and other processes.
6. NEW (d33407a): `FamilyViewModel.loadFamily()` calls `createFamily()` as a fallback if `getFamily()` fails (404 or network error). Any authenticated user — including a child — can trigger this and end up calling POST /family. The server now silently promotes any caller to Parent on CreateFamily (idempotency change). This is a privilege escalation path.
7. NEW (d33407a): `createFamily()` called with `try?` in `registerAndComplete`. If it fails, the user has `nimbus_role="parent"` and a valid auth token but NO family in the DB. `ParentDashboardView.task` calls `loadFamily()` which falls back to `createFamily()` again — so self-healing in the happy path, but creates a second family creation attempt on every dashboard load until it succeeds.

**Why:** Observed during 2026-04-12 review of commit d33407a (routing bug fix).
