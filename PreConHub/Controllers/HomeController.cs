using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PreConHub.Models;
using PreConHub.Models.Entities;
using System.Diagnostics;

namespace PreConHub.Controllers
{
    public class HomeController : Controller
    {
        // TEMPORARY - DELETE AFTER USE!
       /* [AllowAnonymous]
        public async Task<IActionResult> ResetAdminPassword([FromServices] UserManager<ApplicationUser> userManager)
        {
            var user = await userManager.FindByEmailAsync("info@afshahin.com");
            if (user == null) return Content("User not found");

            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var result = await userManager.ResetPasswordAsync(user, token, "Test123!");

            return Content(result.Succeeded ? "Password reset to Test123!" : "Failed: " + string.Join(", ", result.Errors.Select(e => e.Description)));
        }*/

        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }
        public IActionResult Contact()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
