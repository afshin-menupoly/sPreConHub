namespace PreConHub.Models.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using Microsoft.AspNetCore.Http;
    using PreConHub.Models.Entities;

    /// <summary>
    /// ViewModel for displaying a single document
    /// </summary>
    public class DocumentViewModel
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string DocumentTypeName { get; set; } = string.Empty;
        public DocumentType DocumentType { get; set; }
        public string? Description { get; set; }
        public long FileSize { get; set; }
        public string FileSizeFormatted => FormatFileSize(FileSize);
        public DateTime UploadedAt { get; set; }
        public string UploadedAtFormatted => UploadedAt.ToString("MMM dd, yyyy h:mm tt");
        public bool CanDelete { get; set; } = true;

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }
    }

    /// <summary>
    /// ViewModel for the Manage Documents page
    /// </summary>
    public class ManageDocumentsViewModel
    {
        public int UnitId { get; set; }
        public string UnitNumber { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;

        // Required document types
        public List<RequiredDocumentInfo> RequiredDocuments { get; set; } = new List<RequiredDocumentInfo>();

        // All uploaded documents
        public List<DocumentViewModel> UploadedDocuments { get; set; } = new List<DocumentViewModel>();

        // Statistics
        public int TotalRequired { get; set; }
        public int TotalUploaded { get; set; }
        public int CompletionPercentage => TotalRequired > 0
            ? (int)Math.Min(100, (TotalUploaded * 100.0 / TotalRequired))
            : 0;
        public bool IsComplete => TotalUploaded >= TotalRequired;
    }

    /// <summary>
    /// Info about a required document type
    /// </summary>
    public class RequiredDocumentInfo
    {
        public DocumentType DocumentType { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsRequired { get; set; } = true;
        public bool IsUploaded { get; set; }
        public DocumentViewModel? UploadedDocument { get; set; }
    }

    /// <summary>
    /// ViewModel for uploading a document
    /// </summary>
    public class UploadDocumentViewModel
    {
        public int UnitId { get; set; }

        [Required(ErrorMessage = "Please select a document type")]
        [Display(Name = "Document Type")]
        public DocumentType DocumentType { get; set; }

        [Required(ErrorMessage = "Please select a file to upload")]
        [Display(Name = "File")]
        public IFormFile File { get; set; } = null!;

        [StringLength(500)]
        [Display(Name = "Description (Optional)")]
        public string? Description { get; set; }
    }

    /// <summary>
    /// Response model for AJAX upload
    /// </summary>
    public class DocumentUploadResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public DocumentViewModel? Document { get; set; }
    }
}
