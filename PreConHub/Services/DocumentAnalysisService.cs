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

        // Extraction Metadata
        public decimal ConfidenceScore { get; set; }
        public List<string> Warnings { get; set; } = new();
        public string? RawExtractedText { get; set; }
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
        public decimal Amount { get; set; }
        public DateTime? DueDate { get; set; }
        public string? DueDescription { get; set; } // e.g., "Upon signing", "30 days after signing"
        public bool IsPaid { get; set; }
    }

    public class ExtractedUpgrade
    {
        public string? Description { get; set; }
        public decimal Amount { get; set; }
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

            try
            {
                using var pdfReader = new iText.Kernel.Pdf.PdfReader(pdfStream);
                using var pdfDocument = new iText.Kernel.Pdf.PdfDocument(pdfReader);

                for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
                {
                    var page = pdfDocument.GetPage(i);
                    var strategy = new iText.Kernel.Pdf.Canvas.Parser.Listener.SimpleTextExtractionStrategy();
                    var pageText = iText.Kernel.Pdf.Canvas.Parser.PdfTextExtractor.GetTextFromPage(page, strategy);
                    text.AppendLine(pageText);
                    text.AppendLine($"\n--- Page {i} End ---\n");
                }

                _logger.LogInformation("Extracted {CharCount} characters from PDF with {PageCount} pages",
                    text.Length, pdfDocument.GetNumberOfPages());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from PDF");
                throw new Exception("Failed to extract text from PDF. Please ensure the file is a valid PDF document.", ex);
            }

            return text.ToString();
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

            var prompt = BuildExtractionPrompt(documentText);

            try
            {
                var request = new
                {
                    model = "claude-sonnet-4-20250514",
                    max_tokens = 4096,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    }
                };

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
                _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

                var response = await _httpClient.PostAsJsonAsync(
                    "https://api.anthropic.com/v1/messages",
                    request);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Claude API error: {Error}", error);
                    throw new Exception($"AI analysis failed: {response.StatusCode}");
                }

                var result = await response.Content.ReadFromJsonAsync<ClaudeResponse>();
                var jsonContent = result?.Content?.FirstOrDefault()?.Text ?? "";

                // Parse JSON response
                var extractedData = ParseAiResponse(jsonContent);
                extractedData.RawExtractedText = documentText.Substring(0, Math.Min(5000, documentText.Length));

                return extractedData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Claude API for document analysis");
                // Fall back to mock extraction for demo purposes
                return MockExtractData(documentText);
            }
        }

        /// <summary>
        /// Full pipeline: Extract PDF text and analyze with AI
        /// </summary>
        public async Task<ApsExtractedData> ProcessApsUploadAsync(Stream pdfStream)
        {
            var text = await ExtractTextFromPdfAsync(pdfStream);
            return await AnalyzeApsDocumentAsync(text);
        }

        private string BuildExtractionPrompt(string documentText)
        {
            return $@"You are an expert at analyzing Ontario pre-construction real estate Agreement of Purchase and Sale (APS) documents. 

Extract the following information from this APS document and return it as valid JSON. If a field is not found, use null. For boolean fields, use true/false.

Required JSON structure:
{{
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
    ""hasLocker"": boolean,
    ""lockerPrice"": number or null,
    ""lockerNumber"": ""string or null"",
    ""apsSigningDate"": ""YYYY-MM-DD or null"",
    ""expectedOccupancyDate"": ""YYYY-MM-DD or null"",
    ""firmClosingDate"": ""YYYY-MM-DD or null"",
    ""outsideClosingDate"": ""YYYY-MM-DD or null"",
    ""purchasers"": [
        {{
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
        }}
    ],
    ""deposits"": [
        {{
            ""name"": ""string (e.g., Deposit 1, Initial Deposit)"",
            ""amount"": number,
            ""dueDate"": ""YYYY-MM-DD or null"",
            ""dueDescription"": ""string describing when due""
        }}
    ],
    ""builderName"": ""string or null"",
    ""builderAddress"": ""string or null"",
    ""builderLawyerName"": ""string or null"",
    ""builderLawyerFirm"": ""string or null"",
    ""developmentCharges"": number or null,
    ""levyFees"": number or null,
    ""hasCapOnLevies"": boolean,
    ""levyCap"": number or null,
    ""upgrades"": [
        {{
            ""description"": ""string"",
            ""amount"": number
        }}
    ],
    ""totalUpgrades"": number or null,
    ""builderCredits"": number or null,
    ""specialConditions"": [""array of special conditions or notes""],
    ""confidenceScore"": number between 0 and 1,
    ""warnings"": [""array of any data quality warnings or uncertainties""]
}}

Important notes:
- Extract ALL purchaser/buyer names found
- Extract the COMPLETE deposit schedule with amounts and dates
- Prices should be numbers without $ or commas
- Dates should be in YYYY-MM-DD format
- Include confidence score (0-1) based on how clearly the data was found
- Add warnings for any fields that were unclear or possibly incorrect

APS DOCUMENT TEXT:
{documentText}

Return ONLY the JSON object, no other text or explanation.";
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
