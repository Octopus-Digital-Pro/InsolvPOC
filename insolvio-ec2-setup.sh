#!/usr/bin/env bash
# ==============================================================================
#  Insolvio — EC2 One-Time Server Setup Script
#  Run this ON the EC2 instance once, before the first deploy.
#  Tested on Amazon Linux 2023 and Ubuntu 22.04 LTS.
#
#  Usage:
#    chmod +x insolvio-ec2-setup.sh && sudo ./insolvio-ec2-setup.sh
# ==============================================================================
set -euo pipefail

DEPLOY_PATH="/opt/insolvio"
SERVICE_NAME="insolvio"
APP_USER="insolvio"        # dedicated non-root user to run the service
APP_PORT="5000"

# --- Detect distro ------------------------------------------------------------
if [ -f /etc/os-release ]; then
    . /etc/os-release
    DISTRO=$ID
else
    DISTRO="unknown"
fi
echo ">> Detected distro: $DISTRO"

# --- 1. System packages -------------------------------------------------------
echo ">> Installing system packages..."
case "$DISTRO" in
  amzn|fedora|rhel|centos)
    dnf update -y
    dnf install -y wget curl tar gzip
    ;;
  ubuntu|debian)
    apt-get update -y
    apt-get install -y wget curl tar gzip
    ;;
  *)
    echo "Unsupported distro: $DISTRO. Install wget/curl/tar manually." >&2
    ;;
esac

# --- 2. .NET 8 Runtime --------------------------------------------------------
echo ">> Installing .NET 8 Runtime..."
case "$DISTRO" in
  amzn)
    dnf install -y dotnet-runtime-8.0
    ;;
  ubuntu|debian)
    # Microsoft feed
    wget -qO /tmp/packages-microsoft-prod.deb \
        "https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb"
    dpkg -i /tmp/packages-microsoft-prod.deb
    apt-get update -y
    apt-get install -y dotnet-runtime-8.0
    ;;
  *)
    echo "Install .NET 8 Runtime manually: https://dotnet.microsoft.com/download/dotnet/8.0" >&2
    ;;
esac
dotnet --info

# --- 3. SQL Server Express 2022 for Linux ------------------------------------
echo ">> Installing SQL Server Express 2022..."
case "$DISTRO" in
  amzn|fedora|rhel|centos)
    # RHEL-compatible repo
    curl -fsSL "https://packages.microsoft.com/config/rhel/9/mssql-server-2022.repo" \
        -o /etc/yum.repos.d/mssql-server-2022.repo
    dnf install -y mssql-server
    ;;
  ubuntu|debian)
    curl -fsSL "https://packages.microsoft.com/keys/microsoft.asc" \
        | gpg --dearmor -o /usr/share/keyrings/microsoft-archive-keyring.gpg
    echo "deb [arch=amd64 signed-by=/usr/share/keyrings/microsoft-archive-keyring.gpg] \
        https://packages.microsoft.com/ubuntu/$(lsb_release -rs)/mssql-server-2022 jammy main" \
        > /etc/apt/sources.list.d/mssql-server-2022.list
    apt-get update -y
    apt-get install -y mssql-server
    ;;
  *)
    echo "Install SQL Server Express manually: https://www.microsoft.com/sql-server/sql-server-downloads" >&2
    ;;
esac

echo ""
echo "================================================================"
echo "  !!! ACTION REQUIRED — run the SQL Server setup wizard now !!!"
echo "  Select edition = Express (3) and set the SA password."
echo "  The SA password must match 'DefaultConnection' in"
echo "  $DEPLOY_PATH/appsettings.Production.json"
echo "================================================================"
/opt/mssql/bin/mssql-conf setup express
systemctl enable mssql-server
systemctl start  mssql-server

# --- 4. Firewall — keep SQL Server port closed to the outside ----------------
echo ">> Configuring firewall (SQL Server port 1433 is LAN-only)..."
# SQL Server should be reachable only from localhost; the app runs on the same host.
# Only open port $APP_PORT (or 80/443 if nginx is in front).
case "$DISTRO" in
  amzn|fedora|rhel|centos)
    if command -v firewall-cmd &>/dev/null; then
        firewall-cmd --permanent --add-port="${APP_PORT}/tcp"
        firewall-cmd --reload
    fi
    ;;
  ubuntu|debian)
    if command -v ufw &>/dev/null; then
        ufw allow "${APP_PORT}/tcp"
        # Do NOT open 1433
    fi
    ;;
esac

# --- 5. Dedicated service user -----------------------------------------------
echo ">> Creating service user '$APP_USER'..."
if ! id "$APP_USER" &>/dev/null; then
    useradd -r -s /sbin/nologin "$APP_USER"
fi

# --- 6. Deploy directory ------------------------------------------------------
echo ">> Creating deploy directory $DEPLOY_PATH..."
mkdir -p "$DEPLOY_PATH"
chown "${APP_USER}:${APP_USER}" "$DEPLOY_PATH"

# --- 7. systemd service -------------------------------------------------------
echo ">> Registering systemd service '$SERVICE_NAME'..."
cat > "/etc/systemd/system/${SERVICE_NAME}.service" << EOF
[Unit]
Description=Insolvio API (.NET 8)
After=network.target mssql-server.service
Requires=mssql-server.service

[Service]
Type=simple
User=${APP_USER}
WorkingDirectory=${DEPLOY_PATH}
ExecStart=/usr/bin/dotnet ${DEPLOY_PATH}/Insolvio.API.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=${SERVICE_NAME}
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
# Kestrel listens on 5000; put nginx in front for TLS termination
Environment=ASPNETCORE_URLS=http://0.0.0.0:${APP_PORT}

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
systemctl enable "$SERVICE_NAME"

echo ""
echo "================================================================"
echo "  Setup complete!"
echo ""
echo "  Next steps:"
echo "  1. Place appsettings.Production.json in $DEPLOY_PATH"
echo "     (copy from your local machine, fill in real secrets)"
echo ""
echo "  S3 STORAGE:"
echo "  2a. (RECOMMENDED) Attach an IAM Role to this EC2 instance in the"
echo "      AWS console with s3:PutObject, s3:GetObject, s3:DeleteObject,"
echo "      s3:HeadObject on your bucket.  In appsettings.Production.json:"
echo "        Aws:S3:BucketName = <your-bucket>"
echo "        Aws:S3:Region     = <your-region>"
echo "        AccessKeyId / SecretAccessKey = leave empty"
echo "  2b. OR fill in IAM User access keys in appsettings.Production.json:"
echo "        Aws:S3:AccessKeyId     = <key-id>"
echo "        Aws:S3:SecretAccessKey = <secret>"
echo "        Aws:S3:BucketName      = <your-bucket>"
echo "        Aws:S3:Region          = <your-region>"
echo ""
echo "  3. To enable S3 (default is local disk):"
echo "     /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P '<yourpw>' -Q \\"
echo "       \"UPDATE SystemConfigs SET Value='AwsS3' WHERE [Key]='StorageProvider'\""
echo "     sudo systemctl restart $SERVICE_NAME"
echo ""
echo "  4. Run ./deploy-ec2.ps1 from your dev machine to upload the app"
echo "  5. (Optional) Set up nginx as a reverse proxy for TLS:"
echo "     proxy_pass http://127.0.0.1:${APP_PORT};"
echo "================================================================"
