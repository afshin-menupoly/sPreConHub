# PreConHub — Gap Analysis & Remediation Status

**Original analysis date:** 2026-02-18
**Last updated:** 2026-02-19 (Priority 6 SOA Real-World Alignment — planning complete, work not yet started)
**Spec source:** `PreConHub/docs/spec/WORKFLOW_SPEC.md`
**Codebase:** ASP.NET Core 8 MVC at `PreConHub/`

---

## Session Log

| Session | Date | Priorities Applied | Migrations Created |
|---|---|---|---|
| 1 | 2026-02-18 | Priority 1 (Data Model), Priority 2 (AI Logic), Priority 3 (Workflows) | `Priority1_DataModel`, `Priority2_AILogicFixes`, `Priority3_WorkflowsAndComments` |
| 2 | 2026-02-19 | Priority 3 views complete; all 3 migrations applied to DB | Migration fix: conditional index SQL in `Priority1_DataModel` |
| 3 | 2026-02-19 | Priority 4 (Audit & Compliance) + Priority 5 (Bug Fixes) complete | No new migrations required |
| 4 | 2026-02-19 | Marketing Agency full workflow complete (controller + views + toggle + nav link) | No new migrations |
| 5 | 2026-02-19 | SOA real-world alignment — research complete, spec written, work not yet started | `Priority6_SOAAlignment` (pending) |

> **DATABASE:** All 3 migrations have been applied. No further migrations are needed for Priority 4 or 5.

---

## Overall Assessment

> Original estimate: ~65% system change required.
> After Priority 1–5 + Marketing Agency: approximately 90% complete.
> Priority 6 (SOA Real-World Alignment) is the next major work block — 6-part plan, data model + calculation engine + PDF layout overhaul.

---

## PRIORITY STATUS OVERVIEW

| Priority | Scope | Status |
|---|---|---|
| **Priority 1** | Data Model changes | ✅ DONE |
| **Priority 2** | AI Logic corrections | ✅ DONE |
| **Priority 3** | Missing Workflows + Views | ✅ COMPLETE |
| **Priority 4** | Audit & Compliance | ✅ COMPLETE |
| **Priority 5** | Bug Fixes | ✅ COMPLETE |
| **Marketing Agency** | Full MA workflow (unassigned gap) | ✅ COMPLETE |
| **Priority 6** | SOA Real-World Alignment | ⏳ PLANNED — not started |

---

## PRIORITY 1 — Data Model ✅ COMPLETE

### Files changed
- `Models/Entities/AllEntities.cs`
- `Data/ApplicationDbContext.cs`
- Migration: `Priority1_DataModel`

### What was done

**`AllEntities.cs`**
- `UserType` enum: added `MarketingAgency = 4`
- `Project`: added `AllowMarketingAccess` (bool), `Financials` navigation property to `ProjectFinancials`
- `Unit`: added `ExtensionRequests` navigation property (`ICollection<ClosingExtensionRequest>`)
- `ClosingRecommendation` enum: added `MutualRelease = 6`, `CombinationSuggestion = 7`
- `MortgageInfo`: added `IsBlanketMortgage` (bool), `PurchaserAppraisalValue` (decimal?), `EstimatedFundingDate` (DateTime?), `CreditScore` (int?), `CreditBureau` (string?)
- `ShortfallAnalysis`: added `MutualReleaseThreshold` (decimal?), `DecisionAction` (string?), `DecisionByBuilderId` (string?), `DecisionAt` (DateTime?), `BuilderModifiedSuggestion` (string?)
- `AuditLog`: added `Comments` (string?)
- `ProjectSummary`: added `TotalFundNeededToClose` (decimal)
- NEW entity `ProjectFinancials`: `TotalRevenue`, `TotalInvestment`, `MarketingCost`, `ProfitAvailable`, `MaxBuilderCapital`, `Notes`, `CreatedAt`, `UpdatedAt`, `UpdatedByUserId` — one-to-one with `Project`
- NEW entity `ClosingExtensionRequest`: `UnitId`, `RequestedByPurchaserId`, `RequestedDate`, `OriginalClosingDate` (DateTime?), `RequestedNewClosingDate` (DateTime), `Reason`, `Status` (`ClosingExtensionStatus`), `ReviewedByBuilderId`, `ReviewedAt`, `ReviewerNotes`, `CreatedAt` — many-to-one with `Unit`
- NEW enum `ClosingExtensionStatus`: `Pending=0`, `Approved=1`, `Rejected=2`

**`ApplicationDbContext.cs`**
- Added `DbSet<ProjectFinancials> ProjectFinancials`
- Added `DbSet<ClosingExtensionRequest> ClosingExtensionRequests`
- Added Fluent API config for `ProjectFinancials` (one-to-one with Project, cascade delete, unique index on ProjectId)
- Added Fluent API config for `ClosingExtensionRequest` (cascade on Unit delete, restrict on purchaser/builder deletes, indexes on UnitId + Status)
- **Fixed Bug 2:** `LawyerAssignment` unique index changed from `(ProjectId, LawyerId)` to `(UnitId, LawyerId)`

---

## PRIORITY 2 — AI Logic Corrections ✅ COMPLETE

### Files changed
- `Models/Entities/AllEntities.cs` (added `TotalFundNeededToClose` to `ProjectSummary`)
- `Services/CalculationServices.cs`
- Migration: `Priority2_AILogicFixes`

### What was done

**`CalculationServices.cs`**
- **Fixed Bug 1:** `bool isPrimaryResidence = true;` → `bool isPrimaryResidence = unit.IsPrimaryResidence;`
- `IShortfallAnalysisService.DetermineRecommendation`: added `int? creditScore = null` default parameter
- `AnalyzeShortfallAsync`: added `.Include(u => u.Project).ThenInclude(p => p.Financials)` to unit query
- `AnalyzeShortfallAsync`: added Step 2 mutual release check using `MutualReleaseThreshold = APS_Unit - ((APS_Unit - AppraisedValue) / 3)` formula; sets `analysis.MutualReleaseThreshold`; if triggered, bypasses tier logic
- `AnalyzeShortfallAsync`: replaced hardcoded allocation with spec Step 4 formulas (`DiscountAllocated = min(Shortfall, ProfitPerUnit)`; `VTBAllocated = min(Shortfall - Discount, MaxBuilderCapital / UnitsUnsold)`) with graceful fallback when `ProjectFinancials` is null
- `DetermineRecommendation`: fixed thresholds to 10% / 20% / 50% (were 10% / 20% / 30%)
- `DetermineRecommendation`: added CreditScore gate for `VTBFirstMortgage` (≥700) and `HighRiskDefault` (<600); added `CombinationSuggestion` return for unmatched cases
- `GenerateRecommendationReasoning`: added cases for `MutualRelease` and `CombinationSuggestion`
- `MapRecommendationToStatus`: added `MutualRelease → AtRisk`, `CombinationSuggestion → NeedsVTB`
- `CalculateProjectSummaryAsync`: calculates and stores `TotalFundNeededToClose = sum(max(0, Shortfall - Discount - VTB) for all units)`

### Remaining AI gaps (not in Priority 2)
- AI does not read purchaser `Comments` field to influence suggestions
- AI does not read APS parsed data to influence suggestions
- `MutualRelease` tier check requires `AppraisedValue` — if purchaser has not submitted mortgage info with `PurchaserAppraisalValue`, the check falls back to `Unit.CurrentAppraisalValue` (builder-set). This is correct per spec but dependent on Priority 3 form submission working end-to-end.

---

## PRIORITY 3 — Missing Workflows ✅ COMPLETE

### Files changed
- `Models/Entities/AllEntities.cs`
- `Program.cs`
- `Models/ViewModels/AllViewModels.cs`
- `Controllers/PurchaserController.cs`
- `Controllers/UnitsController.cs`
- Migration: `Priority3_WorkflowsAndComments`

### What was done

**`AllEntities.cs`**
- `MortgageInfo`: added `Comments` (string?) — purchaser free-text notes

**`Program.cs`**
- `SeedRolesAsync`: added `"MarketingAgency"` to the seeded roles array

**`AllViewModels.cs`**
- `SubmitMortgageInfoViewModel`: added 6 new fields: `IsBlanketMortgage`, `PurchaserAppraisalValue`, `EstimatedFundingDate`, `CreditScore`, `CreditBureau`, `Comments`
- NEW `SubmitExtensionRequestViewModel`: `UnitId`, `UnitNumber`, `ProjectName`, `CurrentClosingDate`, `ExistingRequestId`, `RequestedNewClosingDate` [Required], `Reason` [Required]
- NEW `ReviewExtensionRequestViewModel`: `RequestId`, `UnitId`, `UnitNumber`, `ProjectName`, `PurchaserName`, `OriginalClosingDate`, `RequestedNewClosingDate`, `Reason`, `RequestedDate`, `ReviewerNotes`, `Approve`
- NEW `ReviewSuggestionViewModel`: `UnitId`, `UnitNumber`, `Decision` [Required] ("Accept"/"Modify"/"Reject"), `ModifiedSuggestion`

**`PurchaserController.cs`**
- `SubmitMortgageInfo` GET: pre-fills all 6 new `MortgageInfo` fields from entity
- `SubmitMortgageInfo` POST: saves all 6 new `MortgageInfo` fields to entity
- NEW action `SubmitExtensionRequest` GET (`/Purchaser/SubmitExtensionRequest/{unitId}`): loads current closing date and any pending request; returns `SubmitExtensionRequestViewModel`
- NEW action `SubmitExtensionRequest` POST: cancels any pending request (sets to Rejected), creates new `ClosingExtensionRequest` with `Status=Pending`

**`UnitsController.cs`**
- `Edit` POST: captures `oldClosingDate` before update; after `SaveChangesAsync`, if closing date changed → calls `_soaService.CalculateSOAAsync(id)` + `_shortfallService.AnalyzeShortfallAsync(id)` (errors are caught and logged, not surfaced to user)
- NEW action `ReviewExtensionRequest` GET (`/Units/ReviewExtensionRequest/{requestId}`): builder sees purchaser name, original date, requested date, reason
- NEW action `ReviewExtensionRequest` POST: approve → updates `Unit.ClosingDate`, saves, recalculates SOA + shortfall; reject → saves status only; both paths record `ReviewedByBuilderId`, `ReviewedAt`, `ReviewerNotes`
- NEW action `ReviewSuggestion` POST (`/Units/ReviewSuggestion`): saves `DecisionAction`, `DecisionByBuilderId`, `DecisionAt`, `BuilderModifiedSuggestion` on `ShortfallAnalysis`

All Priority 3 views are complete — see the Views table below.

---

## PRIORITY 4 — Audit & Compliance ✅ COMPLETE

### Files changed
- `Services/CalculationServices.cs`
- `Controllers/UnitsController.cs`
- `Controllers/ProjectsController.cs`
- `Controllers/PurchaserController.cs`
- `Controllers/LawyerController.cs`
- `Views/Purchaser/AuditTrail.cshtml` (NEW)

### What was done

**22. Consistent `AuditLog` writes across all controllers**
- `UnitsController`: Create, Edit, Delete → `AuditLog` added before/after `SaveChangesAsync`; CalculateSOA and AddDeposit → `userId` added, `AuditLog` written
- `ProjectsController`: Create, Edit → `AuditLog` added; AddFee → `userId` added, `AuditLog` written
- `PurchaserController`: SubmitMortgageInfo, SubmitFinancials, UploadDocument, SubmitExtensionRequest → `AuditLog` added
- `LawyerController`: ApproveUnit, RequestRevision, AddNote → `AuditLog` added (`UnitId ?? 0` for nullable int)
- Role strings: `User.IsInRole("Admin") ? "Admin" : "Builder"` for builder-side; literal `"Purchaser"` / `"Lawyer"` for their respective controllers

**23. SOA version history**
- `CalculationServices.CalculateSOAAsync`: before overwriting existing SOA, serializes `{ BalanceDueOnClosing, TotalDebits, TotalCredits, CalculationVersion, CalculatedAt }` to `AuditLog.OldValues` JSON

**24. Purchaser audit trail view**
- `PurchaserController.AuditTrail` GET: queries `AuditLogs` filtered by `UserId == currentPurchaserId`, ordered descending
- `Views/Purchaser/AuditTrail.cshtml`: table with Date/Time, Action badge (colour-coded), EntityType, Details column

**25. Restrict builder access to mortgage docs**
- `UnitsController.Details`: replaced both placeholder comment stubs with real mappings for `Deposits`, `Documents`, `AllPurchasers`, `PrimaryPurchaser`, `SOA`, `Shortfall`
- Sensitive mortgage fields (`MortgageProvider`, `MortgageAmount`, `ApprovalExpiryDate`, `AdditionalCashAvailable`) are set to `null` unless `User.IsInRole("Admin")`

---

## PRIORITY 5 — Bug Fixes ✅ COMPLETE

### Files changed
- `Controllers/UnitsController.cs`
- `Models/ViewModels/AllViewModels.cs`

### What was done

**26. Fix builder `DownloadSOA` returns plain text, not PDF (Bug 3)**
- Replaced plain-text `.txt` return in `UnitsController.DownloadSOA` with proper `_pdfService.GenerateStatementOfAdjustments(unit, unit.SOA, deposits, purchaserName, coPurchaserNames)` → returns `application/pdf`
- Added `Include(u => u.Deposits)` and `Include(u => u.Purchasers).ThenInclude(p => p.Purchaser)` to the query

**27. Remove enum duplication (Bug 4)**
- Removed `InterestCompoundingType` and `DepositHolder` duplicate definitions from `AllViewModels.cs`
- `DepositViewModel.CompoundingType` now resolves to `PreConHub.Models.Entities.InterestCompoundingType` via the existing `using PreConHub.Models.Entities;` at top of file
- Removed the explicit namespace cast `(PreConHub.Models.ViewModels.InterestCompoundingType)(int)` from `UnitsController.Details` Deposit mapping

---

## MARKETING AGENCY WORKFLOW ✅ COMPLETE (Session 4)

All files created/modified. Committed and pushed to GitHub.

- `MarketingAgencyController.cs` — Dashboard, ProjectUnits, SuggestDiscount POST, AuditTrail
- `Views/MarketingAgency/Dashboard.cshtml` — project cards with toggle state
- `Views/MarketingAgency/ProjectUnits.cshtml` — unit table + per-unit suggest-discount modal
- `Views/MarketingAgency/AuditTrail.cshtml` — MA's own audit log table
- `ProjectsController.cs` — `ToggleMarketingAccess` POST + `ViewBag.AllowMarketingAccess` in Dashboard GET
- `Views/Projects/Dashboard.cshtml` — Marketing Access toggle button
- `Views/Shared/_Layout.cshtml` — nav link for MarketingAgency role
- MA discount suggestions stored in `AuditLog` (Action="SuggestDiscount") — no new migration needed

---

## REMAINING GAPS

### Lawyer SOA upload (spec Process D)
- ❌ `LawyerController` has no action to upload a new SOA document
- Only builder can upload/recalculate SOA via `UnitsController`

### SOA differences flag (spec Process D)
- ❌ No UI comparing system-calculated SOA vs. lawyer-uploaded balance due
- Would require a field on `StatementOfAdjustments` for `LawyerUploadedBalanceDue` and a view showing the delta

### Purchaser `Comments` field in UI
- ✅ `MortgageInfo.Comments` field added to entity and ViewModel (Priority 3)
- ✅ `Views/Purchaser/SubmitMortgageInfo.cshtml` updated to show all 6 new fields (Priority 3 views)

---

## WHAT IS ALREADY CORRECTLY IMPLEMENTED (unchanged from original analysis)

- SOA calculation engine (purchase price, LTT Ontario + Toronto with FTB rebate, Tarion fee, HST/rebates, dev charges with levy cap, common expense adjustments, deposit interest compound types, credits)
- Shortfall calculation: `TotalFunds = DepositsPaid + PurchaserFunds + MortgageAmount`; `Shortfall = BalanceDueOnClosing - TotalFunds`
- All 7 closing recommendation tiers now correct (Priority 2 fixed thresholds + added MutualRelease/CombinationSuggestion)
- Role-based access: Builder, Lawyer, Purchaser, Admin with controller-level `[Authorize]` enforcement
- Document upload and management (SOA PDF, APS PDF via Claude AI parsing, mortgage documents)
- Purchaser invitation workflow
- Lawyer assignment, review, approval, and revision request workflow
- In-app notification system with SignalR real-time delivery
- Full PDF SOA generation via QuestPDF (used by Purchaser, Lawyer, and Builder — Bug 3 fixed)
- Excel/CSV/PDF export reports
- Admin impersonation and user management
- Builder bulk import of units via CSV
- Occupancy fee tracking
- Deposit tracking with interest eligibility flags
- SOA locking after builder + lawyer dual confirmation
- `AuditLog` entity and DbSet, written consistently across all controllers (Priority 4 complete)

---

## COMPLETE PRIORITIZED REMEDIATION — CURRENT STATUS

### Priority 1 — Data Model ✅ DONE
1. ✅ Add `CreditScore` to `MortgageInfo`
2. ✅ Add `IsBlanketMortgage` and `EstimatedFundingDate` to `MortgageInfo`
3. ✅ Create `ProjectFinancials` entity
4. ✅ Create `ClosingExtensionRequest` entity
5. ✅ Add `MutualRelease` and `CombinationSuggestion` to `ClosingRecommendation` enum
6. ✅ Add `DecisionByBuilderId`, `DecisionAt`, `DecisionAction` to `ShortfallAnalysis`
7. ✅ Add `Comments` to `AuditLog`

### Priority 2 — AI Logic Corrections ✅ DONE
8. ✅ Fix shortfall thresholds: 10% / 20% / 50%
9. ✅ Add CreditScore gates to VTBFirstMortgage and HighRiskDefault tiers
10. ✅ Implement `MutualReleaseThreshold` formula
11. ✅ Implement discount allocation using `ProfitPerUnit = ProfitAvailable / UnitsUnsold`
12. ✅ Implement VTB allocation using `MaxBuilderCapital`
13. ✅ Implement `TotalFundNeededToClose` at project level
14. ✅ Fix `IsPrimaryResidence` hardcode in `SoaCalculationService`

### Priority 3 — Missing Workflows ✅ COMPLETE
15. ✅ Purchaser closing extension/reschedule request submit (`PurchaserController.SubmitExtensionRequest`)
15b. ✅ Builder approve/reject extension request (`UnitsController.ReviewExtensionRequest`)
16. ✅ Auto-recalculate SOA when closing date changes (`UnitsController.Edit`)
17. ✅ Builder decision log for AI suggestions (`UnitsController.ReviewSuggestion`)
18. ✅ Marketing Agency role seeded in `Program.cs`
19. ✅ Purchaser comments/notes field (`MortgageInfo.Comments`, ViewModel, controller)
20. ❌ Lawyer SOA upload via `LawyerController` — NOT YET DONE
21. ❌ SOA differences flag (system calc vs. lawyer-uploaded balance) — NOT YET DONE

### Priority 4 — Audit & Compliance ✅ COMPLETE
22. ✅ Consistent `AuditLog` writes across all 4 controllers
23. ✅ SOA version history / snapshot storage (OldValues JSON before overwrite)
24. ✅ Purchaser audit trail view (`GET /Purchaser/AuditTrail` + `AuditTrail.cshtml`)
25. ✅ Restrict builder access to mortgage docs (Details mapping complete; sensitive fields null for non-Admin)

### Priority 5 — Bug Fixes ✅ COMPLETE
26. ✅ Fix builder `DownloadSOA` to use `IPdfService` instead of plain text (Bug 3)
27. ✅ Remove enum duplication (`InterestCompoundingType`, `DepositHolder`) (Bug 4)

---

## DATABASE STATUS

| Migration | Status |
|---|---|
| `Priority1_DataModel` | ✅ Applied (with conditional index SQL fix) |
| `Priority2_AILogicFixes` | ✅ Applied |
| `Priority3_WorkflowsAndComments` | ✅ Applied |

---

## VIEWS THAT NEED TO BE CREATED OR UPDATED

| View File | Status | Notes |
|---|---|---|
| `Views/Purchaser/SubmitMortgageInfo.cshtml` | ✅ DONE | Added `IsBlanketMortgage`, `PurchaserAppraisalValue`, `EstimatedFundingDate`, `CreditScore`, `CreditBureau`, `Comments` |
| `Views/Purchaser/SubmitExtensionRequest.cshtml` | ✅ DONE | Created — pending request warning, date picker, reason textarea |
| `Views/Units/ReviewExtensionRequest.cshtml` | ✅ DONE | Created — request summary, Approve/Reject radios, reviewer notes |
| `Views/Purchaser/AuditTrail.cshtml` | ✅ DONE | Created — audit log table for purchaser's own activity history |

---

---

## PRIORITY 6 — SOA Real-World Alignment ⏳ PLANNED

**Source specs:** `PreConHub/docs/spec/FinalSOA1607.pdf` (real SOA for Suite 1607, 35 Parliament St, Toronto) and `PreConHub/docs/spec/DynamicSOAClosingDate.docx` (dynamic formula spec).
**Goal:** Make the calculation engine and PDF output match the real Ontario SOA structure exactly.
**Broken into 6 parts — do one at a time, build + commit after each.**

---

### Research Findings (confirmed 2026-02-19)

**Ontario LTT — Province-wide formula, no builder inputs required:**

| Price Range | Marginal Rate | Quick Formula |
|---|---|---|
| $0–$55,000 | 0.5% | `Price × 0.005` |
| $55,001–$250,000 | 1.0% | `(Price × 0.01) − $275` |
| $250,001–$400,000 | 1.5% | `(Price × 0.015) − $1,525` |
| $400,001–$2,000,000 | 2.0% | `(Price × 0.02) − $3,525` |
| Over $2,000,000 | 2.5% | `(Price × 0.025) − $13,525` |

FTHB Rebate: Max $4,000 (Ontario) + $4,475 (Toronto). LTT is already calculated correctly; user decision: **show as informational, not as a debit in the SOA balance.**

**Ontario Flat Fee Schedule (all province-wide, no regional variation):**

| Fee | Base Amount | HST Treatment | Notes |
|---|---|---|---|
| HCRA Regulatory Oversight Fee | $170.00 | +13% = $192.10 | Per unit; HCRA may update periodically |
| Electronic Registration (Teranet) | $85.00 | Included | Per instrument registered |
| Status Certificate | $100.00 | Tax-inclusive | Regulated max under Condominium Act 1998 |
| Transaction Levy Surcharge (LAWPRO) | $65.00 | +HST when disbursed = ~$73.45 | LAWPRO insurance levy |

Storage: `SystemFeeConfig` table — admin-editable key/value pairs so amounts can be updated without a code deploy.

**Deposit Interest Rates:** Each project has its own government-published rate schedule. Builder enters periods per deposit (PeriodStart, PeriodEnd, AnnualRate%) on the project/unit page.

---

### Gaps Identified (vs. FinalSOA1607.pdf)

**Calculation Engine (`CalculationServices.cs`):**

| # | Gap | Severity |
|---|---|---|
| P6-1 | Deposit interest uses single rate + compounding; must be per-period daily simple interest | Critical |
| P6-2 | Interest on Deposit Interest (OccupancyDate → ClosingDate) — completely missing | Critical |
| P6-3 | Land tax uses estimated 1% of price; must use actual annual tax × (PurchaserDays/365) | Critical |
| P6-4 | Common expenses uses $0.60/sqft estimate; must use actual monthly fee × (VendorDays/DaysInMonth) | Critical |
| P6-5 | Occupancy fees: only one field; must split into Chargeable (Credit Vendor) + Paid (Credit Purchaser) | Critical |
| P6-6 | HST shown as a debit; must be embedded in Sale Price section | High |
| P6-7 | LTT is a debit in SOA; must be informational only | High |
| P6-8 | HCRA fee completely missing | High |
| P6-9 | Electronic Registration fee completely missing | High |
| P6-10 | Status Certificate fee completely missing | High |
| P6-11 | Transaction Levy fee completely missing | High |
| P6-12 | Security Deposit refund (Credit Purchaser) completely missing | Medium |
| P6-13 | HST not added to vendor fees (dev charges, Tarion, HCRA, connection charges, etc.) | High |
| P6-14 | Tarion: single tiered amount; must be Unit Enrolment + Low-rise Common Element + 13% HST | Medium |
| P6-15 | "Additional Consideration" concept (π) — fees eligible for HST rebate calculation — not modeled | Medium |

**PDF Layout (`PdfService.cs`):**

| # | Gap | Severity |
|---|---|---|
| P6-16 | PDF uses Debits/Credits sections; must use two-column Credit Vendor / Credit Purchaser structure | Critical |
| P6-17 | Sale Price section missing (Agreed Price → HST extracted → Rebate → Net Sale Price → Credit Vendor) | Critical |
| P6-18 | Deposit interest shown as one line; must be per-deposit with per-period rate rows | High |
| P6-19 | All missing fee lines (HCRA, ElecReg, StatusCert, TransactionLevy, SecurityDeposit) | High |
| P6-20 | LTT section shown as debit; must be informational-only section at bottom | Medium |

---

### Part A — Data Model (Migration Required) ⏳ NOT STARTED

**New entity: `DepositInterestPeriod`**
```csharp
public class DepositInterestPeriod {
    public int Id { get; set; }
    public int DepositId { get; set; }
    public Deposit Deposit { get; set; } = null!;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal AnnualRate { get; set; }  // e.g. 1.500 = 1.5%
}
```

**New entity: `SystemFeeConfig`**
```csharp
public class SystemFeeConfig {
    public int Id { get; set; }
    public string Key { get; set; } = "";        // "HCRA", "ElectronicReg", "StatusCert", "TransactionLevy"
    public string DisplayName { get; set; } = "";
    public decimal Amount { get; set; }
    public bool HSTApplicable { get; set; }      // true = add 13% HST on top
    public bool HSTIncluded { get; set; }        // true = amount is already tax-inclusive
    public string? Notes { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedByUserId { get; set; }
}
```

**New fields on `Unit`:**
- `ActualAnnualLandTax` (decimal?) — builder enters actual municipal tax
- `ActualMonthlyMaintenanceFee` (decimal?) — builder enters actual maintenance fee

**New fields on `StatementOfAdjustments`:**
- `HCRAFee` (decimal) — from SystemFeeConfig
- `ElectronicRegFee` (decimal) — from SystemFeeConfig
- `StatusCertFee` (decimal) — from SystemFeeConfig
- `TransactionLevyFee` (decimal) — from SystemFeeConfig
- `SecurityDepositRefund` (decimal) — Credit Purchaser
- `OccupancyFeesChargeable` (decimal) — Credit Vendor (replaces/supplements OccupancyFeesOwing)
- `OccupancyFeesPaid` (decimal) — Credit Purchaser
- `InterestOnDepositInterest` (decimal) — Credit Purchaser
- `TotalVendorCredits` (decimal) — replaces TotalDebits semantically
- `TotalPurchaserCredits` (decimal) — replaces TotalCredits semantically

**Migration name:** `Priority6_SOAAlignment`
**Seed data:** Insert 4 rows in `SystemFeeConfig` (HCRA $170 + HST, ElectronicReg $85 included, StatusCert $100 included, TransactionLevy $65 + HST when disbursed)

---

### Part B — Builder UI: Deposit Interest Rate Periods ⏳ NOT STARTED

- Add "Interest Rate Periods" section to the unit deposit management page
- Per deposit: table showing existing periods (PeriodStart, PeriodEnd, AnnualRate%)
- Add/Edit/Delete period rows
- Controller: `UnitsController.AddDepositInterestPeriod` POST and `DeleteDepositInterestPeriod` POST

---

### Part C — Builder UI: Unit-level Fields ⏳ NOT STARTED

- Add `ActualAnnualLandTax` (decimal, optional) to `UnitsController.Edit` GET/POST + `Views/Units/Edit.cshtml`
- Add `ActualMonthlyMaintenanceFee` (decimal, optional) to same form
- Labels: "Actual Annual Land Tax ($)" and "Actual Monthly Maintenance Fee ($)"
- Note: if left blank, SOA engine falls back to estimates with a warning flag in the SOA

---

### Part D — Admin UI: Fee Schedule ⏳ NOT STARTED

- New admin page: `Views/Admin/FeeSchedule.cshtml`
- Lists all `SystemFeeConfig` rows with Edit inline form
- `AdminController.FeeSchedule` GET + `UpdateFeeConfig` POST
- Only accessible to Admin role

---

### Part E — Calculation Engine Rewrite ⏳ NOT STARTED

**File:** `Services/CalculationServices.cs` — `CalculateSOAAsync` method

Key changes:
1. **Deposit interest** — replace `CalculateDepositInterestEnhanced` with period-based daily simple:
   - For each deposit: loop through its `DepositInterestPeriods`, calculate `Amount × (Rate/100) × (DaysInPeriod/365)`
   - `DaysInPeriod = (MIN(ClosingDate, PeriodEnd) − MAX(DepositDate, PeriodStart)).Days`
2. **Interest on Deposit Interest** — after calculating total deposit interest, calculate interest on that amount from `OccupancyDate` to `ClosingDate` at the last applicable government rate
3. **Land tax** — use `unit.ActualAnnualLandTax` if set; VendorDays = (ClosingDate − Jan1).Days; Credit Vendor = AnnualTax × (PurchaserDays/365); fallback to estimate with flag
4. **Common expenses** — use `unit.ActualMonthlyMaintenanceFee` if set; VendorShare = Monthly × (VendorDays/DaysInMonth); Credit Vendor = Monthly − VendorShare; fallback to estimate
5. **Occupancy fees** — split: `OccupancyFeesChargeable` (Credit Vendor) vs `OccupancyFeesPaid` (Credit Purchaser)
6. **HST** — move from debit line to Sale Price section; NetSalePrice = Total / 1.13; show HST federal + provincial components
7. **LTT** — calculate and store as informational; exclude from `TotalVendorCredits` / `TotalPurchaserCredits` balance
8. **HCRA/ElecReg/StatusCert/TransactionLevy** — load from `SystemFeeConfig`, apply HST rules, add to vendor credits
9. **HST on vendor fees** — add 13% to dev charges, Tarion, HCRA, connection charges where applicable
10. **Tarion** — split into Unit Enrolment + Low-rise Common Element + 13% HST
11. **Balance Due** = `TotalVendorCredits − TotalPurchaserCredits`

---

### Part F — PDF Layout Rewrite ⏳ NOT STARTED

**File:** `Services/PdfService.cs` — `GenerateStatementOfAdjustments` method

Target structure (matching `FinalSOA1607.pdf`):
```
SALE PRICE
  Agreed Sale Price                         Credit Vendor: $xxx
  + Additional Consideration (π)            Credit Vendor: $xxx
  = Total including HST                                    $xxx
  Less: HST Federal ($xxx) + Provincial ($xxx)
  Less: HST Rebate                                        ($xxx)
  = Net Sale Price (Credit Vendor)          Credit Vendor: $xxx

DEPOSITS (each listed separately)
  [Date] Deposit                            Credit Purchaser: $xxx

HST REBATE (if assigned to builder)         Credit Vendor: $xxx

INTEREST ON DEPOSITS (per deposit, per period)
  [Deposit label] [Period dates] @ [Rate]%  Credit Purchaser: $xxx
  [Deposit label] [Period dates] @ [Rate]%  Credit Purchaser: $xxx

INTEREST ON DEPOSIT INTEREST
  [OccupancyDate to ClosingDate] @ [Rate]%  Credit Purchaser: $xxx

LAND TAXES
  Annual Tax: $xxx; Vendor: [days]/365      Credit Vendor: $xxx

COMMON EXPENSES
  Monthly: $xxx; Vendor: [days]/[month]     Credit Vendor: $xxx

TARION WARRANTY
  Unit Enrolment: $xxx
  Low-rise Common Element: $xxx
  HST: $xxx                                 Credit Vendor: $xxx

HCRA REGULATORY OVERSIGHT FEE
  $xxx + HST                                Credit Vendor: $xxx

OCCUPANCY FEES CHARGEABLE                   Credit Vendor: $xxx
OCCUPANCY FEES PAID                         Credit Purchaser: $xxx

ELECTRONIC REGISTRATION FEE                 Credit Vendor: $xxx
STATUS CERTIFICATE                          Credit Vendor: $xxx
TRANSACTION LEVY SURCHARGE                  Credit Vendor: $xxx

CONNECTION/ENERGIZATION CHARGES + HST      Credit Vendor: $xxx
DEVELOPMENT CHARGES + HST                  Credit Vendor: $xxx

REIMBURSE SECURITY DEPOSIT                  Credit Purchaser: $xxx

TOTAL VENDOR CREDITS                                       $xxx
TOTAL PURCHASER CREDITS                                    $xxx
BALANCE DUE ON CLOSING                                     $xxx

─────────────────────────────────────────────────────
INFORMATIONAL — LAND TRANSFER TAX (paid at registration)
  Ontario LTT:   $xxx
  Toronto MLTT:  $xxx
  FTHB Rebate:  ($xxx)
  Net LTT:       $xxx
─────────────────────────────────────────────────────
```

---

## HOW TO RESUME NEXT SESSION

Marketing Agency workflow is complete. Priority 6 (SOA Real-World Alignment) is fully planned — start with Part A.

Tell Claude Code:

> "Continue PreConHub. Marketing Agency workflow is done. Priority 6 SOA alignment is next — start with Part A (data model + migration). Read GAP_ANALYSIS.md for the full spec."

**Migration status:**
| Migration | Status |
|---|---|
| `Priority1_DataModel` | ✅ Applied |
| `Priority2_AILogicFixes` | ✅ Applied |
| `Priority3_WorkflowsAndComments` | ✅ Applied |
| `Priority6_SOAAlignment` | ⏳ Not yet created |
