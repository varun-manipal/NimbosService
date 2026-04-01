Architecture Plan: NimbusApp → .NET REST API + SQL Backend

Context

Currently all business logic and data live on-device using UserDefaults + in-memory Swift state. Moving to a .NET backend enables multi-device sync,
server-side analytics, and removes data loss on app reinstall.

 ---
SQL Schema

users

CREATE TABLE users (
id            UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
device_id     NVARCHAR(64)  NOT NULL UNIQUE,  -- or push token / auth token
name          NVARCHAR(100) NOT NULL,
vibe          NVARCHAR(10)  NOT NULL,          -- 'bestie' | 'boss'
list_pin      NVARCHAR(10)  NULL,
total_stars   INT           NOT NULL DEFAULT 0,
daily_stars   INT           NOT NULL DEFAULT 0,
last_opened   DATETIME2     NULL,              -- for daily reset logic
created_at    DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
updated_at    DATETIME2     NOT NULL DEFAULT GETUTCDATE()
);

tasks

CREATE TABLE tasks (
id                  UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
user_id             UNIQUEIDENTIFIER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
title               NVARCHAR(200) NOT NULL,
is_completed        BIT NOT NULL DEFAULT 0,
is_snoozed          BIT NOT NULL DEFAULT 0,
is_dismissed_today  BIT NOT NULL DEFAULT 0,
is_skipped_tomorrow BIT NOT NULL DEFAULT 0,
is_tomorrow_only    BIT NOT NULL DEFAULT 0,  -- tomorrowExtras (auto-deleted after next dawn)
last_updated        DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
created_at          DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
INDEX ix_tasks_user (user_id)
);

daily_snapshots

CREATE TABLE daily_snapshots (
id                    UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
user_id               UNIQUEIDENTIFIER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
snapshot_date         DATE NOT NULL,
completion_percentage FLOAT NOT NULL,
stars_lit             INT   NOT NULL,
created_at            DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
UNIQUE (user_id, snapshot_date),
INDEX ix_snapshots_user_date (user_id, snapshot_date)
);

shields

CREATE TABLE shields (
user_id    UNIQUEIDENTIFIER PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
fragments  INT NOT NULL DEFAULT 0,
is_active  BIT NOT NULL DEFAULT 0,
updated_at DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

 ---
.NET REST API Endpoints

Base URL: https://api.nimbusapp.com/v1

Auth

All requests carry a device token in the Authorization: Bearer {token} header. The token identifies the user row. (Can be upgraded to full OAuth
later.)

 ---
User

┌────────┬───────────┬─────────────────────────────────────┐
│ Method │   Path    │               Purpose               │
├────────┼───────────┼─────────────────────────────────────┤
│ POST   │ /users    │ Register (onboarding completion)    │
├────────┼───────────┼─────────────────────────────────────┤
│ GET    │ /users/me │ Fetch full user state on app launch │
├────────┼───────────┼─────────────────────────────────────┤
│ PATCH  │ /users/me │ Update name, vibe, pin              │
└────────┴───────────┴─────────────────────────────────────┘

POST /users — called once, at OnboardingViewModel.setPin()
Request:  { "deviceId": "...", "name": "Varun", "vibe": "bestie", "pin": "1234", "tasks": ["Hydrate","Meditate"] }
Response: { "userId": "uuid", "token": "...", "user": { ...UserDTO } }

GET /users/me — called on app foreground (replaces HabitViewModel.load())
Response: {
"name": "Varun", "vibe": "bestie",
"totalStars": 23, "dailyStars": 3,
"shield": { "fragments": 1, "isActive": false },
"tasks": [ { "id": "...", "title": "Hydrate", "isCompleted": true, ... } ],
"tomorrowExtras": [ ... ]
}

 ---
Tasks

┌────────┬─────────────────────────────┬───────────────────────────────────────────────────────┐
│ Method │            Path             │                        Purpose                        │
├────────┼─────────────────────────────┼───────────────────────────────────────────────────────┤
│ POST   │ /tasks                      │ Add a new recurring habit                             │
├────────┼─────────────────────────────┼───────────────────────────────────────────────────────┤
│ PATCH  │ /tasks/{id}                 │ Toggle complete/snooze/dismiss/skipTomorrow or rename │
├────────┼─────────────────────────────┼───────────────────────────────────────────────────────┤
│ DELETE │ /tasks/{id}                 │ Remove a habit permanently                            │
├────────┼─────────────────────────────┼───────────────────────────────────────────────────────┤
│ POST   │ /tasks/tomorrow-extras      │ Add one-time tomorrow task                            │
├────────┼─────────────────────────────┼───────────────────────────────────────────────────────┤
│ DELETE │ /tasks/tomorrow-extras/{id} │ Remove tomorrow-only task                             │
└────────┴─────────────────────────────┴───────────────────────────────────────────────────────┘

PATCH /tasks/{id} — used by all task actions
Request:  { "isCompleted": true }   // or isSnooze, isDismissed, isSkippedTomorrow, title
Response: { "task": {...TaskDTO}, "user": { "totalStars": 24, "dailyStars": 4 } }
Star counts are recalculated server-side on toggle.

 ---
Daily Lifecycle

┌────────┬────────────────────────────────┬─────────────────────────────────────────────────────────────────────┐
│ Method │              Path              │                               Purpose                               │
├────────┼────────────────────────────────┼─────────────────────────────────────────────────────────────────────┤
│ POST   │ /daily/new-day                 │ Trigger daily reset (replaces DailyRefreshViewModel.checkForNewDay) │
├────────┼────────────────────────────────┼─────────────────────────────────────────────────────────────────────┤
│ GET    │ /daily/snapshots?month=2026-03 │ Fetch snapshots for a calendar month                                │
└────────┴────────────────────────────────┴─────────────────────────────────────────────────────────────────────┘

POST /daily/new-day — called on app foreground when client detects date change
Request:  { "lastOpenedDate": "2026-03-28" }
Response: {
"wasNewDay": true,
"snapshot": { "date": "2026-03-28", "completionPercentage": 0.8, "starsLit": 4 },
"shield": { "fragments": 1, "isActive": false },
"tasks": [ ...reset tasks ]
}
Server handles: archive snapshot, reset task flags, apply isSkippedTomorrow → isSnoozed, merge tomorrow_only tasks, shield logic.

 ---
iOS Client Changes

New: APIClient.swift

A single networking layer (URLSession-based, async/await) replacing UserDefaults:

class APIClient {
static let shared = APIClient()
private let baseURL = URL(string: "https://api.nimbusapp.com/v1")!
private var token: String { UserDefaults.standard.string(forKey: "nimbus_token") ?? "" }

     func get<T: Decodable>(_ path: String) async throws -> T
     func post<B: Encodable, T: Decodable>(_ path: String, body: B) async throws -> T
     func patch<B: Encodable, T: Decodable>(_ path: String, body: B) async throws -> T
     func delete(_ path: String) async throws
}

Modified: HabitViewModel.swift

- Remove all UserDefaults read/write
- Replace save() with targeted PATCH /tasks/{id} or PATCH /users/me calls
- Replace load() with GET /users/me on init (async)
- Add @Published var isLoading: Bool and @Published var error: String? for UI feedback
- Keep @Published properties — they still drive the UI, just populated from API responses

Modified: DailyRefreshViewModel.swift

- checkForNewDay() → posts to POST /daily/new-day instead of resetting locally
- Response carries the reset task list and shield state — update HabitViewModel from response

Modified: OnboardingViewModel.swift

- setPin() → calls POST /users instead of building local HabitViewModel
- Store returned token in UserDefaults (the only thing persisted locally)

Modified: HistoryViewModel.swift

- reload() → calls GET /daily/snapshots?month=... instead of DailyRefreshViewModel.loadSnapshots()

Unchanged

- All Views — no view code changes needed
- NotificationViewModel — stays local (UNUserNotificationCenter is device-only)
- All animation/haptic logic — stays in ViewModels as-is
- ConstellationViewModel — still just local selection state during onboarding

 ---
Offline Resilience (Optional Phase 2)

- Cache last-known state in UserDefaults as a fallback JSON blob
- Queue failed mutations in a local write-ahead log, flush on next foreground
- Optimistic UI updates (update local state immediately, reconcile with server response)

 ---
Verification

1. Onboarding: Fresh install → complete onboarding → verify user row + tasks created in DB
2. Toggle task: Complete a habit → verify tasks row updated, users.total_stars incremented
3. New day: Change device date → foreground app → verify snapshot archived, tasks reset, shield updated
4. History: Open calendar → verify correct snapshots returned for the month
5. Tomorrow planner: Skip a habit, add an extra → next-day reset → verify skip became snoozed, extra appeared in tasks
6. Multi-device: Sign in on second device → verify same state loaded from GET /users/me