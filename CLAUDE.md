# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

```bash
# Build
dotnet build PreConHub/PreConHub.csproj

# Run (HTTPS on localhost:7260, HTTP on localhost:5143)
dotnet run --project PreConHub/PreConHub.csproj

# Apply EF Core migrations
dotnet ef database update --project PreConHub/PreConHub.csproj

# Add a new migration
dotnet ef migrations add <MigrationName> --project PreConHub/PreConHub.csproj

# Restore NuGet packages
dotnet restore PreConHub/PreConHub.csproj
```

There is no test project in this solution.

## Architecture

PreConHub is an ASP.NET Core 8.0 MVC application for coordinating pre-construction real estate closings. It connects builders, purchasers, and lawyers around Statement of Adjustments (SOA) calculations, shortfall risk analysis, and closing workflows.

### Layered Structure

```
PreConHub/
├── Controllers/        # 8 MVC controllers, role-gated with [Authorize]
├── Models/
│   ├── Entities/       # AllEntities.cs — all 30+ domain entities in one file
│   └── ViewModels/     # AllViewModels.cs + ReportViewModels.cs + DocumentViewModels.cs
├── Services/           # 7 service classes (business logic, calculations, PDF, email)
├── Data/
│   ├── ApplicationDbContext.cs  # EF Core DbContext (extends IdentityDbContext)
│   └── Migrations/
├── Views/              # Razor .cshtml templates organized by controller
├── Hubs/               # NotificationHub.cs — SignalR real-time notifications
├── Areas/Identity/     # ASP.NET Identity Razor Pages (login, register)
└── wwwroot/            # Static assets (Bootstrap 5, jQuery, site.css/js)
```

### Four User Roles

- **Admin** — platform administration, user management
- **Builder** — creates projects, manages units/fees, assigns lawyers, views dashboards
- **Purchaser** — submits mortgage/financial info, views SOA, receives invitations
- **Lawyer** — reviews units, approves or requests revisions, adds notes

Controllers enforce roles: `ProjectsController` and `UnitsController` require Builder/Admin; `PurchaserController` requires Purchaser; `LawyerController` requires Lawyer.

### Key Services

| Service | Responsibility |
|---|---|
| `SoaCalculationService` | Statement of Adjustments: land transfer tax (Ontario/Toronto), HST/rebates, levy caps with builder absorption, credits |
| `ShortfallAnalysisService` | Compares SOA totals vs. purchaser funds; assigns risk levels (Low/Medium/High/VeryHigh) and closing recommendations |
| `ProjectSummaryService` | Cached dashboard aggregations (ready-to-close, needs-discount, at-risk counts) |
| `PdfService` | QuestPDF-based SOA document generation |
| `EmailService` | SMTP templated emails (invitations, approvals, status updates). Currently disabled via config |
| `DocumentAnalysisService` | AI-powered APS document parsing using iText7 + Claude API. Currently disabled via config |
| `NotificationService` | Database-backed in-app notifications with real-time SignalR delivery |

`CalculationServices.cs` contains three service interfaces/implementations: `ISoaCalculationService`, `IShortfallAnalysisService`, and `IProjectSummaryService`.

### Database

- SQL Server via Entity Framework Core 8.0.8
- Connection string in `appsettings.json` under `ConnectionStrings:DefaultConnection`
- `ApplicationDbContext` has 18 DbSets with Fluent API relationship configuration
- Key relationships: Project -> Units -> (UnitFees, Deposits, UnitPurchasers, Documents, SOA, ShortfallAnalysis)
- Admin user is seeded on startup in `Program.cs`

### Real-Time

SignalR hub at `/notificationHub` delivers notifications to connected clients. `NotificationBackgroundService` runs scheduled checks as a hosted background service.

### External Integrations (configurable, currently disabled)

- **Claude API** — document analysis (`ClaudeApi` config section)
- **Google Drive** — document storage (`GoogleDrive` config section)
- **Gmail SMTP** — email delivery (`EmailSettings` config section)

## Key Conventions

- All entity classes live in a single file: `Models/Entities/AllEntities.cs`
- All primary view models live in: `Models/ViewModels/AllViewModels.cs`
- Nullable reference types are enabled project-wide
- Implicit usings are enabled
- Services are registered via dependency injection in `Program.cs` (scoped/transient)
- Session timeout is 2 hours
- Tax configuration (land transfer tax rebates, HST rates) is stored in `appsettings.json` under `LandTransferTax`
