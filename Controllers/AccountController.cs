using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using DACS_Nhom3.Models;
using DACS_Nhom3.Models.ViewModels;
using DACS_Nhom3.Services;
using System.Net;
using System.Security.Claims;

namespace DACS_Nhom3.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IEmailService _emailService;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            IWebHostEnvironment webHostEnvironment,
            IEmailService emailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _webHostEnvironment = webHostEnvironment;
            _emailService = emailService;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null, bool disabled = false)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            if (disabled)
            {
                ViewBag.DisabledMessage = "Tài khoản của bạn đã bị vô hiệu hóa. Vui lòng liên hệ quản trị viên.";
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null)
                {
                    if (!user.IsActive)
                    {
                        ModelState.AddModelError(string.Empty, "Tài khoản của bạn đã bị vô hiệu hóa. Vui lòng liên hệ quản trị viên.");
                        return View(model);
                    }

                    var result = await _signInManager.PasswordSignInAsync(user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: false);
                    if (result.Succeeded)
                    {
                        user.LastLoginAt = DateTime.Now;
                        await _userManager.UpdateAsync(user);
                        return RedirectToLocal(returnUrl);
                    }
                }
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
            }
            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider, string? returnUrl = null)
        {
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);
        }

        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
        {
            returnUrl ??= Url.Content("~/");

            if (!string.IsNullOrWhiteSpace(remoteError))
            {
                ModelState.AddModelError(string.Empty, $"Đăng nhập Google thất bại: {remoteError}");
                ViewData["ReturnUrl"] = returnUrl;
                return View(nameof(Login));
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ModelState.AddModelError(string.Empty, "Không lấy được thông tin đăng nhập từ Google.");
                ViewData["ReturnUrl"] = returnUrl;
                return View(nameof(Login));
            }

            var signInResult = await _signInManager.ExternalLoginSignInAsync(
                info.LoginProvider,
                info.ProviderKey,
                isPersistent: false,
                bypassTwoFactor: true);

            if (signInResult.Succeeded)
            {
                var existingUser = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                if (existingUser != null)
                {
                    if (!existingUser.IsActive)
                    {
                        await _signInManager.SignOutAsync();
                        TempData["Error"] = "Tài khoản của bạn đã bị vô hiệu hóa. Vui lòng liên hệ quản trị viên.";
                        return RedirectToAction(nameof(Login));
                    }

                    existingUser.LastLoginAt = DateTime.Now;
                    await _userManager.UpdateAsync(existingUser);
                }

                return RedirectToLocal(returnUrl);
            }

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError(string.Empty, "Tài khoản Google chưa cung cấp email.");
                ViewData["ReturnUrl"] = returnUrl;
                return View(nameof(Login));
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user != null)
            {
                if (!user.IsActive)
                {
                    TempData["Error"] = "Tài khoản của bạn đã bị vô hiệu hóa. Vui lòng liên hệ quản trị viên.";
                    return RedirectToAction(nameof(Login));
                }

                var addLoginResult = await _userManager.AddLoginAsync(user, info);
                if (!addLoginResult.Succeeded && !addLoginResult.Errors.Any(e => e.Code == "LoginAlreadyAssociated"))
                {
                    foreach (var error in addLoginResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    ViewData["ReturnUrl"] = returnUrl;
                    return View(nameof(Login));
                }

                await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
                user.LastLoginAt = DateTime.Now;
                await _userManager.UpdateAsync(user);
                return RedirectToLocal(returnUrl);
            }

            var fullName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email.Split('@')[0];
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                CreatedAt = DateTime.Now,
                LastLoginAt = DateTime.Now,
                IsActive = true,
                Avatar = "/images/default-avatar.png"
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                ViewData["ReturnUrl"] = returnUrl;
                return View(nameof(Login));
            }

            if (!await _roleManager.RoleExistsAsync("User"))
            {
                await _roleManager.CreateAsync(new IdentityRole("User"));
            }
            await _userManager.AddToRoleAsync(user, "User");

            var loginResult = await _userManager.AddLoginAsync(user, info);
            if (!loginResult.Succeeded)
            {
                foreach (var error in loginResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                ViewData["ReturnUrl"] = returnUrl;
                return View(nameof(Login));
            }

            await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
            return RedirectToLocal(returnUrl);
        }

        [HttpGet]
        public IActionResult Register(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError(string.Empty, "Email đã được sử dụng.");
                    return View(model);
                }

                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FullName = model.FullName,
                    CreatedAt = DateTime.Now,
                    IsActive = true,
                    Avatar = "/images/default-avatar.png"
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    if (!await _roleManager.RoleExistsAsync("User"))
                    {
                        await _roleManager.CreateAsync(new IdentityRole("User"));
                    }
                    await _userManager.AddToRoleAsync(user, "User");

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToLocal(returnUrl);
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            return View(model);
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            return View(new EditProfileViewModel
            {
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                Address = user.Address,
                Age = user.Age,
                Gender = user.Gender,
                CurrentAvatar = user.Avatar,
                HasPassword = await _userManager.HasPasswordAsync(user)
            });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(EditProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            model.CurrentAvatar = user.Avatar;
            model.HasPassword = await _userManager.HasPasswordAsync(user);

            var wantsToChangePassword = !string.IsNullOrWhiteSpace(model.NewPassword) ||
                                        !string.IsNullOrWhiteSpace(model.ConfirmNewPassword) ||
                                        !string.IsNullOrWhiteSpace(model.CurrentPassword);

            if (wantsToChangePassword)
            {
                if (string.IsNullOrWhiteSpace(model.NewPassword))
                {
                    ModelState.AddModelError(nameof(model.NewPassword), "Vui lòng nhập mật khẩu mới.");
                }

                if (model.HasPassword && string.IsNullOrWhiteSpace(model.CurrentPassword))
                {
                    ModelState.AddModelError(nameof(model.CurrentPassword), "Vui lòng nhập mật khẩu hiện tại.");
                }
            }

            if (model.AvatarFile != null && model.AvatarFile.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = Path.GetExtension(model.AvatarFile.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError(nameof(model.AvatarFile), "Avatar chỉ chấp nhận JPG, PNG, GIF hoặc WEBP.");
                }

                if (model.AvatarFile.Length > 2 * 1024 * 1024)
                {
                    ModelState.AddModelError(nameof(model.AvatarFile), "Avatar không được vượt quá 2MB.");
                }
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.AvatarFile != null && model.AvatarFile.Length > 0)
            {
                var extension = Path.GetExtension(model.AvatarFile.FileName).ToLowerInvariant();
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "avatars");
                Directory.CreateDirectory(uploadsFolder);

                var fileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.AvatarFile.CopyToAsync(stream);
                }

                if (!string.IsNullOrWhiteSpace(user.Avatar) &&
                    user.Avatar.StartsWith("/uploads/avatars/", StringComparison.OrdinalIgnoreCase))
                {
                    var oldFileName = Path.GetFileName(user.Avatar);
                    var oldPath = Path.Combine(uploadsFolder, oldFileName);
                    if (System.IO.File.Exists(oldPath))
                    {
                        System.IO.File.Delete(oldPath);
                    }
                }

                user.Avatar = "/uploads/avatars/" + fileName;
            }

            user.FullName = model.FullName.Trim();
            user.PhoneNumber = model.PhoneNumber;
            user.Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim();
            user.Age = model.Age;
            user.Gender = string.IsNullOrWhiteSpace(model.Gender) ? null : model.Gender;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                model.CurrentAvatar = user.Avatar;
                return View(model);
            }

            if (wantsToChangePassword)
            {
                IdentityResult passwordResult;

                if (model.HasPassword)
                {
                    passwordResult = await _userManager.ChangePasswordAsync(user, model.CurrentPassword!, model.NewPassword!);
                }
                else
                {
                    passwordResult = await _userManager.AddPasswordAsync(user, model.NewPassword!);
                }

                if (!passwordResult.Succeeded)
                {
                    foreach (var error in passwordResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }

                    model.CurrentAvatar = user.Avatar;
                    model.HasPassword = await _userManager.HasPasswordAsync(user);
                    return View(model);
                }

                await _signInManager.RefreshSignInAsync(user);
                TempData["Success"] = "Cập nhật thông tin cá nhân và đổi mật khẩu thành công.";
                return RedirectToAction(nameof(Profile));
            }

            TempData["Success"] = "Cập nhật thông tin cá nhân thành công.";
            return RedirectToAction(nameof(Profile));
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null || !user.IsActive)
            {
                TempData["Success"] = "Nếu email tồn tại trong hệ thống, mật khẩu mới sẽ được gửi đến email của bạn.";
                return RedirectToAction(nameof(Login));
            }

            var newPassword = GenerateTemporaryPassword();
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await _userManager.ResetPasswordAsync(user, resetToken, newPassword);

            if (!resetResult.Succeeded)
            {
                foreach (var error in resetResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(model);
            }

            var encodedPassword = WebUtility.HtmlEncode(newPassword);
            var body = $@"
                <h2>DACS_Nhóm3 - Mật khẩu mới</h2>
                <p>Xin chào {WebUtility.HtmlEncode(user.FullName)},</p>
                <p>Mật khẩu mới của bạn là:</p>
                <p style='font-size:20px;font-weight:bold;letter-spacing:1px'>{encodedPassword}</p>
                <p>Vui lòng đăng nhập và đổi lại mật khẩu sau khi vào tài khoản.</p>";

            try
            {
                await _emailService.SendEmailAsync(user.Email!, "DACS_Nhóm3 - Mật khẩu mới", body);
                TempData["Success"] = "Mật khẩu mới đã được gửi về email của bạn.";
                return RedirectToAction(nameof(Login));
            }
            catch (Exception ex)
            {
                // Nếu gửi email thất bại, khóa mật khẩu vừa tạo bằng cách báo lỗi để admin cấu hình SMTP.
                ModelState.AddModelError(string.Empty, "Không gửi được email. Vui lòng kiểm tra cấu hình SMTP trong appsettings.json. Chi tiết: " + ex.Message);
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        private static string GenerateTemporaryPassword()
        {
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower = "abcdefghijkmnopqrstuvwxyz";
            const string digits = "23456789";
            const string all = upper + lower + digits;
            var random = Random.Shared;

            var chars = new List<char>
            {
                upper[random.Next(upper.Length)],
                lower[random.Next(lower.Length)],
                digits[random.Next(digits.Length)]
            };

            while (chars.Count < 10)
            {
                chars.Add(all[random.Next(all.Length)]);
            }

            return new string(chars.OrderBy(_ => random.Next()).ToArray());
        }
    }
}
