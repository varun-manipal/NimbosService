---
name: Security Anti-Patterns in NimbosService
description: Recurring security weaknesses found in the first full review (2026-04-11)
type: project
---

**Critical recurring issues to check on every review:**

1. Token = user GUID. The bearer token sent by the client is literally the user's database primary key. Anyone who knows (or guesses) a UUID can impersonate that user. No HMAC, no expiry, no revocation.

2. Apple Sign-In is NOT cryptographically verified. The `AppleAuth` endpoint accepts `UserIdentifier` from the request body and does a direct DB lookup — no JWT verification against Apple's public keys. Contrast with Google where tokeninfo is called.

3. `Console.WriteLine` leaks the raw Google Client ID and the token audience value in production logs (AuthController lines 56–57). Sensitive config data in stdout.

4. `GoogleAuthResponse` returns `Token: user.Id.ToString()` — the GUID token — which is the security hole described in point 1.

5. No rate limiting on any endpoint. Auth endpoints, invite code generation, and task creation are all unprotected.

6. `ListPin` is stored in plaintext in the User model. Should be hashed.

**Why:** These were all found in the 2026-04-11 review of commit a00b2ab.
