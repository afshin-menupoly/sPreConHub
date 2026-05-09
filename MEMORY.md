# PreConHub — Project Handoff Memory

> **READ THIS FIRST.** Comprehensive context for any Claude session continuing work on PreConHub.
> Author: Previous Claude session (Opus 4.7) | Handoff date: 2026-05-09 | Last commit: `489f0c3`
>
> A copy of this file also lives at `C:\My Projects\PreConHub\MEMORY.md` (one level above the git root) so it can be read before any cd into the repo.

---

## 1. WHAT THIS PROJECT IS

**PreConHub** is an ASP.NET Core 8.0 MVC web application for coordinating pre-construction real estate closings in Ontario, Canada. It connects builders, purchasers, lawyers, and marketing agencies around the Statement of Adjustments (SOA) workflow.

Core domain concepts:
- **APS** = Agreement of Purchase and Sale (the contract between builder and purchaser)
- **SOA** = Statement of Adjustments (the closing-day financial reconciliation)
- **Schedule B** = APS section listing closing fees (cheque admin, PDI, wire transfer, etc.)
- **DC / EDC** = Development Charges / Education Development Charges
- **Occupancy Fee** = monthly fee paid during interim occupancy (interest + common expense + property tax)
- **Tarion** = Ontario's home builder regulator (warranty, addendum dates)
- **HCRA** = Home Construction Regulatory Authority

---

## 2. DIRECTORY LAYOUT (IMPORTANT — nested folders)

```
C:\My Projects\PreConHub\                ← parent folder (NOT a git repo)
├── MEMORY.md                            ← handoff copy for new Claude to read first
├── API SETTING.txt, token.txt, etc.    ← user's loose notes
├── Claude Files\, SQL\, Files\         ← misc working folders
└── PreConHub\                          ← GIT ROOT (cwd in Claude Code)
    ├── CLAUDE.md                       ← project instructions (read on every session)
    ├── MEMORY.md                       ← THIS FILE (committed in git)
    ├── PreConHub.sln
    └── PreConHub\                      ← actual ASP.NET project
        ├── PreConHub.csproj
        ├── Program.cs
        ├── appsettings.json
        ├── Controllers\                ← 12 MVC controllers
        ├── Models\Entities\AllEntities.cs   ← ALL 40+ domain entities in one file
        ├── Models\ViewModels\
        │   ├── AllViewModels.cs
        │   ├── ReportViewModels.cs
        │   └── DocumentViewModels.cs
        ├── Services\                   ← 7 service classes
        ├── Data\
        │   ├── ApplicationDbContext.cs
        │   └── Migrations\             ← 36 migrations applied
        ├── Views\                      ← Razor templates by controller
        ├── Hubs\NotificationHub.cs    ← SignalR
        ├── Areas\Identity\             ← ASP.NET Identity Razor Pages (login)
        └── docs\spec\                  ← spec PDFs, WORKFLOW_SPEC.md, GAP_ANALYSIS.md
```

**`cwd` for Claude Code is `C:\My Projects\PreConHub\PreConHub\` (the git root with the .sln).**
All `dotnet` commands target `PreConHub/PreConHub.csproj` (one level deeper).

---

## 3. BUILD, RUN, MIGRATE

```powershell
# Build
dotnet build PreConHub/PreConHub.csproj

# Run (HTTPS on https://localhost:7260, HTTP on http://localhost:5143)
dotnet run --project PreConHub/PreConHub.csproj

# Apply EF Core migrations to the configured DB
dotnet ef database update --project PreConHub/PreConHub.csproj

# Create a new migration
dotnet ef migrations add <MigrationName> --project PreConHub/PreConHub.csproj

# Restore packages
dotnet restore PreConHub/PreConHub.csproj
```

**There is no test project.** Verification is done by build + manual end-to-end testing in browser.

---

## 4. TECH STACK

- **.NET 8.0** (ASP.NET Core MVC + Razor Views)
- **Entity Framework Core 8.0.8** with SQL Server
- **ASP.NET Identity** (cookie auth, 2FA, lockout)
- **SignalR** for real-time notifications (`/notificationHub`)
- **QuestPDF 2023.12.0** — SOA PDF generation
- **iText7 8.0.2** — APS PDF text extraction (for AI parsing)
- **CsvHelper 30.0.1** — bulk import / export
- **ClosedXML 0.102.1** — Excel exports
- **Bootstrap 5 + jQuery** on the front end
- **Claude API** (anthropic SDK via raw HTTP) — APS document analysis, currently DISABLED in config
- **Gmail SMTP** — emails, currently DISABLED in config (`EmailSettings.IsEnabled = false`)

`<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` are on project-wide.

---

## 5. SIX USER ROLES

| Role | Capabilities |
|---|---|
| **SuperAdmin** | Full access; seeded user `info@afshahin.com` |
| **Admin** | Platform admin: user management, builder quotas, system fees |
| **Builder** | Creates projects/units/fees, assigns lawyers, dashboards |
| **Purchaser** | Submits mortgage/financial info, views SOA, receives invitations, requests extensions |
| **Lawyer** | Two sub-roles via `LawyerAssignment.Role`: `BuilderLawyer` (reviews units for builder) and `PurchaserLawyer` (buyer's lawyer — invited by purchaser or assigned by builder). Approves/revisions, penalty actions, SOA upload/confirm |
| **MarketingAgency** | Per-project assignment via `Project.MarketingAgencyUserId`. Views assigned projects, suggests discounts/credits |

Controllers enforce roles via `[Authorize(Roles = ...)]`. Buyer's lawyer flow lives in `BuyerLawyerController`; builder's lawyer flow in `LawyerController`.

---

## 6. DATABASE

- **SQL Server** at `74.208.251.169` — see `appsettings.json` `ConnectionStrings:DefaultConnection`
- Connection string is **plaintext in `appsettings.json`** (production-prep TODO: move to User Secrets/Key Vault)
- 22 DbSets; key relationships: `Project → Units → (UnitFees, Deposits, UnitPurchasers, Documents, SOA, ShortfallAnalysis, ClosingPenalties)`
- Admin user is seeded on startup in `Program.cs`
- **36 migration files applied** (see `PreConHub/Data/Migrations/`)

### DB Reset SQL — important order
A corrected SQL script exists to clear all test data while preserving `SystemFeeConfigs`, `AspNetRoles`, and the SuperAdmin user. **Critical FK order**: delete `LawyerAssignments` BEFORE `Units` (FK: `UnitId → Units`). Full delete order:
```
DepositInterestPeriods → LawyerNotes → SOAVersions → ClosingExtensionRequests
→ MortgageInfos → PurchaserFinancials → UnitPurchasers → UnitFees → OccupancyFees
→ Deposits → ShortfallAnalyses → StatementsOfAdjustments → Documents
→ LawyerAssignments → Units → ProjectFees → ProjectSummaries → ProjectFinancials
→ Projects → AuditLogs → Notifications → ClosingPenalties → NSFCharges
→ AspNetUser* (except SuperAdmin)
```
The `SQL/` folder under the parent dir likely holds the script.

---

## 7. KEY SERVICES (in `PreConHub/Services/`)

| Service | Responsibility |
|---|---|
| `CalculationServices.cs` (3 services in one file) | `ISoaCalculationService` (Statement of Adjustments — LTT, HST/rebates, levy caps with builder absorption, credits, deposit interest), `IShortfallAnalysisService` (compares SOA vs purchaser funds; risk levels Low/Medium/High/VeryHigh), `IProjectSummaryService` (cached dashboard aggregations) |
| `PdfService.cs` | QuestPDF-based SOA document generation |
| `EmailService.cs` | SMTP templated emails (invitations, approvals, penalties, status). Disabled by default |
| `DocumentAnalysisService.cs` | AI-powered APS parsing using iText7 + Claude API. Disabled by default |
| `NotificationService.cs` | DB-backed in-app notifications + real-time SignalR + cross-party penalty emails |
| `NotificationBackgroundService.cs` | Hosted background service running daily checks (late penalty accrual, etc.) |
| `ReportExportService.cs` | Excel / CSV exports for reports |

DI registration is in `Program.cs` (scoped/transient).

---

## 8. EXTERNAL INTEGRATIONS (configurable, currently disabled)

```json
"GoogleDrive":   { "Enabled": false }
"ClaudeApi":     { "Enabled": false, "Model": "claude-sonnet-4-6" }
"EmailSettings": { "IsEnabled": false }
```

**Use alias model IDs** for Claude API: `claude-sonnet-4-6`, `claude-opus-4-6`, `claude-haiku-4-5`. Date-suffixed IDs (e.g. `claude-sonnet-4-6-20250520`) returned 404 in testing — never guess date suffixes.

---

## 9. SAFETY RULES (ALWAYS FOLLOW — user's standing instructions)

1. **Never modify code without user's explicit approval.**
2. Before any change: show minimal change set, affected files, before/after diffs.
3. After approval: apply in small batches, run build after each batch, fix errors.
4. User approves batch-by-batch.
5. **Always commit and push after end of any task.** (User explicitly wants this — see workflow rules below.)
6. Get approval for each step before proceeding.

These rules are enforced by the user across every session — do NOT skip them even if a change "feels small."

---

## 10. WORKFLOW & GIT

- **Repo:** `C:\My Projects\PreConHub\PreConHub\` (the git root)
- **Remote:** `https://github.com/afshin-menupoly/sPreConHub.git` → `main` branch
- **Latest commit at handoff:** `489f0c3 — feat: add searchable existing-user list to lawyer and MA assignment pages`
- **108 commits total**
- **Video files** (>100 MB) are excluded via `.gitignore`: `**/wwwroot/videos/`
- Currently uncommitted: `.claude/settings.local.json` (modified), 5 spec PDFs untracked under `PreConHub/docs/spec/` (APS.pdf, APS1-3.pdf, SOA.pdf — these are real-world reference documents)

After every task: `git add -A && git commit -m "..." && git push origin main`. The user explicitly requires this.

---

## 11. PROJECT HISTORY — 27 SESSIONS (all features complete)

The project is **feature-complete** as of Session 27. Currently in **end-to-end testing phase**.

| Session | Date | Theme |
|---|---|---|
| 1–8 | up to 2026-02-19 | Initial build: data model, AI logic, workflows, audit, MA workflow, SOA real-world alignment, Lawyer SOA upload |
| 9–13 | 2026-02-20 to 22 | P7–P20: SOAVersion, color system, Project Investment, MA per-project, SOA history, extensions, purchaser/unit details, notifications, reports, shortfall calc fixes, role restructuring, Builder Decision + edit lock |
| 14 | 2026-02-22 | Two critical SOA bugs: NetSalePrice formula (`-` → `+`) and deposit interest fallback rate (`/100m`) |
| 15 | 2026-02-23 | UX (pagination on 5 pages, profile pages with 2FA, login redesign, extension request improvements) + 8 security fixes (CSRF, CORS, headers, cookies, backdoor removal) |
| 16 | 2026-02-26 | Buyer's lawyer feature: purchaser invites lawyer + new `BuyerLawyerController` (13 actions) |
| 17 | 2026-02-26 | Builder assigns buyer's lawyer (individual + bulk + CSV import) + `AddPurchaser` bug fix |
| 18 | 2026-03-02 | APS gap analysis — identified 19 gaps from real APS document |
| 19 | 2026-03-02 | APS Phases 1-4: Schedule B fee types (9 enum values), combined levy caps, missing entity fields (Tarion dates, vendor solicitor, parking/locker), AI parser expansion |
| 20 | 2026-03-02 | Unit-level APS upload + unit fee management UI |
| 21 | 2026-03-02 | Late closing penalty system — daily accrual, pause/resume, close unit, background service, 3 email templates |
| 22 | 2026-03-02 | Builder's lawyer penalty access + cross-party notifications & emails |
| 23 | 2026-03-03 | APS gaps 16-19: NSF charges, default interest (24% p.a.), delayed occupancy compensation ($150/day max $7,500), assignment fee |
| 24 | 2026-03-03 | APS upload refinements — unit-only flow, AI Section Guide, save & download APS PDF |
| 25 | 2026-03-16 | Form completeness, Unit Details redesign, APS extraction fixes, SOA calc improvements (10 commits, 4 migrations: ParkingLockerNumbers, OccupancyFeeFields, UnitFeeFeeType, UpgradeChargesFields) |
| 26 | 2026-03-17 | SOA real-world alignment vs Line 5 / Suite 405-S real lawyer SOA — P1/P2/P3 (7 commits, 4 migrations: UpgradeChargeInterestPeriods, P1_ParkingLockerCommonExpense_OccupancyFeeAdj, P2_PriorYearLandTax, P3_LegalIdentifiers). After fixes, SOA matches real builder SOA closely |
| 27 | 2026-03-18 | UX — searchable existing-user lists for AssignLawyer, AssignBuyerLawyer, ManageMarketingAccess (commit `489f0c3`) |

Detailed per-session notes (with commit hashes and files modified) live in:
- `C:\Users\afshi\.claude\projects\C--My-Projects-PreConHub-PreConHub\memory\session25-summary.md`
- `C:\Users\afshi\.claude\projects\C--My-Projects-PreConHub-PreConHub\memory\session26-summary.md`
- `C:\Users\afshi\.claude\projects\C--My-Projects-PreConHub-PreConHub\memory\preconhub-status.md` (full status — Sessions 14-24)
- `C:\Users\afshi\.claude\projects\C--My-Projects-PreConHub-PreConHub\memory\preconhub-gaps.md` (gap closures)
- `C:\Users\afshi\.claude\projects\C--My-Projects-PreConHub-PreConHub\memory\aps-gap-analysis.md` (APS vs SOA gaps — all 19 closed)
- `C:\Users\afshi\.claude\projects\C--My-Projects-PreConHub-PreConHub\memory\security-audit.md`

These point-in-time files are stale but historically authoritative — verify current code before quoting line numbers.

---

## 12. DATABASE MIGRATIONS (chronological — all applied)

```
CreateIdentitySchema, InitialCreate, AddPurchaserPortalFields, AddLawyerPortal,
LawyerPortal, SOAEnhancements, AddNotifications2,
Priority1_DataModel, Priority2_AILogicFixes, Priority3_WorkflowsAndComments,
Priority6_SOAAlignment, LawyerSOAUpload, AdminMgmt_SuperAdmin_BuilderQuotas,
Priority7_SpecAlignment, AddCreatedByUserId, AddNetSalePriceFields,
AddLawyerSOAConfirmation, AddBuilderDecision,
AddLawFirmToApplicationUser, AddCellPhoneToApplicationUser,
AddBuyerLawyerFields, AddScheduleBClosingFees, AddCombinedLevyCap,
AddAPSEntityFields, AddLateClosingPenalty, AddAPSGaps16To19,
AddParkingLockerNumbers, AddOccupancyFeeFields, AddUnitFeeFeeType,
AddUpgradeChargesFields, AddDevelopmentFeeAndMetersFee,
AddUpgradeChargeInterestPeriods,
P1_ParkingLockerCommonExpense_OccupancyFeeAdj, P2_PriorYearLandTax,
P3_LegalIdentifiers
```
**Total: 35 EF migrations + 1 Identity schema = 36 migration files.**

---

## 13. ARCHITECTURE & FILE LOCATIONS

| What | Path |
|---|---|
| All entities | `PreConHub/Models/Entities/AllEntities.cs` |
| Primary ViewModels | `PreConHub/Models/ViewModels/AllViewModels.cs` |
| Report ViewModels | `PreConHub/Models/ViewModels/ReportViewModels.cs` |
| Document ViewModels | `PreConHub/Models/ViewModels/DocumentViewModels.cs` |
| SOA + Shortfall + Summary calc | `PreConHub/Services/CalculationServices.cs` |
| PDF generation | `PreConHub/Services/PdfService.cs` |
| Notifications + SignalR | `PreConHub/Services/NotificationService.cs` |
| Background jobs | `PreConHub/Services/NotificationBackgroundService.cs` |
| AI APS parser | `PreConHub/Services/DocumentAnalysisService.cs` |
| Email | `PreConHub/Services/EmailService.cs` |
| DB context | `PreConHub/Data/ApplicationDbContext.cs` |
| Workflow spec | `PreConHub/docs/spec/WORKFLOW_SPEC.md` |
| Source spec doc | `PreConHub/docs/spec/PreConHubReconstructionPlatform.docx` |
| Real APS reference | `PreConHub/docs/spec/APS.pdf` (Line 5 South, Suite 405 S) |
| Real SOA reference | `PreConHub/docs/spec/FinalSOA1607.pdf` and `SOA.pdf` |
| Layout / nav | `PreConHub/Views/Shared/_Layout.cshtml` |
| Login page | `PreConHub/Areas/Identity/Pages/Account/Login.cshtml` |

---

## 14. CONTROLLERS (12 total)

`AccountController, AdminController, BuyerLawyerController, ExtensionRequestController, HomeController, LawyerController, MarketingAgencyController, NotificationsController, ProjectsController, PurchaserController, ReportsController, UnitsController`

All gated with `[Authorize]` + role restrictions. CSRF tokens enforced via `[ValidateAntiForgeryToken]` on state-changing POSTs.

---

## 15. BUSINESS-LOGIC GOTCHAS (cost the previous session real time to figure out)

### Entity / Field naming
- `UnitPurchaser.IsPrimaryPurchaser` (NOT `IsPrimary`)
- `LawyerAssignment.UnitId` is `int?` (nullable) — use `?? 0` when assigning to `AuditLog.EntityId`
- `LawyerAssignment.Role` distinguishes `BuilderLawyer` vs `PurchaserLawyer`. Unique index on `(UnitId, LawyerId, Role)`
- `DepositHolder` and `InterestCompoundingType` enums live ONLY in `AllEntities.cs` (not in ViewModels namespace)
- `DepositViewModel.Holder` is `string` — map from entity enum with `.ToString()`
- `ApplicationUser.LawFirm` (string 200), `ApplicationUser.CellPhone` (string 20)

### Razor / Views
- Razor `@` in view text must be escaped as `@@`
- `<option>` tag helper does NOT support `@(...)` in attributes — use `@foreach` with conditional `selected` attribute (this caused RZ1031 errors)
- `@Html.Raw(JsonSerializer.Serialize(...))` exists in 2 report views — low risk but defense-in-depth says use `@Json.Serialize()`

### EF Core / DbContext
- **SOA recalc:** must set `soa.Id = existingSoa.Id` BEFORE `Entry().CurrentValues.SetValues()` — otherwise EF throws "Id is part of a key" error
- Background `Task.Run`: always use `IServiceScopeFactory` and capture primitives (int IDs, strings) BEFORE entering the task — never capture DbContext or tracked entities

### SOA calculation rules (the heart of the app)
- **Net Sale Price formula:** `(TotalSalePrice + HSTRebateTotal) / 1.13` — note the `+` sign. Got this wrong once with `-` and it was a critical bug
- **Deposit interest fallback rate:** divide raw `InterestRate.Value` by `100m` — period-based path already does this; fallback path was missing it (1100% interest bug)
- **Late penalties:** no HST — added directly to `TotalVendorCredits`, NOT through `feeItemsBase`
- **Default interest:** 24% p.a. on bounced + late deposits → `TotalVendorCredits`
- **Delayed occupancy compensation:** $150/day × delay days, max 50 days ($7,500) → `TotalPurchaserCredits` (Tarion Section 7)
- **Assignment fee:** added via `feeItemsBase × 1.13` (HST applies)
- **`UnitFee.FeeType`** (nullable) — when set, SOA uses unit-level fee instead of project-level. Helper: `UnitOrProjectFee(ft)` / `UnitOrProjectFees(fts...)` — unit overrides project
- **Upgrades formula:** `soa.Upgrades = unit.UpgradeAmount + unit.Fees.Where(!IsCredit && !FeeType).Sum()`
- **Upgrade Charge Interest Periods** — per-period interest on upgrade charges (mirrors deposit-interest-period pattern)
- **Monthly common expense** = dwelling + parking + locker (3 separate fields on Unit). Reserve Fund, First Month, Common Expense Adj all include parking + locker
- **`PriorYearAnnualLandTax`** — for multi-year property tax split when occupancy/closing span years
- **`OccupancyFeeTaxRefund`** + **`OccupancyFeeClosingMonthAdj`** — appear in Credit Purchaser items
- **SOA version history** requires `createdByUserId` parameter — always pass `userId` to `CalculateSOAAsync` (3 historic call sites had `null`, all fixed)
- **Interest-on-deposit-interest** uses per-period rates with fallback to last known rate when no period covers occupancy→closing
- **HST rebate** requires `IsPrimaryResidence` only (Ontario New Housing Rebate). The `IsFirstTimeBuyer` flag affects LTT rebate only (Ontario $4K + Toronto $4,475)
- **AI prompt:** `purchasePrice` = dwelling-only (exclude parking + locker) to avoid double-counting in `SalePrice`
- **Deposit interest days:** inclusive counting (+1) — both start and end dates count (e.g. Oct 1 – Mar 31 = 182 days)
- **Deposit interest rate:** 0% is valid (form `min="0"`)

### Spec-aligned constants
- **VTB First Mortgage** capped at 75% APS
- **Mutual Release formula:** `APS - ((APS - AppraisedValue) / 3)`
- **Credit score thresholds:** ≥ 700 for VTB First Mortgage, < 600 for Default
- **NSF Fee:** $500 + $65 HST = $565 (`FeeAmount=500, HSTAmount=65, TotalCharge=565`)
- **HST Rebate caps:** Federal $6,300 + Ontario $24,000

---

## 16. CURRENT STATUS (as of handoff 2026-05-09)

**Phase: END-TO-END TESTING** (all features built — no new feature work pending)

### Test data state
Project 6 (Line 5, unit 405S) has full real-world data loaded for SOA validation:
- All fees entered: Tarion override ($1,595), Meters ($657), Carbon Monoxide ($150), Parkland Levy ($21,361)
- Parking common expense ($71.44), Locker common expense ($11.91)
- Prior year land tax ($4,542), Closing year land tax ($4,788.45)
- Legal identifiers entered (Tarion B47271, HST#, TSCC 1600, unit numbers)
- 4 upgrade charge interest periods (Dec 2022 – Nov 2024)

After Session 26 fixes, generated SOA matches real lawyer SOA closely. Remaining tiny diffs are data-entry precision, not code issues.

### End-to-end testing checklist (priority order)
1. Penalty system E2E (Builder + Lawyer set/pause/resume; background daily accrual; cross-party notifications; AuditLog `UserRole` per action)
2. NSF flow (record bounce → SOA shows `NSFChargesTotal + DefaultInterest`)
3. Assignment flow (record assignment → SOA shows `AssignmentFeeTotal`)
4. Delayed occupancy (set Tarion dates → SOA shows `DelayedOccupancyCompensation`)
5. APS upload E2E (requires Claude API key in `appsettings.json`; test save/download/re-upload)
6. Manual fee entry (levy caps, Tarion dates, parking/locker in unit forms)
7. Buyer's lawyer flow (purchaser invites + builder assigns + lawyer reviews)
8. Full role-by-role walkthrough: Builder → Purchaser → Lawyer → BuyerLawyer → MA → Admin

### Production deployment prep TODOs
- **CORS origins** — add prod domain to `WithOrigins()` in `Program.cs` (currently `localhost:7260` + `localhost:5143`)
- **AllowedHosts** — currently `localhost`; add prod domain in `appsettings.json`
- **Secrets** — move connection string + SMTP password to User Secrets (dev) / Azure Key Vault (prod). Currently plaintext in `appsettings.json`
- **File upload limits** — add `[RequestSizeLimit]` to upload actions
- **AuditLog.IpAddress** — populate `HttpContext.Connection.RemoteIpAddress?.ToString()` on AuditLog inserts (field exists but never set)
- **Failed login logging** — add AuditLog entry in `Login.cshtml.cs` on failed sign-in (Identity handles 5-attempt 15-min lockout but doesn't audit)
- **`@Html.Raw` cleanup** — replace 2 remaining instances in `Views/Reports/ProjectReport.cshtml:238` and `Views/Reports/AllProjects.cshtml:114` with `@Json.Serialize()`
- **Cookie hardening already done:** `HttpOnly=true, SecurePolicy=Always, SameSite=Strict`
- **Security headers already done:** `X-Content-Type-Options: nosniff, X-Frame-Options: DENY, Referrer-Policy, Permissions-Policy, X-XSS-Protection`

---

## 17. APP CONFIGURATION SUMMARY

```
appsettings.json
├── ConnectionStrings.DefaultConnection: SQL Server @ 74.208.251.169 (PLAINTEXT)
├── EmailSettings (Gmail SMTP, IsEnabled=false)
├── AllowedHosts: "localhost"
├── ClaudeApi (Enabled=false, Model="claude-sonnet-4-6")
├── GoogleDrive (Enabled=false)
└── LandTransferTax: { OntarioFirstTimeBuyerRebate: 4000, TorontoFirstTimeBuyerRebate: 4475 }
```

Session timeout: **2 hours**.
Identity uses `AddDefaultTokenProviders()` for 2FA (App / Email / SMS-placeholder).

---

## 18. WHERE PERSISTENT MEMORY LIVES

The previous Claude session used file-based memory at:
```
C:\Users\afshi\.claude\projects\C--My-Projects-PreConHub-PreConHub\memory\
├── MEMORY.md                  ← per-Claude-session index (auto-loaded each turn)
├── preconhub-status.md
├── preconhub-gaps.md
├── aps-gap-analysis.md
├── security-audit.md
├── session25-summary.md
├── session26-summary.md
└── feedback_api_model_ids.md
```

If the new Claude account uses a different memory path, those files won't auto-load — but they remain valuable as deep references. Read them when you need historical detail beyond what's summarized here.

---

## 19. USER PROFILE & WORKING STYLE

- Email: `mikemenupolymanager@gmail.com` (memory context says `info@afshahin.com` is the seeded SuperAdmin)
- Git author: `afshin`
- Plays the role of senior PM + product owner; understands the domain deeply
- Wants **batch-by-batch approval** before any code change
- Wants **commit + push after every task** — non-negotiable
- Communication: short, direct, professional. Don't pad responses
- Will frequently compare app output against real-world reference docs (APS.pdf, SOA.pdf in `docs/spec/`) — accuracy of SOA math is paramount

---

## 20. SLN / CSPROJ DETAIL

- Solution: `C:\My Projects\PreConHub\PreConHub\PreConHub.sln`
- Single project: `PreConHub/PreConHub.csproj`
- Target: `net8.0`, `Nullable=enable`, `ImplicitUsings=enable`
- UserSecretsId: `aspnet-PreConHub-0a8fbd62-194e-48ed-9396-eec305c93dc7` (use `dotnet user-secrets` for secrets in dev)

---

## 21. QUICK START FOR NEW CLAUDE SESSION

1. Read this file (you just did).
2. `git log --oneline -10` — see what's recent.
3. `git status` — see uncommitted state.
4. Read `PreConHub/CLAUDE.md` for project instructions (you'll see it auto-loaded).
5. Wait for the user's first task — **do not modify code without approval**.
6. When in doubt about SOA math, consult `PreConHub/docs/spec/APS.pdf` (input) and `PreConHub/docs/spec/SOA.pdf` (expected output) for project 6 / unit 405S.
7. After any task: build → commit → push → confirm with user.

---

**End of handoff. The new Claude session should now have full context to continue work safely.**
