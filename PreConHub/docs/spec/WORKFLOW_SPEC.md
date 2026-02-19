# PreConHub Reconstruction Platform – Full Workflow Specification

> Source: `PreConHubReconstructionPlatform.docx`
> Last updated: 2026-02-18

---

## Table of Contents

1. [Roles & Permissions](#1-roles--permissions)
2. [Core Processes](#2-core-processes)
   - [A. Document Upload & Management](#a-document-upload--management)
   - [B. Dynamic SOA Calculation](#b-dynamic-soa-calculation)
   - [C. Closing Date / Extension / Reschedule](#c-closing-date--extension--reschedule)
   - [D. APS & Legal Confirmation](#d-aps--legal-confirmation)
   - [E. Financial & Project Investment Management](#e-financial--project-investment-management)
   - [F. Audit & Version History](#f-audit--version-history)
   - [G. AI Tiered Suggestion System](#g-ai-tiered-suggestion-system)
   - [H. Summary of Access Control & Visibility](#h-summary-of-access-control--visibility)
   - [I. Decision & Confirmation Workflow](#i-decision--confirmation-workflow)
3. [AI Suggestion Logic – Shortfall & Credit Score](#3-ai-suggestion-logic--shortfall--credit-score)
   - [Inputs Per Unit](#inputs-per-unit)
   - [Project Level Inputs](#project-level-inputs)
   - [Step 1 – Calculate Shortfall & Total Funds](#step-1--calculate-shortfall--total-funds)
   - [Step 2 – Check Mutual Release Condition](#step-2--check-mutual-release-condition)
   - [Step 3 – AI Suggestion Logic](#step-3--ai-suggestion-logic)
   - [Step 4 – Allocate Discounts / VTB with Constraints](#step-4--allocate-discounts--vtb-with-constraints)
   - [Step 5 – Total Fund Needed to Close (Project Level)](#step-5--total-fund-needed-to-close-project-level)
   - [Step 6 – Output / Color Coding](#step-6--output--color-coding)
4. [Design Guarantees](#4-design-guarantees)

---

## 1. Roles & Permissions

| Role | Access | Actions | Notes |
|---|---|---|---|
| **Builder** | Full access | Upload documents, change closing date, enter project investment data & profits, approve VTB, extensions, discounts, credit adjustments, defaults | Can see all financial reports and full system history |
| **Builder's Lawyer** | Limited access | Upload SOA & APS, confirm SOA calculations, view purchase agreement | Only sees APS and SOA section; cannot see builder profits |
| **Purchaser** | Limited access | Upload mortgage docs, personal funds, comments, request extensions, request reschedule of closing date | Can see own required closing amounts, shortfalls, estimated closing date |
| **Marketing Agency** _(optional)_ | Conditional access | View design, suggest discounts or credit adjustments, confirm suggestions | Access granted only if builder allows; cannot see SOA/APS calculations or builder profits |
| **System AI** _(Tiered Suggestions)_ | System role | Analyze SOA, APS, purchaser comments, mortgage/funds; provide tiered suggestions for VTB, discounts, credit adjustments, extensions, defaults | Advisory only; final decision by Builder |

---

## 2. Core Processes

### A. Document Upload & Management

#### 1. Builder / Builder's Lawyer Uploads SOA

SOA upload triggers automatic calculations:

- Total Purchase Price
- HST & rebates
- Deposits + statutory interest
- Common expense adjustments
- Land taxes
- Builder fees & credits
- Final Balance Due

SOA version history is maintained:

- Who uploaded
- Timestamp
- Changes made

#### 2. Purchaser Uploads Mortgage Documents / Personal Funds / Comments

Mortgage & personal funds are linked automatically to the SOA.

**Mortgage Questions:**

- Can purchaser get a mortgage? (Yes / No)
  - If **No**: ask credit score, name of provider (TransUnion or Equifax), upload document
  - If **Yes** → Status: Approved / In Process / Conditional
    - Mortgage Amount
    - Upload Mortgage Approval Document (PDF / scanned)
    - Blanket Mortgage? (Yes / No)
      - If **No** → Appraisal Value Amount required
    - Estimated Mortgage Funding / Closing Date
- Personal Funds / Savings Amount Available / Gathered
- Comments / Notes

**Purchaser can also request:**

- Closing extension
- Reschedule closing date

**System calculates total funds & shortfall:**

```
TotalFunds = MortgageAmount + PersonalFunds
Shortfall  = BalanceDueOnClosing - TotalFunds
```

**Audit / History records:**

- Purchaser name
- Timestamp
- Action type (upload, update, comment)

#### 3. Marketing Agency Uploads (Optional)

- Can view design and pricing sections
- Suggest discounts or adjustments
- Must confirm suggestions
- All actions logged

---

### B. Dynamic SOA Calculation

System recalculates SOA automatically whenever:

- Closing date changes
- APS changes
- Builder / lawyer updates SOA inputs (e.g., deposits, upgrades, fees)

> Only lawyer or builder's SOA upload is used for calculations.

AI can suggest:

- VTB eligibility
- Extension approvals
- Discount or credit adjustments
- Default risks

Builder makes the final decision; system logs who made the decision and the timestamp.

---

### C. Closing Date / Extension / Reschedule

- Purchaser can submit a reschedule request
- Builder can approve / reject
- All calculations (deposit interest, statutory interest, common expenses, land taxes, HST rebates) update automatically to reflect new closing date

**History keeps:**

- Original date
- Requests
- Approvals / rejections
- Calculated changes

---

### D. APS & Legal Confirmation

Builder's lawyer:

- Sees APS section
- Can upload SOA (prior to system calculation)
- Uploads amount of balance due
- Confirms SOA calculation; can see differences flagged between system calculation and uploaded document
- Cannot see builder's profits

APS & SOA are **locked once confirmed**. Any subsequent changes create a new version with a full audit trail.

---

### E. Financial & Project Investment Management

**Builder can enter:**

- Project investment data
- Expected profits

> Only the Builder can see these reports.

**Purchaser sees only:**

- Amount needed for closing
- Shortfalls

> Marketing Agency cannot see any financial data — only the discounts / credits sections if permitted.

---

### F. Audit & Version History

All uploads, changes, approvals, and decisions include:

| Field | Description |
|---|---|
| User name | Who performed the action |
| Role | Their system role |
| Timestamp | When the action occurred |
| Action type | upload, approve, confirm, change |
| Previous version reference | Link to prior state |
| Optional comments | Free-text notes |

System maintains a full change log for compliance.

---

### G. AI Tiered Suggestion System

**AI analyzes:**

- Uploaded SOA and APS
- Purchaser comments / requests
- Builder financial data (private)

**Suggests tiers of action:**

| Tier | Suggestion |
|---|---|
| VTB Eligibility | Approve / Decline |
| Extension | Tiered days (1 week, 2 weeks, 1 month) |
| Discount / Credit Adjustment | Based on rules & thresholds |
| Default / Risk Assessment | Flag high-risk units |

Builder can **accept / modify / reject** AI suggestions. Decision is logged with user info.

---

### H. Summary of Access Control & Visibility

| Data / Section | Builder | Lawyer | Purchaser | Marketing Agency |
|---|:---:|:---:|:---:|:---:|
| SOA Upload / Calculation | ✅ | ✅ (confirm only) | ❌ | ❌ |
| APS | ✅ | ✅ | ❌ | ❌ |
| Project Investment / Profit | ✅ | ❌ | ❌ | ❌ |
| Closing Amount / Shortfall | ✅ | ❌ | ✅ | ❌ |
| Mortgage Docs / Comments | ❌ | ❌ | ✅ | ❌ |
| Reschedule / Extension Requests | ✅ | ❌ | ✅ | ❌ |
| Discounts / Credits | ✅ | ✅ (confirm) | ❌ | Conditional |
| Audit Trail / Version History | ✅ | ✅ | ✅ (own actions only) | ✅ (own actions only) |

---

### I. Decision & Confirmation Workflow

1. Purchaser submits requests → AI suggestions generated → Builder reviews
2. Builder approves / modifies / rejects → System logs decision maker
3. Lawyer confirms SOA / APS → System updates calculation
4. _(Optional)_ Marketing Agency confirms discount suggestions → System logs
5. Closing date or extensions updated dynamically → recalculations applied automatically

---

## 3. AI Suggestion Logic – Shortfall & Credit Score

### Inputs Per Unit

| Input | Description |
|---|---|
| `APS_Unit` | Purchase price of the unit |
| `DepositsPaid` | Amount purchaser already paid |
| `PurchaserFunds` | Cash purchaser can bring at closing (excluding mortgage) |
| `MortgageApproved` | Yes / No |
| `MortgageAmount` | Mortgage amount approved (if applicable) |
| `CreditScore` | Purchaser credit score |
| `AppraisedValue` | Appraised value if mortgage not approved |
| `EstimatedClosingDate` | Requested closing date |

### Project Level Inputs

| Input | Description |
|---|---|
| `ProfitAvailable` | Builder's total available profit (Revenue – Investment – Marketing Cost) |
| `MaxBuilderCapital` | Maximum capital builder can provide for VTB or adjustments |

---

### Step 1 – Calculate Shortfall & Total Funds

```python
# Total funds purchaser can bring including deposits and mortgage
if MortgageApproved:
    TotalFunds = DepositsPaid + PurchaserFunds + MortgageAmount
else:
    TotalFunds = DepositsPaid + PurchaserFunds

# Shortfall in dollars and %
Shortfall    = APS_Unit - TotalFunds
ShortfallPct = (Shortfall / APS_Unit) * 100
```

---

### Step 2 – Check Mutual Release Condition

```python
MutualReleaseThreshold = APS_Unit - ((APS_Unit - AppraisedValue) / 3)

if (DepositsPaid + PurchaserFunds) >= MutualReleaseThreshold:
    Suggestion = "Mutual Release"
    Color      = "Purple"
```

> **Note:** If mutual release applies, no discount or VTB is suggested.

#### Mutual Release Threshold Formula — Explanation

| Variable | Meaning |
|---|---|
| `APS_Unit` | The purchase price of the unit |
| `AppraisedValue` | What the lender / appraiser says the unit is worth |

**Step-by-step logic:**

1. Calculate the difference between purchase price and appraised value: `APS_Unit - AppraisedValue`
2. Take one-third of that difference: `(APS_Unit - AppraisedValue) / 3`
3. Subtract that from the original `APS_Unit`: `APS_Unit - ((APS_Unit - AppraisedValue) / 3)`

> The idea: if the purchaser has already paid deposits + funds ≥ this threshold, they can do a mutual release because the loss is manageable for both parties.

---

### Step 3 – AI Suggestion Logic

| Condition | Action | Color | Notes / Calculation |
|---|---|---|---|
| `Shortfall ≤ 0` | Proceed to Close | Dark Green | Purchaser has enough funds; no action required |
| `ShortfallPct ≤ 10%` | Suggest Discount / Credit Adjustment | Light Green | Discount ≤ `min(Shortfall, ProfitPerUnit)`; `ProfitPerUnit = ProfitAvailable / UnitsUnsold` |
| `10% < ShortfallPct ≤ 20%` | Suggest VTB as Second Mortgage | Light Yellow | VTB ≤ Shortfall; Total VTB allocated ≤ `MaxBuilderCapital` |
| `20% < ShortfallPct ≤ 50%` & `CreditScore ≥ 700` | Suggest VTB as First Mortgage | Orange | VTB ≤ `min(75% of APS_Unit, MaxBuilderCapital)` |
| `ShortfallPct > 50%` & `CreditScore < 600` | Suggest Default | Red | Purchaser cannot fund or get mortgage; high risk |
| `(DepositsPaid + PurchaserFunds) ≥ MutualReleaseThreshold` | Suggest Mutual Release | Purple | Purchaser can release contract without additional loss |
| Other cases | AI Suggests Combination (Discount + VTB + Minor Extension) | Yellow | AI splits shortfall into discount + second mortgage + optional extension; capped by `ProfitAvailable` & `MaxBuilderCapital` |

---

### Step 4 – Allocate Discounts / VTB with Constraints

**Discount Allocation per Unit:**

```python
DiscountAllocated = min(Shortfall, ProfitPerUnit)
```

**VTB Allocation per Unit:**

```python
VTBAllocated = min(Shortfall - DiscountAllocated, MaxBuilderCapital / UnitsUnsold)
```

**Check Total Allocation vs Profit / Capital:**

```python
if sum(DiscountAllocated for all units) > ProfitAvailable:
    # Scale discounts proportionally

if sum(VTBAllocated for all units) > MaxBuilderCapital:
    # Scale VTB proportionally
```

---

### Step 5 – Total Fund Needed to Close (Project Level)

```python
TotalFundNeededToClose = sum(
    max(0, Shortfall - DiscountAllocated - VTBAllocated)
    for all units
)
```

- Shows how much cash builder & purchasers still need to close all unsold units
- Updates dynamically if purchaser changes funds, deposits, or mortgage approval

---

### Step 6 – Output / Color Coding

| Status | Color |
|---|---|
| Proceed to Close | Dark Green |
| Discount / Credit Adjustment | Light Green |
| VTB as Second Mortgage | Light Yellow |
| VTB as First Mortgage | Orange |
| Default | Red |
| Mutual Release | Purple |
| Partial Combination (AI suggestion) | Yellow |

---

## 4. Design Guarantees

- Dynamic SOA & closing calculations
- Role-based access & strict permissions
- Full audit trail
- AI suggestions tiered for builder decisions
- Confidential financials for builder only
- Purchaser sees only what they need for closing
- Optional marketing input without access to sensitive financials
