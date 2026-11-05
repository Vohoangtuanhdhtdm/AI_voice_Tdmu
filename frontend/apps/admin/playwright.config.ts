import { defineConfig } from "@playwright/test";

const dbConnection =
  process.env.E2E_DB ?? "Host=localhost;Port=5432;Database=tdmu_e2e;Username=postgres;Password=postgres";
const chromiumPath = process.env.PLAYWRIGHT_CHROMIUM_PATH;

export default defineConfig({
  testDir: "./e2e",
  timeout: 60_000,
  fullyParallel: false,
  workers: 1,
  reporter: [["list"]],
  use: {
    baseURL: "http://localhost:5173",
    trace: "retain-on-failure",
    launchOptions: chromiumPath ? { executablePath: chromiumPath } : {},
  },
  webServer: [
    {
      command: "dotnet run --project ../../../backend/src/Tdmu.Advisor.Api --urls http://localhost:5110",
      url: "http://localhost:5110/health",
      timeout: 180_000,
      reuseExistingServer: !process.env.CI,
      env: {
        ASPNETCORE_ENVIRONMENT: "Development",
        ConnectionStrings__Default: dbConnection,
        Auth__JwtKey: "e2e-only-jwt-signing-key-0123456789abcdef",
        Auth__SeedAdminEmail: "admin@e2e.local",
        Auth__SeedAdminPassword: "E2e!Passw0rd",
        Seed__SampleData: "false",
      },
    },
    {
      command: "pnpm dev --port 5173",
      url: "http://localhost:5173",
      timeout: 60_000,
      reuseExistingServer: !process.env.CI,
    },
  ],
});
