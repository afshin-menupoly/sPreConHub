using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PreConHub.Models.Entities;
using PreConHub.Models.ViewModels;

namespace PreConHub.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<AccountController> _logger;
        private readonly UrlEncoder _urlEncoder;

        private const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<AccountController> logger,
            UrlEncoder urlEncoder)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _urlEncoder = urlEncoder;
        }

        // =============================================
        // PROFILE
        // =============================================

        // GET: /Account/Profile
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var vm = new UserProfileViewModel
            {
                UserId = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email ?? "",
                Phone = user.PhoneNumber,
                CellPhone = user.CellPhone,
                CompanyName = user.CompanyName,
                LawFirm = user.LawFirm,
                UserType = user.UserType,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                IsEmailReadOnly = user.UserType == UserType.Builder,
                IsCompanyNameReadOnly = user.UserType == UserType.Builder,
                TwoFactorEnabled = await _userManager.GetTwoFactorEnabledAsync(user),
                HasAuthenticator = await _userManager.GetAuthenticatorKeyAsync(user) != null
            };

            return View(vm);
        }

        // POST: /Account/Profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(UserProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            // Re-set read-only flags for validation
            model.UserType = user.UserType;
            model.CreatedAt = user.CreatedAt;
            model.LastLoginAt = user.LastLoginAt;
            model.IsEmailReadOnly = user.UserType == UserType.Builder;
            model.IsCompanyNameReadOnly = user.UserType == UserType.Builder;
            model.TwoFactorEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
            model.HasAuthenticator = await _userManager.GetAuthenticatorKeyAsync(user) != null;

            // For builders, restore read-only fields from DB (ignore submitted values)
            if (user.UserType == UserType.Builder)
            {
                model.Email = user.Email ?? "";
                model.CompanyName = user.CompanyName;
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Update editable fields
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.PhoneNumber = model.Phone;
            user.CellPhone = model.CellPhone;

            // Only non-builders can update email
            if (user.UserType != UserType.Builder && !string.IsNullOrWhiteSpace(model.Email))
            {
                if (model.Email != user.Email)
                {
                    var setEmailResult = await _userManager.SetEmailAsync(user, model.Email);
                    if (!setEmailResult.Succeeded)
                    {
                        foreach (var error in setEmailResult.Errors)
                            ModelState.AddModelError("", error.Description);
                        return View(model);
                    }
                    await _userManager.SetUserNameAsync(user, model.Email);
                }
            }

            // Only non-builders can update company name
            if (user.UserType != UserType.Builder)
            {
                user.CompanyName = model.CompanyName;
            }

            // Law firm — only relevant for lawyers
            if (user.UserType == UserType.Lawyer)
            {
                user.LawFirm = model.LawFirm;
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
                return View(model);
            }

            _logger.LogInformation("User {UserId} updated their profile", user.Id);
            TempData["Success"] = "Your profile has been updated.";
            return RedirectToAction(nameof(Profile));
        }

        // =============================================
        // CHANGE PASSWORD
        // =============================================

        // GET: /Account/ChangePassword
        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel());
        }

        // POST: /Account/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
                return View(model);
            }

            await _signInManager.RefreshSignInAsync(user);
            _logger.LogInformation("User {UserId} changed their password", user.Id);
            TempData["Success"] = "Your password has been changed successfully.";
            return RedirectToAction(nameof(Profile));
        }

        // =============================================
        // TWO-FACTOR AUTHENTICATION
        // =============================================

        // GET: /Account/SetupAuthenticatorApp
        public async Task<IActionResult> SetupAuthenticatorApp()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(unformattedKey))
            {
                await _userManager.ResetAuthenticatorKeyAsync(user);
                unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
            }

            var vm = new SetupAuthenticatorViewModel
            {
                SharedKey = FormatKey(unformattedKey!),
                AuthenticatorUri = GenerateQrCodeUri(user.Email!, unformattedKey!)
            };

            return View(vm);
        }

        // POST: /Account/SetupAuthenticatorApp
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetupAuthenticatorApp(SetupAuthenticatorViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            if (!ModelState.IsValid)
            {
                // Re-populate key and URI
                var key = await _userManager.GetAuthenticatorKeyAsync(user);
                model.SharedKey = FormatKey(key!);
                model.AuthenticatorUri = GenerateQrCodeUri(user.Email!, key!);
                return View(model);
            }

            var code = model.Code.Replace(" ", "").Replace("-", "");
            var is2faTokenValid = await _userManager.VerifyTwoFactorTokenAsync(
                user, _userManager.Options.Tokens.AuthenticatorTokenProvider, code);

            if (!is2faTokenValid)
            {
                ModelState.AddModelError("Code", "Verification code is invalid.");
                var key = await _userManager.GetAuthenticatorKeyAsync(user);
                model.SharedKey = FormatKey(key!);
                model.AuthenticatorUri = GenerateQrCodeUri(user.Email!, key!);
                return View(model);
            }

            await _userManager.SetTwoFactorEnabledAsync(user, true);
            _logger.LogInformation("User {UserId} enabled 2FA via authenticator app", user.Id);

            var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
            model.RecoveryCodes = recoveryCodes?.ToArray();

            TempData["Success"] = "Authenticator app has been configured. Save your recovery codes!";
            return View("AuthenticatorConfirmation", model);
        }

        // GET: /Account/EnableEmailTwoFactor
        public async Task<IActionResult> EnableEmailTwoFactor()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            // Send code to email
            var code = await _userManager.GenerateTwoFactorTokenAsync(user, "Email");
            // In a real implementation, send this code via email
            // For now, store in TempData for testing
            TempData["2FACode"] = code;
            TempData["Info"] = "A verification code has been sent to your email address.";

            return View(new VerifyTwoFactorCodeViewModel { Provider = "Email" });
        }

        // POST: /Account/EnableEmailTwoFactor
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnableEmailTwoFactor(VerifyTwoFactorCodeViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var isValid = await _userManager.VerifyTwoFactorTokenAsync(user, "Email", model.Code);
            if (!isValid)
            {
                ModelState.AddModelError("Code", "Invalid verification code.");
                model.Provider = "Email";
                return View(model);
            }

            await _userManager.SetTwoFactorEnabledAsync(user, true);
            _logger.LogInformation("User {UserId} enabled 2FA via email", user.Id);

            TempData["Success"] = "Email two-step verification has been enabled.";
            return RedirectToAction(nameof(Profile));
        }

        // GET: /Account/EnableSmsTwoFactor
        public async Task<IActionResult> EnableSmsTwoFactor()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            if (string.IsNullOrWhiteSpace(user.CellPhone))
            {
                TempData["Error"] = "Please add your cellphone number in your profile before enabling SMS verification.";
                return RedirectToAction(nameof(Profile));
            }

            // SMS service to be implemented later
            TempData["Info"] = $"SMS verification will be sent to {user.CellPhone}. SMS service coming soon.";
            return View(new VerifyTwoFactorCodeViewModel { Provider = "Phone" });
        }

        // POST: /Account/EnableSmsTwoFactor
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnableSmsTwoFactor(VerifyTwoFactorCodeViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            if (string.IsNullOrWhiteSpace(user.CellPhone))
            {
                TempData["Error"] = "Please add your cellphone number first.";
                return RedirectToAction(nameof(Profile));
            }

            var isValid = await _userManager.VerifyTwoFactorTokenAsync(user, "Phone", model.Code);
            if (!isValid)
            {
                ModelState.AddModelError("Code", "Invalid verification code.");
                model.Provider = "Phone";
                return View(model);
            }

            await _userManager.SetTwoFactorEnabledAsync(user, true);
            _logger.LogInformation("User {UserId} enabled 2FA via SMS", user.Id);

            TempData["Success"] = "SMS two-step verification has been enabled.";
            return RedirectToAction(nameof(Profile));
        }

        // POST: /Account/DisableTwoFactor
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisableTwoFactor()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            await _userManager.SetTwoFactorEnabledAsync(user, false);
            await _userManager.ResetAuthenticatorKeyAsync(user);
            _logger.LogInformation("User {UserId} disabled 2FA", user.Id);

            await _signInManager.RefreshSignInAsync(user);
            TempData["Success"] = "Two-step verification has been disabled.";
            return RedirectToAction(nameof(Profile));
        }

        // =============================================
        // HELPERS
        // =============================================

        private static string FormatKey(string unformattedKey)
        {
            var result = new StringBuilder();
            int currentPosition = 0;
            while (currentPosition + 4 < unformattedKey.Length)
            {
                result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
                currentPosition += 4;
            }
            if (currentPosition < unformattedKey.Length)
                result.Append(unformattedKey.AsSpan(currentPosition));
            return result.ToString().ToLowerInvariant();
        }

        private string GenerateQrCodeUri(string email, string unformattedKey)
        {
            return string.Format(
                AuthenticatorUriFormat,
                _urlEncoder.Encode("PreConHub"),
                _urlEncoder.Encode(email),
                unformattedKey);
        }
    }
}
