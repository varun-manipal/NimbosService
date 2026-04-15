---
name: Performance Anti-Patterns in NimbosService
description: Recurring EF Core and query efficiency issues found in the first full review (2026-04-11)
type: project
---

**Recurring patterns to watch:**

1. Missing `AsNoTracking()` on read-only queries. `GetMe`, auth login flows, `GetFamily`, `GetChildren`, `GetSnapshots`, and `GetChild` all load entities into the change tracker unnecessarily.

2. Double DB round-trip in `Register` (UsersController): tasks and shield are added via `_db.Tasks.Add` / `_db.Shields.Add` then fetched again with `_db.Tasks.Where(...).ToListAsync()` and `_db.Shields.FindAsync(...)` immediately after `SaveChangesAsync`. The in-memory objects are already available.

3. Extra DB query in `NewDay` (DailyController): after `SaveChangesAsync`, a second `_db.Tasks.Where(...)` is issued to re-fetch the updated tasks. The in-memory `regularTasks` list already reflects all changes.

4. Two separate DB queries in `UpdateTask` (TasksController): one for the task (`FirstOrDefaultAsync`), then a second `FindAsync` for the user. These could be a single join query or at minimum batched.

5. `GetParentFamily` in FamilyController is called once per action AND `IsParentOf` is called separately — two DB round trips for overlapping data on `GetChild`, `AddTaskToChild`, `UpdateChildTask`, `DeleteChildTask`. `IsParentOf` itself makes two queries internally.

6. No `AsNoTracking` on `GetChildren` — loads full User + Tasks + Shield entities with change tracking for a read-only response.

**Why:** Found in 2026-04-11 review. No service layer, all EF Core access is direct in controllers.
