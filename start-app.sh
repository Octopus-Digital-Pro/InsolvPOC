#!/usr/bin/env bash
# =============================================================
#  INSOLVEX - Full Stack Development Startup  (macOS / Linux)
# =============================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Colour helpers
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m' # No Colour

info()    { echo -e "${CYAN}${BOLD}$*${NC}"; }
success() { echo -e "${GREEN}  ?  $*${NC}"; }
warn()    { echo -e "${YELLOW}  ?  $*${NC}"; }
error()   { echo -e "${RED}${BOLD}  ?  $*${NC}" >&2; }
die()     { error "$*"; exit 1; }

echo ""
echo -e "${BOLD} ============================================${NC}"
echo -e "${BOLD}  INSOLVEX - Full Stack Development Startup${NC}"
echo -e "${BOLD} ============================================${NC}"
echo ""

# -----------------------------------------------
# 0. Install prerequisites (macOS only, via Homebrew)
# -----------------------------------------------
info "[SETUP] Checking and installing prerequisites..."

# --- Homebrew ---
if ! command -v brew &>/dev/null; then
    warn "Homebrew not found. Installing Homebrew..."
    /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
    # Add brew to PATH for Apple Silicon Macs
    if [[ -f /opt/homebrew/bin/brew ]]; then
        eval "$(/opt/homebrew/bin/brew shellenv)"
    fi
fi
success "Homebrew found: $(brew --version | head -1)"

# --- Docker ---
if ! command -v docker &>/dev/null; then
    warn "Docker not found. Installing Docker via Homebrew Cask..."
    brew install --cask docker
    warn "Docker Desktop installed. Please open Docker Desktop once and allow it to start, then re-run this script."
    open -a Docker
    exit 0
fi
success "Docker found: $(docker --version)"

# Ensure Docker daemon is running
if ! docker info &>/dev/null; then
    warn "Docker daemon is not running. Attempting to start Docker Desktop..."
    open -a Docker
    echo "  Waiting up to 60 seconds for Docker to start..."
    for i in $(seq 1 30); do
        sleep 2
        if docker info &>/dev/null; then
      break
   fi
        echo "  ...still waiting ($((i*2))s)"
    done
    docker info &>/dev/null || die "Docker daemon did not start in time. Please start Docker Desktop manually and re-run."
fi
success "Docker daemon is running."

# --- .NET SDK ---
if ! command -v dotnet &>/dev/null; then
    warn ".NET SDK not found. Installing via Homebrew..."
    brew install --cask dotnet-sdk
fi
success ".NET SDK found: $(dotnet --version)"

# --- Node.js / npm ---
if ! command -v node &>/dev/null; then
    warn "Node.js not found. Installing via Homebrew..."
  brew install node
fi
success "Node.js found: $(node --version)  |  npm: $(npm --version)"

echo ""
success "All prerequisites satisfied."
echo ""

# -----------------------------------------------
# 1. Start SQL Server in Docker
# -----------------------------------------------
info "[1/6] Starting SQL Server container..."
cd "$SCRIPT_DIR"
docker compose up -d sqlserver || die "Failed to start SQL Server container. Make sure Docker Desktop is running."

# -----------------------------------------------
# 2. Wait for SQL Server to be healthy
# -----------------------------------------------
info "[2/6] Waiting for SQL Server to accept connections..."

RETRIES=0
MAX_RETRIES=40

while true; do
    if [[ $RETRIES -ge $MAX_RETRIES ]]; then
    echo ""
        die "SQL Server did not become ready within ~80 seconds.\n    Check: docker logs insolvex-db"
    fi

    # Method 1: check Docker healthcheck status
    HEALTH=$(docker inspect --format "{{.State.Health.Status}}" insolvex-db 2>/dev/null || true)
    if [[ "$HEALTH" == "healthy" ]]; then
        success "SQL Server is ready!  [healthy]"
     break
    fi

    # Method 2: fallback – direct sqlcmd probe
    if docker exec insolvex-db /opt/mssql-tools18/bin/sqlcmd \
      -S localhost -U sa -P "InsolvexDev2025#Strong" \
        -Q "SELECT 1" -C -b &>/dev/null 2>&1; then
        success "SQL Server is ready!  [sqlcmd]"
        break
    fi

    RETRIES=$((RETRIES + 1))
 echo "  Waiting... ($RETRIES/$MAX_RETRIES)  status=${HEALTH:-unknown}"
    sleep 2
done

# -----------------------------------------------
# 3. Install / update EF Core tools
# -----------------------------------------------
info "[3/6] Checking EF Core tools..."
if ! dotnet tool list -g 2>/dev/null | grep -qi "dotnet-ef"; then
    warn "dotnet-ef not found. Installing..."
    dotnet tool install --global dotnet-ef
else
    success "dotnet-ef is already installed."
fi

# Make sure the tools path is on PATH
export PATH="$PATH:$HOME/.dotnet/tools"

# -----------------------------------------------
# 4. Apply database migrations
# -----------------------------------------------
info "[4/6] Applying database migrations..."
cd "$SCRIPT_DIR/Insolvex.API"
dotnet ef database update || die "Failed to apply migrations.\n    Check connection string in appsettings.json"
success "Migrations applied successfully!"

# -----------------------------------------------
# 5. Install npm dependencies for frontend
# -----------------------------------------------
info "[5/6] Checking frontend npm dependencies..."
cd "$SCRIPT_DIR/Insolvex.Web"
if [[ ! -d "node_modules" ]]; then
    warn "node_modules not found. Running npm install..."
    npm install
else
    success "node_modules already present. Skipping npm install."
fi

# -----------------------------------------------
# 6. Launch backend and frontend in new terminals
# -----------------------------------------------
info "[6/6] Starting backend and frontend..."
echo ""
echo -e "${BOLD} ============================================${NC}"
echo ""
echo "  Backend API  : http://localhost:5000"
echo "  Swagger UI   : http://localhost:5000/swagger"
echo "  Frontend     : http://localhost:5173"
echo ""
echo -e "${BOLD} ============================================${NC}"
echo ""
echo "  Demo accounts (seeded on first run):"
echo ""
echo "    admin@insolvex.local        / Admin123!      (GlobalAdmin)"
echo "    practitioner@insolvex.local / Pract123!      (Practitioner)"
echo "    secretary@insolvex.local    / Secr123!       (Secretary)"
echo ""
echo -e "${BOLD} ============================================${NC}"
echo ""

# Start backend in a new Terminal tab/window
osascript \
  -e 'tell application "Terminal"' \
  -e "  do script \"cd '$SCRIPT_DIR/Insolvex.API' && dotnet run --launch-profile Insolvex.API\"" \
  -e '  set custom title of front window to "Insolvex API"' \
  -e 'end tell'

# Give the API a few seconds to bind its port
echo "  Waiting for API to start (4 s)..."
sleep 4

# Start frontend in a new Terminal tab/window
osascript \
  -e 'tell application "Terminal"' \
  -e "  do script \"cd '$SCRIPT_DIR/Insolvex.Web' && npm run dev\"" \
  -e '  set custom title of front window to "Insolvex React"' \
  -e 'end tell'

echo ""
success "Both servers are starting in separate Terminal windows."
echo "  This launcher window can be closed — the servers will keep running."
echo ""
