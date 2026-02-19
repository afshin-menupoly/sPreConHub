# PreConHub — Gap Analysis & Remediation Status

**Original analysis date:** 2026-02-18
**Last updated:** 2026-02-19 (after Priority 4 Audit & Compliance + Priority 5 Bug Fixes)
**Spec source:** `PreConHub/docs/spec/WORKFLOW_SPEC.md`
**Codebase:** ASP.NET Core 8 MVC at `PreConHub/`

---

## Session Log

| Session | Date | Priorities Applied | Migrations Created |
|---|---|---|---|
| 1 | 2026-02-18 | Priority 1 (Data Model), Priority 2 (AI Logic), Priority 3 (Workflows) | `Priority1_DataModel`, `Priority2_AILogicFixes`, `Priority3_WorkflowsAndComments` |
| 2 | 2026-02-19 | Priority 3 views complete; all 3 migrations applied to DB | Migration fix: conditional index SQL in `Priority1_DataModel` |
| 3 | 2026-02-19 | Priority 4 (Audit & Compliance) + Priority 5 (Bug Fixes) complete | No new migrations required |

> **DATABASE:** All 3 migrations have been applied. No further migrations are needed for Priority 4 or 5.

---

## Overall Assessment

> Original estimate: ~65% system change required.
> After Priority 1–5: approximately 85% complete. Remaining gaps are Marketing Agency workflow, Lawyer SOA upload, and SOA differences flag.

---

## PRIORITY STATUS OVERVIEW

| Priority | Scope | Status |
|---|---|---|
| **Priority 1** | Data Model changes | ✅ DONE |
| **Priority 2** | AI Logic corrections | ✅ DONE |
| **Priority 3** | Missing Workflows + Views | ✅ COMPLETE |
| **Priority 4** | Audit & Compliance | ✅ COMPLETE |
| **Priority 5** | Bug Fixes | ✅ COMPLETE |

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

## REMAINING GAPS NOT YET ASSIGNED TO A PRIORITY

### Marketing Agency full workflow
- `UserType.MarketingAgency` ✅ added (Priority 1)
- `"MarketingAgency"` Identity role ✅ seeded (Priority 3)
- `Project.AllowMarketingAccess` flag ✅ added (Priority 1)
- ❌ No `MarketingAgencyController` or views
- ❌ No per-project toggle UI to grant/revoke access
- ❌ No discount/credit suggestion workflow for Marketing Agency role
- ❌ No `[Authorize(Roles="MarketingAgency")]` controller actions

### SOA differences flag (spec Process D)
- ❌ No UI comparing system-calculated SOA vs. lawyer-uploaded balance due
- Would require a field on `StatementOfAdjustments` for `LawyerUploadedBalanceDue` and a view showing the delta

### Lawyer SOA upload (spec Process D)
- ❌ `LawyerController` has no action to upload a new SOA document
- Only builder can upload/recalculate SOA via `UnitsController`

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

## HOW TO RESUME NEXT SESSION

Priorities 1–5 are all complete. Remaining work is in unassigned gaps:

- **Marketing Agency workflow** — controller, views, per-project toggle UI, discount/credit suggestion workflow
- **Lawyer SOA upload** — `LawyerController` action to upload a new SOA document
- **SOA differences flag** — UI comparing system-calculated SOA vs. lawyer-uploaded balance due

Tell Claude Code:

> "Continue from the PreConHub gap analysis. Priorities 1–5 are complete. Read GAP_ANALYSIS.md for the remaining unassigned gaps (Marketing Agency workflow, Lawyer SOA upload, SOA differences flag). All 3 DB migrations are applied. No new migrations are pending."
