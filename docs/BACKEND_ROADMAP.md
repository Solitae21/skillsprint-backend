# SkillSprint — Backend Roadmap (.NET)

A step-by-step roadmap for building the **SkillSprint API**: the backend that powers
course discovery, authentication, and enrollment for the existing React frontend.

> **Canonical copy.** This document lives in the backend repo (`skillsprint-backend`) and is the
> authoritative version. An older TypeScript/Express copy still exists in the frontend repo at
> `SkillSprint/docs/BACKEND_ROADMAP.md` — it describes a stack that was never built out and should be
> treated as historical.

> **Scope of this document:** This is a planning/learning guide. It does not contain the
> backend code itself — it tells you _how_ to build it, in what order, and why.

---

## 1. Overview & Goals

SkillSprint is a course discovery + enrollment platform. The frontend is already built
(React + TypeScript + Vite) but runs entirely on stubbed local data:

- Courses come from a static array via `src/services/courseService.ts`.
- "Auth" stores only an **email** in `localStorage` and ignores passwords.
- Enrollment is **in-memory** (`src/hooks/useCourses.ts`) and is lost on refresh.

The backend's job is to replace those stubs with a real, persistent, secure API.

**The contract that must not break:** the frontend already expects a `Course` shaped like
this (`src/types/index.ts`):

```ts
interface Course {
  id: string;
  courseName: string;
  instructor: string;
  thumbnail: string;
}
```

Your API responses must be able to produce this shape, so the frontend swap is painless.
Note that `id` is a **slug** like `"intro-to-javascript"`, not a database identifier.

### Tech decisions

| Concern        | Choice                                 | Why                                                                   |
| -------------- | -------------------------------------- | --------------------------------------------------------------------- |
| Language       | C# 14                                  | Strong static typing, records, nullable reference types               |
| Runtime/Server | .NET 10 (LTS) + ASP.NET Core           | LTS through Nov 2028; first-class DI, config, hosting, and auth       |
| API style      | MVC Controllers (`[ApiController]`)    | Maps 1:1 onto the route → controller → service layering in §3         |
| Database       | MongoDB + official C# driver 3.x       | Flexible documents; typed `FilterDefinition<T>` builders              |
| Cache          | `HybridCache` behind `ICacheService`   | In-box cache-aside with stampede protection; Redis is a later swap    |
| Auth           | JWT (access + refresh) via `JwtBearer` | Stateless, standard for SPA + API                                     |
| Validation     | FluentValidation                       | Composable, expressive rules kept out of the DTOs themselves          |
| Logging        | Serilog                                | Structured JSON logs + per-request enrichment                         |
| Testing        | xUnit + `WebApplicationFactory`        | In-process integration tests without binding a real port              |
| Containers     | Docker + Compose (multi-stage)         | No local Mongo install; you deploy the image you tested               |
| Repo layout    | Separate repo (`skillsprint-backend`)  | Independent deploys; clean separation of concerns                     |

> **Historical note.** This API was originally scaffolded in TypeScript on Node/Express with
> Mongoose, zod, and Redis. That scaffold never got past two Mongoose schemas and a hello-world
> handler, and was replaced wholesale by the .NET stack above. The final Node state is preserved
> under the git tag `node-express-final`. MongoDB is unchanged and carried over.

### 1.1 Deferred: Redis

The original design leaned on Redis for four jobs. Each has an in-box substitute that needs no extra
infrastructure, so **Redis is deferred** — every use sits behind an interface and can be swapped
later without touching service code.

| Redis was going to… | Instead, for now | Swapping Redis back in |
| --- | --- | --- |
| Store refresh-token sessions with native TTL expiry | A MongoDB `sessions` collection with a **TTL index** on `expiresAt` — documents self-delete, no cleanup job. Rotation and reuse detection are unchanged (§9). | Reimplement `ITokenStore` |
| Hold the logout denylist | A `denylist` collection, same TTL-index trick | Reimplement `ITokenStore` |
| Cache-aside for hot GETs | `HybridCache`, which adds request-collapsing the Redis design never had | Register a distributed-cache backplane; `ICacheService` is untouched |
| Back a distributed rate limiter | The in-box `AddRateLimiter` (per-process) | Needed only once more than one instance runs |

The one real loss is that rate-limit counters are per-instance rather than shared. That does not
matter until the API is horizontally scaled.

---

## 2. Project Setup

### 2.1 Prerequisites

```bash
dotnet --list-sdks     # expect a 10.0.x entry
```

Install the .NET 10 SDK if it is missing (`winget install Microsoft.DotNet.SDK.10` on Windows).

**Docker Desktop** is the second prerequisite. The database comes from a container rather than a
local install — `docker compose up -d mongo` is all it takes (§11). A locally installed MongoDB still
works if you prefer one; only the connection string differs. The tests spin up their own database and
need **no** Docker daemon at all (§12).

> If `dotnet run` fails with a Mongo connection timeout, the cause is almost always Docker Desktop
> not being started.

### 2.2 Solution & projects

```bash
dotnet new sln -n SkillSprint

dotnet new classlib -o src/SkillSprint.Domain
dotnet new classlib -o src/SkillSprint.Application
dotnet new classlib -o src/SkillSprint.Infrastructure
dotnet new webapi   -o src/SkillSprint.Api --use-controllers
dotnet new xunit    -o tests/SkillSprint.UnitTests
dotnet new xunit    -o tests/SkillSprint.IntegrationTests
```

Then wire the references so the dependency arrows only ever point inward (see §3).

### 2.3 NuGet packages

| Project | Packages |
| --- | --- |
| `Domain` | *(none — deliberately dependency-free)* |
| `Application` | `FluentValidation`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Options` |
| `Infrastructure` | `MongoDB.Driver`, `Microsoft.Extensions.Caching.Hybrid`, `BCrypt.Net-Next`, `System.IdentityModel.Tokens.Jwt` |
| `Api` | `Microsoft.AspNetCore.Authentication.JwtBearer`, `Serilog.AspNetCore`, `NetEscapades.AspNetCore.SecurityHeaders`, `FluentValidation.DependencyInjectionExtensions`, `Scalar.AspNetCore` |
| Tests | `Microsoft.AspNetCore.Mvc.Testing`, `EphemeralMongo`, `NSubstitute`, `Shouldly` |

> Use `FluentValidation.DependencyInjectionExtensions`, **not** the old `FluentValidation.AspNetCore`
> auto-validation package — that one is deprecated by its author. Wire validation through an action
> filter instead (§9).

### 2.4 Build configuration

Two files at the repo root do what `tsconfig.json` used to:

- **`global.json`** pins the SDK to `10.0.x` so installing another SDK later can't silently change
  the build. (An 8.0.x SDK is also present on some machines — without the pin, `dotnet build` may
  quietly pick it.)
- **`Directory.Build.props`** applies to every project: `net10.0`, `<Nullable>enable</Nullable>`,
  `<ImplicitUsings>enable</ImplicitUsings>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
  Nullable reference types + warnings-as-errors together are the closest thing to `"strict": true`.

`.editorconfig` replaces ESLint + Prettier. `dotnet format` replaces `prettier --write`.

### 2.5 Everyday commands

| Task | Command |
| --- | --- |
| Start MongoDB | `docker compose up -d mongo` |
| Stop it (keeps data) | `docker compose down` |
| Stop it and wipe data | `docker compose down -v` |
| Tail Mongo logs | `docker compose logs -f mongo` |
| Run the API | `dotnet run --project src/SkillSprint.Api` |
| Run with hot reload | `dotnet watch --project src/SkillSprint.Api` |
| Build everything | `dotnet build` |
| Test | `dotnet test` |
| Format | `dotnet format` |
| Seed the database | `dotnet run --project src/SkillSprint.Api -- seed` |
| Run the whole stack in containers | `docker compose --profile full up --build` |

### 2.6 Configuration & secrets

Never read configuration ad hoc. Bind it **once** at boot into a typed options object and validate
it, so a missing setting fails at startup rather than on the first request that needs it:

```csharp
builder.Services
    .AddOptions<MongoOptions>()
    .Bind(builder.Configuration.GetSection("Mongo"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

`appsettings.json` holds non-secret defaults and **is committed**. Real secrets go in user-secrets,
which live outside the repo entirely:

```bash
dotnet user-secrets init  --project src/SkillSprint.Api
dotnet user-secrets set "Jwt:AccessSecret"  "<64+ random chars>" --project src/SkillSprint.Api
dotnet user-secrets set "Jwt:RefreshSecret" "<a different 64+>"  --project src/SkillSprint.Api
```

Settings surface:

```jsonc
{
  "Mongo":  { "ConnectionString": "mongodb://localhost:27017", "Database": "skillsprint" },
  "Jwt":    { "Issuer": "skillsprint", "Audience": "skillsprint-web",
              "AccessTokenMinutes": 15, "RefreshTokenDays": 7 },
  "Cors":   { "Origin": "http://localhost:5173" },
  "Cache":  { "CoursesTtlSeconds": 300, "CategoriesTtlSeconds": 3600 }
}
```

In production, every one of these can be overridden by an environment variable using `__` as the
section separator (`Mongo__ConnectionString`), which is how hosting platforms inject them.

⚠️ **The connection string depends on where the code is running.** From the **host** (`dotnet run`,
`dotnet watch`) Mongo is at `mongodb://localhost:27017`, which is the committed default above. From
**inside a container** on the Compose network it is `mongodb://mongo:27017` — the Compose service
name is the DNS name, and `localhost` inside a container means *that container*, not your machine.
This is the single most common Docker-plus-Mongo failure. No `appsettings.json` change is needed for
it: Compose sets `Mongo__ConnectionString` as an environment variable on the `api` service, using
exactly the `__` override mechanism described above (§11).

---

## 3. Architecture (Layered)

Keep each layer with a single responsibility. A request flows in one direction:

```
HTTP request
   │
   ▼
[ controller ]  ── attributes define the route; filters handle auth + validation
   │              reads the request, calls a service, shapes the HTTP response
   ▼
[ service ]  ── business logic (the "what should happen"). No HttpContext here.
   │         checks the cache first (cache-aside) ──► [ cache ] ── hit? return it
   │         miss/write ▼
   ▼
[ repository ]  ── the only code that speaks MongoDB: filters, projections, indexes
   │
   ▼
MongoDB
```

**Golden rules:**

- Controllers know about HTTP; services do not.
- Services contain the business rules and are the only callers of repositories.
- Repositories define queries and data access only — **no business logic**.
- **Only the service layer touches the cache** — controllers and repositories never do.
  This keeps cache-aside logic (check → miss → load → backfill → invalidate) in one place per resource.
- This separation is what makes services easy to unit-test.

### Project layout

The rules above aren't just convention here — **project references enforce them at compile time.**
`Application` references neither ASP.NET Core nor `MongoDB.Driver`, so a service physically *cannot*
reach for `HttpContext` or an `IMongoCollection<T>`; the code won't build.

```
skillsprint-backend/
├── SkillSprint.sln
├── global.json                  # pin the SDK to 10.0.x
├── Directory.Build.props        # net10.0, nullable, warnings-as-errors
├── .editorconfig
├── Dockerfile                   # multi-stage: SDK builds, aspnet runs
├── .dockerignore                # keep bin/, obj/, .git out of the build context
├── compose.yaml                 # mongo (default) + api (profile: full)
├── docs/BACKEND_ROADMAP.md      # this file
├── docs/BUILD_CHECKLIST.md      # the day-by-day execution view of §13
├── src/
│   ├── SkillSprint.Domain/            # → references nothing
│   │   ├── Common/MongoDocument.cs    #    shared Id + timestamps
│   │   ├── Entities/                  #    User, Course, Enrollment, RefreshSession
│   │   ├── Enums/                     #    Role, EnrollmentStatus
│   │   └── Exceptions/AppException.cs #    typed operational errors
│   ├── SkillSprint.Application/       # → Domain
│   │   ├── Abstractions/              #    I*Repository, ICacheService, ITokenStore, ITokenService
│   │   ├── Common/                    #    ApiResponse<T>, PagedResult<T>
│   │   ├── Courses/                   #    DTOs, validators, CourseService
│   │   ├── Auth/                      #    DTOs, validators, AuthService
│   │   └── Enrollments/               #    DTOs, validators, EnrollmentService
│   ├── SkillSprint.Infrastructure/    # → Application, Domain
│   │   ├── Mongo/                     #    MongoContext, conventions, index initializer
│   │   ├── Repositories/              #    Mongo implementations of I*Repository
│   │   ├── Caching/HybridCacheService.cs
│   │   ├── Auth/                      #    JwtTokenService, MongoTokenStore, PasswordHasher
│   │   └── Seeding/CourseSeeder.cs
│   └── SkillSprint.Api/               # → Application, Infrastructure (composition root)
│       ├── Program.cs
│       ├── Controllers/
│       ├── Filters/                   #    validation filter
│       ├── Handlers/                  #    IExceptionHandler
│       └── appsettings.json
└── tests/
    ├── SkillSprint.UnitTests/         # services with mocked repositories
    └── SkillSprint.IntegrationTests/  # WebApplicationFactory + EphemeralMongo
```

> Repositories are a deliberate addition. The Mongoose design had services call models directly, but
> `IMongoCollection<T>` is notoriously hard to mock, so without this seam the "services are easy to
> unit-test" promise above doesn't survive the move to C#.

> `Program.cs` uses top-level statements. Append `public partial class Program { }` to the end of it
> so `WebApplicationFactory<Program>` can find the entry point — this is why the old design split
> `app.ts` from `server.ts`, achieved here with one line.

---

## 4. Data Models

Three collections cover the current product: **User**, **Course**, **Enrollment** — plus two
internal collections for session state (§9).

### 4.0 Conventions and the shared base

Register BSON conventions **once at startup, before any collection is touched** (conventions applied
after a class map is built are silently ignored):

```csharp
var pack = new ConventionPack
{
    new CamelCaseElementNameConvention(),      // CourseName -> "courseName"
    new IgnoreExtraElementsConvention(true),
};
ConventionRegistry.Register("skillsprint", pack, _ => true);
```

That one convention keeps the stored documents camelCase without a single `[BsonElement]` attribute.
Roles are persisted as lowercase strings (`"student"`) via a small enum serializer, matching the
values the API exposes.

Because `Domain` takes **no dependency on the MongoDB driver**, entities carry no `[Bson*]`
attributes at all. Anything the conventions can't infer — the `_id` mapping, id generation, and the
ObjectId-typed foreign keys — is declared with explicit class maps in `Infrastructure`:

```csharp
// Domain: a plain class, no persistence concerns
public abstract class MongoDocument
{
    public string Id { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// Infrastructure: how it maps, registered once at startup
BsonClassMap.RegisterClassMap<Course>(cm =>
{
    cm.AutoMap();
    cm.MapIdMember(c => c.Id)
      .SetSerializer(new StringSerializer(BsonType.ObjectId))
      .SetIdGenerator(StringObjectIdGenerator.Instance);
});
```

> Keeping the attributes out is what lets `Application` reference `Domain` without transitively
> gaining any ability to talk to MongoDB — the compile-time guarantee in §3 only holds if the
> entities themselves stay driver-free.

> ⚠️ **`timestamps: true` has no driver equivalent.** Mongoose maintained `createdAt`/`updatedAt`
> automatically; the C# driver does not. Set them explicitly in the repository's insert/update paths
> and centralise that so it can't be forgotten.

### 4.1 User

```csharp
public class User : MongoDocument
{
    public string Email { get; set; } = string.Empty;   // stored lowercased + trimmed
    public string PasswordHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Role Role { get; set; } = Role.Student;
}

public enum Role { Student, Instructor, Admin }
```

`PasswordHash` is protected not by a serializer hook but by **never mapping it into a DTO**.
Controllers return DTOs, never entities — a stronger guarantee than the old `toJSON` transform,
because it can't be bypassed by a code path that forgets to call it.

> Session state (refresh tokens) is deliberately **not** on this document — it lives in its own
> collection with a TTL index (§9), so revocation is a targeted delete rather than an array rewrite.

### 4.2 Course

The frontend's `id` is a slug like `"intro-to-javascript"`. Keep a `slug` field and map it to `id`
in responses so the frontend contract is preserved.

```csharp
public class Course : MongoDocument
{
    public string Slug { get; set; } = string.Empty;        // maps to frontend `id`
    public string CourseName { get; set; } = string.Empty;
    public string Instructor { get; set; } = string.Empty;
    public string Thumbnail { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Categories { get; set; } = [];
    public List<string> Topics { get; set; } = [];
}
```

### 4.3 Enrollment (join between User and Course)

```csharp
public class Enrollment : MongoDocument
{
    public string UserId { get; set; } = string.Empty;    // stored as ObjectId (see §4.0)
    public string CourseId { get; set; } = string.Empty;  // stored as ObjectId (see §4.0)
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;
    public int Progress { get; set; }              // 0–100
    public DateTime EnrolledAt { get; set; }
}

public enum EnrollmentStatus { Active, Completed, Dropped }
```

The old Mongoose schema called these fields `user` and `course`. The collection has never existed,
so there is no data to migrate and the clearer `userId` / `courseId` names are used instead.

### 4.4 Indexes — the one thing that is *not* automatic

⚠️ **This is the biggest trap in the port.** Mongoose created indexes from the schema declaration.
The C# driver never creates anything implicitly. Every index must be declared explicitly and applied
at startup by a hosted service:

| Collection | Index | Why |
| --- | --- | --- |
| `users` | `email` unique | Prevents duplicate accounts at the DB level |
| `courses` | `slug` unique | The public identifier must be unique |
| `courses` | **`courseName` text** | Required for `?q=` search — queries fail without it |
| `enrollments` | `(userId, courseId)` unique | Blocks double-enroll at the DB level |
| `sessions` | `(userId, jti)` unique | Refresh-token lookup |
| `sessions` | `expiresAt` **TTL** | Expired sessions self-delete |
| `denylist` | `jti` unique + `expiresAt` **TTL** | Logout revocation, self-cleaning |

```csharp
await courses.Indexes.CreateManyAsync(
[
    new CreateIndexModel<Course>(
        Builders<Course>.IndexKeys.Ascending(c => c.Slug),
        new CreateIndexOptions { Unique = true, Name = "slug_unique" }),

    new CreateIndexModel<Course>(
        Builders<Course>.IndexKeys.Text(c => c.CourseName),
        new CreateIndexOptions { Name = "courseName_text" }),
], ct);
```

Miss this and the failures are quiet but severe: `?q=` errors at runtime, and duplicate enrollments
succeed silently.

> **Future:** `instructor`, `categories`, and `topics` are strings today to match the frontend. When
> you need instructor profiles or category pages, promote each to its own collection with references.
> Start simple.

---

## 5. Types & Contracts

Three families of types:

1. **Entities** — the shape of a stored record. Plain classes in `Domain` (§4).

2. **DTOs** — request input and response output, as `record` types. Validation rules live beside them
   in a FluentValidation validator rather than inside the DTO, so the shape stays readable:

   ```csharp
   public sealed record RegisterRequest(string Email, string Password, string Name);

   public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
   {
       public RegisterRequestValidator()
       {
           RuleFor(x => x.Email).NotEmpty().EmailAddress();
           RuleFor(x => x.Password).MinimumLength(8);
           RuleFor(x => x.Name).NotEmpty();
       }
   }
   ```

3. **API response envelope** — one consistent shape for every response:

   ```csharp
   public sealed record ApiResponse<T>(
       bool Success,
       T? Data = default,
       ApiErrorBody? Error = null,
       Meta? Meta = null);

   public sealed record ApiErrorBody(string Code, string Message, object? Details = null);
   public sealed record Meta(int Page, int Limit, long Total);
   ```

   Configure JSON once with `DefaultIgnoreCondition = WhenWritingNull` so unused members are omitted
   (matching the optional fields of the original TypeScript interface), and
   `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)` so roles serialize as `"student"`.

**The one hard frontend contract:** `CourseDto` exposes `slug` as `id`.

```csharp
public sealed record CourseDto(
    string Id,            // <- course.Slug
    string CourseName,
    string Instructor,
    string Thumbnail,
    string? Description = null,
    IReadOnlyList<string>? Categories = null,
    IReadOnlyList<string>? Topics = null);
```

> **Sharing types with the frontend:** the two sides are no longer the same language, so a shared npm
> package of types is off the table. The equivalent is to **generate a TypeScript client from the
> OpenAPI document** the API already publishes (§12) — the contract is still compiler-checked on both
> ends, just derived rather than shared.

---

## 6. API Routes (REST, versioned `/api/v1`)

Version the API from day one so future breaking changes can live at `/api/v2`.

### Auth

| Method | Path                    | Auth   | Purpose                                   |
| ------ | ----------------------- | ------ | ----------------------------------------- |
| POST   | `/api/v1/auth/register` | —      | Create account, return tokens             |
| POST   | `/api/v1/auth/login`    | —      | Verify credentials, return tokens         |
| POST   | `/api/v1/auth/refresh`  | cookie | Issue new access token from refresh token |
| POST   | `/api/v1/auth/logout`   | ✓      | Revoke refresh token, clear cookie        |
| GET    | `/api/v1/auth/me`       | ✓      | Return the current user                   |

### Courses

| Method | Path                   | Auth             | Purpose                                            |
| ------ | ---------------------- | ---------------- | -------------------------------------------------- |
| GET    | `/api/v1/courses`      | —                | List courses; `?q=` search, `?page=&limit=` paging  |
| GET    | `/api/v1/courses/{id}` | —                | Get one course (by slug)                           |
| POST   | `/api/v1/courses`      | instructor/admin | Create a course                                    |
| PATCH  | `/api/v1/courses/{id}` | instructor/admin | Update a course                                    |
| DELETE | `/api/v1/courses/{id}` | admin            | Delete a course                                    |

### Enrollments

| Method | Path                        | Auth | Purpose                              |
| ------ | --------------------------- | ---- | ------------------------------------ |
| GET    | `/api/v1/enrollments`       | ✓    | List the current user's enrollments  |
| POST   | `/api/v1/enrollments`       | ✓    | Enroll in a course (`{ courseId }`)  |
| PATCH  | `/api/v1/enrollments/{id}`  | ✓    | Update progress/status               |
| DELETE | `/api/v1/enrollments/{id}`  | ✓    | Unenroll                             |

### Meta / Ops

| Method | Path                 | Auth | Purpose                        |
| ------ | -------------------- | ---- | ------------------------------ |
| GET    | `/api/v1/categories` | —    | List categories (for chips)    |
| GET    | `/api/v1/topics`     | —    | List topics (for suggestions)  |
| GET    | `/health`            | —    | Liveness/readiness probe       |

> **Conventions:** plural nouns, no verbs in paths (`POST /courses`, not `/createCourse`). List
> endpoints accept `page`, `limit`, `sort`, and filters; return the `meta` block with `total`.
> **Cap `limit`** (max 100) — an uncapped `?limit=` is a trivial DoS against the database.

---

## 7. Services

Business logic lives here, framework-agnostic and unit-testable.

| Service             | Responsibilities                                                                       |
| ------------------- | -------------------------------------------------------------------------------------- |
| `AuthService`       | register, login, logout, refresh — orchestrates user + token logic                      |
| `TokenService`      | sign/validate access & refresh JWTs, rotate + revoke refresh tokens (§9)                |
| `CacheService`      | thin wrapper over `HybridCache` — see below                                             |
| `CourseService`     | list/search (with pagination), get, create, update, delete — reads go through the cache |
| `EnrollmentService` | enroll (guard against duplicates), list-by-user, update progress, remove                |
| `UserService`       | fetch/update profile                                                                    |

### `ICacheService`

A thin, generic wrapper — nothing resource-specific lives here:

```csharp
public interface ICacheService
{
    Task<T> GetOrSetAsync<T>(string key, TimeSpan ttl, Func<CancellationToken, Task<T>> loader,
                             CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
}
```

`GetOrSetAsync` is the cache-aside helper everything else calls: try the cache, on miss invoke
`loader`, store, return. `HybridCache` adds **stampede protection** for free — concurrent misses on
the same key collapse into a single loader call.

**Fails open, never throws:** if the cache faults, log and fall straight through to `loader`. A cache
outage should degrade read latency, not take the API down.

### Cache-aside in `CourseService`

- `ListAsync(query)` → key `courses:list:v{version}:{hash of q,page,limit,sort}`, TTL `Cache:CoursesTtlSeconds`.
- `GetBySlugAsync(slug)` → key `course:{slug}`, same TTL.
- `/categories` and `/topics` cache under `categories:all` / `topics:all` with the longer
  `Cache:CategoriesTtlSeconds`, since they change far less often than the course list.
- **Writes invalidate by bumping a version counter**, not by pattern-matching keys. `create`/`update`/
  `delete` remove `course:{slug}`, `categories:all`, `topics:all`, and increment `version` — which
  orphans every `courses:list:v{old}:*` key at once and lets TTL reap them.

> The Redis design used `SCAN`-based prefix deletion for this. The version counter is simpler, is
> O(1) instead of O(keyspace), works identically on any cache backend, and doesn't depend on
> tag-invalidation support. Keep it even after Redis is introduced.

Example of the "no HTTP in services" rule:

```csharp
// service: pure logic, throws typed errors
public async Task<EnrollmentDto> EnrollAsync(string userId, string courseId, CancellationToken ct)
{
    var course = await _courses.GetByIdAsync(courseId, ct)
        ?? throw new AppException(404, "COURSE_NOT_FOUND", "Course not found");

    return await _enrollments.CreateAsync(userId, course.Id, ct);
}

// controller: translates logic results into HTTP
[HttpPost]
public async Task<IActionResult> Enroll(EnrollRequest body, CancellationToken ct)
{
    var enrollment = await _enrollmentService.EnrollAsync(CurrentUserId, body.CourseId, ct);
    return StatusCode(201, ApiResponse.Ok(enrollment));
}
```

---

## 8. Best Practices

- **Centralized error handling.** Define an `AppException` (status, code, message, details) and
  register an `IExceptionHandler` with `app.UseExceptionHandler()`. Unlike Express, **no
  `asyncHandler` wrapper is needed** — exceptions thrown from `async` controller actions propagate to
  the pipeline on their own, so an entire category of boilerplate simply disappears.
- **Validate every input** through the validation filter. Never trust a bound DTO without it.
- **Consistent responses.** Always return the `ApiResponse` envelope. Override
  `InvalidModelStateResponseFactory` so even model-binding failures come back in that same shape.
- **Strict tooling.** Nullable reference types on, warnings as errors, `.editorconfig`, `dotnet format`.
- **Structured logging** with Serilog + `UseSerilogRequestLogging()`; attach a request id per request.
  Never log secrets, tokens, or password fields.
- **Mongo performance.** Project into DTOs rather than pulling whole documents (the analogue of
  Mongoose's `.lean()` — no change tracking exists here, so the win is purely in bytes over the wire).
  Declare indexes explicitly (§4.4), match them to your query patterns, and cap pagination.
- **Cancellation.** Take a `CancellationToken` in every async method down to the driver call, so an
  abandoned request stops doing work instead of finishing into the void.
- **Async all the way.** Never `.Result` or `.Wait()` on a task — it deadlocks under load.
- **Operational endpoints.** Expose `GET /health` via `AddHealthChecks()`. Graceful shutdown is
  handled by the generic host — it drains in-flight requests on SIGTERM without extra code.
- **Seed script.** Port the frontend's `src/data/courses.ts` array into MongoDB via
  `dotnet run --project src/SkillSprint.Api -- seed`, idempotently (upsert on `slug`), so dev data
  matches what the frontend already shows.

---

## 9. Security

Security is a first-class section, not an afterthought.

### Passwords

- Hash with **bcrypt** (`BCrypt.Net-Next`, cost ≥ 12). Store only the hash. Never log or return it —
  enforced structurally by keeping `PasswordHash` out of every DTO (§4.1).

### JWT (access + refresh) — session state lives in MongoDB

- **Access token:** short-lived (~15 min), sent as `Authorization: Bearer <token>`, carries a `jti`,
  `sub` (user id) and `role`. Validated by `AddJwtBearer` — signature, lifetime, issuer, audience.
- **Refresh token:** longer-lived (~7 days), delivered in an **httpOnly, Secure, SameSite** cookie so
  JavaScript can't read it (mitigates XSS token theft), also carries a `jti`.
- **Rotation + reuse detection:** on login/refresh, mint a refresh token with a fresh `jti` and
  insert `{ userId, jti, tokenHash, expiresAt }` into `sessions`. To refresh, look the row up by
  `(userId, jti)`; to rotate, delete the old row and insert the new one. If a presented `jti` is
  **not found** (already rotated, or never issued), treat it as theft: delete **every** session for
  that user, forcing a re-login everywhere. The TTL index on `expiresAt` expires stale rows
  automatically — no cleanup job needed.
- **Logout via access-token denylist:** on logout, insert `{ jti, expiresAt }` into `denylist` with
  `expiresAt` set to the access token's remaining lifetime. A `JwtBearerEvents.OnTokenValidated`
  handler rejects any request whose `jti` is denylisted — this is what makes logout actually revoke a
  still-valid access token instead of just discarding it client-side.
- **Degradation posture:** revocation depends on the database being reachable. Keep the access-token
  TTL short (~15 min) so any gap is time-bounded.

### Transport & HTTP hardening

- `NetEscapades.AspNetCore.SecurityHeaders` for secure headers (the `helmet` equivalent).
- **CORS** locked to the configured origin with `AllowCredentials()` so the refresh cookie works.
  Note that `AllowAnyOrigin()` and `AllowCredentials()` are mutually exclusive by spec — the origin
  must be named explicitly.
- **Rate limiting** via the in-box `AddRateLimiter`, with a stricter named policy applied to
  `/auth/*` to slow brute-force. Counters are per-instance until Redis is introduced (§1.1).
- **Request body size cap** on the JSON endpoints.
- **HTTPS redirection + HSTS** in production.

### Two middlewares that are no longer needed

The Express design used `express-mongo-sanitize` and `hpp`. Neither has a counterpart here, and that
is a real structural gain rather than an omission:

- **NoSQL injection.** `express-mongo-sanitize` existed because in JavaScript `req.body` becomes a
  raw query document, so a posted `{ "$gt": "" }` is an attack. Binding to typed C# DTOs and building
  filters with `Builders<T>.Filter` removes that class of bug entirely — user input lands in a
  `string`, never in an operator position. ⚠️ **It comes straight back** if any code ever parses user
  input into a `BsonDocument` and passes it as a filter. Don't.
- **Parameter pollution.** `hpp` guarded against `?limit=1&limit=999`. ASP.NET Core model binding
  resolves duplicate query keys against the target type — a scalar property takes one value, and only
  a collection property collects many.

### Authorization

- `[Authorize]` on any endpoint needing a valid access token.
- `[Authorize(Roles = "Instructor,Admin")]` guards role-restricted routes — this replaces the
  hand-written `authorize(...)` middleware entirely.

### Operational hygiene

- Secrets only in user-secrets (dev) or environment variables (prod), never committed; rotate if leaked.
- HTTPS everywhere in production.
- Consider login throttling / temporary account lockout after repeated failures.

---

## 10. Frontend Integration Plan

Each existing frontend seam maps cleanly to a new endpoint — minimal rewiring:

| Frontend code (frontend repo)                          | Replace with                                       |
| ------------------------------------------------------ | -------------------------------------------------- |
| `courseService.getCourses()`                           | `GET /api/v1/courses`                               |
| `courseService.searchCourses(q)`                       | `GET /api/v1/courses?q=<q>`                         |
| `useCourses` → `enroll(course)`                        | `POST /api/v1/enrollments` `{ courseId }`           |
| `useCourses` → `enrolledCourses`                       | `GET /api/v1/enrollments`                           |
| `AuthProvider` email-in-localStorage stub              | `POST /auth/register` + `/auth/login`, real tokens  |
| `LoginPage` / `RegisterPage` forms (passwords ignored) | wire to `/auth/login` and `/auth/register`          |

Frontend changes needed:

- Add `VITE_API_URL` to the frontend `.env` and a small `fetch` wrapper (API client) that attaches
  the access token and sends `credentials: "include"` (for the refresh cookie).
- Replace the localStorage-email auth with real login/register; store the access token in memory
  (and rely on the httpOnly refresh cookie to re-hydrate sessions).
- Keep `courseService.ts` as the single swap point — only its bodies change, callers stay the same
  (exactly as its comments anticipate).

---

## 11. Containerization

### 11.1 Two jobs, two phases

Docker does one thing in Phase 0 and a different thing in Phase 5, and it helps to keep them apart:

- **Phase 0 — supply MongoDB.** `docker compose up -d mongo` replaces installing a database on your
  machine. This is the only thing Docker does for most of the build.
- **Phase 5 — package the API.** A multi-stage `Dockerfile` produces the artifact you actually deploy.

Note what is *not* on that list: the API is **not** containerized for day-to-day development.
`dotnet watch` on the host rebuilds in under a second; a bind-mounted container loop does not. Run
the database in a container and the app you are editing on the host.

### 11.2 `compose.yaml` — the dev database

- Use the Compose Spec, so **no `version:` key**. It has been obsolete for years and current Compose
  warns about it — most tutorials you'll find still have it.
- Pin `mongo` to an explicit major version, never `:latest`, so a rebuild months from now doesn't
  quietly hand you a different server. Map `27017:27017`.
- Give it a **named volume** for `/data/db`. Without one, every `docker compose down` is an empty
  database and you re-run the seeder; with one, only `down -v` discards data.
- Add a `healthcheck` running `mongosh --eval "db.adminCommand('ping')"`. It costs nothing now and
  Phase 5 depends on it existing.
- The `api` service is added in Phase 5 behind `profiles: [full]`, so the plain `docker compose up`
  you've been typing since Phase 0 keeps starting Mongo alone.

### 11.3 The Dockerfile — multi-stage

Build with the SDK image, ship with the runtime image:

- Build stage: `mcr.microsoft.com/dotnet/sdk:10.0`.
- Runtime stage: `mcr.microsoft.com/dotnet/aspnet:10.0`, or `10.0-noble-chiseled` for a much smaller,
  shell-less image. The SDK is roughly ten times the size of the runtime and has no business in the
  shipped image.

**Layer caching is the whole trick.** Copy the files that describe *dependencies* first, restore, and
only then copy the source:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props SkillSprint.sln ./
COPY src/SkillSprint.Domain/*.csproj          src/SkillSprint.Domain/
COPY src/SkillSprint.Application/*.csproj     src/SkillSprint.Application/
# … one line per project …
RUN dotnet restore
COPY . .
RUN dotnet publish src/SkillSprint.Api -c Release -o /app/publish
```

`restore` is the slow step. Ordered this way it re-runs only when a project file changes, not on
every source edit. Note `publish`, not `build` — the runtime stage copies `/app/publish` and nothing
else.

### 11.4 `.dockerignore` — not optional

⚠️ Without one, the build context includes your host's `bin/` and `obj/` directories. That bloats
every build and can hand the container stale or wrong-architecture artifacts that make `restore`
behave inexplicably. Exclude at least:

```
bin/
obj/
.git/
docs/
tests/
.vs/
**/appsettings.Development.json
.env
```

Docker does **not** read `.gitignore`. This is a separate file that happens to overlap.

### 11.5 Runtime hardening

- **Run as non-root.** The aspnet images define an `APP_UID` — add `USER $APP_UID`. Chiseled images
  are already non-root and have no shell at all, which removes `docker exec … sh` as a step in an
  attack.
- ⚠️ **The port is 8080, not 80.** Since .NET 8 the container images default to
  `ASPNETCORE_HTTP_PORTS=8080`, precisely so the app can run as non-root (binding below 1024 needs
  privilege). `EXPOSE 8080` and map accordingly. A compose file copied from an older tutorial with
  `80:80` will just fail to connect, with no useful error.
- ⚠️ **TLS terminates at the edge, not in the container.** §9 calls for HTTPS redirection and HSTS in
  production, but behind a platform's proxy your app receives plain HTTP — so it would either
  redirect-loop or set HSTS on a request it thinks is insecure. The fix is `UseForwardedHeaders`
  (`XForwardedFor | XForwardedProto`) registered **before** the redirection middleware, so
  `Request.Scheme` reflects the original client request. Middleware order is what makes this work.
- **Healthcheck and start ordering.** Give the `api` service a healthcheck hitting `GET /health`
  (§6), and depend on Mongo with `depends_on: mongo: condition: service_healthy`. Plain `depends_on`
  waits only for the container to *start*, not for Mongo to accept connections — that difference is
  why a stack works on your warm machine and fails on a cold one.
- **Graceful shutdown already works.** `docker stop` sends SIGTERM and the generic host drains
  in-flight requests (§8). Nothing to write; just know the default grace period is about 10 seconds.

### 11.6 Secrets in containers

⚠️ Never `COPY` a secrets file into the image and never bake `ENV Jwt__AccessSecret=…` into the
Dockerfile. Both persist in the image layers and are readable by anyone who can pull the image —
deleting them in a later layer does not remove them.

Note also that `dotnet user-secrets` is a **development-only** mechanism tied to your user profile;
it does not exist inside the container. So:

- **Local containers:** Compose reads a gitignored `.env` via `env_file`.
- **Production:** the hosting platform injects `Mongo__ConnectionString` and the `Jwt__*` variables
  as environment variables (§2.6).

### 11.7 Why the tests don't use Docker

Now that Docker is here, it would be natural to point the integration tests at a real container. This
roadmap deliberately doesn't: tests stay on **EphemeralMongo** (§12), so `dotnet test` and CI need no
Docker daemon and pay no container startup cost on every run.

The trade-off accepted is that tests run against EphemeralMongo's bundled `mongod` rather than the
exact image Compose runs. If that difference ever causes a bug that only shows in production,
**Testcontainers** is the swap — the test fixture changes, the tests themselves don't.

---

## 12. Extras Worth Including

- **API docs:** the API publishes an OpenAPI document via `Microsoft.AspNetCore.OpenApi`, rendered by
  **Scalar** in Development. That document doubles as the source for a generated TypeScript client
  (§5).
- **Testing strategy:**
  - _Unit_ tests for services — mock the repository interfaces with NSubstitute.
  - _Integration_ tests with `WebApplicationFactory<Program>` hitting the API in-process, backed by
    **EphemeralMongo** (a throwaway `mongod`, the `mongodb-memory-server` equivalent) so no real
    database and no Docker are needed in CI. That is a deliberate choice, not an oversight — see
    §11.7 for the trade-off and the escape hatch.
- **CI (GitHub Actions):** `dotnet format --verify-no-changes` → `dotnet build` → `dotnet test` on
  every PR. Warnings-as-errors means the build step covers what a separate typecheck used to.
- **Containerization:** covered in full in §11 — Compose for the dev database from Phase 0, a
  multi-stage image for the API at Phase 5. Add `redis` only when §1.1 is revisited.
- **Deployment:** host the API on Render / Railway / Fly.io / Azure App Service, with the database on
  **MongoDB Atlas** (free tier). With a `Dockerfile` present these platforms build and run *your*
  image rather than guessing at a build from source, so what runs in production is what you tested
  locally. The Compose `mongo` service is local-dev only — production points at Atlas. Set
  `Mongo__ConnectionString` and the `Jwt__*` secrets in the host's dashboard.
- **Observability:** `GET /health` reports Mongo reachability. Add error tracking (e.g. Sentry) and
  ship structured logs somewhere queryable.
- **Future features:** email verification, password reset (token + email), instructor dashboards,
  course ratings/reviews, lessons within a course, progress tracking.

---

## 13. Phased Milestone Roadmap

Build in vertical slices — each phase ends with something runnable.

> **This section is the overview — the phases and why they're ordered this way.** The day-by-day
> version, sized for about an hour a session, is [BUILD_CHECKLIST.md](./BUILD_CHECKLIST.md). That is
> the file you tick off; this one is what you re-read when you've lost the thread.

### Phase 0 — Setup & Foundation

- [ ] `compose.yaml` with a `mongo` service, named volume, and healthcheck; `docker compose up -d mongo`
- [ ] Solution + six projects, references wired inward only
- [ ] `global.json`, `Directory.Build.props`, `.editorconfig`
- [ ] Typed options bound from `appsettings.json` with `ValidateOnStart()`; secrets in user-secrets
- [ ] `MongoContext` (singleton `IMongoClient`) + BSON conventions registered once
- [ ] `MongoIndexInitializer` hosted service creating every index in §4.4
- [ ] `GET /health`, `AppException`, `IExceptionHandler`, Serilog request logging

### Phase 1 — Authentication

- [ ] `User` entity + BCrypt password hashing
- [ ] `TokenService` (sign/validate access & refresh, `jti`-based)
- [ ] `ITokenStore` over the `sessions` collection + reuse detection (§9)
- [ ] Access-token denylist for logout (§9)
- [ ] `register`, `login`, `refresh`, `logout`, `me` endpoints
- [ ] `AddJwtBearer` + `OnTokenValidated` denylist check; `[Authorize(Roles=…)]` on protected routes

### Phase 2 — Courses

- [ ] `Course` entity + slug-unique and text indexes
- [ ] Seeder porting the frontend's `src/data/courses.ts`
- [ ] `ICacheService` over `HybridCache` (`GetOrSetAsync`, fails open)
- [ ] `GET /courses` (text search + pagination), `GET /courses/{id}` by slug — cache-aside
- [ ] Protected create/update/delete (instructor/admin) — invalidate keys + bump the list version
- [ ] `GET /categories`, `GET /topics` — cached with the longer TTL

### Phase 3 — Enrollments

- [ ] `Enrollment` entity + unique `(userId, courseId)` index
- [ ] Enroll / list-mine / update-progress / unenroll endpoints
- [ ] Duplicate enrollment → 409 via the duplicate-key error; ownership enforced in the service

### Phase 4 — Hardening

- [ ] FluentValidation on every request DTO + validation filter + envelope-shaped binding errors
- [ ] Security headers, CORS allowlist with credentials, body size cap
- [ ] Rate limiting, stricter on `/auth/*`
- [ ] Confirm refresh-token rotation + reuse detection holds up under test
- [ ] Unit + integration tests; OpenAPI document + Scalar UI

### Phase 5 — Ship

- [ ] GitHub Actions CI (format / build / test)
- [ ] `.dockerignore`, then a multi-stage `Dockerfile` with restore-layer caching (§11.3–11.4)
- [ ] Non-root `USER $APP_UID`, `EXPOSE 8080`, `UseForwardedHeaders` for TLS at the edge (§11.5)
- [ ] `api` service in `compose.yaml` under `profiles: [full]`, `depends_on: service_healthy`
- [ ] Smoke test the stack: `docker compose --profile full up --build`, then `/health` and a course list
- [ ] Deploy API + MongoDB Atlas; secrets set in the host dashboard
- [ ] Point the frontend at the live API (`VITE_API_URL`), swap `courseService` + auth

---

### Where to start

Phase 0 top to bottom, starting with bringing Mongo up in a container — nothing else can be tested
until a database answers, the app boots against it, and the indexes exist. Then Phase 1: get a user able to register and log in against a real database with JWTs,
with refresh-token rotation and the logout denylist in place from the start, before touching courses.
Courses (Phase 2) is where the cache-aside pattern gets exercised for the first time.
