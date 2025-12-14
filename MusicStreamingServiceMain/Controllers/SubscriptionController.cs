using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicStreamingService.Data;
using MusicStreamingService.Models;
using System.Security.Claims;

namespace MusicStreamingService.Controllers
{
    public class SubscriptionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SubscriptionController> _logger;
        private readonly IConfiguration _configuration;

        public SubscriptionController(
            ApplicationDbContext context,
            ILogger<SubscriptionController> logger,
            IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        // GET: /Subscription/Plans - Страница с планами подписки
        public IActionResult Plans()
        {
            return View();
        }

        // GET: /Subscription/Create - Оформление подписки
        [Authorize]
        public async Task<IActionResult> Create()
        {
            // Проверяем, есть ли активная подписка
            var username = User.Identity.Name;
            var user = await _context.Users
                .Include(u => u.Subscriptions)
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
                return RedirectToAction("Login", "Account");

            // Проверяем активную подписку
            var activeSubscription = user.Subscriptions?
                .FirstOrDefault(s => s.IsActivated && s.EndDate > DateTime.Now);

            if (activeSubscription != null)
            {
                TempData["InfoMessage"] = "У вас уже есть активная подписка!";
                return RedirectToAction("Profile", "Account");
            }

            return View(new SubscriptionViewModel());
        }

        // POST: /Subscription/Create - Обработка оформления подписки
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SubscriptionViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var username = User.Identity.Name;
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
                return RedirectToAction("Login", "Account");

            try
            {
                // Генерируем ID транзакции
                string transactionId = $"SUB_{DateTime.Now:yyyyMMddHHmmss}_{user.Id}_{Guid.NewGuid().ToString()[..8]}";

                // Для тестового режима имитируем успешную оплату
                bool paymentSuccess = await ProcessSberPayment(model, transactionId);

                if (!paymentSuccess)
                {
                    ModelState.AddModelError("", "Ошибка оплаты. Проверьте данные карты.");
                    return View(model);
                }

                // Создаем подписку
                var subscription = new Subscription
                {
                    UserId = user.Id,
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddMonths(1),
                    IsActivated = true,
                    Amount = model.Amount,
                    TransactionId = transactionId,
                    Status = "paid"
                };

                _context.Subscriptions.Add(subscription);

                // Обновляем роль пользователя
                user.Role = UserRole.Subscriber;
                _context.Users.Update(user);

                await _context.SaveChangesAsync();

                _logger.LogInformation("Пользователь {Username} оформил подписку. Transaction ID: {TransactionId}",
                    user.Username, transactionId);

                TempData["SuccessMessage"] = "Подписка успешно оформлена! Теперь вы можете создавать плейлисты.";
                return RedirectToAction("Success", new { id = subscription.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при оформлении подписки");
                ModelState.AddModelError("", "Произошла ошибка при оформлении подписки. Попробуйте позже.");
                return View(model);
            }
        }

        // GET: /Subscription/Success/{id} - Страница успешной оплаты
        [Authorize]
        public async Task<IActionResult> Success(int id)
        {
            var subscription = await _context.Subscriptions
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Id == id && s.User.Username == User.Identity.Name);

            if (subscription == null)
                return NotFound();

            return View(subscription);
        }

        // GET: /Subscription/My - Мои подписки
        [Authorize]
        public async Task<IActionResult> My()
        {
            var username = User.Identity.Name;
            var user = await _context.Users
                .Include(u => u.Subscriptions)
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
                return RedirectToAction("Login", "Account");

            var subscriptions = user.Subscriptions
                .OrderByDescending(s => s.StartDate)
                .ToList();

            // Проверяем активную подписку
            var activeSubscription = subscriptions
                .FirstOrDefault(s => s.IsActivated && s.EndDate > DateTime.Now);

            ViewBag.ActiveSubscription = activeSubscription;

            return View(subscriptions);
        }

        // GET: /Subscription/Terms - Условия подписки
        public IActionResult Terms()
        {
            return View();
        }

        // Вспомогательный метод для обработки платежа
        private async Task<bool> ProcessSberPayment(SubscriptionViewModel model, string transactionId)
        {
            try
            {
                // Тестовые данные для успешной оплаты
                string testCardNumber = "2200 0000 0000 0004"; // Тестовая карта Сбера
                bool isTestMode = _configuration.GetValue<bool>("Sberbank:TestMode", true);

                if (isTestMode)
                {
                    // В тестовом режиме всегда успех
                    _logger.LogInformation("Тестовый платеж успешен. Transaction ID: {TransactionId}", transactionId);

                    // Логируем тестовые данные
                    _logger.LogDebug("Тестовый платеж: Card ending with {CardEnding}",
                        model.CardNumber[^4..]);

                    return true;
                }
                else
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке платежа");
                return false;
            }
        }

        // Фоновая задача для проверки истечения подписок
        public static async Task CheckExpiredSubscriptions(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SubscriptionController>>();

            try
            {
                var expiredSubscriptions = await context.Subscriptions
                    .Include(s => s.User)
                    .Where(s => s.IsActivated && s.EndDate <= DateTime.Now)
                    .ToListAsync();

                foreach (var subscription in expiredSubscriptions)
                {
                    subscription.IsActivated = false;

                    // Возвращаем пользователю роль User, если нет других активных подписок
                    var user = subscription.User;
                    var hasOtherActive = await context.Subscriptions
                        .AnyAsync(s => s.UserId == user.Id && s.IsActivated && s.EndDate > DateTime.Now);

                    if (!hasOtherActive)
                    {
                        user.Role = UserRole.User;
                        context.Users.Update(user);
                        logger.LogInformation("Подписка пользователя {Username} истекла. Роль изменена на User.",
                            user.Username);
                    }
                }

                if (expiredSubscriptions.Any())
                {
                    await context.SaveChangesAsync();
                    logger.LogInformation("Проверены истекшие подписки: {Count} подписок деактивировано",
                        expiredSubscriptions.Count);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при проверке истекших подписок");
            }
        }
    }
}