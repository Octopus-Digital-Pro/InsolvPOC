# Insolvex Development Rules

These rules are **mandatory** for all contributors. They enforce DDD, Clean Architecture, and maintainability across the solution.

---

## 1. Architecture & Layer Rules

| Layer             | Allowed dependencies               | Forbidden                                                 |
| ----------------- | ---------------------------------- | --------------------------------------------------------- |
| `Insolvex.Domain` | None                               | Everything above                                          |
| `Insolvex.Core`   | `Insolvex.Domain`                  | `Insolvex.API`, EF Core, ASP.NET                          |
| `Insolvex.API`    | `Insolvex.Core`, `Insolvex.Domain` | Direct `Insolvex.Domain` entity mutation from controllers |

- **DTOs and request/response models** live in `Insolvex.Core/DTOs/`. Never define them inline in controllers.
- **Service interfaces** live in `Insolvex.Core/Abstractions/`. Never in `Insolvex.API`.
- **Exception types** live in `Insolvex.Core/Exceptions/`.
- **Service implementations** live in `Insolvex.Core/Services/`.
- **Integration services and implementations** live in `Insolvex.Integrations/`.
- **Domain entities** live in `Insolvex.Domain/Entities/`. Never mutated outside a service.

---

## 2. Controller Rules

- Controllers are **thin orchestrators only** — no business logic, no EF Core queries.
- The only allowed operations in a controller method:
  1. Validate/parse input
  2. Call one service method
  3. Return an HTTP result
- **Never inject `ApplicationDbContext` into a controller.** If you find yourself writing `_db.` in a controller, extract the logic to a service.
- Always include a `CancellationToken ct = default` parameter on every async action.
- Return `IActionResult` or `ActionResult<T>`, not raw objects.
- Do not use `HttpContext.RequestServices.GetRequiredService<T>()` (service locator antipattern). Inject all dependencies via constructor.

```csharp
// ✅ CORRECT
[HttpPost]
public async Task<IActionResult> Create([FromBody] TribunalRequest request, CancellationToken ct)
{
    var result = await _service.CreateAsync(request, ct);
    return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
}

// ❌ WRONG — direct DB access, inline business logic
[HttpPost]
public async Task<IActionResult> Create([FromBody] TribunalRequest request)
{
    var entity = new Tribunal { ... };
    _db.Tribunals.Add(entity);
    await _db.SaveChangesAsync();
    return Ok(entity);
}
```

---

## 3. Service Rules

- Services are sealed classes implementing an interface from `Insolvex.Core/Abstractions/`.
- Services own all business logic and database access.
- Use `NotFoundException` when a requested resource does not exist.
- Use `ForbiddenException` when a user lacks permission to access a resource.
- Use `BusinessException` for domain rule violations (validation errors, illegal state transitions).
- Always set `LastModifiedOn` and `LastModifiedBy` on entity updates.
- Always call `IAuditService` after every mutating operation (create, update, delete, import, export).

```csharp
// ✅ CORRECT
public sealed class TribunalService : ITribunalService
{
    public async Task<TribunalDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Tribunals.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Tribunal", id);
        ...
    }
}
```

---

## 4. Dependency Injection Rules

- Always register services as **interface → implementation**:

  ```csharp
  // ✅ CORRECT
  builder.Services.AddScoped<ITribunalService, TribunalService>();

  // ❌ WRONG — concrete registration hides the contract
  builder.Services.AddScoped<TribunalService>();
  ```

- Use `AddScoped` for request-scoped services (EF Core context, most services).
- Use `AddSingleton` only for truly stateless or thread-safe services (e.g., `IDocumentSigningService`).
- Use `AddHostedService` for background services.

---

## 5. Exception Handling Rules

| Exception Type        | HTTP Status               | When to Use                          |
| --------------------- | ------------------------- | ------------------------------------ |
| `NotFoundException`   | 404 Not Found             | Entity with given ID does not exist  |
| `ForbiddenException`  | 403 Forbidden             | User exists but lacks permission     |
| `BusinessException`   | 400 Bad Request           | Domain rule violation, invalid state |
| Unhandled `Exception` | 500 Internal Server Error | Unexpected/infrastructure errors     |

- **Never return `null`** from a service when the resource was not found. Throw `NotFoundException` instead.
- **Never catch and swallow exceptions** in services. Let them propagate to `GlobalExceptionHandler`.
- Do not use `return NotFound()` / `return Forbid()` in controllers — throw the appropriate exception in the service.

---

## 6. Audit Log Rules

### Required: readable English action names

Audit action strings must be **plain English sentences** describing what happened, written from the user's perspective.

```csharp
// ✅ CORRECT — readable, human-friendly
await _audit.LogEntityAsync("Insolvency Case Opened", "InsolvencyCase", id, ...);
await _audit.LogAsync("User Logged In", userId);
await _audit.LogEntityAsync("Document Template Deleted", "DocumentTemplate", id, ...);

// ❌ WRONG — dot-notation codes are not human-readable
await _audit.LogEntityAsync("Case.Created", ...);
await _audit.LogAsync("User.Login", userId);
await _audit.LogEntityAsync("Template.Deleted", ...);
```

### Rules

- Log **every** mutating operation: Create, Update, Delete, Import, Export, Sign, Approve, Reject, etc.
- Always pass `oldValues` and `newValues` for Update operations.
- Use `Severity = "Warning"` for deletions and security events (password reset, key deactivation).
- Use `Severity = "Critical"` for destructive bulk operations (demo reset, bulk delete).
- Use `Category` to group related audit entries: `"CaseManagement"`, `"DocumentManagement"`, `"TaskManagement"`, `"EmailManagement"`, `"UserManagement"`, `"SystemData"`, etc.
- Create every audit log with a unique code and link i18n translations to the code when displaying , show corresponding name and description.

---

## 7. Naming Conventions

| Item            | Convention                               | Example               |
| --------------- | ---------------------------------------- | --------------------- |
| Interface       | `I` prefix + PascalCase                  | `ITribunalService`    |
| Service         | PascalCase, no suffix                    | `TribunalService`     |
| DTO (read)      | `EntityDto`                              | `TribunalDto`         |
| Request (write) | `EntityRequest` or `CreateEntityCommand` | `TribunalRequest`     |
| CSV row         | `EntityCsvRow`                           | `TribunalCsvRow`      |
| Controller      | `EntityController` plural                | `TribunalsController` |
| Exception       | Descriptive + `Exception`                | `NotFoundException`   |

---

## 8. Multi-Tenancy Rules

- All entities with a `TenantId` property must be filtered by tenant in every query.
- Use `IgnoreQueryFilters()` only when explicitly reading global/cross-tenant records (e.g., authority lists where `TenantId == null` means global).
- GlobalAdmin users (`_currentUser.IsGlobalAdmin == true`) may read all tenants. Regular users see only their own tenant.
- Never use `GlobalAdmin` privileges to write tenant-specific data under a different tenant's ID.
- Tenant overrides of global records must set `OverridesGlobalId` to link back to the global record.

---

## 9. Async / CancellationToken Rules

- Every public service method must accept `CancellationToken ct = default` and pass it to all EF Core and I/O calls.
- Every controller async action must accept `CancellationToken ct = default`.
- Never use `.Result` or `.Wait()` on async tasks — always `await`.
- Never use `Task.Run()` to offload work in web request handlers.

---

## 10. CSV Import / Export Rules

- CSV imports are handled exclusively in services (not controllers).
- Always validate each row inside a try/catch and collect errors rather than aborting the entire import.
- Return an `AuthorityImportResult` (or equivalent) with `{ Imported, Updated, Errors }` counts.
- CSV export returns `byte[]` from the service; the controller converts it to `File(bytes, "text/csv", filename)`.
- Use `CsvHelper` (already referenced) with `CultureInfo.InvariantCulture`.

---

## 11. What Belongs Where — Quick Reference

```
Request comes in
    ↓
Controller         ← Only: parse input, call service, return HTTP result
    ↓
Service            ← Business logic, DB access, validation, audit logging
    ↓
ApplicationDbContext / IAuditService / IEmailService / etc.
    ↓
Database / External services
```
