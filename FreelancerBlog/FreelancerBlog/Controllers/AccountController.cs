﻿using System.Security.Claims;
using System.Threading.Tasks;
using FreelancerBlog.Core.DomainModels;
using FreelancerBlog.Core.Queries.Services.Shared;
using FreelancerBlog.Core.Services.Shared;
using FreelancerBlog.Core.Types;
using FreelancerBlog.Core.Wrappers;
using FreelancerBlog.Web.ViewModels.Account;
using FreelancerBlog.Web.ViewModels.Email;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace FreelancerBlog.Web.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger _logger;
        private IRazorViewToString _razorViewToString;
        private readonly IMediator _mediator;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender,
            ILoggerFactoryWrapper loggerFactoryWrapper,
            IRazorViewToString render,
            IMediator mediator)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _logger = loggerFactoryWrapper.CreateLogger<AccountController>();
            _razorViewToString = render;
            _mediator = mediator;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user != null)
            {
                if (!user.EmailConfirmed)
                {
                    ViewData["LoginMessage"] = "EmailIsNotConfirmed";
                    return View(model);
                }
            }

            // This doesn't count login failures towards account lockout
            // To enable password failures to trigger account lockout, set lockoutOnFailure: true
            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                _logger.LogInformation(1, "User logged in.");
                return RedirectToLocal(returnUrl);
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning(2, "User account locked out.");

                return View("Lockout");
            }

            ModelState.AddModelError(string.Empty, "نام کاربری یا کلمه عبور اشتباه است.");
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {

            CaptchaResponse captchaResult = await _mediator.Send(new ValidateCaptchaQuery());

            if (captchaResult.Success != "true")
            {
                ViewData["FailedTheCaptchaValidation"] = "true";
                return View(model);
            }

            if (!ModelState.IsValid) return View(model);

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                UserFullName = model.UserFullName,
                UserGender = model.UserGender,
                UserHowFindUs = model.UserHowFindUs
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);

                var callbackUrl = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, code = code }, protocol: HttpContext.Request.Scheme);

                var htmlString = _razorViewToString.Render("EmailTemplates/IdentityTemplate",
                    new IdentityTemplateViewModel
                    {
                        CallBackLink = callbackUrl,
                        CallBackLinkText = "فعال سازی",
                        EmailMessageHeader = "فعال سازی اکانت",
                        EmailPreviewMessage = "",
                        EmailMessageBody = "پیوستن شما به جمع کاربران وب برای ایران را تبریک میگوییم، برای فعال سازی نام کاربری خود فقط کافیست بر روی دکمه زیر کلیک کنید."
                    });

                await _emailSender.SendEmailAsync(model.Email, "فعال سازی نام کاربری - وب برای ایران", htmlString);

                _logger.LogInformation(3, "User created a new account with password.");

                ViewBag.InfoMessage = "RegistrationConfirmEmail";

                return View("InfoMessage");
            }

            AddErrors(result);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogOff()
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation(4, "User logged out.");
            return RedirectToAction(nameof(HomeController.Index), "Home");
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider, string returnUrl = null)
        {
            // Request a redirect to the external login provider.
            var redirectUrl = Url.Action("ExternalLoginCallback", "Account", new { ReturnUrl = returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return new ChallengeResult(provider, properties);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginCallback(string returnUrl = null)
        {
            var info = await _signInManager.GetExternalLoginInfoAsync();

            if (info == null)
            {
                return RedirectToAction(nameof(Login));
            }

            // Sign in the user with this external login provider if the user already has a login.
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false);

            if (result.Succeeded)
            {
                _logger.LogInformation(5, "User logged in with {Name} provider.", info.LoginProvider);
                return RedirectToLocal(returnUrl);
            }

            if (result.IsLockedOut)
            {
                return View("Lockout");
            }

            // If the user does not have an account, then ask the user to create an account.
            ViewData["ReturnUrl"] = returnUrl;
            ViewData["LoginProvider"] = info.LoginProvider;
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            var fullName = info.Principal.FindFirstValue(ClaimTypes.Name);
            return View("ExternalLoginConfirmation", new ExternalLoginConfirmationViewModel { Email = email, UserFullName = fullName });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExternalLoginConfirmation(ExternalLoginConfirmationViewModel model, string returnUrl = null)
        {
            if (_signInManager.IsSignedIn(User))
            {
                return RedirectToAction(nameof(ManageController.Index), "Manage");
            }

            // Get the information about the user from the external login provider
            var info = await _signInManager.GetExternalLoginInfoAsync();

            if (ModelState.IsValid)
            {
                if (info == null)
                {
                    return View("ExternalLoginFailure");
                }

                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    EmailConfirmed = true,
                    UserFullName = model.UserFullName,
                    UserGender = model.UserGender,
                    UserHowFindUs = model.UserHowFindUs
                };

                var result = await _userManager.CreateAsync(user);

                if (result.Succeeded)
                {
                    result = await _userManager.AddLoginAsync(user, info);

                    if (result.Succeeded)
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        _logger.LogInformation(6, "User created an account using {Name} provider.", info.LoginProvider);
                        return RedirectToLocal(returnUrl);
                    }

                }

                AddErrors(result);
            }

            ViewData["LoginProvider"] = info.LoginProvider;
            ViewData["ReturnUrl"] = returnUrl;

            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail(string userId, string code)
        {
            if (userId == null || code == null)
            {
                return View("Error");
            }

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return View("Error");
            }

            var result = await _userManager.ConfirmEmailAsync(user, code);

            ViewBag.InfoMessage = "EmailConfirmed";

            return View(result.Succeeded ? "InfoMessage" : "Error");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResendConfirmEmail()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ResendConfirmEmail(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);

                if (user == null || user.EmailConfirmed)
                {
                    ViewBag.InfoMessage = "ResendConfirmEmailSent";

                    // Don't reveal that the user does not exist or is not confirmed
                    return View("InfoMessage");
                }

                string code = await _userManager.GenerateEmailConfirmationTokenAsync(user);

                var callbackUrl = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, code = code }, protocol: HttpContext.Request.Scheme);

                var htmlString = _razorViewToString.Render("EmailTemplates/IdentityTemplate",
                         new IdentityTemplateViewModel
                         {
                             CallBackLink = callbackUrl,
                             CallBackLinkText = "فعال سازی",
                             EmailMessageHeader = "فعال سازی اکانت",
                             EmailPreviewMessage = "",
                             EmailMessageBody = "پیوستن شما به جمع کاربران وب برای ایران را تبریک میگوییم، برای فعال سازی نام کاربری خود فقط کافیست بر روی دکمه زیر کلیک کنید."
                         });

                await _emailSender.SendEmailAsync(model.Email, "فعال سازی نام کاربری - وب برای ایران", htmlString);

                ViewBag.InfoMessage = "ResendConfirmEmailSent";

                return View("InfoMessage");
            }

            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByNameAsync(model.Email);
                if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
                {
                    // Don't reveal that the user does not exist or is not confirmed
                    ViewBag.InfoMessage = "ForgotPasswordEmailSent";
                    return View("InfoMessage");
                }

                // For more information on how to enable account confirmation and password reset please visit http://go.microsoft.com/fwlink/?LinkID=532713
                // Send an email with this link
                var code = await _userManager.GeneratePasswordResetTokenAsync(user);


                var callbackUrl = Url.Action("ResetPassword", "Account", new { userId = user.Id, code = code }, protocol: HttpContext.Request.Scheme);

                var htmlString = _razorViewToString.Render("EmailTemplates/IdentityTemplate",
                         new IdentityTemplateViewModel
                         {
                             CallBackLink = callbackUrl,
                             CallBackLinkText = "بازنشانی پسورد",
                             EmailMessageHeader = "بازنشانی کلمه عبور",
                             EmailPreviewMessage = "",
                             EmailMessageBody = "در صورتی که کلمه عبور خود را فراموش کرده اید برای بازنشانی کلمه عبور خود کافیست بر روی دکمه زیر کلیک کنید، و یک کلمه عبور جدید برای خود انتخاب کنید."
                         });

                await _emailSender.SendEmailAsync(model.Email, "بازنشانی پسورد - وب برای ایران", htmlString);

                ViewBag.InfoMessage = "ForgotPasswordEmailSent";


                return View("InfoMessage");
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string code = null)
        {
            return code == null ? View("Error") : View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByNameAsync(model.Email);

            if (user == null)
            {
                // Don't reveal that the user does not exist
                ViewBag.InfoMessage = "ResetPasswordConfirmation";
                return View("InfoMessage");
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Code, model.Password);

            if (result.Succeeded)
            {
                //return RedirectToAction(nameof(AccountController.ResetPasswordConfirmation), "Account");
                ViewBag.InfoMessage = "ResetPasswordConfirmation";
                return View("InfoMessage");
            }

            AddErrors(result);

            return View();
        }

        #region Helpers

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(HomeController.Index), "Home");
        }

        #endregion
    }
}
