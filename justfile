# BallastLane Expense Tracker — task runner.
#
# Install just:
#   - macOS:   brew install just
#   - Linux:   cargo install just  (or your distro package manager)
#   - Windows: winget install Casey.Just  (or scoop install just)
#
# Then `just` (no args) lists all recipes.

# Default recipe lists available recipes.
default:
    @just --list

# One-time setup after cloning: SQL up + migrations + frontend install.
setup: db-up db-migrate web-install
    @echo ""
    @echo "Setup complete. Open two terminals:"
    @echo "  Terminal 1:  just api    # backend on http://localhost:5080"
    @echo "  Terminal 2:  just web    # frontend on http://localhost:4200"
    @echo ""
    @echo "Demo login: demo@ballastlane.test / Demo@123"

# Bring up the SQL Server container via podman compose. Idempotent.
db-up:
    podman compose up -d
    @sleep 5

# Stop the SQL Server container (preserves the data volume).
db-down:
    podman compose down

# Apply SQL migrations + seed via grate.
db-migrate:
    dotnet run --project db/BallastLane.Migrations

# Drop the SQL data volume and re-apply all migrations from scratch.
db-reset:
    podman compose down -v
    @just db-up
    @just db-migrate

# Install the Angular dev dependencies.
web-install:
    cd web && npm install

# Run the API. Uses src/BallastLane.Api/Properties/launchSettings.json
# for the URL binding (5080) and the Development environment.
api:
    dotnet run --project src/BallastLane.Api

# Run the Angular dev server. Proxies /auth, /expenses, /health to :5080.
web:
    cd web && npm start

# Run the full backend test suite.
test:
    dotnet test --nologo

# Build the Angular frontend in production mode (validates bundle budgets).
build-web:
    cd web && npx ng build --configuration=production

# Smoke a running API: health, login, list expenses. Requires `just api`
# to be running in another terminal.
smoke:
    @curl -s -o /dev/null -w "health:   HTTP %{http_code}\n" http://localhost:5080/health
    @curl -s -c /tmp/bl-jar -X POST http://localhost:5080/auth/login \
        -H "Content-Type: application/json" \
        -d '{"email":"demo@ballastlane.test","password":"Demo@123"}' \
        -o /dev/null -w "login:    HTTP %{http_code}\n"
    @curl -s -b /tmp/bl-jar http://localhost:5080/expenses \
        -o /dev/null -w "expenses: HTTP %{http_code}\n"
    @rm -f /tmp/bl-jar
