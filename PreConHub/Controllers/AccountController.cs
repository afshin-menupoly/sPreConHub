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
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

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
                CompanyName = user.CompanyName,
                LawFirm = user.LawFirm,
                UserType = user.UserType,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                IsEmailReadOnly = user.UserType == UserType.Builder,
                IsCompanyNameReadOnly = user.UserType == UserType.Builder
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
    }
}
