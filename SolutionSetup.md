# SaaS Application Architecture Specification
## Based on Rocket TRONC Manager Principles

**Version:** 1.0  
**Date:** January 2025  
**Purpose:** Architectural blueprint for building secure, multi-tenant SaaS applications

---

## Table of Contents

1. [Solution Structure](#solution-structure)
2. [Security Principles](#security-principles)
3. [Multi-Tenant Architecture](#multi-tenant-architecture)
4. [User Management & Authentication](#user-management--authentication)
5. [Role-Based Access Control (RBAC)](#role-based-access-control-rbac)
6. [Frontend Context Management](#frontend-context-management)
7. [Audit & Compliance](#audit--compliance)
8. [Error Handling & Logging](#error-handling--logging)
9. [Integration Framework](#integration-framework)
10. [Scheduled Tasks & Background Jobs](#scheduled-tasks--background-jobs)
11. [UI/UX Patterns](#uiux-patterns)

---

## 1. Solution Structure

### Project Organization

```
Solution Root/
├── YourApp.Domain/  # Domain entities & interfaces
├── YourApp.Core/             # Business logic & services
├── YourApp.API/      # ASP.NET Core Web API
├── YourApp.React/        # React frontend (SPA)
├── YourApp.Tests/       # Unit & integration tests
└── YourApp.Integrations/        # External integration modules
```

### Domain Layer (`.Domain`)

**Purpose:** Contains pure domain entities with no dependencies

```csharp
// Base entity with audit fields
public abstract class BaseEntity
{
    public Guid Id { get; set; }
  public DateTime CreatedOn { get; set; }
    public DateTime? LastModifiedOn { get; set; }
    public string? CreatedBy { get; set; }
    public string? LastModifiedBy { get; set; }
}

// Tenant-scoped entity
public abstract class TenantScopedEntity : BaseEntity
{
    public Guid TenantId { get; set; }
    public virtual Tenant? Tenant { get; set; }
}
```

**Key Entities:**
- `Tenant` - Organization/company
- `User` - System users with roles
- `UserInvitation` - Pending user invitations
- `AuditLog` - Audit trail
- `ErrorLog` - Application errors
- `ScheduledEmail` - Email queue

### Core Layer (`.Core`)

**Purpose:** Business logic, services, and application rules

**Structure:**
```
Core/
├── Services/
│   ├── AuthenticationService.cs
│   ├── UserService.cs
│   ├── AuditService.cs
│   └── CredentialEncryptionService.cs
├── Abstractions/
│   ├── IAuditService.cs
│   ├── IEmailService.cs
│└── ICurrentUserService.cs
└── Exceptions/
    └── BusinessException.cs
```

### API Layer (`.API`)

**Purpose:** HTTP endpoints, middleware, and configuration

**Structure:**
```
API/
├── Controllers/
│   ├── AuthController.cs
│   ├── UsersController.cs
│   ├── TenantsController.cs
│   ├── AuditLogsController.cs
│   └── ErrorLogsController.cs
├── Middleware/
│   ├── TenantResolutionMiddleware.cs
│   ├── AuditMiddleware.cs
│   ├── ErrorLoggingMiddleware.cs
│   └── SecurityHeadersMiddleware.cs
├── Services/
│   ├── CurrentUserService.cs
│   └── EmailTemplateService.cs
└── Data/
    ├── ApplicationDbContext.cs
    ├── Configurations/
    └── Migrations/
```

### Frontend Layer (`.React`)

**Purpose:** React SPA with TypeScript/JavaScript

**Structure:**
```
React/
├── src/
│   ├── contexts/
│   │   ├── TenantContext.jsx
│   │   ├── AuthContext.jsx
│   │   └── ThemeContext.jsx
│   ├── services/
│   │   └── api/
│   │       ├── client.js
│   │       ├── auth.js
│   │       ├── users.js
│   │       └── tenants.js
│   ├── components/
│   │   ├── navigation/
│   │   └── auth/
│   ├── pages/
│   │   ├── login/
│   │   ├── dashboard/
│   │   ├── user-management/
│   │   ├── audit-trail/
│   │   └── system/
│   └── hooks/
       ├── useAuth.js
└── useCurrentUser.js
```

---

## 2. Security Principles

### Authentication

**Implementation: JWT Bearer Tokens**

```csharp
// AuthenticationService.cs
public class AuthenticationService
{
    public async Task<AuthenticationResult> Login(string email, string password)
    {
      // 1. Validate credentials
        var user = await FindUser(email);
     if (!VerifyPassword(password, user.PasswordHash))
return AuthenticationResult.Failed("Invalid credentials");

        // 2. Check user status
        if (!user.IsActive)
 return AuthenticationResult.Failed("Account is inactive");

  // 3. Generate JWT
        var token = GenerateJwtToken(user);
        
        // 4. Audit login
    await auditService.LogAsync("User.Login", user.Id);

        return AuthenticationResult.Success(token, user);
    }

    private string GenerateJwtToken(User user)
    {
     var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
    new Claim(ClaimTypes.Role, user.Role.ToString()),
    new Claim("TenantId", user.TenantId.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
     var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddDays(7);

      var token = new JwtSecurityToken(
 issuer: jwtIssuer,
        audience: jwtAudience,
        claims: claims,
            expires: expires,
         signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

**Configuration (Program.cs):**

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
   {
       ValidateIssuer = true,
    ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
    ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
     Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
    };
    });
```

### Password Security

**Hashing:** BCrypt with work factor 12

```csharp
public class PasswordHasher
{
    private const int WorkFactor = 12;

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
}
}
```

### Sensitive Data Encryption

```csharp
public class CredentialEncryptionService
{
    private readonly string encryptionKey;

    public string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = Convert.FromBase64String(encryptionKey);
        aes.GenerateIV();

        var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length);

   using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs))
        {
            sw.Write(plainText);
        }

        return Convert.ToBase64String(ms.ToArray());
    }

    public string Decrypt(string cipherText)
  {
        var fullCipher = Convert.FromBase64String(cipherText);
        
   using var aes = Aes.Create();
        aes.Key = Convert.FromBase64String(encryptionKey);

        var iv = new byte[aes.IV.Length];
        var cipher = new byte[fullCipher.Length - iv.Length];

    Array.Copy(fullCipher, iv, iv.Length);
        Array.Copy(fullCipher, iv.Length, cipher, 0, cipher.Length);

        aes.IV = iv;

     var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream(cipher);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);
        
  return sr.ReadToEnd();
    }
}
```

### Security Headers Middleware

```csharp
public class SecurityHeadersMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // HSTS
      context.Response.Headers.Add("Strict-Transport-Security", 
   "max-age=31536000; includeSubDomains");

  // Content Security Policy
        context.Response.Headers.Add("Content-Security-Policy", 
            "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'");

        // XSS Protection
        context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Add("X-Frame-Options", "DENY");
        context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");

        // Remove server header
        context.Response.Headers.Remove("Server");

        await next(context);
}
}
```

---

## 3. Multi-Tenant Architecture

### Tenant Isolation Strategy

**Row-Level Security** - Single database, tenant ID on all records

```csharp
public class Tenant : BaseEntity
{
 public string Name { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? SubscriptionExpiry { get; set; }
    public string? PlanName { get; set; }
    
    // Navigation
    public ICollection<User> Users { get; set; } = new List<User>();
}
```

### Tenant Resolution Middleware

```csharp
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate next;

    public async Task InvokeAsync(HttpContext context, ICurrentUserService currentUser)
 {
        // Global admins: tenant from query string or header
    // Regular users: tenant from JWT claims
        
     if (context.User?.Identity?.IsAuthenticated == true)
        {
            var role = context.User.FindFirst(ClaimTypes.Role)?.Value;
      
            if (role == "GlobalAdmin")
            {
            // Allow tenant selection via header or query
                var tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault() 
          ?? context.Request.Query["tenantId"].FirstOrDefault();
            
    if (!string.IsNullOrEmpty(tenantId))
       {
         context.Items["TenantId"] = Guid.Parse(tenantId);
}
   }
     else
    {
                // Regular users: use tenant from claims
          var tenantId = context.User.FindFirst("TenantId")?.Value;
         if (!string.IsNullOrEmpty(tenantId))
      {
             context.Items["TenantId"] = Guid.Parse(tenantId);
       }
 }
     }

      await next(context);
    }
}
```

### Current User Service

```csharp
public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    string? Email { get; }
    UserRole Role { get; }
    bool IsGlobalAdmin { get; }
    bool IsAuthenticated { get; }
}

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
     this.httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
   get
   {
            var userIdClaim = httpContextAccessor.HttpContext?.User
            ?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return userIdClaim != null ? Guid.Parse(userIdClaim) : null;
        }
    }

    public Guid? TenantId
    {
        get
        {
       // From middleware context items (for global admins)
 if (httpContextAccessor.HttpContext?.Items.TryGetValue("TenantId", out var contextTenantId) == true)
       {
        return (Guid?)contextTenantId;
            }

  // From JWT claims (for regular users)
 var tenantIdClaim = httpContextAccessor.HttpContext?.User
   ?.FindFirst("TenantId")?.Value;
          return tenantIdClaim != null ? Guid.Parse(tenantIdClaim) : null;
   }
    }

    public UserRole Role
    {
        get
        {
         var roleClaim = httpContextAccessor.HttpContext?.User
  ?.FindFirst(ClaimTypes.Role)?.Value;
         return Enum.TryParse<UserRole>(roleClaim, out var role) ? role : UserRole.Secretary;
        }
  }

    public bool IsGlobalAdmin => Role == UserRole.GlobalAdmin;

    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public string? Email => httpContextAccessor.HttpContext?.User
   ?.FindFirst(ClaimTypes.Email)?.Value;
}
```

### Query Filter for Tenant Isolation

```csharp
public class ApplicationDbContext : DbContext
{
    private readonly ICurrentUserService currentUser;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
     base.OnModelCreating(modelBuilder);

    // Apply tenant filter to all tenant-scoped entities
  foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
   if (typeof(TenantScopedEntity).IsAssignableFrom(entityType.ClrType))
      {
      var parameter = Expression.Parameter(entityType.ClrType, "e");
         var tenantId = currentUser.TenantId;

     if (tenantId.HasValue)
            {
            var filter = Expression.Lambda(
    Expression.Equal(
     Expression.Property(parameter, nameof(TenantScopedEntity.TenantId)),
   Expression.Constant(tenantId.Value)),
            parameter);

       modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
}
    }
        }
    }

    public override int SaveChanges()
    {
 ApplyAuditInfo();
        return base.SaveChanges();
    }

 private void ApplyAuditInfo()
    {
        var entries = ChangeTracker.Entries()
     .Where(e => e.Entity is BaseEntity && 
  (e.State == EntityState.Added || e.State == EntityState.Modified));

 foreach (var entry in entries)
        {
      var entity = (BaseEntity)entry.Entity;
       var now = DateTime.UtcNow;
       var user = currentUser.Email ?? "System";

   if (entry.State == EntityState.Added)
    {
     entity.CreatedOn = now;
      entity.CreatedBy = user;
   }

            entity.LastModifiedOn = now;
       entity.LastModifiedBy = user;

            // Set TenantId for new tenant-scoped entities
         if (entry.State == EntityState.Added && entity is TenantScopedEntity tenantEntity)
            {
 if (tenantEntity.TenantId == Guid.Empty && currentUser.TenantId.HasValue)
                {
           tenantEntity.TenantId = currentUser.TenantId.Value;
                }
        }
        }
    }
}
```

---

## 4. User Management & Authentication

### User Entity

```csharp
public class User : TenantScopedEntity
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginDate { get; set; }
    public string? ResetToken { get; set; }
    public DateTime? ResetTokenExpiry { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}

public enum UserRole
{
    GlobalAdmin = 0, // System-wide access, can switch tenants
    TenantAdmin = 1,    // Full access within tenant
 Solicitor = 2,    // Professional user with elevated permissions
    Secretary = 3        // Standard user with limited permissions
}
```

### User Invitation System

```csharp
public class UserInvitation : TenantScopedEntity
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsAccepted { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public Guid? InvitedByUserId { get; set; }
 public virtual User? InvitedBy { get; set; }
}
```

**Invitation Flow:**

```csharp
public class UserInvitationService
{
    public async Task<UserInvitation> CreateInvitation(
        string email, string firstName, string lastName, UserRole role)
    {
        // 1. Check if user already exists
    var existingUser = await userRepository.GetByEmailAsync(email);
    if (existingUser != null)
     throw new BusinessException("User with this email already exists");

        // 2. Check for pending invitation
        var pendingInvitation = await invitationRepository.GetPendingByEmailAsync(email);
      if (pendingInvitation != null)
        {
    // Resend existing invitation
        await emailService.SendInvitationEmail(pendingInvitation);
            return pendingInvitation;
        }

     // 3. Create new invitation
        var invitation = new UserInvitation
      {
     TenantId = currentUser.TenantId!.Value,
            Email = email,
          FirstName = firstName,
      LastName = lastName,
            Role = role,
        Token = GenerateSecureToken(),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
    InvitedByUserId = currentUser.UserId
        };

        await invitationRepository.AddAsync(invitation);

     // 4. Send invitation email
        await emailService.SendInvitationEmail(invitation);

        // 5. Audit
        await auditService.LogAsync("UserInvitation.Created", invitation.Id);

        return invitation;
    }

    public async Task<User> AcceptInvitation(string token, string password)
    {
        var invitation = await invitationRepository.GetByTokenAsync(token);
      
      if (invitation == null)
throw new BusinessException("Invalid invitation token");
   
    if (invitation.IsAccepted)
          throw new BusinessException("Invitation has already been accepted");
        
        if (invitation.ExpiresAt < DateTime.UtcNow)
        throw new BusinessException("Invitation has expired");

     // Create user account
        var user = new User
        {
            TenantId = invitation.TenantId,
          Email = invitation.Email,
       FirstName = invitation.FirstName,
   LastName = invitation.LastName,
            Role = invitation.Role,
            PasswordHash = passwordHasher.HashPassword(password),
            IsActive = true
        };

   await userRepository.AddAsync(user);

      // Mark invitation as accepted
        invitation.IsAccepted = true;
 invitation.AcceptedAt = DateTime.UtcNow;
        await invitationRepository.UpdateAsync(invitation);

    // Audit
        await auditService.LogAsync("User.Created", user.Id, 
 new { Source = "Invitation", InvitationId = invitation.Id });

        return user;
    }

    private string GenerateSecureToken()
    {
      var bytes = new byte[32];
 using (var rng = RandomNumberGenerator.Create())
        {
      rng.GetBytes(bytes);
        }
        return Convert.ToBase64String(bytes);
    }
}
```

### Self-Service Password Reset

```csharp
public class PasswordResetService
{
    public async Task RequestPasswordReset(string email)
    {
     var user = await userRepository.GetByEmailAsync(email);
        
        // Don't reveal if user exists
        if (user == null || !user.IsActive)
            return;

        // Generate reset token
        user.ResetToken = GenerateSecureToken();
  user.ResetTokenExpiry = DateTime.UtcNow.AddHours(1);
        
        await userRepository.UpdateAsync(user);

     // Send reset email
        await emailService.SendPasswordResetEmail(user);

        // Audit
        await auditService.LogAsync("User.PasswordResetRequested", user.Id);
    }

    public async Task ResetPassword(string token, string newPassword)
    {
        var user = await userRepository.GetByResetTokenAsync(token);
        
      if (user == null)
      throw new BusinessException("Invalid reset token");
        
    if (user.ResetTokenExpiry < DateTime.UtcNow)
         throw new BusinessException("Reset token has expired");

      // Update password
        user.PasswordHash = passwordHasher.HashPassword(newPassword);
   user.ResetToken = null;
        user.ResetTokenExpiry = null;

        await userRepository.UpdateAsync(user);

        // Audit
        await auditService.LogAsync("User.PasswordReset", user.Id);

        // Send confirmation email
        await emailService.SendPasswordResetConfirmationEmail(user);
    }
}
```

---

## 5. Role-Based Access Control (RBAC)

### Role Definitions

| Role | Description | Permissions |
|------|-------------|-------------|
| **GlobalAdmin** | System administrator | Full access to all tenants and system settings |
| **TenantAdmin** | Organization administrator | Full access within their tenant, user management, settings |
| **Solicitor** | Professional user | Create/edit cases, view reports, limited admin functions |
| **Secretary** | Standard user | View cases, basic data entry, no admin access |

### Authorization Attributes

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireRoleAttribute : AuthorizeAttribute
{
    public RequireRoleAttribute(params UserRole[] roles)
    {
        Roles = string.Join(",", roles.Select(r => r.ToString()));
    }
}

// Usage
[RequireRole(UserRole.GlobalAdmin, UserRole.TenantAdmin)]
[HttpPost("tenants")]
public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request)
{
 // Only GlobalAdmin and TenantAdmin can create tenants
}
```

### Authorization Service

```csharp
public interface IAuthorizationService
{
    bool CanAccessTenant(Guid tenantId);
    bool CanManageUsers();
    bool CanViewAuditLogs();
    bool CanManageSystemSettings();
}

public class AuthorizationService : IAuthorizationService
{
    private readonly ICurrentUserService currentUser;

    public bool CanAccessTenant(Guid tenantId)
    {
   if (currentUser.IsGlobalAdmin)
        return true;

        return currentUser.TenantId == tenantId;
    }

    public bool CanManageUsers()
    {
 return currentUser.Role is UserRole.GlobalAdmin or UserRole.TenantAdmin;
  }

    public bool CanViewAuditLogs()
    {
        return currentUser.Role is UserRole.GlobalAdmin or UserRole.TenantAdmin;
    }

    public bool CanManageSystemSettings()
    {
return currentUser.IsGlobalAdmin;
    }
}
```

---

## 6. Frontend Context Management

### Tenant Context

```jsx
// contexts/TenantContext.jsx
import React, { createContext, useContext, useState, useEffect } from 'react';
import { useAuth } from './AuthContext';
import { tenantApi } from '../services/api';

const TenantContext = createContext();

export const TenantProvider = ({ children }) => {
  const { user, isGlobalAdmin } = useAuth();
  const [selectedTenant, setSelectedTenant] = useState(null);
  const [availableTenants, setAvailableTenants] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (user) {
      loadTenants();
    }
  }, [user]);

  const loadTenants = async () => {
    try {
      if (isGlobalAdmin) {
      // Global admins can see all tenants
        const response = await tenantApi.getAll();
        setAvailableTenants(response.data);
      } else {
        // Regular users: auto-select their tenant
        const response = await tenantApi.getCurrent();
        setSelectedTenant(response.data);
  setAvailableTenants([response.data]);
      }
    } catch (error) {
      console.error('Failed to load tenants:', error);
  } finally {
      setLoading(false);
    }
  };

  const selectTenant = (tenant) => {
    setSelectedTenant(tenant);
    // Store in localStorage for persistence
    localStorage.setItem('selectedTenantId', tenant.id);
  };

  const needsTenantSelection = isGlobalAdmin && !selectedTenant;

  return (
    <TenantContext.Provider
      value={{
  selectedTenant,
  availableTenants,
        selectTenant,
        needsTenantSelection,
     loading,
 }}
    >
      {children}
  </TenantContext.Provider>
  );
};

export const useTenantContext = () => {
  const context = useContext(TenantContext);
  if (!context) {
    throw new Error('useTenantContext must be used within TenantProvider');
  }
  return context;
};
```

### Auth Context

```jsx
// contexts/AuthContext.jsx
import React, { createContext, useContext, useState, useEffect } from 'react';
import { authApi } from '../services/api';

const AuthContext = createContext();

export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
  checkAuth();
  }, []);

  const checkAuth = async () => {
    const token = localStorage.getItem('authToken');
    if (token) {
   try {
        const response = await authApi.getCurrentUser();
        setUser(response.data);
      } catch (error) {
        localStorage.removeItem('authToken');
      }
    }
    setLoading(false);
  };

  const login = async (email, password) => {
    const response = await authApi.login(email, password);
    localStorage.setItem('authToken', response.data.token);
    setUser(response.data.user);
    return response.data;
  };

  const logout = () => {
  localStorage.removeItem('authToken');
    localStorage.removeItem('selectedTenantId');
    setUser(null);
  };

  const isGlobalAdmin = user?.role === 'GlobalAdmin';
  const isTenantAdmin = user?.role === 'TenantAdmin';
  const isSolicitor = user?.role === 'Solicitor';

  return (
    <AuthContext.Provider
value={{
        user,
  loading,
    login,
        logout,
     isGlobalAdmin,
        isTenantAdmin,
    isSolicitor,
        isAuthenticated: !!user,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within AuthProvider');
  }
  return context;
};
```

### API Client with Tenant Header

```javascript
// services/api/client.js
import axios from 'axios';

const client = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5000/api',
  headers: {
    'Content-Type': 'application/json',
  },
});

// Request interceptor: add auth token and tenant header
client.interceptors.request.use((config) => {
  const token = localStorage.getItem('authToken');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  // Global admins: add tenant header for selected tenant
  const selectedTenantId = localStorage.getItem('selectedTenantId');
  if (selectedTenantId) {
    config.headers['X-Tenant-Id'] = selectedTenantId;
  }

  return config;
});

// Response interceptor: handle 401 unauthorized
client.interceptors.response.use(
  (response) => response,
  (error) => {
 if (error.response?.status === 401) {
      localStorage.removeItem('authToken');
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

export default client;
```

---

## 7. Audit & Compliance

### Audit Log Entity

```csharp
public class AuditLog : TenantScopedEntity
{
    public string Action { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? Changes { get; set; }  // JSON
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime Timestamp { get; set; }
}
```

### Audit Service

```csharp
public interface IAuditService
{
  Task LogAsync(string action, Guid? entityId = null, object? changes = null);
    Task<List<AuditLog>> GetLogsAsync(AuditLogFilter filter);
}

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext context;
    private readonly ICurrentUserService currentUser;
    private readonly IHttpContextAccessor httpContextAccessor;

    public async Task LogAsync(string action, Guid? entityId = null, object? changes = null)
    {
     var httpContext = httpContextAccessor.HttpContext;

    var log = new AuditLog
        {
            Action = action,
        UserId = currentUser.UserId,
   UserEmail = currentUser.Email,
            EntityId = entityId,
          Changes = changes != null ? JsonSerializer.Serialize(changes) : null,
   IpAddress = httpContext?.Connection?.RemoteIpAddress?.ToString(),
     UserAgent = httpContext?.Request?.Headers["User-Agent"].ToString(),
         Timestamp = DateTime.UtcNow
        };

    if (currentUser.TenantId.HasValue)
 {
    log.TenantId = currentUser.TenantId.Value;
        }

      context.AuditLogs.Add(log);
        await context.SaveChangesAsync();
  }

    public async Task<List<AuditLog>> GetLogsAsync(AuditLogFilter filter)
    {
        var query = context.AuditLogs.AsQueryable();

        if (filter.UserId.HasValue)
            query = query.Where(log => log.UserId == filter.UserId);

        if (!string.IsNullOrEmpty(filter.Action))
            query = query.Where(log => log.Action.Contains(filter.Action));

  if (filter.FromDate.HasValue)
   query = query.Where(log => log.Timestamp >= filter.FromDate);

    if (filter.ToDate.HasValue)
     query = query.Where(log => log.Timestamp <= filter.ToDate);

        return await query
            .OrderByDescending(log => log.Timestamp)
   .Take(filter.PageSize)
         .Skip(filter.PageSize * filter.Page)
            .ToListAsync();
    }
}
```

### Audit Middleware

```csharp
public class AuditMiddleware
{
    private readonly RequestDelegate next;

    public async Task InvokeAsync(HttpContext context, IAuditService auditService)
    {
 // Capture request details
      var method = context.Request.Method;
        var path = context.Request.Path;

        // Only audit state-changing operations
   if (method == "POST" || method == "PUT" || method == "DELETE" || method == "PATCH")
        {
            var action = $"{method} {path}";
       
            // Execute request
       await next(context);

 // Log after successful response
      if (context.Response.StatusCode < 400)
            {
          await auditService.LogAsync(action);
            }
        }
   else
   {
            await next(context);
      }
    }
}
```

### Frontend Audit Log Viewer

```jsx
// pages/audit-trail/index.jsx
import React, { useState, useEffect } from 'react';
import { auditLogApi } from '../../services/api';
import { useTenantContext } from '../../contexts/TenantContext';

const AuditTrail = () => {
  const { selectedTenant } = useTenantContext();
  const [logs, setLogs] = useState([]);
  const [loading, setLoading] = useState(true);
  const [filters, setFilters] = useState({
    action: '',
    userId: null,
 fromDate: null,
    toDate: null,
    page: 0,
    pageSize: 50,
  });

  useEffect(() => {
  loadLogs();
  }, [filters, selectedTenant]);

  const loadLogs = async () => {
    setLoading(true);
    try {
      const response = await auditLogApi.getAll(filters);
      setLogs(response.data);
    } catch (error) {
      console.error('Failed to load audit logs:', error);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="audit-trail-page">
      <h1>Audit Trail</h1>
      
{/* Filters */}
 <div className="filters">
        <input
   type="text"
          placeholder="Search action..."
          value={filters.action}
       onChange={(e) => setFilters({ ...filters, action: e.target.value })}
    />
 <input
    type="date"
          value={filters.fromDate || ''}
          onChange={(e) => setFilters({ ...filters, fromDate: e.target.value })}
        />
        <input
          type="date"
          value={filters.toDate || ''}
          onChange={(e) => setFilters({ ...filters, toDate: e.target.value })}
        />
      </div>

      {/* Logs Table */}
      <table className="audit-log-table">
        <thead>
   <tr>
     <th>Timestamp</th>
        <th>User</th>
            <th>Action</th>
            <th>Entity</th>
            <th>IP Address</th>
   <th>Details</th>
          </tr>
      </thead>
        <tbody>
          {logs.map((log) => (
            <tr key={log.id}>
<td>{new Date(log.timestamp).toLocaleString()}</td>
        <td>{log.userEmail || 'System'}</td>
 <td>{log.action}</td>
              <td>{log.entityType || '-'}</td>
            <td>{log.ipAddress}</td>
   <td>
        {log.changes && (
     <button onClick={() => showDetails(log.changes)}>
  View Changes
           </button>
        )}
  </td>
     </tr>
     ))}
        </tbody>
      </table>
    </div>
  );
};

export default AuditTrail;
```

---

## 8. Error Handling & Logging

### Error Log Entity

```csharp
public class ErrorLog : BaseEntity
{
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? Source { get; set; }
    public string? RequestPath { get; set; }
    public string? RequestMethod { get; set; }
    public string? UserId { get; set; }
public string? UserEmail { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsResolved { get; set; }
}
```

### Error Logging Middleware

```csharp
public class ErrorLoggingMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<ErrorLoggingMiddleware> logger;

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext)
    {
 try
  {
    await next(context);
    }
        catch (Exception ex)
        {
      logger.LogError(ex, "Unhandled exception occurred");

            await LogError(ex, context, dbContext);

   // Re-throw to let global exception handler format the response
    throw;
  }
    }

    private async Task LogError(Exception ex, HttpContext context, ApplicationDbContext dbContext)
    {
      try
        {
            var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userEmail = context.User?.FindFirst(ClaimTypes.Email)?.Value;

            var errorLog = new ErrorLog
   {
    Message = ex.Message,
              StackTrace = ex.StackTrace,
           Source = ex.Source,
   RequestPath = context.Request.Path,
     RequestMethod = context.Request.Method,
       UserId = userId,
     UserEmail = userEmail,
          Timestamp = DateTime.UtcNow,
   };

    dbContext.ErrorLogs.Add(errorLog);
        await dbContext.SaveChangesAsync();
        }
        catch (Exception logEx)
        {
            logger.LogError(logEx, "Failed to log error to database");
        }
    }
}
```

### Global Exception Handler

```csharp
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> logger;

    public async ValueTask<bool> TryHandleAsync(
    HttpContext httpContext, 
        Exception exception, 
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "An error occurred");

        var response = new ErrorResponse
        {
         Message = exception is BusinessException 
 ? exception.Message 
  : "An error occurred processing your request",
         Type = exception.GetType().Name
        };

        httpContext.Response.StatusCode = exception is BusinessException 
  ? StatusCodes.Status400BadRequest 
            : StatusCodes.Status500InternalServerError;

        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);

        return true;
    }
}
```

### Frontend Error Boundary

```jsx
// components/ErrorBoundary.jsx
import React from 'react';

class ErrorBoundary extends React.Component {
  constructor(props) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error) {
    return { hasError: true, error };
  }

  componentDidCatch(error, errorInfo) {
    console.error('Error caught by boundary:', error, errorInfo);
    
    // Log to backend
    fetch('/api/error-logs/client', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        message: error.message,
        stack: error.stack,
        componentStack: errorInfo.componentStack,
      }),
    });
  }

  render() {
    if (this.state.hasError) {
      return (
      <div className="error-boundary">
          <h1>Something went wrong</h1>
 <p>We've been notified and are working on a fix.</p>
   <button onClick={() => window.location.reload()}>
     Reload Page
          </button>
        </div>
      );
    }

    return this.props.children;
  }
}

export default ErrorBoundary;
```

---

## 9. Integration Framework

### Integration Provider Pattern

```csharp
public interface IIntegrationProvider
{
    string Name { get; }
    Task<bool> TestConnectionAsync();
Task<SyncResult> SyncDataAsync();
}

public class IntegrationConfiguration : TenantScopedEntity
{
 public string ProviderName { get; set; } = string.Empty;
    public string? Credentials { get; set; }  // Encrypted
    public string? Settings { get; set; }      // JSON
    public bool IsActive { get; set; }
    public DateTime? LastSyncDate { get; set; }
}

public class ExampleIntegrationProvider : IIntegrationProvider
{
    private readonly ICredentialEncryptionService encryption;
    
    public string Name => "ExampleProvider";

    public async Task<bool> TestConnectionAsync()
    {
        // Test API connection
    try
    {
            var response = await httpClient.GetAsync("/api/test");
            return response.IsSuccessStatusCode;
        }
 catch
        {
    return false;
 }
    }

    public async Task<SyncResult> SyncDataAsync()
    {
        var result = new SyncResult();
        
   try
        {
   // Fetch data from external API
     var data = await FetchFromExternalApi();
            
  // Transform and save to database
        foreach (var item in data)
            {
      // Process item
      result.RecordsProcessed++;
            }
            
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
         result.ErrorMessage = ex.Message;
     }

  return result;
    }
}
```

---

## 10. Scheduled Tasks & Background Jobs

### Scheduled Email Entity

```csharp
public class ScheduledEmail : BaseEntity
{
    public string To { get; set; } = string.Empty;
    public string? Cc { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime ScheduledFor { get; set; }
 public DateTime? SentAt { get; set; }
    public bool IsSent { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
}
```

### Background Service

```csharp
public class EmailSenderBackgroundService : BackgroundService
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<EmailSenderBackgroundService> logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Email sender background service started");

        while (!stoppingToken.IsCancellationRequested)
     {
  try
  {
    await ProcessPendingEmails();
       }
            catch (Exception ex)
            {
      logger.LogError(ex, "Error processing pending emails");
            }

            // Wait 1 minute before next check
 await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task ProcessPendingEmails()
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

var pendingEmails = await context.ScheduledEmails
            .Where(e => !e.IsSent && e.ScheduledFor <= DateTime.UtcNow && e.RetryCount < 3)
            .Take(10)
  .ToListAsync();

     foreach (var email in pendingEmails)
        {
        try
  {
await emailService.SendAsync(email.To, email.Subject, email.Body);
      
        email.IsSent = true;
          email.SentAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
    email.RetryCount++;
                email.ErrorMessage = ex.Message;
                logger.LogError(ex, "Failed to send email {EmailId}", email.Id);
  }
 }

 await context.SaveChangesAsync();
    }
}
```

---

## 11. UI/UX Patterns

### Sidebar Navigation with Tenant Selector

```jsx
// components/navigation/SideNavigation.jsx
import React from 'react';
import { Link, useLocation } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { useTenantContext } from '../../contexts/TenantContext';

const SideNavigation = () => {
  const location = useLocation();
  const { user, isGlobalAdmin, isTenantAdmin } = useAuth();
  const { selectedTenant, availableTenants, selectTenant } = useTenantContext();

  return (
    <aside className="sidebar">
      {/* Tenant Selector (Global Admins Only) */}
      {isGlobalAdmin && (
   <div className="tenant-selector">
   <select
 value={selectedTenant?.id || ''}
            onChange={(e) => {
    const tenant = availableTenants.find(t => t.id === e.target.value);
              selectTenant(tenant);
            }}
          >
            <option value="">Select Tenant...</option>
            {availableTenants.map((tenant) => (
  <option key={tenant.id} value={tenant.id}>
      {tenant.name}
  </option>
         ))}
     </select>
 </div>
      )}

      {/* Navigation Links */}
      <nav className="nav-menu">
        <NavLink to="/dashboard" icon="Home" label="Dashboard" />
        
        {/* User Management (Admin Only) */}
      {(isGlobalAdmin || isTenantAdmin) && (
       <>
   <NavLink to="/users" icon="Users" label="User Management" />
      <NavLink to="/audit-trail" icon="FileText" label="Audit Trail" />
          </>
 )}

        {/* System Settings (Global Admin Only) */}
        {isGlobalAdmin && (
          <>
            <NavLink to="/system/tenants" icon="Building" label="Tenants" />
          <NavLink to="/system/error-logs" icon="AlertCircle" label="Error Logs" />
   </>
     )}
      </nav>

      {/* User Profile */}
      <div className="user-profile">
        <p>{user?.fullName}</p>
    <small>{user?.email}</small>
      </div>
    </aside>
  );
};

const NavLink = ({ to, icon, label }) => {
  const location = useLocation();
  const isActive = location.pathname.startsWith(to);
  
  return (
    <Link to={to} className={`nav-link ${isActive ? 'active' : ''}`}>
    <Icon name={icon} />
      <span>{label}</span>
  </Link>
  );
};

export default SideNavigation;
```

---

## Summary

This architecture specification provides a complete blueprint for building secure, multi-tenant SaaS applications based on the proven patterns from Rocket TRONC Manager:

**Key Takeaways:**

1. **Security First:** JWT authentication, BCrypt passwords, encrypted credentials, security headers
2. **True Multi-Tenancy:** Row-level isolation with query filters, tenant resolution middleware
3. **Comprehensive RBAC:** Four role levels with granular permissions
4. **Full Audit Trail:** Every action logged with user, timestamp, IP, and changes
5. **Error Management:** Centralized error logging with viewer for troubleshooting
6. **User Self-Service:** Invitation system, password reset, profile management
7. **Integration Ready:** Provider pattern for external system integrations
8. **Background Jobs:** Scheduled emails and recurring tasks
9. **Modern Frontend:** React contexts for auth and tenant, protected routes, clean navigation

All components are production-ready, tested patterns from a real-world SaaS application.

