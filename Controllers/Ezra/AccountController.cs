using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace starfold_mobile.Controllers.Ezra;

[Route("ezra"), HitCounter]
public class AccountController(IConfiguration config, ILogger<AccountController> logger) : Controller
{
    private static readonly PasswordHasher<object> Hasher = new();

    [HttpGet("login")]
    public IActionResult Login(string? returnUrl)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost("login"), EnableRateLimiting("login")]
    public async Task<IActionResult> Login(string? password, string? returnUrl)
    {
        var hash = config["Ezra:AdminPasswordHash"];
        if (string.IsNullOrEmpty(hash))
            logger.LogWarning("Ezra:AdminPasswordHash is not set, so nobody can sign in.");

        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(password)
            || Hasher.VerifyHashedPassword(null!, hash, password) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError("", "Wrong password.");
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "admin")], CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true });
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "/ezra");
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return LocalRedirect("/ezra");
    }
}
