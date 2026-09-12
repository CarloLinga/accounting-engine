# Local-only: run the API against Neon instead of localhost.
# Copy/paste, replace the password, run once per machine. Never commit this.
# Clears on process exit — does NOT pollute other projects like a
# machine-level DATABASE_URL would.
$env:ConnectionStrings__DefaultConnection = "Host=ep-frosty-hill-axf8grdu-pooler.c-4.us-east-2.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=REPLACE_ME;SslMode=Require"
dotnet run --project src/AccountingEngine.Api/AccountingEngine.Api.csproj --urls http://localhost:5255