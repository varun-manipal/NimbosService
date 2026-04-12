# Agent Memory Index

- [NimbosService Architecture Overview](project_architecture.md) — auth model (token=GUID), domain, tech stack, no service layer
- [Security Anti-Patterns](security_patterns.md) — token=GUID, unverified Apple auth, Console.WriteLine leaks, plaintext PIN, no rate limiting
- [Performance Anti-Patterns](performance_patterns.md) — missing AsNoTracking, double round-trips in Register/NewDay/UpdateTask, IsParentOf double-query
