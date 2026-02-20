// ============================================================
// EMAIL SERVICE FOR PRECONHUB
// ============================================================
// File: Services/EmailService.cs
// ============================================================

using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace PreConHub.Services
{
    // ===== EMAIL SETTINGS CLASS =====
    public class EmailSettings
    {
        public string SmtpServer { get; set; } = "";
        public int SmtpPort { get; set; } = 587;
        public string SmtpUsername { get; set; } = "";
        public string SmtpPassword { get; set; } = "";
        public string FromEmail { get; set; } = "";
        public string FromName { get; set; } = "PreConHub";
        public bool EnableSsl { get; set; } = true;
        public bool IsEnabled { get; set; } = true; // Set to false to disable emails
    }

    // ===== EMAIL SERVICE INTERFACE =====
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody);
        Task<bool> SendPurchaserInvitationAsync(string toEmail, string purchaserName, string unitNumber, string projectName, string invitationLink);
        Task<bool> SendLawyerInvitationAsync(string toEmail, string lawyerName, int unitCount, List<string> projectNames, string invitationLink);
        Task<bool> SendLawyerApprovalNotificationAsync(string builderEmail, string builderName, string lawyerName, string unitNumber, string projectName);
        Task<bool> SendLawyerRevisionRequestAsync(string builderEmail, string builderName, string lawyerName, string unitNumber, string projectName, string revisionNotes);
        Task<bool> SendPurchaserStatusUpdateAsync(string toEmail, string purchaserName, string unitNumber, string projectName, string statusMessage);
        Task<bool> SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink);
        Task<bool> SendAdminCreatedUserEmailAsync(string toEmail, string userName, string roleName, string loginLink);
    }

    // ===== EMAIL SERVICE IMPLEMENTATION =====
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _settings;
        private readonly ILogger<EmailService> _logger;
        private readonly IWebHostEnvironment _env;

        public EmailService(
            IOptions<EmailSettings> settings,
            ILogger<EmailService> logger,
            IWebHostEnvironment env)
        {
            _settings = settings.Value;
            _logger = logger;
            _env = env;
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            if (!_settings.IsEnabled)
            {
                _logger.LogWarning("Email is disabled. Would have sent to {Email}: {Subject}", toEmail, subject);
                return true; // Return true so app continues working
            }

            try
            {
                using var client = new SmtpClient(_settings.SmtpServer, _settings.SmtpPort)
                {
                    Credentials = new NetworkCredential(_settings.SmtpUsername, _settings.SmtpPassword),
                    EnableSsl = _settings.EnableSsl
                };

                var message = new MailMessage
                {
                    From = new MailAddress(_settings.FromEmail, _settings.FromName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                message.To.Add(toEmail);

                await client.SendMailAsync(message);
                
                _logger.LogInformation("Email sent successfully to {Email}: {Subject}", toEmail, subject);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}: {Subject}", toEmail, subject);
                return false;
            }
        }

        // ===== PURCHASER INVITATION EMAIL =====
        public async Task<bool> SendPurchaserInvitationAsync(
            string toEmail, 
            string purchaserName, 
            string unitNumber, 
            string projectName, 
            string invitationLink)
        {
            var subject = $"Welcome to {projectName} - Activate Your PreConHub Account";
            
            var htmlBody = GetEmailTemplate("PurchaserInvitation")
                .Replace("{{PurchaserName}}", purchaserName)
                .Replace("{{UnitNumber}}", unitNumber)
                .Replace("{{ProjectName}}", projectName)
                .Replace("{{InvitationLink}}", invitationLink)
                .Replace("{{Year}}", DateTime.Now.Year.ToString());

            return await SendEmailAsync(toEmail, subject, htmlBody);
        }

        // ===== LAWYER INVITATION EMAIL =====
        public async Task<bool> SendLawyerInvitationAsync(
            string toEmail, 
            string lawyerName, 
            int unitCount, 
            List<string> projectNames, 
            string invitationLink)
        {
            var subject = $"PreConHub Legal Review Assignment - {unitCount} Unit(s) Assigned";
            
            var projectList = string.Join(", ", projectNames.Distinct());
            
            var htmlBody = GetEmailTemplate("LawyerInvitation")
                .Replace("{{LawyerName}}", lawyerName)
                .Replace("{{UnitCount}}", unitCount.ToString())
                .Replace("{{ProjectNames}}", projectList)
                .Replace("{{InvitationLink}}", invitationLink)
                .Replace("{{Year}}", DateTime.Now.Year.ToString());

            return await SendEmailAsync(toEmail, subject, htmlBody);
        }

        // ===== LAWYER APPROVAL NOTIFICATION (to Builder) =====
        public async Task<bool> SendLawyerApprovalNotificationAsync(
            string builderEmail, 
            string builderName, 
            string lawyerName, 
            string unitNumber, 
            string projectName)
        {
            var subject = $"✅ Unit {unitNumber} Approved for Closing - {projectName}";
            
            var htmlBody = GetEmailTemplate("LawyerApproval")
                .Replace("{{BuilderName}}", builderName)
                .Replace("{{LawyerName}}", lawyerName)
                .Replace("{{UnitNumber}}", unitNumber)
                .Replace("{{ProjectName}}", projectName)
                .Replace("{{ApprovalDate}}", DateTime.Now.ToString("MMMM dd, yyyy"))
                .Replace("{{Year}}", DateTime.Now.Year.ToString());

            return await SendEmailAsync(builderEmail, subject, htmlBody);
        }

        // ===== LAWYER REVISION REQUEST (to Builder) =====
        public async Task<bool> SendLawyerRevisionRequestAsync(
            string builderEmail, 
            string builderName, 
            string lawyerName, 
            string unitNumber, 
            string projectName, 
            string revisionNotes)
        {
            var subject = $"⚠️ Revision Requested for Unit {unitNumber} - {projectName}";
            
            var htmlBody = GetEmailTemplate("RevisionRequest")
                .Replace("{{BuilderName}}", builderName)
                .Replace("{{LawyerName}}", lawyerName)
                .Replace("{{UnitNumber}}", unitNumber)
                .Replace("{{ProjectName}}", projectName)
                .Replace("{{RevisionNotes}}", revisionNotes)
                .Replace("{{RequestDate}}", DateTime.Now.ToString("MMMM dd, yyyy"))
                .Replace("{{Year}}", DateTime.Now.Year.ToString());

            return await SendEmailAsync(builderEmail, subject, htmlBody);
        }

        // ===== PURCHASER STATUS UPDATE =====
        public async Task<bool> SendPurchaserStatusUpdateAsync(
            string toEmail, 
            string purchaserName, 
            string unitNumber, 
            string projectName, 
            string statusMessage)
        {
            var subject = $"Update on Your Unit {unitNumber} - {projectName}";
            
            var htmlBody = GetEmailTemplate("PurchaserStatusUpdate")
                .Replace("{{PurchaserName}}", purchaserName)
                .Replace("{{UnitNumber}}", unitNumber)
                .Replace("{{ProjectName}}", projectName)
                .Replace("{{StatusMessage}}", statusMessage)
                .Replace("{{Year}}", DateTime.Now.Year.ToString());

            return await SendEmailAsync(toEmail, subject, htmlBody);
        }

        // ===== PASSWORD RESET EMAIL =====
        public async Task<bool> SendPasswordResetEmailAsync(
            string toEmail,
            string userName,
            string resetLink)
        {
            var subject = "Reset Your PreConHub Password";

            var htmlBody = GetEmailTemplate("PasswordReset")
                .Replace("{{UserName}}", userName)
                .Replace("{{ResetLink}}", resetLink)
                .Replace("{{Year}}", DateTime.Now.Year.ToString());

            return await SendEmailAsync(toEmail, subject, htmlBody);
        }

        // ===== ADMIN-CREATED USER EMAIL =====
        public async Task<bool> SendAdminCreatedUserEmailAsync(
            string toEmail,
            string userName,
            string roleName,
            string loginLink)
        {
            var subject = "Your PreConHub Account Has Been Created";

            var htmlBody = GetEmailTemplate("AdminCreatedUser")
                .Replace("{{UserName}}", userName)
                .Replace("{{RoleName}}", roleName)
                .Replace("{{LoginLink}}", loginLink)
                .Replace("{{Year}}", DateTime.Now.Year.ToString());

            return await SendEmailAsync(toEmail, subject, htmlBody);
        }

        // ===== GET EMAIL TEMPLATE =====
        private string GetEmailTemplate(string templateName)
        {
            // Try to load from file first
            var templatePath = Path.Combine(_env.ContentRootPath, "EmailTemplates", $"{templateName}.html");
            
            if (File.Exists(templatePath))
            {
                return File.ReadAllText(templatePath);
            }

            // Fall back to embedded templates
            return templateName switch
            {
                "PurchaserInvitation" => GetPurchaserInvitationTemplate(),
                "LawyerInvitation" => GetLawyerInvitationTemplate(),
                "LawyerApproval" => GetLawyerApprovalTemplate(),
                "RevisionRequest" => GetRevisionRequestTemplate(),
                "PurchaserStatusUpdate" => GetPurchaserStatusUpdateTemplate(),
                "PasswordReset" => GetPasswordResetTemplate(),
                "AdminCreatedUser" => GetAdminCreatedUserTemplate(),
                _ => GetDefaultTemplate()
            };
        }

        // ===== EMBEDDED TEMPLATES =====
        
        private string GetPurchaserInvitationTemplate()
        {
            return @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin:0; padding:0; font-family: Arial, sans-serif; background-color: #f4f4f4;'>
    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
        <div style='background: linear-gradient(135deg, #0d6efd 0%, #0a58ca 100%); padding: 30px; text-align: center; border-radius: 10px 10px 0 0;'>
            <h1 style='color: white; margin: 0; font-size: 28px;'>🏠 PreConHub</h1>
            <p style='color: rgba(255,255,255,0.9); margin: 10px 0 0 0;'>Pre-Construction Closing Portal</p>
        </div>
        
        <div style='background: white; padding: 40px 30px; border-radius: 0 0 10px 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1);'>
            <h2 style='color: #333; margin-top: 0;'>Welcome, {{PurchaserName}}! 👋</h2>
            
            <p style='color: #555; line-height: 1.6;'>
                Congratulations on your purchase of <strong>Unit {{UnitNumber}}</strong> at <strong>{{ProjectName}}</strong>!
            </p>
            
            <p style='color: #555; line-height: 1.6;'>
                You've been invited to access the PreConHub portal where you can:
            </p>
            
            <ul style='color: #555; line-height: 1.8;'>
                <li>View your Statement of Adjustments</li>
                <li>Submit your mortgage information</li>
                <li>Upload required documents</li>
                <li>Track your closing progress</li>
            </ul>
            
            <div style='text-align: center; margin: 30px 0;'>
                <a href='{{InvitationLink}}' style='background: #0d6efd; color: white; padding: 15px 40px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block;'>
                    Activate My Account
                </a>
            </div>
            
            <p style='color: #888; font-size: 14px; line-height: 1.6;'>
                If the button doesn't work, copy and paste this link into your browser:<br>
                <a href='{{InvitationLink}}' style='color: #0d6efd; word-break: break-all;'>{{InvitationLink}}</a>
            </p>
            
            <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;'>
            
            <p style='color: #888; font-size: 12px; text-align: center;'>
                This invitation link will expire in 7 days. If you didn't expect this email, please ignore it.
            </p>
        </div>
        
        <div style='text-align: center; padding: 20px; color: #888; font-size: 12px;'>
            © {{Year}} PreConHub. All rights reserved.
        </div>
    </div>
</body>
</html>";
        }

        private string GetLawyerInvitationTemplate()
        {
            return @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin:0; padding:0; font-family: Arial, sans-serif; background-color: #f4f4f4;'>
    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
        <div style='background: linear-gradient(135deg, #198754 0%, #146c43 100%); padding: 30px; text-align: center; border-radius: 10px 10px 0 0;'>
            <h1 style='color: white; margin: 0; font-size: 28px;'>⚖️ PreConHub</h1>
            <p style='color: rgba(255,255,255,0.9); margin: 10px 0 0 0;'>Legal Review Portal</p>
        </div>
        
        <div style='background: white; padding: 40px 30px; border-radius: 0 0 10px 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1);'>
            <h2 style='color: #333; margin-top: 0;'>Hello, {{LawyerName}}! 👋</h2>
            
            <p style='color: #555; line-height: 1.6;'>
                You have been assigned to review <strong>{{UnitCount}} unit(s)</strong> for the following project(s):
            </p>
            
            <div style='background: #f8f9fa; padding: 15px 20px; border-radius: 8px; margin: 20px 0; border-left: 4px solid #198754;'>
                <strong style='color: #333;'>{{ProjectNames}}</strong>
            </div>
            
            <p style='color: #555; line-height: 1.6;'>
                Through the PreConHub portal, you will be able to:
            </p>
            
            <ul style='color: #555; line-height: 1.8;'>
                <li>Review Statement of Adjustments for each unit</li>
                <li>View purchaser mortgage and financial information</li>
                <li>Add notes and questions</li>
                <li>Approve units for closing or request revisions</li>
            </ul>
            
            <div style='text-align: center; margin: 30px 0;'>
                <a href='{{InvitationLink}}' style='background: #198754; color: white; padding: 15px 40px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block;'>
                    Access Review Portal
                </a>
            </div>
            
            <p style='color: #888; font-size: 14px; line-height: 1.6;'>
                If the button doesn't work, copy and paste this link into your browser:<br>
                <a href='{{InvitationLink}}' style='color: #198754; word-break: break-all;'>{{InvitationLink}}</a>
            </p>
            
            <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;'>
            
            <p style='color: #888; font-size: 12px; text-align: center;'>
                This invitation link will expire in 7 days.
            </p>
        </div>
        
        <div style='text-align: center; padding: 20px; color: #888; font-size: 12px;'>
            © {{Year}} PreConHub. All rights reserved.
        </div>
    </div>
</body>
</html>";
        }

        private string GetLawyerApprovalTemplate()
        {
            return @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin:0; padding:0; font-family: Arial, sans-serif; background-color: #f4f4f4;'>
    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
        <div style='background: linear-gradient(135deg, #198754 0%, #146c43 100%); padding: 30px; text-align: center; border-radius: 10px 10px 0 0;'>
            <h1 style='color: white; margin: 0; font-size: 28px;'>✅ Unit Approved</h1>
        </div>
        
        <div style='background: white; padding: 40px 30px; border-radius: 0 0 10px 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1);'>
            <h2 style='color: #333; margin-top: 0;'>Good news, {{BuilderName}}!</h2>
            
            <p style='color: #555; line-height: 1.6;'>
                <strong>{{LawyerName}}</strong> has approved <strong>Unit {{UnitNumber}}</strong> at <strong>{{ProjectName}}</strong> for closing.
            </p>
            
            <div style='background: #d1e7dd; padding: 20px; border-radius: 8px; margin: 20px 0; text-align: center;'>
                <span style='font-size: 48px;'>✅</span>
                <h3 style='color: #0f5132; margin: 10px 0 5px 0;'>Lawyer Confirmed</h3>
                <p style='color: #0f5132; margin: 0;'>{{ApprovalDate}}</p>
            </div>
            
            <p style='color: #555; line-height: 1.6;'>
                The unit is now ready to proceed to closing. You can view the full details in your PreConHub dashboard.
            </p>
            
            <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;'>
            
            <p style='color: #888; font-size: 12px; text-align: center;'>
                This is an automated notification from PreConHub.
            </p>
        </div>
        
        <div style='text-align: center; padding: 20px; color: #888; font-size: 12px;'>
            © {{Year}} PreConHub. All rights reserved.
        </div>
    </div>
</body>
</html>";
        }

        private string GetRevisionRequestTemplate()
        {
            return @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin:0; padding:0; font-family: Arial, sans-serif; background-color: #f4f4f4;'>
    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
        <div style='background: linear-gradient(135deg, #dc3545 0%, #b02a37 100%); padding: 30px; text-align: center; border-radius: 10px 10px 0 0;'>
            <h1 style='color: white; margin: 0; font-size: 28px;'>⚠️ Revision Requested</h1>
        </div>
        
        <div style='background: white; padding: 40px 30px; border-radius: 0 0 10px 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1);'>
            <h2 style='color: #333; margin-top: 0;'>Attention Required, {{BuilderName}}</h2>
            
            <p style='color: #555; line-height: 1.6;'>
                <strong>{{LawyerName}}</strong> has requested revisions for <strong>Unit {{UnitNumber}}</strong> at <strong>{{ProjectName}}</strong>.
            </p>
            
            <div style='background: #fff3cd; padding: 20px; border-radius: 8px; margin: 20px 0; border-left: 4px solid #ffc107;'>
                <h4 style='color: #664d03; margin: 0 0 10px 0;'>Revision Notes:</h4>
                <p style='color: #664d03; margin: 0; white-space: pre-wrap;'>{{RevisionNotes}}</p>
            </div>
            
            <p style='color: #555; line-height: 1.6;'>
                Please review the notes and make the necessary updates. Once completed, the lawyer will be able to re-review the unit.
            </p>
            
            <p style='color: #888; font-size: 14px;'>
                Request Date: {{RequestDate}}
            </p>
            
            <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;'>
            
            <p style='color: #888; font-size: 12px; text-align: center;'>
                This is an automated notification from PreConHub.
            </p>
        </div>
        
        <div style='text-align: center; padding: 20px; color: #888; font-size: 12px;'>
            © {{Year}} PreConHub. All rights reserved.
        </div>
    </div>
</body>
</html>";
        }

        private string GetPurchaserStatusUpdateTemplate()
        {
            return @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin:0; padding:0; font-family: Arial, sans-serif; background-color: #f4f4f4;'>
    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
        <div style='background: linear-gradient(135deg, #0d6efd 0%, #0a58ca 100%); padding: 30px; text-align: center; border-radius: 10px 10px 0 0;'>
            <h1 style='color: white; margin: 0; font-size: 28px;'>📋 Status Update</h1>
        </div>
        
        <div style='background: white; padding: 40px 30px; border-radius: 0 0 10px 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1);'>
            <h2 style='color: #333; margin-top: 0;'>Hello, {{PurchaserName}}!</h2>
            
            <p style='color: #555; line-height: 1.6;'>
                There's an update on your <strong>Unit {{UnitNumber}}</strong> at <strong>{{ProjectName}}</strong>:
            </p>
            
            <div style='background: #e7f1ff; padding: 20px; border-radius: 8px; margin: 20px 0; border-left: 4px solid #0d6efd;'>
                <p style='color: #084298; margin: 0;'>{{StatusMessage}}</p>
            </div>
            
            <p style='color: #555; line-height: 1.6;'>
                Log in to your PreConHub account to view more details.
            </p>
            
            <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;'>
            
            <p style='color: #888; font-size: 12px; text-align: center;'>
                This is an automated notification from PreConHub.
            </p>
        </div>
        
        <div style='text-align: center; padding: 20px; color: #888; font-size: 12px;'>
            © {{Year}} PreConHub. All rights reserved.
        </div>
    </div>
</body>
</html>";
        }

        private string GetPasswordResetTemplate()
        {
            return @"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1.0'></head>
<body style='margin:0; padding:0; font-family: Arial, sans-serif; background-color: #f4f4f4;'>
    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
        <div style='background: linear-gradient(135deg, #6f42c1 0%, #5a32a3 100%); padding: 30px; text-align: center; border-radius: 10px 10px 0 0;'>
            <h1 style='color: white; margin: 0; font-size: 28px;'>PreConHub</h1>
            <p style='color: rgba(255,255,255,0.9); margin: 10px 0 0 0;'>Password Reset</p>
        </div>
        <div style='background: white; padding: 40px 30px; border-radius: 0 0 10px 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1);'>
            <h2 style='color: #333; margin-top: 0;'>Hello, {{UserName}}</h2>
            <p style='color: #555; line-height: 1.6;'>An administrator has requested a password reset for your account. Click the button below to set a new password:</p>
            <div style='text-align: center; margin: 30px 0;'>
                <a href='{{ResetLink}}' style='background: #6f42c1; color: white; padding: 15px 40px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block;'>Reset My Password</a>
            </div>
            <p style='color: #888; font-size: 14px;'>If the button doesn't work, copy and paste this link:<br><a href='{{ResetLink}}' style='color: #6f42c1; word-break: break-all;'>{{ResetLink}}</a></p>
            <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;'>
            <p style='color: #888; font-size: 12px; text-align: center;'>This link will expire in 24 hours. If you didn't request this, please ignore this email.</p>
        </div>
        <div style='text-align: center; padding: 20px; color: #888; font-size: 12px;'>&copy; {{Year}} PreConHub. All rights reserved.</div>
    </div>
</body>
</html>";
        }

        private string GetAdminCreatedUserTemplate()
        {
            return @"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1.0'></head>
<body style='margin:0; padding:0; font-family: Arial, sans-serif; background-color: #f4f4f4;'>
    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
        <div style='background: linear-gradient(135deg, #0d6efd 0%, #0a58ca 100%); padding: 30px; text-align: center; border-radius: 10px 10px 0 0;'>
            <h1 style='color: white; margin: 0; font-size: 28px;'>PreConHub</h1>
            <p style='color: rgba(255,255,255,0.9); margin: 10px 0 0 0;'>Welcome to PreConHub</p>
        </div>
        <div style='background: white; padding: 40px 30px; border-radius: 0 0 10px 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1);'>
            <h2 style='color: #333; margin-top: 0;'>Welcome, {{UserName}}!</h2>
            <p style='color: #555; line-height: 1.6;'>An account has been created for you on PreConHub as a <strong>{{RoleName}}</strong>.</p>
            <p style='color: #555; line-height: 1.6;'>You can now log in and start using the platform:</p>
            <div style='text-align: center; margin: 30px 0;'>
                <a href='{{LoginLink}}' style='background: #0d6efd; color: white; padding: 15px 40px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block;'>Log In to PreConHub</a>
            </div>
            <p style='color: #888; font-size: 14px;'>If the button doesn't work, copy and paste this link:<br><a href='{{LoginLink}}' style='color: #0d6efd; word-break: break-all;'>{{LoginLink}}</a></p>
            <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;'>
            <p style='color: #888; font-size: 12px; text-align: center;'>If you didn't expect this email, please contact your administrator.</p>
        </div>
        <div style='text-align: center; padding: 20px; color: #888; font-size: 12px;'>&copy; {{Year}} PreConHub. All rights reserved.</div>
    </div>
</body>
</html>";
        }

        private string GetDefaultTemplate()
        {
            return @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
</head>
<body style='font-family: Arial, sans-serif; padding: 20px;'>
    <h1>PreConHub Notification</h1>
    <p>You have a new notification from PreConHub.</p>
</body>
</html>";
        }
    }
}
