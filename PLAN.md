# Scalable URL Shortener API

## 1. Project Overview

Build a production-grade URL shortener using:

* **ASP.NET Core 10 Web API**
* **SQL Server** as the primary relational database
* **Redis** for caching, rate limiting, and short-lived distributed state
* **Entity Framework Core 10**
* **Background workers** for asynchronous analytics processing
* **Docker** for local development and deployment
* **OpenAPI/Swagger** for API documentation
* **JWT/OAuth-based authentication** for user accounts

The system should initially be simple enough to develop as a modular monolith, while its architecture should allow horizontal scaling when traffic increases.

### Target scale

The system should eventually support approximately:

```text
1,000 URL creations / second
100,000 redirects / second
```

This means the system is fundamentally **read-heavy**.

A simplified workload ratio is:

```text
1 write : 100 reads
```

Therefore, the redirect path should avoid hitting SQL Server for every request.

---

# 2. High-Level Architecture

```text
                         ┌──────────────────┐
                         │      Client      │
                         │ Browser / Mobile │
                         └────────┬─────────┘
                                  │
                                  ▼
                         ┌──────────────────┐
                         │ Load Balancer /  │
                         │ Reverse Proxy    │
                         └────────┬─────────┘
                                  │
                    ┌─────────────┼─────────────┐
                    │             │             │
                    ▼             ▼             ▼
              ┌──────────┐  ┌──────────┐  ┌──────────┐
              │ API #1   │  │ API #2   │  │ API #N   │
              │ ASP.NET  │  │ ASP.NET  │  │ ASP.NET  │
              │ Core 10  │  │ Core 10  │  │ Core 10  │
              └────┬─────┘  └────┬─────┘  └────┬─────┘
                   │             │             │
                   └─────────────┼─────────────┘
                                 │
                ┌────────────────┼────────────────┐
                │                                 │
                ▼                                 ▼
        ┌──────────────┐                  ┌──────────────┐
        │    Redis     │                  │  SQL Server  │
        │              │                  │              │
        │ URL Cache    │                  │ URLs         │
        │ Rate Limits  │                  │ Users        │
        │ Hot Data     │                  │ Aliases      │
        └──────────────┘                  │ Analytics    │
                                          └──────────────┘
                                                 ▲
                                                 │
                                          ┌──────┴───────┐
                                          │ Background   │
                                          │ Workers      │
                                          └──────────────┘
```

---

# 3. Architectural Style

Start with a **modular monolith** rather than immediately creating microservices.

Recommended structure:

```text
URLShortener
│
├── Api
│   ├── Controllers
│   ├── Middleware
│   ├── Filters
│   └── Extensions
│
├── Application
│   ├── Users
│   ├── Urls
│   ├── Redirects
│   ├── Analytics
│   ├── QrCodes
│   └── Common
│
├── Domain
│   ├── Entities
│   ├── ValueObjects
│   ├── Enums
│   └── Exceptions
│
├── Infrastructure
│   ├── Persistence
│   ├── Redis
│   ├── Authentication
│   ├── RateLimiting
│   ├── BackgroundJobs
│   └── ExternalServices
│
└── Tests
    ├── UnitTests
    ├── IntegrationTests
    └── LoadTests
```

The important principle is:

> Keep business logic independent from ASP.NET Core, SQL Server, and Redis.

---

# 4. Core Features

The API should eventually support:

* Create short URLs
* Redirect short URLs
* Custom aliases
* URL expiration
* User accounts
* Authentication
* Authorization
* Click statistics
* IP-independent analytics
* Rate limiting
* QR code generation
* URL management
* URL deletion/deactivation
* Pagination
* Search/filtering
* Admin functionality
* Health checks
* Observability

---

# 5. URL Lifecycle

A typical request:

```text
POST /api/v1/urls
```

with:

```json
{
  "originalUrl": "https://example.com/some/very/long/url",
  "customAlias": null,
  "expiresAt": "2027-01-01T00:00:00Z"
}
```

The API generates:

```text
a8Fx21
```

and returns:

```json
{
  "id": "01K...",
  "shortCode": "a8Fx21",
  "shortUrl": "https://myshort.com/a8Fx21",
  "originalUrl": "https://example.com/some/very/long/url",
  "expiresAt": "2027-01-01T00:00:00Z"
}
```

A user then requests:

```text
GET /a8Fx21
```

The API responds with:

```text
HTTP 302
Location: https://example.com/some/very/long/url
```

---

# 6. API Design

Use versioned endpoints.

```text
/api/v1/...
```

Redirects are deliberately outside the API prefix:

```text
/{shortCode}
```

because the short URL should be as small as possible.

---

# 7. Authentication Endpoints

```http
POST /api/v1/auth/register
POST /api/v1/auth/login
POST /api/v1/auth/refresh
POST /api/v1/auth/logout
GET  /api/v1/users/me
```

Example registration:

```json
{
  "email": "user@example.com",
  "password": "StrongPassword123!"
}
```

Use:

* ASP.NET Core Identity, or
* a dedicated user/authentication module

For this project, ASP.NET Core Identity is a good choice if you want to learn the framework's authentication ecosystem.

---

# 8. URL Endpoints

## Create URL

```http
POST /api/v1/urls
Authorization: Bearer <token>
```

Request:

```json
{
  "originalUrl": "https://example.com/some/very/long/url",
  "customAlias": null,
  "expiresAt": "2027-01-01T00:00:00Z"
}
```

Response:

```json
{
  "id": "01K...",
  "shortCode": "a8Fx21",
  "shortUrl": "https://myshort.com/a8Fx21",
  "originalUrl": "https://example.com/some/very/long/url",
  "expiresAt": "2027-01-01T00:00:00Z",
  "createdAt": "2026-09-05T10:00:00Z"
}
```

---

# 9. Get URL

```http
GET /api/v1/urls/{id}
```

Returns URL metadata.

---

# 10. List User URLs

```http
GET /api/v1/urls?page=1&pageSize=20
```

Optional filters:

```text
?search=github
?active=true
?expired=false
```

Use pagination.

Never return an unlimited collection.

---

# 11. Update URL

```http
PUT /api/v1/urls/{id}
```

Possible fields:

```json
{
  "expiresAt": "2027-06-01T00:00:00Z",
  "isActive": true
}
```

Avoid allowing arbitrary modification of the original URL unless there is a good product reason.

---

# 12. Delete URL

```http
DELETE /api/v1/urls/{id}
```

Prefer a soft-delete/deactivation model.

For example:

```text
IsActive = false
```

rather than immediately deleting the database record.

---

# 13. Redirect Endpoint

```http
GET /{shortCode}
```

This is the most performance-sensitive endpoint.

Desired flow:

```text
Request
   │
   ▼
ASP.NET Core
   │
   ▼
Redis
   │
   ├── HIT ──────► Redirect
   │
   └── MISS
          │
          ▼
      SQL Server
          │
          ▼
       Redis
          │
          ▼
       Redirect
```

The redirect endpoint should **not** perform unnecessary database queries.

---

# 14. Redirect HTTP Status

Use:

```text
302 Found
```

initially.

This provides flexibility if the destination changes.

Be careful with permanent redirects such as:

```text
301
308
```

because browsers and intermediate caches can cache them aggressively.

---

# 15. Database Design

SQL Server is the source of truth.

Recommended tables:

```text
Users
Urls
UrlClicks
RefreshTokens
```

Potential future tables:

```text
ApiKeys
RateLimitPolicies
AuditLogs
CustomDomains
```

---

# 16. Urls Table

Suggested structure:

```text
Urls
--------------------------------
Id
ShortCode
OriginalUrl
UserId
CreatedAt
ExpiresAt
IsActive
DeletedAt
CreatedByIp
```

Use a database-generated or application-generated identifier for `Id`.

For example:

```text
Id: UNIQUEIDENTIFIER
```

or a suitable sequential identifier.

The short code should be a separate field.

---

# 17. ShortCode Requirements

The short code should:

* Be URL-safe
* Be reasonably short
* Have enough entropy
* Be unique
* Not expose sensitive database IDs
* Support distributed generation

Example:

```text
a8Fx21
Q7kP2m
zX91Ab
```

A Base62 alphabet is a good choice:

```text
0-9
A-Z
a-z
```

This gives:

```text
62 possible characters
```

For 7 characters:

```text
62^7 ≈ 3.5 trillion
```

possible combinations.

---

# 18. ID Generation Strategy

Do not use:

```text
SELECT MAX(Id) + 1
```

and do not generate IDs by querying SQL Server for every request.

For the first version, generate a cryptographically strong random Base62 short code and enforce uniqueness in SQL Server.

Example:

```text
Random bytes
    ↓
Base62 encoding
    ↓
a8Fx21
    ↓
INSERT
    ↓
UNIQUE constraint
```

If there is a collision:

```text
generate again
```

At 1,000 creations/second, this is still practical with a sufficiently large code space.

At much larger scale, introduce a distributed ID-generation strategy.

---

# 19. SQL Server Indexes

The most important index is:

```sql
UNIQUE INDEX IX_Urls_ShortCode
ON Urls(ShortCode)
```

This makes:

```text
GET /a8Fx21
```

efficient when Redis misses.

For user management:

```sql
INDEX IX_Urls_UserId_CreatedAt
ON Urls(UserId, CreatedAt DESC)
```

For expiration/background processing:

```sql
INDEX IX_Urls_ExpiresAt
ON Urls(ExpiresAt)
```

Potential filtered indexes can be considered after measuring the workload.

---

# 20. Important Database Rule

Do not create indexes for every column.

Every additional index increases:

* INSERT cost
* UPDATE cost
* storage
* maintenance
* transaction overhead

Create indexes based on actual query patterns.

---

# 21. EF Core

Use EF Core for normal application operations.

Recommended:

```text
DbContext
Repositories only where useful
Specification/query objects where complexity justifies them
AsNoTracking() for read-only queries
Explicit transactions for multi-step writes
```

Avoid building an abstraction such as:

```text
IRepository<T>
```

for every entity merely because it is a common pattern.

EF Core already provides a strong data-access abstraction.

---

# 22. Redis Architecture

Redis is extremely important because redirects are read-heavy.

Store:

```text
shortcode -> destination
```

Example:

```text
url:a8Fx21
    ↓
https://example.com/some/very/long/url
```

A more complete value could be:

```json
{
  "url": "https://example.com/some/very/long/url",
  "expiresAt": "2027-01-01T00:00:00Z",
  "isActive": true
}
```

---

# 23. Redis Cache Key

Use a predictable namespace:

```text
url:{shortCode}
```

Example:

```text
url:a8Fx21
```

This makes cache management easier.

---

# 24. Cache-Aside Pattern

Redirect flow:

```text
GET /a8Fx21
       │
       ▼
Redis GET url:a8Fx21
       │
       ├── Found
       │     │
       │     ▼
       │   Redirect
       │
       └── Not Found
              │
              ▼
        SQL Server query
              │
              ▼
        Store in Redis
              │
              ▼
           Redirect
```

This is the **cache-aside** pattern.

---

# 25. Cache TTL

Use a TTL rather than caching forever.

For example:

```text
Redis TTL: 1 hour
```

The exact TTL should be determined through measurement.

When the URL is updated:

```text
SQL UPDATE
     ↓
Redis DELETE
```

The next redirect loads the fresh value.

---

# 26. Cache Stampede Protection

A popular URL could expire from Redis while receiving thousands of requests simultaneously.

Without protection:

```text
10,000 requests
      ↓
10,000 Redis misses
      ↓
10,000 SQL queries
```

This is a cache stampede.

Use one or more of:

* Redis distributed lock
* Per-key request coalescing
* Stale-while-revalidate
* Probabilistic early expiration

For the first implementation, a distributed lock or request coalescing mechanism is enough.

---

# 27. Hot Data

Some URLs will be extremely popular.

Example:

```text
url:abc123
```

receives:

```text
50,000 requests/sec
```

You do not want those requests reaching SQL Server.

The architecture should therefore be:

```text
Internet
   ↓
Load Balancer
   ↓
ASP.NET instances
   ↓
Redis
   ↓
Redirect
```

SQL Server should primarily serve:

```text
cache misses
management operations
analytics
```

not every redirect.

---

# 28. Negative Caching

Consider caching nonexistent short codes.

For example:

```text
url:doesnotexist
```

could temporarily store:

```text
NOT_FOUND
```

with a short TTL such as:

```text
10-30 seconds
```

This protects SQL Server from repeated requests for invalid URLs.

Be careful not to allow attackers to fill Redis with arbitrary keys indefinitely.

---

# 29. Expiration

There are two expiration mechanisms:

## Application-level expiration

Every redirect checks:

```text
ExpiresAt
```

If:

```text
ExpiresAt < UtcNow
```

return:

```text
404
```

or an appropriate "expired link" response.

## Redis TTL

Redis should also expire the cache entry.

However:

> Redis TTL is a cache mechanism, not the authoritative expiration mechanism.

SQL Server remains the source of truth.

---

# 30. Analytics

Do not synchronously write a SQL row for every redirect.

At:

```text
100,000 redirects/sec
```

you could produce:

```text
8.64 billion clicks/day
```

if traffic were constant.

A design like:

```text
redirect
   ↓
INSERT UrlClicks
   ↓
redirect
```

would become extremely expensive.

---

# 31. Asynchronous Click Tracking

Use an event-based flow:

```text
GET /a8Fx21
      │
      ├──────────────► Redirect immediately
      │
      ▼
Click event
      │
      ▼
Queue / Redis Stream / Message Broker
      │
      ▼
Background Worker
      │
      ▼
Analytics storage
```

The redirect should not wait for analytics persistence.

---

# 32. Click Event

Example:

```json
{
  "urlId": "01K...",
  "timestamp": "2026-09-05T10:00:00Z",
  "country": "EG",
  "deviceType": "mobile",
  "browser": "Chrome",
  "referrer": "https://google.com"
}
```

Do **not** store raw IP addresses unless there is a clear requirement and appropriate privacy/legal basis.

---

# 33. IP-Independent Analytics

The requirement says:

> IP-independent analytics

Design analytics around aggregated dimensions instead of identifying individual users.

Useful metrics:

```text
Total clicks
Clicks per day
Clicks per hour
Country
Device type
Browser
Operating system
Referrer
```

For example:

```text
URL: a8Fx21

Total clicks: 1,284,921

Today:
  12,432

Countries:
  Egypt: 5,230
  Germany: 2,100
  USA: 1,920

Devices:
  Mobile: 71%
  Desktop: 26%
  Tablet: 3%
```

---

# 34. Analytics Storage Strategy

Do not immediately design the analytics system around billions of individual SQL rows.

Start with aggregated data.

For example:

```text
UrlDailyStats
--------------------------------
UrlId
Date
ClickCount
```

and:

```text
UrlCountryDailyStats
--------------------------------
UrlId
Date
CountryCode
ClickCount
```

and:

```text
UrlDeviceDailyStats
--------------------------------
UrlId
Date
DeviceType
ClickCount
```

This dramatically reduces storage requirements.

---

# 35. Background Workers

Use ASP.NET Core hosted services initially:

```text
BackgroundService
```

Workers can process:

```text
Click events
Expired URLs
Analytics aggregation
Cleanup jobs
```

For example:

```text
Click Queue
     ↓
AnalyticsWorker
     ↓
Batch
     ↓
SQL Server
```

Batch inserts/updates rather than one SQL transaction per click.

---

# 36. Queue Choice

For the first implementation:

```text
Redis Streams
```

can be used to learn distributed event processing.

At larger scale, consider a dedicated message broker such as:

```text
Kafka
RabbitMQ
Azure Service Bus
```

The important architectural concept is:

> Redirect requests should be decoupled from analytics persistence.

---

# 37. Rate Limiting

Rate limiting must work across multiple API instances.

Do not rely only on:

```text
IMemoryCache
```

or an in-process counter.

Otherwise:

```text
API #1 → 100 requests
API #2 → 100 requests
API #3 → 100 requests
```

can bypass a supposed:

```text
100 requests/minute
```

limit.

Use Redis for distributed rate limiting.

---

# 38. Rate Limit Dimensions

Different endpoints can have different policies.

Example:

```text
POST /api/v1/auth/register
    5 requests / hour / IP

POST /api/v1/auth/login
    10 requests / minute / IP

POST /api/v1/urls
    100 requests / minute / user

GET /api/v1/urls
    60 requests / minute / user

GET /{shortCode}
    much higher limit
```

The redirect endpoint requires special treatment because legitimate traffic can be extremely high.

---

# 39. Rate-Limiting Algorithm

Good candidates:

```text
Token Bucket
Sliding Window
Fixed Window
```

For a distributed system, implement the counter/state in Redis.

Token Bucket is a particularly useful algorithm to understand because it supports bursts while maintaining an average rate.

---

# 40. Abuse Prevention

URL shorteners are attractive to attackers and spammers.

Add protections such as:

* Rate limiting
* Maximum URL length
* Allowed URI schemes
* HTTPS-only destinations, if appropriate
* Malware/phishing scanning
* Abuse reporting
* URL deactivation
* Domain restrictions where appropriate
* Authentication for high-volume creation
* CAPTCHA/challenge mechanisms where appropriate

Never allow arbitrary protocols such as:

```text
javascript:
data:
file:
```

as redirect destinations.

---

# 41. URL Validation

Use a strict validation policy.

At minimum:

```text
Absolute URI
HTTP
HTTPS
Reasonable maximum length
```

For example:

```text
https://example.com/...
```

is valid.

But:

```text
javascript:alert(...)
```

should be rejected.

Also consider SSRF-related risks if the server ever fetches the destination for validation/scanning.

---

# 42. Custom Aliases

Example:

```http
POST /api/v1/urls
```

```json
{
  "originalUrl": "https://example.com",
  "customAlias": "my-blog"
}
```

Result:

```text
https://myshort.com/my-blog
```

The database must enforce:

```sql
UNIQUE(ShortCode)
```

Do not rely solely on:

```text
SELECT ... WHERE ShortCode = ...
```

followed by an INSERT.

Two requests can race.

The database unique constraint is the final authority.

---

# 43. Reserved Aliases

Maintain a reserved namespace.

Examples:

```text
api
admin
login
register
health
swagger
favicon.ico
robots.txt
```

A user should not be able to create:

```text
/api
/admin
/login
```

as custom short codes.

---

# 44. Case Sensitivity

Choose whether:

```text
abc123
```

and:

```text
ABC123
```

are different.

For Base62-generated codes, case-sensitive codes maximize the namespace.

If using SQL Server, explicitly configure the column/database collation appropriately so that uniqueness behaves exactly as intended.

This decision should be made early.

---

# 45. QR Codes

Provide:

```http
GET /api/v1/urls/{id}/qr
```

Possible formats:

```text
PNG
SVG
```

Example:

```text
GET /api/v1/urls/123/qr?format=png
```

The QR code should contain:

```text
https://myshort.com/a8Fx21
```

Generate QR codes on demand or cache the generated image.

---

# 46. QR Code Caching

QR codes are deterministic.

For:

```text
a8Fx21
```

the QR image does not need to be regenerated every request.

Possible strategy:

```text
Redis
   ↓
QR cache
```

or:

```text
Object storage/CDN
```

for a larger deployment.

---

# 47. Authentication Model

For user-owned URLs:

```text
User
  │
  ├── URL A
  ├── URL B
  └── URL C
```

Every management request must verify ownership.

For example:

```text
GET /api/v1/urls/123
```

should not simply query:

```text
WHERE Id = 123
```

It should effectively enforce:

```text
WHERE Id = 123
AND UserId = currentUserId
```

This prevents IDOR/BOLA vulnerabilities.

---

# 48. API Security

Implement:

* HTTPS
* Authentication
* Authorization
* Input validation
* Rate limiting
* Request size limits
* Secure headers
* CORS policy
* Secret management
* SQL parameterization through EF Core
* Structured logging
* Audit logging for sensitive operations

Never put secrets in:

```text
appsettings.json
```

for production.

Use environment variables or a secret-management system.

---

# 49. Response Format

Use consistent API responses.

Successful response:

```json
{
  "data": {
    "id": "01K...",
    "shortCode": "a8Fx21"
  }
}
```

Error:

```json
{
  "type": "https://myshort.com/errors/validation",
  "title": "Validation failed",
  "status": 400,
  "detail": "The supplied URL is invalid.",
  "errors": {
    "originalUrl": [
      "The URL must use HTTP or HTTPS."
    ]
  }
}
```

Use ASP.NET Core's Problem Details support.

---

# 50. HTTP Status Codes

Use appropriate status codes.

```text
201 Created
200 OK
204 No Content
400 Bad Request
401 Unauthorized
403 Forbidden
404 Not Found
409 Conflict
422 Unprocessable Content
429 Too Many Requests
500 Internal Server Error
```

For custom alias conflicts:

```text
409 Conflict
```

is appropriate.

---

# 51. Idempotency

Creating a URL is not necessarily idempotent.

Two identical requests could intentionally create:

```text
abc123
xyz789
```

Therefore:

```text
POST /api/v1/urls
```

should normally create a new resource.

For clients that need safe retries, support an optional:

```text
Idempotency-Key
```

header.

Example:

```http
Idempotency-Key: 01JXYZ...
```

Store the result temporarily in Redis.

---

# 52. Observability

Implement three pillars:

```text
Logs
Metrics
Traces
```

## Logs

Use structured logging.

Example:

```json
{
  "event": "url_redirect",
  "shortCode": "a8Fx21",
  "cacheHit": true,
  "durationMs": 1.4
}
```

Do not log sensitive information unnecessarily.

---

# 53. Metrics

Track:

```text
http_requests_total
http_request_duration
redirect_requests_total
redirect_cache_hits
redirect_cache_misses
sql_query_duration
redis_operation_duration
url_creation_total
rate_limit_rejections
analytics_events_processed
analytics_queue_depth
```

Important metric:

```text
Redis cache hit ratio
```

You want the redirect path to have a very high cache-hit rate.

---

# 54. Distributed Tracing

Use OpenTelemetry.

Trace:

```text
HTTP request
   ↓
Redis
   ↓
SQL Server
```

For example:

```text
GET /a8Fx21
  ├── Redis GET       0.8ms
  └── Redirect        1.4ms
```

A cache miss:

```text
GET /a8Fx21
  ├── Redis GET       0.8ms
  ├── SQL SELECT      5.2ms
  ├── Redis SET       0.9ms
  └── Redirect        7.1ms
```

---

# 55. Health Checks

Provide:

```http
GET /health/live
GET /health/ready
```

Liveness:

```text
Is the process alive?
```

Readiness:

```text
Can this instance serve traffic?
```

Readiness can check:

```text
SQL Server
Redis
```

Do not make liveness depend on external services.

---

# 56. Configuration

Use strongly typed options.

Example conceptual configuration:

```text
Database
Redis
Jwt
RateLimiting
UrlShortening
Analytics
QrCode
```

Avoid scattering:

```text
IConfiguration["Some:Random:Setting"]
```

throughout the application.

Prefer:

```text
IOptions<T>
```

with validation.

---

# 57. Dependency Injection

Use ASP.NET Core built-in DI.

Example conceptual registrations:

```text
IUrlService
IUrlRepository
ICacheService
IShortCodeGenerator
IAnalyticsPublisher
IQRCodeGenerator
IRateLimiter
```

Infrastructure implements these abstractions.

---

# 58. Concurrency

The system must assume concurrent operations.

Example:

```text
Request A → custom alias "github"
Request B → custom alias "github"
```

Both can check:

```text
Does github exist?
```

and both could receive:

```text
No
```

Then both attempt INSERT.

Only the database constraint should determine the winner.

The API converts the resulting unique-constraint violation into:

```text
409 Conflict
```

---

# 59. Redirect Concurrency

Multiple instances can simultaneously request the same missing URL.

Use:

```text
Redis distributed locking
```

or request coalescing.

Conceptually:

```text
          Redis MISS
              │
       ┌──────┴──────┐
       │             │
    Request A     Request B
       │             │
    gets lock       waits
       │             │
    SQL query        │
       │             │
    Redis SET        │
       │             │
       └──────┬──────┘
              ▼
          Redirect
```

This is particularly useful for very hot keys.

---

# 60. Database Connection Pooling

At high API concurrency, do not create unlimited SQL connections.

Use the connection pool provided by the SQL Server driver.

Monitor:

```text
Connection pool utilization
Query latency
Blocked queries
Deadlocks
CPU
IO
```

Scale the database based on measurements.

---

# 61. SQL Server Read Scaling

Initially:

```text
ASP.NET
   ↓
SQL Server
```

Later:

```text
             ┌── Read Replica
             │
ASP.NET ─────┼── Primary
             │
             └── Read Replica
```

However, don't prematurely add replicas.

Because the redirect path should mostly use Redis, the first scaling solution should be:

```text
Redis + more API instances
```

rather than immediately adding many SQL replicas.

---

# 62. SQL Server Partitioning

Do not partition the main `Urls` table initially.

If analytics data becomes enormous, partition analytics tables by:

```text
Date
```

For example:

```text
2026-09
2026-10
2026-11
...
```

Partitioning should solve a demonstrated problem, not be added simply because the system is "large."

---

# 63. Read-Heavy Scaling Strategy

At:

```text
100,000 redirects/sec
```

the primary scaling path is:

```text
                 Load Balancer
                      │
       ┌──────────────┼──────────────┐
       ▼              ▼              ▼
    API #1          API #2          API #N
       │              │              │
       └──────────────┼──────────────┘
                      ▼
                    Redis
                      │
                 Cache misses
                      ▼
                  SQL Server
```

Adding API servers is easy because the API should be stateless.

---

# 64. Stateless API

Do not store request/session state in:

```text
static variables
IMemoryCache
in-process sessions
```

if that state is required across requests.

Use:

```text
Redis
SQL Server
JWT
distributed storage
```

This allows:

```text
API #1
API #2
API #3
...
API #100
```

to behave equivalently.

---

# 65. CDN

A CDN can be useful for:

```text
QR images
static assets
documentation
```

However, be careful with CDN caching of redirects.

A CDN can potentially cache:

```text
302
```

responses, but invalidation and expiration behavior must be deliberately designed.

For the core project, Redis is the primary application cache.

---

# 66. Failure Scenarios

Design explicitly for failures.

## Redis unavailable

Question:

> Can the system still work?

Ideally:

```text
Redis DOWN
   ↓
API falls back to SQL Server
```

But this may overload SQL Server under high redirect traffic.

Therefore, implement:

* Circuit breaker
* Request limits
* Fail-fast behavior where appropriate
* Monitoring
* Redis redundancy

---

# 67. SQL Server Unavailable

Existing cached URLs should ideally continue redirecting.

Conceptually:

```text
Redis
   ↓
URL found
   ↓
Redirect
```

This means a temporary SQL outage does not necessarily break all redirects.

However:

```text
new URL creation
```

must fail if SQL Server is unavailable because SQL Server is the source of truth.

---

# 68. Redis High Availability

For production, use a highly available Redis deployment.

Depending on infrastructure:

```text
Redis Cluster
Redis Sentinel
Managed Redis
```

The exact choice depends on deployment environment.

Do not assume a single Redis container is production-ready.

---

# 69. SQL Server High Availability

Production options include:

```text
Always On Availability Groups
Managed SQL Server
Cloud SQL offerings
Database failover mechanisms
```

Again, start simple during development.

---

# 70. Docker Development Environment

Create:

```text
docker-compose.yml
```

with:

```text
API
SQL Server
Redis
```

Potential development architecture:

```text
docker compose
│
├── api
├── sqlserver
└── redis
```

This makes onboarding easy.

---

# 71. Production Deployment

The application should be containerized.

Example:

```text
Docker image
     ↓
Container platform
     ↓
Multiple API instances
```

Possible platforms:

```text
Kubernetes
Azure Container Apps
AWS ECS
Docker Swarm
VM-based deployment
```

You don't need Kubernetes to learn the architecture.

---

# 72. CI/CD

Pipeline:

```text
git push
   ↓
Build
   ↓
Unit tests
   ↓
Integration tests
   ↓
Security scanning
   ↓
Build Docker image
   ↓
Push image
   ↓
Deploy
   ↓
Smoke tests
```

Use database migrations carefully.

Never blindly run destructive migrations in production.

---

# 73. Database Migrations

Use EF Core migrations.

Development:

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

Production:

```text
Migration artifact
       ↓
Deployment pipeline
       ↓
Controlled migration
```

Prefer controlled migration execution rather than allowing every API instance to race to migrate the database during startup.

---

# 74. Testing Strategy

Use three major categories.

## Unit Tests

Test:

```text
ShortCodeGenerator
UrlService
UrlValidation
ExpirationRules
AuthorizationRules
RateLimit logic
```

---

# 75. Integration Tests

Use real dependencies where practical:

```text
SQL Server
Redis
ASP.NET Core TestServer
```

Docker/Testcontainers is an excellent approach.

Test:

```text
Create URL
Custom alias
Duplicate alias
Expiration
Redirect
Cache hit
Cache miss
Authentication
Authorization
Rate limiting
```

---

# 76. Contract/API Tests

Verify:

```text
HTTP status
response schema
headers
Problem Details
authentication behavior
```

These tests protect the API contract.

---

# 77. Load Testing

This project should absolutely include load testing.

Test progressively:

```text
1,000 redirects/sec
10,000 redirects/sec
50,000 redirects/sec
100,000 redirects/sec
```

For creation:

```text
100 creations/sec
500 creations/sec
1,000 creations/sec
```

Tools:

```text
k6
NBomber
JMeter
wrk
```

For a .NET-focused project, **NBomber** is particularly useful.

---

# 78. Load-Test Scenarios

Test at least:

### Scenario A — Cache hit

```text
GET /a8Fx21
```

where Redis already contains the URL.

This represents the normal hot path.

### Scenario B — Cache miss

```text
GET /random-existing-code
```

with Redis intentionally cold.

### Scenario C — Invalid code

```text
GET /does-not-exist
```

### Scenario D — URL creation

```text
POST /api/v1/urls
```

### Scenario E — Hot key

Thousands of simultaneous requests for:

```text
/a8Fx21
```

This tests cache stampede protection.

---

# 79. Performance Goals

Set measurable goals.

For example:

```text
Redirect cache hit:
    p50 < 10 ms
    p95 < 50 ms
    p99 < 100 ms

URL creation:
    p95 < 200 ms

Redis cache hit ratio:
    > 95% for normal workloads
```

These are initial engineering targets, not guarantees.

Measure the real system before optimizing.

---

# 80. Logging Strategy

Avoid logging every successful redirect at high traffic.

At:

```text
100,000 requests/sec
```

logging every request produces enormous amounts of data.

Instead:

```text
Metrics → every request
Logs → sampled/important events
Analytics → asynchronous events
Tracing → sampled traces
```

This distinction is important.

---

# 81. Caching Layers

Eventually you may have:

```text
Browser cache
     ↓
CDN
     ↓
Load balancer
     ↓
ASP.NET
     ↓
Redis
     ↓
SQL Server
```

Do not introduce every layer on day one.

Build in this order:

```text
SQL Server
   ↓
Redis
   ↓
Horizontal API scaling
   ↓
Queue/background processing
   ↓
CDN if necessary
```

---

# 82. Recommended Domain Model

Conceptually:

```text
User
 ├── Id
 ├── Email
 ├── CreatedAt
 └── ...

Url
 ├── Id
 ├── ShortCode
 ├── OriginalUrl
 ├── UserId
 ├── CreatedAt
 ├── ExpiresAt
 ├── IsActive
 └── DeletedAt

UrlDailyStat
 ├── UrlId
 ├── Date
 └── ClickCount

UrlCountryDailyStat
 ├── UrlId
 ├── Date
 ├── CountryCode
 └── ClickCount
```

---

# 83. Entity Relationships

```text
User
 │
 │ 1
 │
 │ *
 ▼
Url
 │
 ├──────────────► UrlDailyStat
 │
 └──────────────► UrlCountryDailyStat
```

A URL belongs to a user.

Analytics belong to a URL.

---

# 84. Recommended Short-Code Generation

Initial implementation:

```text
RandomNumberGenerator
        ↓
random bytes
        ↓
Base62
        ↓
6-8 character code
```

Use enough entropy to make collisions extremely unlikely.

Do not use predictable sequential codes such as:

```text
000001
000002
000003
```

because they make enumeration easy.

---

# 85. URL Enumeration Protection

Even random short codes can theoretically be brute-forced.

Add:

```text
Rate limiting
Random codes
Abuse detection
Optional authorization for private URLs
```

If private URLs are introduced later, require authentication rather than relying on obscurity.

---

# 86. Privacy

Analytics should be designed with privacy in mind.

Avoid collecting unnecessary personal data.

Instead of storing:

```text
IP = 192.168.1.10
```

consider deriving only the information needed:

```text
Country = EG
Device = Mobile
Browser = Chrome
```

If IP-derived geolocation is needed, process it transiently and avoid retaining the raw address unless there is a legitimate requirement.

---

# 87. Caching User Data

Do not aggressively cache everything.

Useful candidates:

```text
ShortCode → URL
```

Potential candidates:

```text
User profile
URL statistics
```

The most valuable cache is the redirect mapping.

---

# 88. Cache Invalidation

When a URL changes:

```text
SQL Server
    ↓
Update URL
    ↓
Redis DEL url:a8Fx21
```

Never update Redis first and SQL later without a deliberate consistency strategy.

SQL remains authoritative.

---

# 89. Consistency Model

For redirects:

```text
Eventually consistent cache
```

is acceptable in many cases.

For ownership/custom aliases:

```text
Strong consistency
```

is required.

For example:

```text
Two users cannot own the same alias.
```

SQL Server's unique constraint guarantees this.

---

# 90. Distributed System Principle

The system should explicitly distinguish:

```text
Strong consistency
```

from:

```text
Eventual consistency
```

Use strong consistency for:

```text
URL creation
Custom alias uniqueness
User ownership
Authentication
```

Use eventual consistency for:

```text
Click statistics
Analytics dashboards
Cache updates
```

---

# 91. Recommended Project Phases

## Phase 1 — Basic API

Implement:

```text
ASP.NET Core 10
SQL Server
EF Core
```

Features:

* Create URL
* Redirect
* List URLs
* Get URL
* Delete URL

No Redis yet.

Goal:

> Understand the basic domain.

---

# 92. Phase 2 — Authentication

Add:

```text
Users
Registration
Login
JWT
Authorization
```

Ensure URLs belong to users.

Goal:

> Learn authentication and resource authorization.

---

# 93. Phase 3 — Custom Aliases

Implement:

```text
customAlias
```

Add:

```text
UNIQUE INDEX
```

Handle:

```text
409 Conflict
```

Goal:

> Learn concurrency and database constraints.

---

# 94. Phase 4 — Expiration

Add:

```text
ExpiresAt
```

Implement:

```text
Expired URL detection
Cleanup worker
```

Goal:

> Learn time-based lifecycle management.

---

# 95. Phase 5 — Redis

Introduce:

```text
Redis
```

Implement:

```text
Cache-aside
TTL
Cache invalidation
Negative caching
```

Goal:

> Move the redirect path away from SQL Server.

---

# 96. Phase 6 — Distributed Rate Limiting

Implement:

```text
Redis-based rate limiting
```

Test:

```text
multiple API instances
```

Goal:

> Understand distributed state.

---

# 97. Phase 7 — Analytics

Implement:

```text
Click event
   ↓
Queue
   ↓
Background worker
   ↓
Aggregated SQL statistics
```

Goal:

> Understand asynchronous processing.

---

# 98. Phase 8 — QR Codes

Implement:

```text
GET /api/v1/urls/{id}/qr
```

Add caching.

Goal:

> Learn binary responses and deterministic resource generation.

---

# 99. Phase 9 — Observability

Add:

```text
OpenTelemetry
Structured logging
Metrics
Health checks
Tracing
```

Goal:

> Learn how production systems are operated.

---

# 100. Phase 10 — Horizontal Scaling

Run:

```text
API #1
API #2
API #3
API #4
```

behind a load balancer.

Verify that:

* Authentication works
* Rate limiting works
* Redis state is shared
* Cache works across instances
* No local state is required

Goal:

> Make the API genuinely stateless.

---

# 101. Phase 11 — Load Testing

Start benchmarking.

Example:

```text
10k redirects/sec
25k redirects/sec
50k redirects/sec
100k redirects/sec
```

Measure:

```text
CPU
Memory
Redis
SQL Server
Network
Latency
Error rate
Cache hit ratio
```

Find the bottleneck.

---

# 102. Phase 12 — Failure Testing

Intentionally break components.

Test:

```text
Redis unavailable
SQL unavailable
Redis latency
SQL latency
API instance crashes
Worker crashes
Queue backlog
Database deadlocks
```

The goal is to understand:

> What happens when something fails?

rather than merely:

> What happens when everything works?

---

# 103. Final Production Architecture

A mature version could look like:

```text
                         Internet
                            │
                            ▼
                    ┌───────────────┐
                    │ CDN / WAF     │
                    └───────┬───────┘
                            │
                            ▼
                    ┌───────────────┐
                    │ Load Balancer │
                    └───────┬───────┘
                            │
             ┌──────────────┼──────────────┐
             │              │              │
             ▼              ▼              ▼
        ┌─────────┐    ┌─────────┐    ┌─────────┐
        │ API #1  │    │ API #2  │    │ API #N  │
        └────┬────┘    └────┬────┘    └────┬────┘
             │              │              │
             └──────────────┼──────────────┘
                            │
               ┌────────────┴────────────┐
               │                         │
               ▼                         ▼
        ┌──────────────┐         ┌──────────────┐
        │ Redis        │         │ SQL Server   │
        │ Cluster      │         │ Primary      │
        └──────┬───────┘         └──────┬───────┘
               │                        │
               │                 ┌──────┴──────┐
               │                 │             │
               │                 ▼             ▼
               │             Read Replica   Analytics
               │
               ▼
        Hot URL Cache

                            │
                            ▼
                     ┌──────────────┐
                     │ Message      │
                     │ Broker       │
                     └──────┬───────┘
                            │
                            ▼
                     ┌──────────────┐
                     │ Workers      │
                     │ Analytics    │
                     │ Cleanup      │
                     └──────────────┘
```

---

# 104. Most Important Design Decision

The most important insight in this project is:

```text
100,000 redirects/sec
```

does **not** mean:

```text
100,000 SQL queries/sec
```

Instead:

```text
100,000 redirects/sec
        │
        ▼
      Redis
        │
        ▼
   URL destination
        │
        ▼
    HTTP 302
```

SQL Server becomes the durable source of truth rather than the bottleneck on every read.

This is the central architectural lesson of the project.

---

# 105. What You Should Learn From Each Component

| Component             | Engineering concept                  |
| --------------------- | ------------------------------------ |
| ASP.NET Core 10       | API design, middleware, DI           |
| EF Core               | ORM and database access              |
| SQL Server            | persistence, indexes, transactions   |
| Redis                 | caching and distributed state        |
| Rate limiter          | distributed coordination             |
| BackgroundService     | asynchronous processing              |
| Message broker        | event-driven architecture            |
| OpenTelemetry         | observability                        |
| Docker                | reproducible environments            |
| Load balancer         | horizontal scaling                   |
| Load testing          | capacity planning                    |
| SQL indexes           | query performance                    |
| Cache invalidation    | consistency                          |
| Short-code generation | distributed ID generation            |
| Analytics             | aggregation and eventual consistency |

---

# 106. Recommended Definition of Done

The project is complete when you can demonstrate:

### Functional

* [ ] User registration
* [ ] User login
* [ ] Create short URL
* [ ] Custom aliases
* [ ] URL expiration
* [ ] Redirect
* [ ] URL management
* [ ] QR code
* [ ] Click statistics

### Security

* [ ] Authentication
* [ ] Authorization
* [ ] Input validation
* [ ] Rate limiting
* [ ] Abuse protection
* [ ] Secure URL schemes
* [ ] No IDOR/BOLA
* [ ] Secrets outside source code

### Performance

* [ ] Redis cache
* [ ] Cache invalidation
* [ ] Cache stampede protection
* [ ] Database indexes
* [ ] Async analytics
* [ ] Stateless API
* [ ] Horizontal scaling

### Reliability

* [ ] Health checks
* [ ] Redis failure handling
* [ ] SQL failure handling
* [ ] Background-worker recovery
* [ ] Retry policies
* [ ] Circuit breakers
* [ ] Graceful shutdown

### Observability

* [ ] Structured logs
* [ ] Metrics
* [ ] Distributed tracing
* [ ] Cache hit/miss metrics
* [ ] Database performance metrics
* [ ] Queue-depth metrics

### Testing

* [ ] Unit tests
* [ ] Integration tests
* [ ] API/contract tests
* [ ] Load tests
* [ ] Stress tests
* [ ] Failure tests

---

# 107. The Development Order I Recommend

Do **not** start by implementing the 100,000 redirects/sec architecture.

Build it progressively:

```text
                    START
                      │
                      ▼
              ASP.NET Core API
                      │
                      ▼
                SQL Server
                      │
                      ▼
              Basic Redirect
                      │
                      ▼
                User Accounts
                      │
                      ▼
               Custom Aliases
                      │
                      ▼
                 Expiration
                      │
                      ▼
                   Redis
                      │
                      ▼
              Distributed Limits
                      │
                      ▼
              Async Analytics
                      │
                      ▼
               QR Generation
                      │
                      ▼
               Observability
                      │
                      ▼
              Docker Deployment
                      │
                      ▼
             Multiple API Nodes
                      │
                      ▼
                Load Testing
                      │
                      ▼
             Failure Testing
                      │
                      ▼
               Scale Analysis
                      │
                      ▼
                   DONE
```

The key is to **measure each stage before introducing the next optimization**.

The finished project should demonstrate not only that you can build REST endpoints, but that you understand why a system needs caching, indexes, distributed rate limiting, asynchronous processing, stateless services, consistency boundaries, and horizontal scaling.
