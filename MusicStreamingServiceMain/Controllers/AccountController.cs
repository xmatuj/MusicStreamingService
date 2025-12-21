using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicStreamingService.Data;
using MusicStreamingService.Models;
using System.Security.Claims;

namespace MusicStreamingService.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AccountController> _logger;

        public AccountController(ApplicationDbContext context, ILogger<AccountController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: /Account/Register
        public IActionResult Register()
        {
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Проверяем, существует ли пользователь с таким именем
                    if (await _context.Users.AnyAsync(u => u.Username == model.Username))
                    {
                        ModelState.AddModelError("Username", "Пользователь с таким именем уже существует");
                        return View(model);
                    }

                    // Проверяем, существует ли пользователь с таким email
                    if (await _context.Users.AnyAsync(u => u.Email == model.Email))
                    {
                        ModelState.AddModelError("Email", "Пользователь с таким email уже существует");
                        return View(model);
                    }

                    // Создаем нового пользователя (всегда как обычный User)
                    var user = new User
                    {
                        Username = model.Username,
                        Email = model.Email,
                        Role = UserRole.User // Всегда обычный пользователь
                    };

                    user.SetPassword(model.Password);

                    // Сохраняем пользователя в базу данных
                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();

                    // Автоматически входим после регистрации
                    await SignInUser(user);

                    _logger.LogInformation("Пользователь {Username} успешно зарегистрирован", user.Username);

                    return RedirectToAction("Index", "Home");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при регистрации пользователя");
                    ModelState.AddModelError("", "Произошла ошибка при регистрации. Попробуйте позже.");
                }
            }

            return View(model);
        }

        // GET: /Account/Login
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Ищем пользователя по имени пользователя или email
                    var user = await _context.Users.FirstOrDefaultAsync(u =>
                        u.Username == model.UsernameOrEmail || u.Email == model.UsernameOrEmail);

                    if (user == null || !user.VerifyPassword(model.Password))
                    {
                        ModelState.AddModelError("", "Неверное имя пользователя или пароль");
                        return View(model);
                    }

                    // Входим в систему
                    await SignInUser(user, model.RememberMe);

                    _logger.LogInformation("Пользователь {Username} успешно вошел в систему", user.Username);

                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }

                    return RedirectToAction("Index", "Home");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при входе пользователя");
                    ModelState.AddModelError("", "Произошла ошибка при входе. Попробуйте позже.");
                }
            }

            return View(model);
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            _logger.LogInformation("Пользователь вышел из системы");
            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/Profile
        public async Task<IActionResult> Profile()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login");
            }

            var username = User.Identity.Name;
            var user = await _context.Users
                .Include(u => u.Subscriptions)
                .Include(u => u.Playlists)
                    .ThenInclude(p => p.PlaylistTracks)
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Login");
            }

            // Загружаем треки музыканта, если он музыкант
            if (user.Role == UserRole.Musician)
            {
                var musicianTracks = await _context.Tracks
                    .Include(t => t.Artist)
                    .Include(t => t.Genre)
                    .Include(t => t.Album)
                    .Include(t => t.Moderations)
                    .Where(t => t.UploadedByUserId == user.Id) // Используем UploadedByUserId
                    .OrderByDescending(t => t.Id)
                    .ToListAsync();

                ViewBag.MusicianTracks = musicianTracks;
            }
            else
            {
                ViewBag.MusicianTracks = new List<Track>();
            }

            await CheckAndUpdateUserRole(user);
            return View(user);
        }

        // Метод для проверки и обновления роли пользователя
        private async Task CheckAndUpdateUserRole(User user)
        {
            // Если пользователь подписчик, но нет активной подписки - сбрасываем роль
            if (user.Role == UserRole.Subscriber)
            {
                bool hasActiveSubscription = user.Subscriptions?.Any(s =>
                    s.IsActivated && s.EndDate > DateTime.Now) == true;

                if (!hasActiveSubscription)
                {
                    user.Role = UserRole.User;
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();

                    // Обновляем куки аутентификации
                    await UpdateAuthenticationCookies(user);
                }
            }
        }

        // Обновляем куки аутентификации
        private async Task UpdateAuthenticationCookies(User user)
        {
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, user.Username),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Role, user.Role.ToString()),
        new Claim("UserId", user.Id.ToString())
    };

            var claimsIdentity = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity));
        }

        // Вспомогательный метод для входа пользователя
        private async Task SignInUser(User user, bool rememberMe = false)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim("UserId", user.Id.ToString()),
                new Claim("UserRole", user.Role.ToString())
            };

            var claimsIdentity = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(12)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);
        }
    }
}