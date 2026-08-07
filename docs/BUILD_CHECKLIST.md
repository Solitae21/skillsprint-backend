# SkillSprint — Daily Build Checklist

The execution view of [BACKEND_ROADMAP.md](./BACKEND_ROADMAP.md) §13. The roadmap explains *why*;
this file answers *what do I do when I sit down today*.

**One entry = one sitting of roughly an hour.** 66 of them, in order.

---

## How to use this

- **Go in order.** Each day assumes the previous one compiles. The ordering is deliberate: nothing
  can be tested until the database answers (Day 1), and nothing can be protected until tokens exist
  (Phase 1).
- **Every day has a `Done when`.** It is always something you can run or observe. If you can't
  demonstrate it, the day isn't finished — carry it into tomorrow rather than ticking it.
- **If a day runs long, split it.** The one-hour estimate is a guess; the work is the truth. Half a
  day done properly beats a whole day rushed.
- **A missed day is a missed day.** Not a reason to restart, re-plan, or feel behind. Pick up at the
  next unticked box.
- **The `Concept` line is the point.** You are building this to learn C# and ASP.NET, not to have a
  course API. If a day's task is done but its concept still feels fuzzy, that's the thing worth
  another hour.

**Stuck?** The `reference/dotnet-implementation` branch is a complete build of Phases 0–4, parked for
exactly this. Diff against it *after* you've attempted the day, not before — reading the answer first
turns a practice session into a transcription session.

## Progress

```
Phase 0  Setup & Foundation   xxxxxxxxxx▢▢▢              10/13
Phase 1  Authentication       ▢▢▢▢▢▢▢▢▢▢▢▢▢▢▢▢           0/16
Phase 2  Courses              ▢▢▢▢▢▢▢▢▢▢▢                0/11
Phase 3  Enrollments          ▢▢▢▢▢▢                     0/6
Phase 4  Hardening            ▢▢▢▢▢▢▢▢▢▢▢▢               0/12
Phase 5  Ship                 ▢▢▢▢▢▢▢▢                   0/8
```

---

# Phase 0 — Setup & Foundation (Days 1–13)

Goal: an app that boots, connects to MongoDB, has its indexes, logs properly, and answers `/health`.

### Day 1 — Tools and the database container

- [x] **Do:** Confirm `dotnet --list-sdks` shows a 10.0.x entry and `docker compose version` works.
      Write `compose.yaml` with a single `mongo` service: pinned image tag, `27017:27017`, a named
      volume for `/data/db`, and a `healthcheck`. Bring it up with `docker compose up -d mongo`.
- **Roadmap:** §2.1, §11.1, §11.2
- **Concept:** Containers vs. installs — the volume is what separates the container's lifetime from
  your data's lifetime.
- **Trap:** ⚠️ Don't add a `version:` key at the top of the file; it's obsolete and current Compose
  warns about it. Most tutorials still have it.
- **Done when:** `docker compose ps` shows the service `healthy`, and you can connect from Compass or
  `mongosh mongodb://localhost:27017`.

### Day 2 — Solution and projects

- [x] **Do:** `dotnet new sln -n SkillSprint`, then create the four `src` projects and two `tests`
      projects listed in §2.2 and add them all to the solution.
- **Roadmap:** §2.2
- **Concept:** What a `.sln` actually is — a build grouping, not a compilation unit. Each `.csproj` is
  a separate assembly.
- **Trap:** ⚠️ `dotnet new webapi` defaults to minimal APIs; pass `--use-controllers` or the routing
  in §6 won't match what you build.
- **Done when:** `dotnet build` succeeds and `dotnet sln list` shows all six projects.

### Day 3 — Project references, pointing inward only

- [x] **Do:** Wire the references per §3: Application → Domain; Infrastructure → Application, Domain;
      Api → Application, Infrastructure. Domain references nothing.
- **Roadmap:** §3
- **Concept:** Layering enforced by the compiler rather than by discipline. This is the whole reason
  the solution is split into four projects instead of one.
- **Trap:** ⚠️ It's tempting to reference Infrastructure from Application "just for the interfaces".
  Don't — the interfaces live *in* Application and Infrastructure implements them. That inversion is
  the point.
- **Done when:** you add `using MongoDB.Driver;` to a scratch file in Application, run `dotnet build`,
  and watch it fail. Delete the line. That failure is the guarantee.

### Day 4 — Build configuration and formatting

- [x] **Do:** Add `global.json` pinning the SDK to 10.0.x, `Directory.Build.props` with `net10.0`,
      nullable, implicit usings, and warnings-as-errors, and an `.editorconfig`. Run `dotnet format`.
- **Roadmap:** §2.4
- **Concept:** MSBuild property inheritance — `Directory.Build.props` is imported by every project
  below it automatically, which is why no `.csproj` needs to repeat these settings.
- **Trap:** ⚠️ Turning on `TreatWarningsAsErrors` later is painful; turning it on now costs nothing
  because there's no code yet.
- **Done when:** `dotnet build` still passes with warnings-as-errors on, and `dotnet format
  --verify-no-changes` exits 0.

### Day 5 — NuGet packages

- [x] **Do:** Add the packages from the §2.3 table to their respective projects.
- **Roadmap:** §2.3
- **Concept:** Transitive dependencies — check what Domain ends up with (nothing) and why that
  matters for the guarantee you built on Day 3.
- **Trap:** ⚠️ Use `FluentValidation.DependencyInjectionExtensions`, **not** `FluentValidation.AspNetCore`.
  The latter is deprecated by its own author and most search results still recommend it.
- **Done when:** `dotnet restore` and `dotnet build` both succeed, and Domain's `.csproj` still has
  zero `PackageReference` entries.

### Day 6 — Typed options with startup validation

- [x] **Do:** Add `appsettings.json` with the settings surface from §2.6. Create a `MongoOptions`
      class and bind it with `.Bind(...).ValidateDataAnnotations().ValidateOnStart()`.
- **Roadmap:** §2.6
- **Concept:** The options pattern and `IOptions<T>` — configuration as an injected, typed object
  instead of string lookups scattered through the code.
- **Trap:** ⚠️ Without `ValidateOnStart()` a missing setting fails on the first request that needs it,
  in the middle of some unrelated feature, weeks later.
- **Done when:** you delete the `Mongo` section from `appsettings.json`, run the app, and it refuses
  to start with a message naming the missing setting. Put it back.

### Day 7 — Secrets

- [x] **Do:** `dotnet user-secrets init` on the Api project and set `Jwt:AccessSecret` and
      `Jwt:RefreshSecret` to two *different* long random strings. Add a `JwtOptions` class bound and
      validated the same way as Day 6.
- **Roadmap:** §2.6
- **Concept:** Configuration providers layering — user-secrets overrides `appsettings.json` in
  Development, environment variables override both in Production.
- **Trap:** ⚠️ User-secrets are stored in plain text in your user profile and are dev-only. They are
  not encryption, and they do not exist inside a container (§11.6).
- **Done when:** the app starts, and a temporary log line proves the secret is populated from
  user-secrets while `appsettings.json` contains no secret at all.

### Day 8 — MongoContext and BSON conventions

- [x] **Do:** Register `IMongoClient` as a **singleton** wrapped in a `MongoContext` exposing typed
      `IMongoCollection<T>` properties. Register the convention pack from §4.0 at startup.
- **Roadmap:** §4.0, §2.6
- **Concept:** DI lifetimes — the Mongo client is a singleton because it owns the connection pool;
  creating one per request exhausts connections under load.
- **Trap:** ⚠️ Conventions registered *after* a class map is built are silently ignored. Nothing
  errors; your fields just aren't camelCase. Register them before anything touches a collection.
- **Done when:** the app starts and a temporary endpoint (or breakpoint) confirms
  `ListCollectionNames()` returns without throwing.

### Day 9 — Domain primitives

- [x] **Do:** Create the `MongoDocument` base class (Id + CreatedAt + UpdatedAt) and `AppException`
      (status, code, message, details) in Domain.
- **Roadmap:** §4.0, §8
- **Concept:** Why Domain stays dependency-free — no `[Bson*]` attributes here; mapping is declared
  in Infrastructure with class maps.
- **Trap:** ⚠️ Resist adding `[BsonId]` to `MongoDocument`. It works, and it silently couples Domain
  to the driver, undoing Day 3.
- **Done when:** `dotnet build` passes and Domain still has no package references.

### Day 10 — The index initializer

- [x] **Do:** Write a `MongoIndexInitializer` implementing `IHostedService` (or `BackgroundService`)
      and register it. It can create nothing yet — each later phase adds its indexes here.
- **Roadmap:** §4.4
- **Concept:** Hosted services and the app lifetime — code that runs once at startup, before or
  alongside request serving.
- **Trap:** ⚠️ This is the single biggest trap in the whole port. Mongoose created indexes from the
  schema; the C# driver **never** creates anything implicitly. Every index in §4.4 must be declared
  here by hand or it does not exist.
- **Done when:** a log line from the initializer appears in the console on startup.

### Day 11 — `GET /health`

- [x] **Do:** Add `AddHealthChecks()` with a check that pings MongoDB, and map `GET /health`.
- **Roadmap:** §6, §12
- **Concept:** Liveness vs. readiness — "the process is up" and "the process can serve traffic" are
  different questions, and only the second one involves the database.
- **Trap:** ⚠️ A health check that opens a new Mongo connection each call is a self-inflicted load
  problem. Use the injected client.
- **Done when:** `curl localhost:<port>/health` returns 200 with Mongo up, and returns unhealthy after
  `docker compose stop mongo`. Start it again.

### Day 12 — Error handling and the response envelope

- [ ] **Do:** Add the `ApiResponse<T>` / `ApiErrorBody` / `Meta` records to Application, then an
      `IExceptionHandler` that turns `AppException` into an envelope-shaped response, wired with
      `app.UseExceptionHandler()`.
- **Roadmap:** §5, §8
- **Concept:** The ASP.NET Core middleware pipeline and where exceptions surface in it.
- **Trap:** ⚠️ Unknown exceptions must return a generic 500 — never echo `ex.Message` to the client.
  That is how stack traces and connection strings leak.
- **Done when:** a temporary endpoint that throws `new AppException(404, "NOPE", "test")` returns a
  404 in envelope shape, and one that throws `new Exception("secret")` returns a 500 with that string
  nowhere in the response body.

### Day 13 — Structured logging

- [ ] **Do:** Wire Serilog with `UseSerilogRequestLogging()` and enrich each request with a request id.
- **Roadmap:** §8
- **Concept:** Structured logging — you log an event with properties, not a formatted sentence, which
  is what makes logs queryable later.
- **Trap:** ⚠️ Never log tokens, passwords, or full request bodies. Decide this now, while there are
  no auth endpoints to be careless with.
- **Done when:** hitting `/health` produces one structured completion log with method, path, status,
  duration, and a request id.

> **Phase 0 done.** The app boots, talks to Mongo in a container, logs, and reports health. Commit.

---

# Phase 1 — Authentication (Days 14–29)

Goal: a real user can register and log in against a real database, with refresh-token rotation, reuse
detection, and a logout that actually revokes.

### Day 14 — The `User` entity

- [ ] **Do:** Add `User` and the `Role` enum to Domain. In Infrastructure, register the class map with
      the `_id` mapping from §4.0 and a serializer that persists roles as lowercase strings.
- **Roadmap:** §4.1, §4.0
- **Concept:** Serialization as a separate concern from the type — the same class, two representations.
- **Trap:** ⚠️ Store email lowercased and trimmed on write. A unique index treats `A@b.com` and
  `a@b.com` as different values, so normalizing at write time is what actually prevents duplicates.
- **Done when:** a throwaway insert produces a document in Compass with camelCase fields and
  `"role": "student"`.

### Day 15 — The user index and repository interface

- [ ] **Do:** Add the `users.email` unique index to `MongoIndexInitializer`. Define `IUserRepository`
      in `Application/Abstractions` — the methods the service will need, nothing more.
- **Roadmap:** §4.4, §3
- **Concept:** Dependency inversion — the interface belongs to the consumer's layer (Application), not
  the implementer's (Infrastructure).
- **Trap:** ⚠️ Don't let `FilterDefinition<T>` or any driver type appear in the interface signature;
  that would leak MongoDB into Application and break Day 3's guarantee.
- **Done when:** the app starts, and Compass shows the `email_unique` index on `users`.

### Day 16 — The user repository

- [ ] **Do:** Implement `MongoUserRepository`. Centralize `CreatedAt`/`UpdatedAt` assignment so no
      call site can forget it.
- **Roadmap:** §4.0, §3
- **Concept:** Repository as the only code that speaks MongoDB — filters, projections, and nothing
  resembling a business rule.
- **Trap:** ⚠️ `timestamps: true` has no driver equivalent. If you don't set these fields yourself,
  they are silently `default(DateTime)` forever.
- **Done when:** an integration-style scratch call creates a user and reads it back with both
  timestamps populated.

### Day 17 — Password hashing

- [ ] **Do:** Define `IPasswordHasher` in Application, implement it with `BCrypt.Net-Next` at cost 12
      in Infrastructure, and write a unit test for the hash/verify round trip.
- **Roadmap:** §9
- **Concept:** Why password hashing is deliberately *slow*, and what the cost factor actually controls.
- **Trap:** ⚠️ Never compare hashes with `==`. Use the library's `Verify` — bcrypt embeds the salt in
  the hash string, so equality comparison is both wrong and a timing leak.
- **Done when:** `dotnet test` passes a test proving the same password hashes to two different strings
  and both verify.

### Day 18 — Reading day: how the token scheme works

- [ ] **Do:** No code. Read §9 end to end and write yourself a short answer to: why is the refresh
      token an httpOnly cookie while the access token is not? What does rotation buy? What does reuse
      detection detect?
- **Roadmap:** §9, §1.1
- **Concept:** Access vs. refresh tokens, `jti`, and why revocation needs server state at all when
  JWTs are supposedly stateless.
- **Trap:** ⚠️ Skipping this day is tempting and expensive — Days 19–28 are much harder to write if
  the model isn't clear first.
- **Done when:** you have written the three answers down and they satisfy you.

### Day 19 — Signing access tokens

- [ ] **Do:** Define `ITokenService` in Application; implement access-token creation in Infrastructure
      with `sub`, `role`, `jti`, and a short expiry from `JwtOptions`.
- **Roadmap:** §9
- **Concept:** JWT structure — header, payload, signature — and that the payload is **encoded, not
  encrypted**. Anyone can read it.
- **Trap:** ⚠️ Never put anything secret in claims. Paste your token into a decoder and look at it;
  that's what the client can see too.
- **Done when:** a scratch call produces a token that decodes to the expected claims with a ~15 minute
  expiry.

### Day 20 — Refresh tokens and the sessions collection

- [ ] **Do:** Add refresh-token creation (own secret, own lifetime, its own `jti`), the
      `RefreshSession` entity, and both `sessions` indexes: unique `(userId, jti)` and a **TTL** index
      on `expiresAt`.
- **Roadmap:** §9, §4.4, §1.1
- **Concept:** MongoDB TTL indexes — the database expires documents for you, which is what replaces
  Redis's native key expiry here.
- **Trap:** ⚠️ A TTL index only works on a `DateTime` field, and Mongo's background reaper runs about
  every 60 seconds — expiry is "soon after", not exact. Never rely on it for security decisions; check
  `expiresAt` in code too.
- **Done when:** you insert a session with `expiresAt` a minute out, wait, and watch Mongo delete it.

### Day 21 — The token store

- [ ] **Do:** Define `ITokenStore` in Application; implement `MongoTokenStore` with create / find by
      `(userId, jti)` / delete one / delete all for a user. Store a **hash** of the token, not the
      token.
- **Roadmap:** §9, §1.1
- **Concept:** Treating a refresh token like a credential — a database dump shouldn't hand over live
  sessions.
- **Trap:** ⚠️ Bcrypt is the wrong tool here; it's slow by design and refresh happens on a hot path. A
  SHA-256 of a high-entropy random token is appropriate — the slow-hash argument applies to
  *low-entropy human passwords*.
- **Done when:** a session row exists in Compass with a `tokenHash` that doesn't match the token you
  hold.

### Day 22 — Register: DTO, validator, service

- [ ] **Do:** Add `RegisterRequest` + its FluentValidation validator + `AuthService.RegisterAsync`
      (normalize email, reject duplicates, hash, insert, mint a token pair).
- **Roadmap:** §5, §7, §9
- **Concept:** DTOs as `record` types and why the entity never crosses the HTTP boundary.
- **Trap:** ⚠️ Check-then-insert races. Two simultaneous registrations both pass the "does this email
  exist" check — the unique index is what actually stops it, so catch the duplicate-key error and
  translate it to a 409.
- **Done when:** a unit test with a mocked repository proves a duplicate email produces the right
  `AppException`.

### Day 23 — `POST /auth/register`

- [ ] **Do:** Add `AuthController` with the register endpoint. Return the access token in the body and
      set the refresh token as an httpOnly + Secure + SameSite cookie.
- **Roadmap:** §6, §9
- **Concept:** Cookie attributes as a security mechanism — `httpOnly` is specifically what stops XSS
  from reading the token.
- **Trap:** ⚠️ `SameSite=None` requires `Secure`, and browsers silently drop cookies that get this
  wrong. If the cookie "isn't being set", check this first.
- **Done when:** a request returns 201, the user is in Mongo, and browser devtools shows the refresh
  cookie flagged HttpOnly.

### Day 24 — `POST /auth/login`

- [ ] **Do:** Add `LoginRequest` + validator + `AuthService.LoginAsync` + the endpoint. Verify the
      password, mint a pair, store the session.
- **Roadmap:** §6, §9
- **Concept:** Reusing the token-minting path from register rather than duplicating it.
- **Trap:** ⚠️ Return the *same* error for "no such user" and "wrong password". Distinguishing them
  hands an attacker a list of valid emails.
- **Done when:** correct credentials return tokens; a wrong password and an unknown email return
  identical 401 responses.

### Day 25 — JWT bearer auth and `GET /auth/me`

- [ ] **Do:** Configure `AddJwtBearer` (validate signature, lifetime, issuer, audience), add
      `UseAuthentication()`/`UseAuthorization()`, and add `[Authorize] GET /auth/me`.
- **Roadmap:** §9, §6
- **Concept:** Authentication vs. authorization, and reading the current user from `ClaimsPrincipal`.
- **Trap:** ⚠️ Middleware order: `UseAuthentication()` must come before `UseAuthorization()`, and both
  before endpoint mapping. Wrong order gives 401s that look like a token bug.
- **Done when:** `/auth/me` returns your user with a valid token, 401 without one, and 401 with a
  token whose signature you've tampered with.

### Day 26 — `POST /auth/refresh` with rotation

- [ ] **Do:** Read the refresh cookie, validate it, look up the session by `(userId, jti)`, then
      **delete the old row and insert a new one** with a fresh `jti`. Return a new pair and reset the
      cookie.
- **Roadmap:** §9
- **Concept:** Rotation — a refresh token is single-use, which is what makes theft detectable.
- **Trap:** ⚠️ Delete-then-insert must not leave a window where a legitimate retry finds nothing. Get
  the ordering right and think about what a double-submit from a flaky network does.
- **Done when:** refreshing returns a new pair, and replaying the *same* refresh token a second time
  no longer works.

### Day 27 — Reuse detection

- [ ] **Do:** When a presented `jti` is valid-looking but **not found** in `sessions`, delete every
      session for that user.
- **Roadmap:** §9
- **Concept:** Why "not found" means "already used" — and why already-used implies either theft or a
  replay, both of which warrant a global logout.
- **Trap:** ⚠️ Verify the token's signature *before* acting on its `sub`. Otherwise anyone can forge a
  payload and log out any user at will — a denial-of-service you built yourself.
- **Done when:** a test logs in twice, refreshes with the older token, and confirms both sessions are
  gone.

### Day 28 — Logout and the access-token denylist

- [ ] **Do:** Add the `denylist` collection with its unique `jti` + TTL indexes. On logout, insert the
      access token's `jti` with the token's remaining lifetime, delete the session, clear the cookie.
      Reject denylisted `jti`s in `JwtBearerEvents.OnTokenValidated`.
- **Roadmap:** §9, §4.4, §1.1
- **Concept:** `JwtBearerEvents` as the hook for post-validation checks — this is what makes logout
  revoke a still-valid token instead of just discarding it client-side.
- **Trap:** ⚠️ Set `expiresAt` to the *access token's* remaining lifetime, not the refresh token's.
  Longer is wasted storage; shorter is a revoked token that comes back to life.
- **Done when:** you log out, immediately reuse the old access token on `/auth/me`, and get 401.

### Day 29 — End-to-end auth walkthrough

- [ ] **Do:** No new features. Run the whole flow by hand: register → me → refresh → me with the new
      token → logout → confirm both the old access token and the refresh cookie are dead. Fix what
      you find.
- **Roadmap:** §9
- **Concept:** Integration thinking — six endpoints that each pass in isolation can still fail as a
  sequence.
- **Trap:** ⚠️ Check the *cookie* path and expiry too, not just the response bodies. A cookie scoped
  to the wrong path fails only in the browser, never in curl.
- **Done when:** the full sequence behaves correctly twice in a row from a clean database.

> **Phase 1 done.** Real users, real sessions, real revocation. Commit.

---

# Phase 2 — Courses (Days 30–40)

Goal: the endpoints the frontend actually calls, cached, searchable, and seeded with real data.

### Day 30 — The `Course` entity and its indexes

- [ ] **Do:** Add `Course` to Domain with its class map, plus two indexes: `slug` unique and a
      **text** index on `courseName`.
- **Roadmap:** §4.2, §4.4
- **Concept:** MongoDB text indexes — what they tokenize and why a collection can only have one.
- **Trap:** ⚠️ Without the text index, `?q=` doesn't degrade — it throws at runtime, and only for
  searching users. Create it now.
- **Done when:** both indexes appear in Compass with the names you gave them.

### Day 31 — The course repository

- [ ] **Do:** Define `ICourseRepository` and implement `GetBySlugAsync` and `CreateAsync`.
- **Roadmap:** §3, §7
- **Concept:** `Builders<T>.Filter` and typed filter definitions instead of raw JSON queries.
- **Trap:** ⚠️ Never build a filter by parsing user input into a `BsonDocument` — that reintroduces
  NoSQL injection, the exact class of bug the C# port removes for free (§9).
- **Done when:** a scratch call inserts a course and fetches it back by slug.

### Day 32 — `CourseDto` and `GET /courses/{id}`

- [ ] **Do:** Add `CourseDto` mapping `Slug` → `Id`, and the get-one-by-slug endpoint returning it in
      the envelope.
- **Roadmap:** §5, §6, §1
- **Concept:** The DTO as an anti-corruption layer — storage shape and API shape are allowed to differ.
- **Trap:** ⚠️ This is *the* frontend contract. `id` in the response must be the slug, never the
  ObjectId. Getting this wrong breaks the React app in a way that looks like a frontend bug.
- **Done when:** `GET /api/v1/courses/intro-to-javascript` returns JSON whose `id` is the slug, with
  no `_id` field anywhere in it.

### Day 33 — The seeder

- [ ] **Do:** Port the frontend's `src/data/courses.ts` array into a `CourseSeeder`, run behind
      `dotnet run --project src/SkillSprint.Api -- seed`. Upsert on `slug` so it's idempotent.
- **Roadmap:** §8, §12
- **Concept:** Reading command-line args in a `Program.cs` that also hosts a web app — branch before
  building the full pipeline.
- **Trap:** ⚠️ Idempotent means *runnable twice*. Insert-only seeders explode on the unique index the
  second time; upsert on slug.
- **Done when:** running seed twice in a row succeeds both times and leaves exactly one copy of each
  course.

### Day 34 — `GET /courses` with pagination

- [ ] **Do:** Add the list endpoint with `page` and `limit`, returning the `meta` block with `total`.
      Cap `limit` at 100.
- **Roadmap:** §6, §5
- **Concept:** Skip/limit paging and why it needs a separate count query for `total`.
- **Trap:** ⚠️ An uncapped `?limit=` is a trivial DoS against your own database. Cap it in the
  validator *and* clamp it in the service — the endpoint isn't the only caller.
- **Done when:** `?page=2&limit=2` returns the right slice with correct `meta`, and `?limit=99999`
  returns at most 100.

### Day 35 — Search and sort

- [ ] **Do:** Add `?q=` using the text index and `?sort=` over an allowlist of fields.
- **Roadmap:** §6, §4.4
- **Concept:** Text search scoring, and combining a text filter with other filters in one query.
- **Trap:** ⚠️ Allowlist the sort field. Passing user input straight into a sort definition lets a
  caller sort by any field — including ones you never indexed, which quietly turns into a collection
  scan.
- **Done when:** `?q=javascript` returns matching courses and `?sort=bogus` is rejected rather than
  passed through.

### Day 36 — `ICacheService`

- [ ] **Do:** Define `ICacheService` (`GetOrSetAsync`, `RemoveAsync`) in Application and implement it
      over `HybridCache` in Infrastructure. Make it **fail open**: log and call the loader if the
      cache throws.
- **Roadmap:** §7
- **Concept:** Cache-aside, and the stampede protection `HybridCache` gives you — concurrent misses on
  one key collapse into a single loader call.
- **Trap:** ⚠️ A cache that throws must not take the API down. Every catch here logs and falls through
  to the loader.
- **Done when:** a unit test with a deliberately throwing cache still returns the loader's value.

### Day 37 — Cache-aside on get-by-slug

- [ ] **Do:** Route `GetBySlugAsync` in `CourseService` through `GetOrSetAsync` with key
      `course:{slug}` and the configured TTL.
- **Roadmap:** §7, §3
- **Concept:** Why the *service* owns caching — controllers and repositories never touch it, so the
  logic lives in exactly one place per resource.
- **Trap:** ⚠️ Don't cache a null/not-found as if it were a hit unless you mean to. Decide
  deliberately; a cached 404 outlives the course you just created.
- **Done when:** logs show a repository call on the first request for a slug and none on the second.

### Day 38 — Cache-aside on the list, with a version counter

- [ ] **Do:** Cache list results under `courses:list:v{version}:{hash of q,page,limit,sort}`. Add the
      version counter the writes will bump.
- **Roadmap:** §7
- **Concept:** Cache key design — every input that changes the result must be in the key, or you serve
  page 1 to someone asking for page 2.
- **Trap:** ⚠️ Hash the query parameters into the key rather than concatenating raw user input; `q`
  can contain anything, including cache-key-breaking characters.
- **Done when:** two different query strings produce two different cached entries, and repeating
  either one hits the cache.

### Day 39 — Writes and invalidation

- [ ] **Do:** Add create / update / delete, restricted with `[Authorize(Roles = "Instructor,Admin")]`
      (delete: admin only). Each write removes `course:{slug}` and bumps the list version.
- **Roadmap:** §6, §7, §9
- **Concept:** Role-based authorization via attributes — this replaces a hand-written middleware
  entirely.
- **Trap:** ⚠️ Bumping a version counter orphans old list keys in O(1) instead of scanning the
  keyspace for a prefix. Keep this approach even after Redis arrives — it works on any backend.
- **Done when:** you create a course as an instructor and it appears in the list on the *next*
  request, not after the TTL; a student role gets 403.

### Day 40 — Categories and topics

- [ ] **Do:** Add `GET /categories` and `GET /topics`, distinct over the course collection, cached
      under the longer `CategoriesTtlSeconds`.
- **Roadmap:** §6, §7
- **Concept:** Choosing a TTL from how often data actually changes, not from a default.
- **Trap:** ⚠️ These keys need invalidating on course writes too — they're derived from courses. Easy
  to forget on Day 39's code path; go back and add them.
- **Done when:** both endpoints return the values the seeded data implies, and creating a course with
  a brand-new category makes it appear.

> **Phase 2 done.** The frontend's course screens could run on this today. Commit.

---

# Phase 3 — Enrollments (Days 41–46)

Goal: the join between users and courses, with ownership enforced and double-enrolment impossible.

### Day 41 — The `Enrollment` entity

- [ ] **Do:** Add `Enrollment`, `EnrollmentStatus`, the class map storing `userId`/`courseId` as
      ObjectIds, and the unique `(userId, courseId)` index.
- **Roadmap:** §4.3, §4.4, §4.0
- **Concept:** Modelling a many-to-many relationship as its own collection, and compound indexes.
- **Trap:** ⚠️ The foreign keys are `string` in C# but must be stored as **ObjectId**, or joins and
  filters silently match nothing. This is what the explicit class map in §4.0 is for.
- **Done when:** Compass shows the compound unique index, and an inserted document has ObjectId-typed
  `userId` and `courseId`, not strings.

### Day 42 — The enrollment repository

- [ ] **Do:** Define `IEnrollmentRepository` and implement create, list-by-user, get-by-id, update,
      delete.
- **Roadmap:** §3, §7
- **Concept:** Keeping ownership checks *out* of the repository — it returns what you ask for; the
  service decides who's allowed.
- **Trap:** ⚠️ `GetByIdAsync` should not take a userId parameter. Mixing the authorization rule into
  the query hides it from the place that should be enforcing it.
- **Done when:** the project builds and a scratch call round-trips an enrollment.

### Day 43 — `POST /enrollments`

- [ ] **Do:** Add the endpoint and `EnrollmentService.EnrollAsync`: verify the course exists, insert,
      and translate a duplicate-key error into a 409.
- **Roadmap:** §6, §7
- **Concept:** Letting the database enforce the invariant and catching its error, rather than
  check-then-insert.
- **Trap:** ⚠️ Catch the *duplicate key* error specifically (code 11000) — a blanket
  `catch (MongoWriteException)` turns every write failure into a misleading 409.
- **Done when:** enrolling twice returns 201 then 409, and enrolling in a nonexistent course returns
  404.

### Day 44 — `GET /enrollments`

- [ ] **Do:** List the current user's enrollments with the course data attached, so the frontend gets
      something it can render directly.
- **Roadmap:** §6, §10
- **Concept:** `$lookup` (or two queries plus an in-memory join) — and honestly weighing which is
  clearer at this scale.
- **Trap:** ⚠️ Take the user id from the validated token's `sub`, never from a query parameter. A
  `?userId=` on this endpoint is an authorization hole shaped like a feature.
- **Done when:** two different logged-in users each see only their own enrollments.

### Day 45 — `PATCH /enrollments/{id}`

- [ ] **Do:** Update progress (0–100) and status, with the ownership check in the service.
- **Roadmap:** §6, §7
- **Concept:** Partial updates and validating a bounded numeric range.
- **Trap:** ⚠️ Updating someone else's enrollment must be **404, not 403**. A 403 confirms the record
  exists, which leaks information to someone probing ids.
- **Done when:** your own enrollment updates; another user's id returns 404 rather than 403.

### Day 46 — `DELETE /enrollments/{id}`

- [ ] **Do:** Unenroll, with the same ownership rule.
- **Roadmap:** §6
- **Concept:** Idempotency of DELETE — deleting something already gone is not an error worth failing.
- **Trap:** ⚠️ Reuse the ownership check from Day 45 rather than re-deriving it. Two copies of an
  authorization rule is one copy too many.
- **Done when:** unenrolling works, re-enrolling afterwards works (the unique index doesn't block a
  fresh row), and another user's id returns 404.

> **Phase 3 done.** Every endpoint the frontend needs now exists. Commit.

---

# Phase 4 — Hardening (Days 47–58)

Goal: the API stops being a happy-path demo. Nothing new is added; everything gets defended and tested.

### Day 47 — Validators everywhere

- [ ] **Do:** Write a FluentValidation validator for every request DTO that doesn't have one yet.
- **Roadmap:** §5, §8
- **Concept:** Validation as a separate object from the DTO — the shape stays readable, the rules
  compose.
- **Trap:** ⚠️ Validate the query-parameter objects too, not just JSON bodies. `page` and `limit` are
  user input like any other.
- **Done when:** every request record in the solution has a matching validator class.

### Day 48 — The validation filter

- [ ] **Do:** Add an action filter that runs the validator for the bound model and short-circuits with
      envelope-shaped errors. Override `InvalidModelStateResponseFactory` so model-binding failures
      match too.
- **Roadmap:** §5, §8
- **Concept:** Action filters and where they sit relative to model binding.
- **Trap:** ⚠️ Binding failures (a string where an int belongs) never reach your validator — they fail
  earlier. Without the factory override, those responses have a different shape than everything else,
  and the frontend's error handling breaks on exactly one case.
- **Done when:** a missing required field and a malformed `?limit=abc` both return the same envelope
  shape.

### Day 49 — Security headers and body limits

- [ ] **Do:** Add `NetEscapades.AspNetCore.SecurityHeaders` and cap the request body size on JSON
      endpoints.
- **Roadmap:** §9
- **Concept:** What each header actually mitigates — worth reading rather than pasting a preset.
- **Trap:** ⚠️ A strict CSP can break the Scalar docs UI in Development. Configure the policy per
  environment rather than disabling it.
- **Done when:** response headers show the security set, and an oversized body is rejected with 413.

### Day 50 — CORS

- [ ] **Do:** Lock CORS to the configured frontend origin with `AllowCredentials()`.
- **Roadmap:** §9, §2.6
- **Concept:** Preflight requests and why credentialed cross-origin calls have stricter rules.
- **Trap:** ⚠️ `AllowAnyOrigin()` and `AllowCredentials()` are mutually exclusive by spec. If the
  refresh cookie "just doesn't send", this is why — the origin must be named explicitly.
- **Done when:** a real fetch from the Vite dev server at `localhost:5173` succeeds with
  `credentials: "include"`, and the same call from another origin is blocked.

### Day 51 — Rate limiting

- [ ] **Do:** Add `AddRateLimiter` with a global policy and a stricter named one on `/auth/*`.
- **Roadmap:** §9, §1.1
- **Concept:** Fixed-window vs. sliding-window vs. token-bucket, and which suits a login endpoint.
- **Trap:** ⚠️ These counters are per-process until Redis arrives (§1.1). Fine for one instance;
  remember it the day you scale to two.
- **Done when:** hammering `/auth/login` returns 429 while `/courses` still serves normally.

### Day 52 — Forwarded headers, HTTPS, HSTS

- [ ] **Do:** Add `UseForwardedHeaders` (`XForwardedFor | XForwardedProto`) **before** HTTPS
      redirection, and enable redirection + HSTS in Production only.
- **Roadmap:** §9, §11.5
- **Concept:** Running behind a reverse proxy — the app sees the proxy's connection, not the client's,
  until you tell it otherwise.
- **Trap:** ⚠️ Get this wrong and you get an infinite redirect loop in production and nowhere else,
  because locally there is no proxy. This is why it's on the list before you deploy, not after.
- **Done when:** the app runs normally in Development, and forcing Production locally with a
  simulated `X-Forwarded-Proto: https` header does not redirect-loop.

### Day 53 — Unit tests: `CourseService`

- [ ] **Do:** Test the course service with NSubstitute-mocked repository and cache: search, paging,
      not-found, and that a write invalidates.
- **Roadmap:** §12, §3
- **Concept:** The payoff for the repository seam — this is the test you could not write against
  `IMongoCollection<T>`.
- **Trap:** ⚠️ Assert on behavior, not on how many times a mock was called. Over-specified mock tests
  fail on every refactor and catch nothing.
- **Done when:** `dotnet test` passes and the tests still pass if you rename a private method.

### Day 54 — Unit tests: `AuthService` and `EnrollmentService`

- [ ] **Do:** Cover duplicate registration, wrong-password login, refresh rotation, reuse detection,
      duplicate enrollment, and the ownership rules.
- **Roadmap:** §12, §9
- **Concept:** Testing the paths that matter — the security rules are exactly the code you cannot
  afford to break silently.
- **Trap:** ⚠️ Test the reuse-detection *side effect* (all sessions deleted), not just the thrown
  exception. The exception is the visible part; the revocation is the point.
- **Done when:** every §9 rule has a named test asserting it.

### Day 55 — The integration test harness

- [ ] **Do:** Set up `WebApplicationFactory<Program>` with EphemeralMongo, overriding the Mongo
      connection string per test run. Append `public partial class Program { }` to `Program.cs`.
- **Roadmap:** §12, §3
- **Concept:** In-process integration testing — the real pipeline, real filters, real serialization,
  no port binding.
- **Trap:** ⚠️ Tests must not share database state. Give each fixture its own database name, or you'll
  spend a day debugging failures that only happen when tests run in a particular order.
- **Done when:** one integration test hits `/health` in-process and passes, with no Docker running.

### Day 56 — Integration tests: auth

- [ ] **Do:** Test register → login → me → refresh → logout as real HTTP, including cookie handling
      and reuse detection.
- **Roadmap:** §12, §9
- **Concept:** Testing cookies through `HttpClient` — the handler tracks them if you let it.
- **Trap:** ⚠️ This is the layer that catches middleware-order bugs. A unit test can't tell you
  `UseAuthentication` is in the wrong place.
- **Done when:** the whole Day 29 manual walkthrough now runs as an automated test.

### Day 57 — Integration tests: courses and enrollments

- [ ] **Do:** Test search, paging, the `limit` cap, role-gated writes, duplicate-enroll 409, and
      cross-user 404s.
- **Roadmap:** §12
- **Concept:** Seeding known data per test so assertions are exact rather than "at least one result".
- **Trap:** ⚠️ Test authorization negatively too. "Instructor can create" passing means nothing if
  "student can also create" was never checked.
- **Done when:** `dotnet test` is green and includes at least one 403 and one 404 assertion.

### Day 58 — OpenAPI and Scalar

- [ ] **Do:** Publish the OpenAPI document via `Microsoft.AspNetCore.OpenApi` and render it with
      Scalar in Development.
- **Roadmap:** §12, §5
- **Concept:** The generated document as a contract you can hand to a TypeScript client generator.
- **Trap:** ⚠️ Never expose the docs UI in Production. It's an endpoint map for anyone who finds it.
- **Done when:** the Scalar UI lists every endpoint with correct request/response schemas, and is
  absent when the environment is Production.

> **Phase 4 done.** Tested, validated, and defended. Commit.

---

# Phase 5 — Ship (Days 59–66)

Goal: it runs somewhere other than your machine, and the React app talks to it.

### Day 59 — CI

- [ ] **Do:** Add a GitHub Actions workflow: `dotnet format --verify-no-changes` → `dotnet build` →
      `dotnet test` on every push and PR.
- **Roadmap:** §12
- **Concept:** Workflow syntax, caching the NuGet directory, and failing fast on the cheapest step.
- **Trap:** ⚠️ No Docker service block is needed — EphemeralMongo handles the database (§11.7). Adding
  one is wasted CI minutes.
- **Done when:** a push shows a green check, and deliberately breaking formatting turns it red.

### Day 60 — `.dockerignore` and the Dockerfile

- [ ] **Do:** Write `.dockerignore` **first**, then the multi-stage Dockerfile with the restore-layer
      ordering from §11.3.
- **Roadmap:** §11.3, §11.4
- **Concept:** Build context and layer caching — why the copy order in a Dockerfile is a performance
  decision.
- **Trap:** ⚠️ `.dockerignore` before `Dockerfile`, genuinely. Build once without it and your host's
  `obj/` goes into the image and breaks `restore` in ways the error message won't explain.
- **Done when:** `docker build -t skillsprint-api .` succeeds, and a second build after a source-only
  edit reuses the restore layer.

### Day 61 — Harden and run the image

- [ ] **Do:** Add `USER $APP_UID` and `EXPOSE 8080`. Run the container against the Compose Mongo,
      passing `Mongo__ConnectionString` and the JWT secrets as environment variables.
- **Roadmap:** §11.5, §11.6, §2.6
- **Concept:** Environment-variable configuration overriding `appsettings.json` — the `__` separator
  from §2.6, now doing real work.
- **Trap:** ⚠️ Two at once here. The port is **8080**, not 80. And from inside the container Mongo is
  at `mongo:27017` or `host.docker.internal:27017` — never `localhost`.
- **Done when:** `docker run` serves `/health` as 200 from inside the container, and `whoami` in the
  container is not root.

### Day 62 — The full stack in Compose

- [ ] **Do:** Add the `api` service to `compose.yaml` under `profiles: [full]`, with a healthcheck and
      `depends_on: mongo: condition: service_healthy`. Read secrets from a gitignored `.env`.
- **Roadmap:** §11.2, §11.5, §11.6
- **Concept:** Compose profiles, service DNS on the default network, and start-order conditions.
- **Trap:** ⚠️ Plain `depends_on` waits for the container to *start*, not for Mongo to accept
  connections. Without `condition: service_healthy` this works on your warm machine and fails on a
  cold one.
- **Done when:** `docker compose --profile full up --build` from a stopped state serves `/health` and
  a course list, and plain `docker compose up` still starts Mongo alone.

### Day 63 — MongoDB Atlas

- [ ] **Do:** Create a free-tier cluster, a database user with least privilege, and a network access
      rule. Get the connection string and run the seeder against it once.
- **Roadmap:** §12
- **Concept:** Connection strings for a replica set, and why Atlas requires TLS.
- **Trap:** ⚠️ `0.0.0.0/0` in network access is the fastest way to get moving and the wrong thing to
  leave in place. Narrow it once you know your host's egress addresses.
- **Done when:** the local API, pointed at Atlas via an env var, serves the seeded course list.

### Day 64 — Deploy

- [ ] **Do:** Deploy to Render / Railway / Fly.io from the Dockerfile. Set `Mongo__ConnectionString`
      and both `Jwt__*` secrets in the platform dashboard. Confirm HTTPS and HSTS behave (Day 52).
- **Roadmap:** §12, §11.5
- **Concept:** Deploying an image rather than source — what runs in production is what you tested.
- **Trap:** ⚠️ If you get a redirect loop, it's Day 52's forwarded headers, not your deploy config.
  Check that before changing anything else.
- **Done when:** the public URL serves `/health` over HTTPS and returns a course list.

### Day 65 — Frontend: courses

- [ ] **Do:** In the frontend repo, add `VITE_API_URL` and a small fetch wrapper that attaches the
      access token and sends `credentials: "include"`. Swap the bodies of `courseService.ts` — the
      callers must not change.
- **Roadmap:** §10
- **Concept:** The seam paying off — `courseService.ts` was designed as the single swap point.
- **Trap:** ⚠️ The API returns the `ApiResponse` envelope; the frontend expects bare arrays. Unwrap
  `data` in the fetch wrapper, in one place, not in every caller.
- **Done when:** the React app lists and searches courses from the deployed API with no component
  changes.

### Day 66 — Frontend: auth, and done

- [ ] **Do:** Replace the localStorage-email stub with real register/login. Keep the access token **in
      memory** and rely on the refresh cookie to re-hydrate the session on reload. Wire enrollment to
      the real endpoints.
- **Roadmap:** §10, §9
- **Concept:** Why the access token lives in memory rather than localStorage — anything readable by JS
  is readable by injected JS.
- **Trap:** ⚠️ A page refresh clears the in-memory token, so the app must call `/auth/refresh` on
  startup before deciding the user is logged out. Miss this and every reload looks like a logout.
- **Done when:** you register in the browser, enroll in a course, hard-refresh, and are still logged
  in with the enrollment intact.

> **Done.** The frontend runs on a real, persistent, secured backend that you wrote.

---

## After Day 66

Nothing here is required, and all of it is optional practice on a codebase you now know well. From
§12's *Future features*:

- Email verification and password reset (token + email — a new external dependency to design around)
- Instructor dashboards (a new authorization shape: "resources I own", not just "my role")
- Course ratings and reviews (a second join collection, plus aggregation for averages)
- Lessons within a course, and per-lesson progress (turns the flat `progress` int into real data)
- Revisit §1.1 and swap Redis in behind `ICacheService` / `ITokenStore` — the interfaces exist
  precisely so this is a two-file change
