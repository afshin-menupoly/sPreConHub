// ============================================================
// AI DOCUMENT ANALYSIS SERVICE
// ============================================================
// File: Services/DocumentAnalysisService.cs
//
// REQUIRED NUGET PACKAGES:
// Install-Package Anthropic.SDK (or use HttpClient for Claude API)
// Install-Package itext7 (for PDF text extraction)
// 
// ALTERNATIVE: Use Azure Document Intelligence for more robust PDF parsing
// ============================================================

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PreConHub.Models.Entities;

namespace PreConHub.Services
{
    #region Interfaces

    public interface IDocumentAnalysisService
    {
        Task<string> ExtractTextFromPdfAsync(Stream pdfStream);
        Task<ApsExtractedData> AnalyzeApsDocumentAsync(string documentText);
        Task<ApsExtractedData> ProcessApsUploadAsync(Stream pdfStream);
    }

    #endregion

    #region Extracted Data Models

    /// <summary>
    /// Data extracted from APS document by AI
    /// </summary>
    public class ApsExtractedData
    {
        // Property/Unit Information
        public string? ProjectName { get; set; }
        public string? ProjectAddress { get; set; }
        public string? UnitNumber { get; set; }
        public string? FloorNumber { get; set; }
        public string? UnitType { get; set; }
        public int? Bedrooms { get; set; }
        public int? Bathrooms { get; set; }
        public decimal? SquareFootage { get; set; }
        public decimal? PurchasePrice { get; set; }

        // Parking & Locker
        public bool HasParking { get; set; }
        public decimal? ParkingPrice { get; set; }
        public string? ParkingNumber { get; set; }
        public bool HasLocker { get; set; }
        public decimal? LockerPrice { get; set; }
        public string? LockerNumber { get; set; }

        // Dates
        public DateTime? ApsSigningDate { get; set; }
        public DateTime? ExpectedOccupancyDate { get; set; }
        public DateTime? FirmClosingDate { get; set; }
        public DateTime? OutsideClosingDate { get; set; }

        // Purchaser Information
        public List<ExtractedPurchaser> Purchasers { get; set; } = new();

        // Deposit Schedule
        public List<ExtractedDeposit> Deposits { get; set; } = new();

        // Builder Information
        public string? BuilderName { get; set; }
        public string? BuilderAddress { get; set; }
        public string? BuilderLawyerName { get; set; }
        public string? BuilderLawyerFirm { get; set; }

        // Additional Costs/Fees mentioned
        public decimal? DevelopmentCharges { get; set; }
        public decimal? LevyFees { get; set; }
        public decimal? EducationLevy { get; set; }
        public bool HasCapOnLevies { get; set; }
        public decimal? LevyCap { get; set; }

        // Upgrades/Credits
        public List<ExtractedUpgrade> Upgrades { get; set; } = new();
        public decimal? TotalUpgrades { get; set; }
        public decimal? BuilderCredits { get; set; }

        // Special Conditions
        public List<string> SpecialConditions { get; set; } = new();

        // =====================================
        // Phase 1-3: Extended APS Fields
        // =====================================

        // Schedule B Part I — Closing Fees
        public List<ExtractedScheduleBFee> ScheduleBFees { get; set; } = new();

        // Tarion Addendum Dates
        public DateTime? FirstTentativeOccupancyDate { get; set; }
        public DateTime? OutsideOccupancyDate { get; set; }
        public DateTime? DelayedOccupancyDate { get; set; }
        public DateTime? PurchaserTerminationDate { get; set; }
        public DateTime? CommencementOfConstructionDate { get; set; }

        // Tarion / Legal
        public string? TarionRegistrationNumber { get; set; }
        public string? PropertyLegalDescription { get; set; }

        // Vendor's Solicitor
        public string? VendorSolicitorName { get; set; }
        public string? VendorSolicitorAddress { get; set; }
        public string? VendorSolicitorPhone { get; set; }
        public string? VendorSolicitorEmail { get; set; }

        // Levy Cap details
        public string? LevyCapDescription { get; set; }  // e.g. "DC + EDC combined"
        public List<string> LevyCapCoveredTypes { get; set; } = new();  // e.g. ["DevelopmentCharges","EducationDevelopmentCharges"]

        // Parking & Locker counts
        public int ParkingCount { get; set; }
        public int LockerCount { get; set; }

        // Extraction Metadata
        public decimal ConfidenceScore { get; set; }
        public List<string> Warnings { get; set; } = new();
        public string? RawExtractedText { get; set; }
    }

    public class ExtractedScheduleBFee
    {
        public string Name { get; set; } = string.Empty;
        public string? FeeType { get; set; }  // Maps to FeeType enum name
        public decimal? Amount { get; set; }
        public string? ApsReference { get; set; }  // e.g. "Section 5(a)"
    }

    public class ExtractedPurchaser
    {
        public string? FullName { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Province { get; set; }
        public string? PostalCode { get; set; }
        public bool IsPrimary { get; set; }
        public decimal? OwnershipPercentage { get; set; }
    }

    public class ExtractedDeposit
    {
        public string? Name { get; set; }
        public decimal? Amount { get; set; }
        public decimal? Percentage { get; set; }  // e.g. 5.0 = 5% of purchase price
        public DateTime? DueDate { get; set; }
        public string? DueDescription { get; set; } // e.g., "Upon signing", "30 days after signing"
        public bool IsPaid { get; set; }
    }

    public class ExtractedUpgrade
    {
        public string? Description { get; set; }
        public decimal? Amount { get; set; }
    }

    #endregion

    #region Document Analysis Service Implementation

    public class DocumentAnalysisService : IDocumentAnalysisService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DocumentAnalysisService> _logger;
        private readonly HttpClient _httpClient;

        public DocumentAnalysisService(
            IConfiguration configuration,
            ILogger<DocumentAnalysisService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("ClaudeApi");
        }

        /// <summary>
        /// Extract text from PDF using iText7
        /// </summary>
        public async Task<string> ExtractTextFromPdfAsync(Stream pdfStream)
        {
            var text = new StringBuilder();
            var skippedPages = new List<int>();

            try
            {
                using var pdfReader = new iText.Kernel.Pdf.PdfReader(pdfStream);
                using var pdfDocument = new iText.Kernel.Pdf.PdfDocument(pdfReader);

                for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
                {
                    var page = pdfDocument.GetPage(i);
                    var strategy = new iText.Kernel.Pdf.Canvas.Parser.Listener.SimpleTextExtractionStrategy();
                    var pageText = iText.Kernel.Pdf.Canvas.Parser.PdfTextExtractor.GetTextFromPage(page, strategy);

                    if (IsRelevantPage(pageText, i, pdfDocument.GetNumberOfPages()))
                    {
                        text.AppendLine(pageText);
                        text.AppendLine($"\n--- Page {i} End ---\n");
                    }
                    else
                    {
                        skippedPages.Add(i);
                    }
                }

                _logger.LogInformation(
                    "Extracted {CharCount} characters from PDF. Pages: {Total} total, {Skipped} skipped (irrelevant). Skipped: [{SkippedList}]",
                    text.Length, pdfDocument.GetNumberOfPages(), skippedPages.Count,
                    skippedPages.Count > 0 ? string.Join(", ", skippedPages) : "none");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from PDF");
                throw new Exception("Failed to extract text from PDF. Please ensure the file is a valid PDF document.", ex);
            }

            return text.ToString();
        }

        /// <summary>
        /// Determines if a PDF page contains data relevant to APS extraction.
        /// Skips pages that are purely boilerplate, marketing, floor plans, or warranty text.
        /// First 3 and last 2 pages are always included (cover, signatures, amendments).
        /// </summary>
        private static bool IsRelevantPage(string pageText, int pageNumber, int totalPages)
        {
            // Always include first 3 pages (cover page, agreement summary, unit details)
            if (pageNumber <= 3) return true;
            // Always include last 2 pages (signatures, amendments, schedules)
            if (pageNumber > totalPages - 2) return true;

            // Skip nearly-empty pages (floor plans, images rendered as blank text)
            if (pageText.Trim().Length < 50) return false;

            // Keywords that indicate a page has extractable APS data
            var relevantKeywords = new[]
            {
                // Core agreement terms
                "purchase price", "purchaser", "vendor", "buyer",
                // Schedules
                "schedule a", "schedule b", "schedule c", "schedule d",
                // Financial
                "deposit", "balance due", "closing cost", "adjustment",
                "development charge", "levy", "education", "hst",
                "cheque admin", "wire transfer", "pdi", "partial discharge",
                "engineering report", "internet delivery", "carbon monoxide",
                // Parking & Locker
                "parking", "locker", "storage",
                // Dates & Tarion
                "tarion", "occupancy", "closing date", "firm date",
                "tentative", "termination", "commencement",
                "enrolment", "registration number",
                // Legal
                "solicitor", "legal description", "lot", "plan",
                // Upgrades & Credits
                "upgrade", "credit", "incentive", "discount",
                // Assignment
                "assignment", "consent fee",
                // Document sections (addendums, amendments, schedules)
                "addendum", "amendment", "schedule",
                // Signatures & DocuSign (contain signing dates, purchaser names/emails)
                "docusign", "witness", "sealed and delivered", "agency disclosure",
                "envelope id", "signed:", "purchaser:",
            };

            var lowerText = pageText.ToLowerInvariant();
            return relevantKeywords.Any(kw => lowerText.Contains(kw));
        }

        /// <summary>
        /// Use Claude AI to analyze APS document text and extract structured data
        /// </summary>
        public async Task<ApsExtractedData> AnalyzeApsDocumentAsync(string documentText)
        {
            var apiKey = _configuration["ClaudeApi:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("Claude API key not configured, using mock extraction");
                return MockExtractData(documentText);
            }

            var systemPrompt = BuildSystemPrompt();
            var userPrompt = $"APS DOCUMENT TEXT:\n{documentText}\n\nReturn ONLY the JSON object, no other text or explanation.";
            var model = _configuration["ClaudeApi:Model"] ?? "claude-sonnet-4-6";

            try
            {
                var requestBody = new
                {
                    model = model,
                    max_tokens = 4096,
                    system = new[]
                    {
                        new { type = "text", text = systemPrompt, cache_control = new { type = "ephemeral" } }
                    },
                    messages = new[]
                    {
                        new { role = "user", content = userPrompt }
                    }
                };

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
                httpRequest.Headers.Add("x-api-key", apiKey);
                httpRequest.Headers.Add("anthropic-version", "2023-06-01");
                httpRequest.Headers.Add("anthropic-beta", "prompt-caching-2024-07-31");
                httpRequest.Content = JsonContent.Create(requestBody);

                var response = await _httpClient.SendAsync(httpRequest);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Claude API error for text-based PDF: {StatusCode} {Error}", response.StatusCode, error);
                    throw new Exception($"AI analysis failed: {response.StatusCode}");
                }

                var result = await response.Content.ReadFromJsonAsync<ClaudeResponse>();
                var jsonContent = result?.Content?.FirstOrDefault()?.Text ?? "";

                // Parse JSON response and post-process relative dates
                var extractedData = ParseAiResponse(jsonContent);
                PostProcessExtractedData(extractedData);

                return extractedData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Claude API for text-based document analysis");
                return MockExtractData(documentText);
            }
        }

        /// <summary>
        /// Full pipeline: Extract PDF text and analyze with AI
        /// </summary>
        public async Task<ApsExtractedData> ProcessApsUploadAsync(Stream pdfStream)
        {
            // Try text extraction first (cheapest path)
            string text;
            try
            {
                text = await ExtractTextFromPdfAsync(pdfStream);
            }
            catch (Exception ex)
            {
                // iText7 failed to read PDF (corrupted, encrypted, or pure image-based)
                _logger.LogWarning(ex, "iText7 text extraction failed. Falling back to PDF document API.");
                pdfStream.Position = 0;
                return await AnalyzeScannedApsAsync(pdfStream);
            }

            // Check if PDF is scanned (minimal extractable text)
            var meaningfulText = text.Replace("\n", "").Replace("-", "").Replace(" ", "").Trim();
            if (meaningfulText.Length > 500)
            {
                // Text-based PDF — use text extraction (cheaper)
                return await AnalyzeApsDocumentAsync(text);
            }

            // Scanned PDF — send PDF directly to Claude's document API
            _logger.LogInformation(
                "Scanned PDF detected (only {Chars} extractable chars). Using PDF document API.",
                meaningfulText.Length);
            pdfStream.Position = 0;
            return await AnalyzeScannedApsAsync(pdfStream);
        }

        /// <summary>
        /// Analyze a scanned PDF by sending it directly to Claude's document API.
        /// Uses iText7 to create a subset PDF with only relevant pages to reduce token cost.
        /// </summary>
        private async Task<ApsExtractedData> AnalyzeScannedApsAsync(Stream pdfStream)
        {
            var apiKey = _configuration["ClaudeApi:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("Claude API key not configured — cannot process scanned PDFs");
                return new ApsExtractedData
                {
                    ConfidenceScore = 0,
                    Warnings = new List<string> { "Claude API not configured — scanned PDFs require AI vision processing" }
                };
            }

            // Create a subset PDF with relevant pages to reduce token cost
            byte[] pdfBytes;
            try
            {
                pdfBytes = CreateRelevantPageSubset(pdfStream);
            }
            catch (Exception ex)
            {
                // iText7 can't manipulate this PDF — send raw bytes instead
                _logger.LogWarning(ex, "Could not create page subset. Sending full PDF.");
                pdfStream.Position = 0;
                using var ms = new MemoryStream();
                await pdfStream.CopyToAsync(ms);
                pdfBytes = ms.ToArray();
            }
            var base64Pdf = Convert.ToBase64String(pdfBytes);
            var systemPrompt = BuildSystemPrompt();
            var model = _configuration["ClaudeApi:Model"] ?? "claude-sonnet-4-6";

            _logger.LogInformation("Sending scanned PDF to Claude document API. PDF size: {Size} KB",
                pdfBytes.Length / 1024);

            try
            {
                // Build request with document content block (Claude's native PDF support)
                var request = new Dictionary<string, object>
                {
                    ["model"] = model,
                    ["max_tokens"] = 4096,
                    ["system"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["type"] = "text",
                            ["text"] = systemPrompt,
                            ["cache_control"] = new Dictionary<string, string> { ["type"] = "ephemeral" }
                        }
                    },
                    ["messages"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["role"] = "user",
                            ["content"] = new object[]
                            {
                                new Dictionary<string, object>
                                {
                                    ["type"] = "document",
                                    ["source"] = new Dictionary<string, string>
                                    {
                                        ["type"] = "base64",
                                        ["media_type"] = "application/pdf",
                                        ["data"] = base64Pdf
                                    }
                                },
                                new Dictionary<string, object>
                                {
                                    ["type"] = "text",
                                    ["text"] = "Extract all data from this APS document. Return ONLY the JSON object, no other text or explanation."
                                }
                            }
                        }
                    }
                };

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
                httpRequest.Headers.Add("x-api-key", apiKey);
                httpRequest.Headers.Add("anthropic-version", "2023-06-01");
                httpRequest.Content = JsonContent.Create(request);

                var response = await _httpClient.SendAsync(httpRequest);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Claude API error (scanned PDF): {StatusCode} {Error}", response.StatusCode, error);
                    throw new Exception($"AI analysis failed: {response.StatusCode}");
                }

                var result = await response.Content.ReadFromJsonAsync<ClaudeResponse>();
                var jsonContent = result?.Content?.FirstOrDefault()?.Text ?? "";

                var extractedData = ParseAiResponse(jsonContent);
                PostProcessExtractedData(extractedData);

                return extractedData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Claude API for scanned PDF analysis");
                return new ApsExtractedData
                {
                    ConfidenceScore = 0,
                    Warnings = new List<string> { $"Failed to analyze scanned PDF: {ex.Message}" }
                };
            }
        }

        /// <summary>
        /// Creates a subset PDF containing only likely-relevant pages to reduce token cost.
        /// For PDFs with 30 or fewer pages, returns the full document.
        /// For larger PDFs, skips the middle section (typically boilerplate legal clauses).
        /// APS structure: first ~35% = agreement terms, middle = boilerplate, last ~45% = schedules + Tarion.
        /// </summary>
        private byte[] CreateRelevantPageSubset(Stream pdfStream)
        {
            using var reader = new iText.Kernel.Pdf.PdfReader(pdfStream);
            using var srcDoc = new iText.Kernel.Pdf.PdfDocument(reader);
            var totalPages = srcDoc.GetNumberOfPages();

            // For small docs, keep all pages
            if (totalPages <= 30)
            {
                _logger.LogInformation("PDF has {Pages} pages — sending all pages", totalPages);
                var outputAll = new MemoryStream();
                using var writerAll = new iText.Kernel.Pdf.PdfWriter(outputAll);
                using var destAll = new iText.Kernel.Pdf.PdfDocument(writerAll);
                srcDoc.CopyPagesTo(Enumerable.Range(1, totalPages).ToList(), destAll);
                destAll.Close();
                return outputAll.ToArray();
            }

            // For larger docs, keep first 30% + last 45%, skip middle boilerplate legal clauses
            // APS structure: first pages = agreement terms/deposits, middle = standard clauses, last = schedules + Tarion
            var keepPages = new List<int>();
            var firstSectionEnd = (int)Math.Ceiling(totalPages * 0.30);
            var lastSectionStart = (int)Math.Floor(totalPages * 0.55) + 1;

            for (int i = 1; i <= firstSectionEnd; i++)
                keepPages.Add(i);
            for (int i = lastSectionStart; i <= totalPages; i++)
                keepPages.Add(i);

            _logger.LogInformation(
                "Created subset PDF: {Kept}/{Total} pages (skipped pages {SkipStart}-{SkipEnd})",
                keepPages.Count, totalPages, firstSectionEnd + 1, lastSectionStart - 1);

            var outputStream = new MemoryStream();
            using var writer = new iText.Kernel.Pdf.PdfWriter(outputStream);
            using var destDoc = new iText.Kernel.Pdf.PdfDocument(writer);
            srcDoc.CopyPagesTo(keepPages, destDoc);
            destDoc.Close();
            return outputStream.ToArray();
        }

        private static string BuildSystemPrompt()
        {
            return @"You are an expert at analyzing Ontario pre-construction real estate Agreement of Purchase and Sale (APS) documents.

Extract the following information from the provided APS document and return it as valid JSON. If a field is not found, use null. For boolean fields, use true/false.

Required JSON structure:
{
    ""projectName"": ""string or null"",
    ""projectAddress"": ""string or null"",
    ""unitNumber"": ""string or null"",
    ""floorNumber"": ""string or null"",
    ""unitType"": ""string (Studio/OneBedroom/OnePlusDen/TwoBedroom/TwoPlusDen/ThreeBedroom/Penthouse/Townhouse/Other)"",
    ""bedrooms"": number or null,
    ""bathrooms"": number or null,
    ""squareFootage"": number or null,
    ""purchasePrice"": number or null,
    ""hasParking"": boolean,
    ""parkingPrice"": number or null,
    ""parkingNumber"": ""string or null"",
    ""parkingCount"": number (default 0),
    ""hasLocker"": boolean,
    ""lockerPrice"": number or null,
    ""lockerNumber"": ""string or null"",
    ""lockerCount"": number (default 0),
    ""apsSigningDate"": ""YYYY-MM-DD or null"",
    ""expectedOccupancyDate"": ""YYYY-MM-DD or null"",
    ""firmClosingDate"": ""YYYY-MM-DD or null"",
    ""outsideClosingDate"": ""YYYY-MM-DD or null"",
    ""firstTentativeOccupancyDate"": ""YYYY-MM-DD or null — from Tarion Addendum"",
    ""outsideOccupancyDate"": ""YYYY-MM-DD or null — latest date before purchaser can terminate"",
    ""delayedOccupancyDate"": ""YYYY-MM-DD or null"",
    ""purchaserTerminationDate"": ""YYYY-MM-DD or null — end of termination period"",
    ""commencementOfConstructionDate"": ""YYYY-MM-DD or null — from Tarion Addendum"",
    ""tarionRegistrationNumber"": ""string or null — Tarion enrolment number"",
    ""propertyLegalDescription"": ""string or null — e.g. Lot 35 on Plan 806"",
    ""vendorSolicitorName"": ""string or null — vendor's law firm name"",
    ""vendorSolicitorAddress"": ""string or null"",
    ""vendorSolicitorPhone"": ""string or null"",
    ""vendorSolicitorEmail"": ""string or null"",
    ""purchasers"": [
        {
            ""fullName"": ""string"",
            ""firstName"": ""string or null"",
            ""lastName"": ""string or null"",
            ""email"": ""string or null"",
            ""phone"": ""string or null"",
            ""address"": ""string or null"",
            ""city"": ""string or null"",
            ""province"": ""string or null"",
            ""postalCode"": ""string or null"",
            ""isPrimary"": boolean,
            ""ownershipPercentage"": number or null
        }
    ],
    ""deposits"": [
        {
            ""name"": ""string (e.g., Deposit 1, Initial Deposit)"",
            ""amount"": number,
            ""percentage"": number or null (e.g. 5.0 means 5% of purchase price),
            ""dueDate"": ""YYYY-MM-DD or null"",
            ""dueDescription"": ""string describing when due""
        }
    ],
    ""builderName"": ""string or null"",
    ""builderAddress"": ""string or null"",
    ""builderLawyerName"": ""string or null"",
    ""builderLawyerFirm"": ""string or null"",
    ""developmentCharges"": number or null,
    ""educationLevy"": number or null,
    ""levyFees"": number or null,
    ""hasCapOnLevies"": boolean,
    ""levyCap"": number or null (if tiered by unit type, use the cap for this specific unit),
    ""levyCapDescription"": ""string or null — e.g. DC + EDC combined cap, $12K for 1BR / $16K for 2BR+"",
    ""levyCapCoveredTypes"": [""DevelopmentCharges"", ""EducationDevelopmentCharges""],
    ""scheduleBFees"": [
        {
            ""name"": ""string — fee name from Schedule B Part I"",
            ""feeType"": ""string — one of: ChequeAdministrationFee, PartialDischargeFee, PDIFee, EngineeringReportFee, InternetDeliveryFee, CarbonMonoxideDetectorFee, WireTransferFee, PDFScanFee, ClosingDocChangesFee, MissedAppointmentFee, NSFFee, VendorLienFee, EFTSFee, UtilityDepositFee, or Other"",
            ""amount"": number (before HST),
            ""apsReference"": ""string — APS section reference e.g. Section 5(a)""
        }
    ],
    ""upgrades"": [
        {
            ""description"": ""string"",
            ""amount"": number
        }
    ],
    ""totalUpgrades"": number or null,
    ""builderCredits"": number or null,
    ""specialConditions"": [""array of special conditions or notes""],
    ""confidenceScore"": number between 0 and 1,
    ""warnings"": [""array of any data quality warnings or uncertainties""]
}

Important notes:
- Extract ALL purchaser/buyer names found
- Extract the COMPLETE deposit schedule with amounts, percentages, and dates
- Look for Schedule B Part I fees: cheque admin ($300), partial discharge ($350), PDI ($250), engineering report ($350), internet delivery ($150), CO detector ($150), wire transfer ($150-$250), PDF/scan ($150), closing doc changes ($500), missed appointment/re-booking ($750), NSF admin ($450), vendor's lien fees, EFTS fees, utility security deposit
- Look for levy caps — may be tiered by unit type (e.g. $12K for 1BR, $16K for 2BR+). If tiered, include description with all tiers and use the cap matching the unit being purchased. Often DC and EDC share a combined cap
- Extract Tarion Addendum dates: first tentative occupancy, outside occupancy, termination period
- Extract vendor's solicitor information
- Extract Tarion registration number and property legal description
- ALL amount/price fields MUST be numeric (no $, commas, or text like ""balance"" or ""TBD""). Use 0 if the amount is unknown, variable, or described as ""balance due on closing""
- Dates should be in YYYY-MM-DD format
- Include confidence score (0-1) based on how clearly the data was found
- Add warnings for any fields that were unclear or possibly incorrect
- Some pages may have been filtered out to save processing — extract from what is provided
- Always extract apsSigningDate — check signature pages, DocuSign timestamps (format: YYYY-MM-DD | HH:MM:SS), or ""DATED at"" clauses
- For DocuSign documents: extract purchaser email from signer events, signing date from Signed timestamp
- For deposits with relative due dates (e.g. ""30 days after signing""), set dueDescription to the exact text but leave dueDate null — dates will be calculated automatically
- Keep specialConditions brief — max 5 items, each under 100 characters
- Keep warnings brief — max 5 items, each under 100 characters
- Do NOT include lengthy legal text in any field — summarize instead";
        }

        private ApsExtractedData ParseAiResponse(string jsonResponse)
        {
            try
            {
                // Clean up response - remove markdown code blocks if present
                jsonResponse = jsonResponse.Trim();
                if (jsonResponse.StartsWith("```json"))
                    jsonResponse = jsonResponse.Substring(7);
                if (jsonResponse.StartsWith("```"))
                    jsonResponse = jsonResponse.Substring(3);
                if (jsonResponse.EndsWith("```"))
                    jsonResponse = jsonResponse.Substring(0, jsonResponse.Length - 3);
                jsonResponse = jsonResponse.Trim();

                // Sanitize non-numeric "amount" values (e.g. "balance", "TBD", "$5,000")
                // Replace "amount": "non-numeric-string" with "amount": 0
                jsonResponse = System.Text.RegularExpressions.Regex.Replace(
                    jsonResponse,
                    @"""amount""\s*:\s*""[^""]*""",
                    match =>
                    {
                        // Try parsing the string value — if numeric, keep it; otherwise replace with 0
                        var valMatch = System.Text.RegularExpressions.Regex.Match(match.Value, @"""([^""]*)""$");
                        if (valMatch.Success)
                        {
                            var cleaned = valMatch.Groups[1].Value.Replace("$", "").Replace(",", "").Trim();
                            if (decimal.TryParse(cleaned, out _))
                                return $@"""amount"": ""{cleaned}""";
                        }
                        return @"""amount"": 0";
                    },
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                };

                var data = JsonSerializer.Deserialize<ApsExtractedData>(jsonResponse, options);
                return data ?? new ApsExtractedData { Warnings = new List<string> { "Failed to parse AI response" } };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing AI response JSON");
                return new ApsExtractedData
                {
                    Warnings = new List<string> { $"Error parsing AI response: {ex.Message}" },
                    ConfidenceScore = 0
                };
            }
        }

        /// <summary>
        /// Post-process AI-extracted data: calculate relative dates, fill gaps.
        /// Runs in C# to avoid wasting AI tokens on date arithmetic.
        /// </summary>
        private void PostProcessExtractedData(ApsExtractedData data)
        {
            var signingDate = data.ApsSigningDate;

            // Calculate deposit due dates from relative descriptions
            if (data.Deposits != null)
            {
                foreach (var deposit in data.Deposits)
                {
                    if (deposit.DueDate.HasValue) continue; // already has a date
                    if (string.IsNullOrEmpty(deposit.DueDescription)) continue;

                    var desc = deposit.DueDescription.ToLowerInvariant();

                    // === Signing-date-relative patterns ===
                    if (signingDate.HasValue)
                    {
                        // "upon signing", "on signing", "with this agreement", "upon execution", "submitted with"
                        if (desc.Contains("upon signing") || desc.Contains("on signing") ||
                            desc.Contains("with this agreement") || desc.Contains("upon execution") ||
                            desc.Contains("submitted with") || desc.Contains("at time of signing") ||
                            desc.Contains("together with") || desc.Contains("on execution"))
                        {
                            deposit.DueDate = signingDate.Value;
                            continue;
                        }

                        // Numeric days: "30 days after", "post-dated 120 days", "120 days following execution"
                        var daysMatch = System.Text.RegularExpressions.Regex.Match(desc,
                            @"(\d+)\s*days?\s*(?:after|from|following|of|post[- ]?dated|later)",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (!daysMatch.Success)
                        {
                            // Reverse pattern: "post-dated 120 days"
                            daysMatch = System.Text.RegularExpressions.Regex.Match(desc,
                                @"(?:post[- ]?dated|after|following|within)\s*(\d+)\s*days?",
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        }
                        if (daysMatch.Success && int.TryParse(daysMatch.Groups[1].Value, out var days))
                        {
                            deposit.DueDate = signingDate.Value.AddDays(days);
                            continue;
                        }

                        // Written-out numbers: "thirty (30) days", "one hundred twenty (120) days"
                        var writtenDaysMatch = System.Text.RegularExpressions.Regex.Match(desc,
                            @"[a-z\s]+\((\d+)\)\s*days?",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (writtenDaysMatch.Success && int.TryParse(writtenDaysMatch.Groups[1].Value, out var writtenDays))
                        {
                            deposit.DueDate = signingDate.Value.AddDays(writtenDays);
                            continue;
                        }

                        // Months: "6 months after signing", "twelve (12) months"
                        var monthsMatch = System.Text.RegularExpressions.Regex.Match(desc,
                            @"(\d+)\s*months?\s*(?:after|from|following|later)",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (!monthsMatch.Success)
                        {
                            monthsMatch = System.Text.RegularExpressions.Regex.Match(desc,
                                @"[a-z\s]+\((\d+)\)\s*months?",
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        }
                        if (monthsMatch.Success && int.TryParse(monthsMatch.Groups[1].Value, out var months))
                        {
                            deposit.DueDate = signingDate.Value.AddMonths(months);
                            continue;
                        }
                    }

                    // === Occupancy-date-relative patterns ===
                    if (data.ExpectedOccupancyDate.HasValue &&
                        (desc.Contains("on occupancy") || desc.Contains("upon occupancy") ||
                         desc.Contains("at occupancy") || desc.Contains("occupancy date") ||
                         desc.Contains("on the occupancy")))
                    {
                        deposit.DueDate = data.ExpectedOccupancyDate.Value;
                        continue;
                    }

                    // === Closing-date-relative patterns ===
                    if (data.FirmClosingDate.HasValue &&
                        (desc.Contains("on closing") || desc.Contains("upon closing") ||
                         desc.Contains("at closing") || desc.Contains("closing date") ||
                         desc.Contains("balance due on closing") || desc.Contains("on the closing")))
                    {
                        deposit.DueDate = data.FirmClosingDate.Value;
                        continue;
                    }
                }
            }

            _logger.LogInformation(
                "Post-processing complete. Signing date: {SigningDate}, Deposits with calculated dates: {CalculatedCount}/{TotalCount}",
                signingDate?.ToString("yyyy-MM-dd") ?? "unknown",
                data.Deposits?.Count(d => d.DueDate.HasValue) ?? 0,
                data.Deposits?.Count ?? 0);
        }

        /// <summary>
        /// Mock extraction for demo/testing when API is not configured
        /// </summary>
        private ApsExtractedData MockExtractData(string documentText)
        {
            _logger.LogInformation("Using mock data extraction (API not configured)");

            // Try to extract some basic info using regex patterns
            var data = new ApsExtractedData
            {
                ConfidenceScore = 0.5m,
                Warnings = new List<string> { "Using basic pattern matching (Claude API not configured)" }
            };

            // Try to find unit number
            var unitMatch = System.Text.RegularExpressions.Regex.Match(
                documentText, @"(?:Unit|Suite|Apt)[:\s#]*(\d+[A-Za-z]?)", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (unitMatch.Success)
                data.UnitNumber = unitMatch.Groups[1].Value;

            // Try to find price
            var priceMatch = System.Text.RegularExpressions.Regex.Match(
                documentText, @"(?:Purchase\s*Price|Price)[:\s]*\$?([\d,]+(?:\.\d{2})?)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (priceMatch.Success)
            {
                var priceStr = priceMatch.Groups[1].Value.Replace(",", "");
                if (decimal.TryParse(priceStr, out var price))
                    data.PurchasePrice = price;
            }

            // Try to find square footage
            var sqftMatch = System.Text.RegularExpressions.Regex.Match(
                documentText, @"([\d,]+)\s*(?:sq\.?\s*ft|square\s*feet)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (sqftMatch.Success)
            {
                var sqftStr = sqftMatch.Groups[1].Value.Replace(",", "");
                if (decimal.TryParse(sqftStr, out var sqft))
                    data.SquareFootage = sqft;
            }

            // Extract sample deposits
            data.Deposits = new List<ExtractedDeposit>
            {
                new ExtractedDeposit { Name = "Initial Deposit", Amount = 0, DueDescription = "Upon signing" },
                new ExtractedDeposit { Name = "Second Deposit", Amount = 0, DueDescription = "30 days after signing" },
                new ExtractedDeposit { Name = "Third Deposit", Amount = 0, DueDescription = "90 days after signing" }
            };

            data.Purchasers = new List<ExtractedPurchaser>
            {
                new ExtractedPurchaser { IsPrimary = true, OwnershipPercentage = 100 }
            };

            return data;
        }
    }

    #endregion

    #region Claude API Response Models

    public class ClaudeResponse
    {
        [JsonPropertyName("content")]
        public List<ClaudeContent>? Content { get; set; }
    }

    public class ClaudeContent
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    #endregion
}
