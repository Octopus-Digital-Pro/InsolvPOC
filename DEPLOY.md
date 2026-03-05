# Insolvio — Deployment Guide (IIS on Windows Server EC2)

This guide covers deploying Insolvio to an AWS EC2 instance running Windows Server with IIS and SQL Server Express on the same machine.

---

## Architecture

```
Internet ──▶ EC2 Instance (Windows Server 2022)
               ├── IIS  (HTTP :80 / HTTPS :443)
               │     └── ASP.NET Core Module (ANCM)  ──▶  Insolvio.API.exe (in-process)
               │                                              └── wwwroot/ (React SPA static files)
               └── SQL Server Express 2022              ──▶  :1433 (localhost only)
```

The React frontend is compiled into `wwwroot/` and served directly by IIS as static files — no separate Node process required. IIS hosts the .NET 8 app in-process via the ASP.NET Core Module. SQL Server port 1433 is **never** opened to the internet.

---

## Files overview

| File | Committed | Purpose |
|---|---|---|
| `deploy-ec2.ps1` | No (gitignored) | Main deploy script — run from your dev machine |
| `deploy-ec2.settings.ps1` | No (gitignored) | Per-machine settings (EC2 host, credentials, paths) |
| `insolvio-iis-setup.ps1` | No (gitignored) | One-time Windows Server setup — run on the EC2 instance |
| `Insolvio.API/appsettings.Production.json` | No (gitignored) | Production config (DB password, JWT key, S3, SMTP) |
| `Insolvio.API/Properties/PublishProfiles/EC2Production.pubxml` | Yes | MSBuild publish profile (no secrets) |

---

## Prerequisites

### Dev machine (Windows)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/)
- `dotnet-ef` global tool:
  ```powershell
  dotnet tool install --global dotnet-ef
  ```
- PowerShell 5.1+ (built-in on Windows 10+)

### EC2 instance
- **Windows Server 2022** AMI (t3.small minimum; t3.medium recommended — SQL Server Express needs ≥1 GB RAM)
- EC2 security group inbound rules:
  - Port **3389** (RDP) — your IP only (initial setup)
  - Port **5986** (WinRM HTTPS) — your dev machine IP only (deployments)
  - Port **80** (HTTP) — public (or lock to specific IPs)
  - Port **443** (HTTPS) — public, if using TLS
  - Port **1433** — **do NOT open** (SQL Server stays localhost-only)

---

## Step 1 — One-time Windows Server setup

### 1a. Connect via RDP
Retrieve the initial Administrator password from the AWS Console:
**EC2 → Instances → Select your instance → Actions → Security → Get Windows Password**
(requires the EC2 key pair `.pem` file).

Connect using the Windows Remote Desktop client on your dev machine.

### 1b. Run the setup script
Copy `insolvio-iis-setup.ps1` to the server (paste into a PowerShell window, or copy via RDP clipboard), then run it as Administrator:

```powershell
Set-ExecutionPolicy RemoteSigned -Scope Process
.\insolvio-iis-setup.ps1
```

The setup script:
1. Enables IIS with the required Windows features
2. Installs the **ASP.NET Core Hosting Bundle 8.x** (required for in-process IIS hosting)
3. Installs **SQL Server Express 2022**
4. Creates the `insolvio` IIS app pool (No Managed Code, ApplicationPoolIdentity) and website
5. Sets `ASPNETCORE_ENVIRONMENT=Production` on the IIS site
6. Opens Windows Firewall ports 80 and 443
7. Creates a WinRM HTTPS listener on port 5986 (for PowerShell Remoting deploys)

### 1c. SQL Server post-install configuration
After the setup script completes, open **SQL Server Management Studio** (SSMS) or run these commands in a PowerShell window on the server to enable SQL authentication and set the SA password:

```sql
-- Connect as Windows Authentication, then run:
USE [master]
GO

-- Enable Mixed Mode authentication (SQL + Windows)
EXEC xp_instance_regwrite N'HKEY_LOCAL_MACHINE',
    N'Software\Microsoft\MSSQLServer\MSSQLServer',
    N'LoginMode', REG_DWORD, 2
GO

-- Set a strong SA password
ALTER LOGIN [sa] WITH PASSWORD = N'CHANGE_ME_StrongPassword123!'
GO

-- Enable the SA login
ALTER LOGIN [sa] ENABLE
GO
```

Then restart SQL Server to apply the authentication mode change:

```powershell
Restart-Service -Name "MSSQL`$SQLEXPRESS"
```

> **Note the SA password** — you will need it for `appsettings.Production.json` in the next step.

---

## Step 2 — Configure production settings

`Insolvio.API/appsettings.Production.json` is gitignored and must be created locally before the first deploy. **Create this file on your dev machine** — it will be copied to the server during the deploy.

```jsonc
{
  "ConnectionStrings": {
    // SA password must match what you set in Step 1c
    // Use .\SQLEXPRESS for the named SQL Server Express instance
    "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=InsolvioDb;User Id=sa;Password=CHANGE_ME_StrongPassword123!;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "Jwt": {
    // Generate a strong random key (minimum 32 characters)
    "Key": "CHANGE_ME_StrongRandomJwtKey_MinLength32Characters!!",
    "Issuer": "Insolvio.API",
    "Audience": "Insolvio.React",
    "ExpiryDays": 1
  },
  "Cors": {
    "AllowedOrigins": [ "https://your-domain.com" ]
  },
  "FrontendUrl": "https://your-domain.com",
  "Smtp": {
    "Enabled": true,
    "Host": "smtp.your-provider.com",
    "Port": 587,
    "Username": "your-smtp-user",
    "Password": "your-smtp-password",
    "FromEmail": "noreply@your-domain.com",
    "FromName": "Insolvio",
    "EnableSsl": true
  },
  "Aws": {
    "S3": {
      "AccessKeyId": "",         // leave blank if using IAM Role (recommended)
      "SecretAccessKey": "",     // leave blank if using IAM Role (recommended)
      "Region": "eu-central-1",  // your bucket's region
      "BucketName": "your-bucket-name",
      "KeyPrefix": "documents/"
    }
  }
}
```

### Generating a JWT key

```powershell
# Produces a 64-char base64 key (compatible with PowerShell 5.1 / .NET Framework)
$b = New-Object byte[] 48; [Security.Cryptography.RNGCryptoServiceProvider]::new().GetBytes($b); [Convert]::ToBase64String($b)
```

### SQL Server connection string note
SQL Server Express uses a named instance. The connection string server name is:
- `localhost\SQLEXPRESS` in code / JSON
- `localhost\\SQLEXPRESS` when escaped in a JSON file (as shown above)

---

## Step 3 — Configure deploy settings

Fill in `deploy-ec2.settings.ps1`:

```powershell
$EC2_HOST        = "ec2-12-34-56-78.compute-1.amazonaws.com"  # or Elastic IP
$EC2_CREDENTIAL  = Get-Credential -UserName "Administrator"    # prompts for password
$DEPLOY_PATH     = "C:\inetpub\wwwroot\insolvio"
$IIS_APP_POOL    = "insolvio"
$IIS_SITE_NAME   = "insolvio"
$SKIP_MIGRATIONS = $false
```

---

## Step 4 — Deploy

From the repo root on your dev machine:

```powershell
# Full deploy (build frontend + API + migrations + upload + restart app pool)
.\deploy-ec2.ps1

# Skip the React build (when only backend changed)
.\deploy-ec2.ps1 -SkipFrontend

# Skip EF migrations (when schema hasn't changed)
.\deploy-ec2.ps1 -SkipMigrations

# Dry run — build and publish locally but don't upload to the server
.\deploy-ec2.ps1 -SkipDeploy
```

### What the deploy script does

| Step | Action |
|---|---|
| 1/5 | `npm ci` + `npm run build` in `Insolvio.Web/` |
| 2/5 | `dotnet publish` — win-x64, Release, React `dist/` folded into `wwwroot/` |
| 3/5 | Overlays `appsettings.Production.json` into the publish folder |
| 4/5 | Builds a win-x64 EF Core migration bundle (`efbundle.exe`) |
| 5/5 | Stops IIS app pool → transfers zip via WinRM → extracts → runs migrations → starts app pool |

---

## S3 Storage

The app defaults to **local disk storage** (`C:\inetpub\wwwroot\insolvio\DocumentOutput\`). To switch to S3:

### Option A — IAM Role (recommended)

1. In the AWS Console, create an IAM Role with the `EC2` trusted entity type.
2. Attach an inline S3 policy:
   ```json
   {
     "Version": "2012-10-17",
     "Statement": [{
       "Effect": "Allow",
       "Action": ["s3:PutObject","s3:GetObject","s3:DeleteObject","s3:HeadObject"],
       "Resource": "arn:aws:s3:::YOUR-BUCKET-NAME/*"
     }]
   }
   ```
3. Attach the role to the EC2 instance: **EC2 Console → Instance → Actions → Security → Modify IAM Role**.
4. In `appsettings.Production.json`, set `BucketName` and `Region`. Leave `AccessKeyId` and `SecretAccessKey` **empty** — the AWS SDK automatically discovers credentials from the EC2 instance metadata.

### Option B — IAM User access keys

1. Create an IAM User with the same S3 permissions as above.
2. Generate an access key pair.
3. Fill in `Aws:S3:AccessKeyId` and `Aws:S3:SecretAccessKey` in `appsettings.Production.json`.

### Activating S3

Connect via RDP (or `Invoke-Command`) and run once:

```powershell
sqlcmd -S "localhost\SQLEXPRESS" -U sa -P "YOUR_SA_PASSWORD" `
    -Q "UPDATE SystemConfigs SET Value='AwsS3' WHERE [Key]='StorageProvider'"

# Recycle the app pool to pick up the change
& "$env:WinDir\system32\inetsrv\appcmd.exe" recycle apppool /apppool.name:insolvio
```

To revert to local disk:

```powershell
sqlcmd -S "localhost\SQLEXPRESS" -U sa -P "YOUR_SA_PASSWORD" `
    -Q "UPDATE SystemConfigs SET Value='Local' WHERE [Key]='StorageProvider'"

& "$env:WinDir\system32\inetsrv\appcmd.exe" recycle apppool /apppool.name:insolvio
```

---

## HTTPS / TLS

### Option A — AWS Certificate Manager + Application Load Balancer (recommended for production)

1. Request a free certificate in **AWS Certificate Manager (ACM)** for your domain.
2. Create an **Application Load Balancer (ALB)** with:
   - Listener on port 443 → forward to a Target Group containing your EC2 instance on port 80
   - Listener on port 80 → redirect to HTTPS
3. Associate the ACM certificate with the HTTPS listener.
4. Create a DNS record (Route 53 or external) pointing your domain to the ALB DNS name.
5. Update `appsettings.Production.json` — set `Cors:AllowedOrigins` and `FrontendUrl` to your `https://` domain.

With this setup, TLS terminates at the ALB. IIS receives plain HTTP on port 80.

### Option B — IIS with a certificate (self-managed TLS)

1. Obtain a certificate (Let's Encrypt via win-acme, or import from a CA).
2. In IIS Manager: site bindings → add HTTPS binding on port 443 → select the certificate.
3. Add an HTTP→HTTPS rewrite rule (requires the **URL Rewrite** IIS module).

---

## IIS management

```powershell
# App pool status
& "$env:WinDir\system32\inetsrv\appcmd.exe" list apppool insolvio

# Recycle (soft restart — no downtime)
& "$env:WinDir\system32\inetsrv\appcmd.exe" recycle apppool /apppool.name:insolvio

# Stop / start app pool
& "$env:WinDir\system32\inetsrv\appcmd.exe" stop  apppool /apppool.name:insolvio
& "$env:WinDir\system32\inetsrv\appcmd.exe" start apppool /apppool.name:insolvio

# Full IIS restart (affects all sites)
iisreset /noforce
```

### Application logs

stdout/stderr from the .NET process are written to `logs\stdout_*.log` in the site root when `stdoutLogEnabled="true"` in `web.config`. To enable:

```powershell
# Enable stdout logging for diagnostics (disable again after troubleshooting)
$webConfigPath = "C:\inetpub\wwwroot\insolvio\web.config"
(Get-Content $webConfigPath) -replace 'stdoutLogEnabled="false"', 'stdoutLogEnabled="true"' |
    Set-Content $webConfigPath
& "$env:WinDir\system32\inetsrv\appcmd.exe" recycle apppool /apppool.name:insolvio
```

Windows Event Viewer also captures ASP.NET Core Module messages:
**Event Viewer → Windows Logs → Application** (source: `IIS AspNetCore Module`)

---

## Database management

```powershell
# Connect to SQL Server Express
sqlcmd -S "localhost\SQLEXPRESS" -U sa -P "YOUR_SA_PASSWORD"

# Check which migrations have been applied
sqlcmd -S "localhost\SQLEXPRESS" -U sa -P "YOUR_SA_PASSWORD" -d InsolvioDb `
    -Q "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId"

# Backup the database
sqlcmd -S "localhost\SQLEXPRESS" -U sa -P "YOUR_SA_PASSWORD" `
    -Q "BACKUP DATABASE InsolvioDb TO DISK='C:\Backup\InsolvioDb.bak' WITH FORMAT"
```

---

## Troubleshooting

| Symptom | Check |
|---|---|
| 500.19 / 502.5 on first request | ASP.NET Core Hosting Bundle not installed, or IIS needs `iisreset` after bundle install |
| App pool keeps stopping | Check Event Viewer (Application log) for startup crash; enable stdout logs (see above) |
| 500.30 — startup exception | Check `logs\stdout_*.log` in the site root; common cause is missing config or DB connection failure |
| DB connection refused | Run `Get-Service "MSSQL*"` — SQL Server may not be running; check Mixed Mode + SA login enabled |
| Migrations fail | Confirm connection string in `appsettings.Production.json` uses `localhost\\SQLEXPRESS` (double backslash in JSON) |
| Files not found after S3 switch | Confirm `StorageProvider = AwsS3` in DB, app pool was recycled, and IAM Role/keys are valid |
| Large upload fails (413) | Set `maxAllowedContentLength` in `web.config`; add `<requestLimits maxAllowedContentLength="734003200"/>` under `system.webServer/security/requestFiltering` |
| WinRM connection refused | Confirm EC2 security group allows port 5986 from your IP; run `Test-NetConnection $EC2_HOST -Port 5986` |
