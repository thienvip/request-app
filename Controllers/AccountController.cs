using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using src.Core;
using src.Core.Domains;
using src.Localization.Resources;
using src.Repositories.Roles;
using src.Repositories.Users;
using src.Web.Common.Models.AccountViewModels;
using src.Web.Common.Security;
using System.Diagnostics;

namespace src.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
        private readonly ILogger<AccountController> _logger;
        private readonly ISignInManager _signInManager;
        private readonly IRoleRepository _roleRepository;
        private readonly IDateTime _dateTime;
        private readonly IUserRepository _userRepository;
        private readonly IMemoryCache _cache;


        public AccountController(IStringLocalizer<SharedResource> sharedLocalizer, ILogger<AccountController> logger, ISignInManager signInManager, IDateTime dateTime, IRoleRepository roleRepository,IUserRepository userRepository, IMemoryCache cache)
        {
            _sharedLocalizer = sharedLocalizer;
            _logger = logger;
            _signInManager = signInManager;
            _dateTime = dateTime;
            _userRepository = userRepository;
            _roleRepository = roleRepository;
            _cache = cache;
        }


        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Login(string? returnUrl)
        {           
            await _signInManager.SignOutAsync();           
            ViewData["ReturnUrl"] = returnUrl;
            _cache.Remove("user");  
            var model = new LoginViewModel();
            
            return View("Login", model);
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginViewModel model, string ?returnUrl)
        {
            try
            {          
                if (ModelState.IsValid)
                {
                    //bool result = _authenticationService.ValidateUser(_appSettings.Value.Application.Domain, model.UserName, model.Password);
                    bool result = true;
                 
                    if (result)
                    {
                        var user = await _userRepository.GetUserByUserNameAsync(model.UserName);
                        if (user != null && user.IsActive)
                        {                           
                            var roleNames = (await _roleRepository.GetRolesForUserAsync(user.Id)).Select(r => r.Name)
                                .ToList();
                            await _signInManager.SignInAsync(user, roleNames);
                            user.LastLoginDate = _dateTime.Now;
                            await _userRepository.UpdateUserAsync(user);
                            string t = _sharedLocalizer["IncorrectUsernameOrPassword"];
                            _cache.Set("user", user);
                            _logger.LogInformation("Login Successful: {UserUserName}", user.UserName);
                            if (!string.IsNullOrEmpty(returnUrl) && !string.Equals(returnUrl, "/") &&
                                Url.IsLocalUrl(returnUrl))
                                return RedirectToLocal(returnUrl);
                            return RedirectToAction("Index", "Request");
                        }
                        _logger.LogError("Authorization Fail: {ModelUserName}", model.UserName);
                        ModelState.AddModelError("", _sharedLocalizer["AccessDenied"]);
                      
                    }
                    else
                    {
                        _logger.LogError("Login Fail: {ModelUserName} - Incorrect username or password. ",
                            model.UserName);
                        ModelState.AddModelError("", _sharedLocalizer["IncorrectUsernameOrPassword"]);                       
                    }

                }
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, _sharedLocalizer["InvalidLogin"]);
                return View(model);
            }

            return View(model);
        }

      

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("Logout Successful: {IdentityName}", User?.Identity?.Name);
            return RedirectToAction(nameof(AccountController.Login), "Account");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult SetLanguage(string culture, string returnUrl)
        {
            Response.Cookies.Append(
                    CookieRequestCultureProvider.DefaultCookieName,
                    CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
            );
            return LocalRedirect(returnUrl);
        }
        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Account", "Login");
        }

    }
}