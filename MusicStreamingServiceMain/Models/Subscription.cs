using System.ComponentModel.DataAnnotations;

namespace MusicStreamingService.Models
{
    public class Subscription
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActivated { get; set; } = false;
        public string? TransactionId { get; set; } // ID транзакции от Сбера
        public decimal Amount { get; set; } = 399.00m; // Стоимость подписки
        public string? Status { get; set; } = "pending"; // pending, paid, failed

        public virtual User User { get; set; } = null!;
    }

    // Модель для оформления подписки
    public class SubscriptionViewModel
    {
        [Display(Name = "План подписки")]
        public string PlanName { get; set; } = "Премиум";

        [Display(Name = "Стоимость")]
        public decimal Amount { get; set; } = 399.00m;

        [Display(Name = "Период")]
        public string Period { get; set; } = "1 месяц";

        [Display(Name = "Согласен с условиями")]
        [Required(ErrorMessage = "Необходимо согласие с условиями")]
        public bool AgreeToTerms { get; set; }

        [Display(Name = "Имя на карте")]
        [Required(ErrorMessage = "Введите имя на карте")]
        public string CardName { get; set; } = string.Empty;

        [Display(Name = "Номер карты")]
        [Required(ErrorMessage = "Введите номер карты")]
        [CreditCard(ErrorMessage = "Неверный номер карты")]
        public string CardNumber { get; set; } = string.Empty;

        [Display(Name = "Месяц/Год")]
        [Required(ErrorMessage = "Введите срок действия")]
        [RegularExpression(@"^(0[1-9]|1[0-2])\/[0-9]{2}$", ErrorMessage = "Формат: ММ/ГГ")]
        public string ExpiryDate { get; set; } = string.Empty;

        [Display(Name = "CVV")]
        [Required(ErrorMessage = "Введите CVV")]
        [StringLength(4, MinimumLength = 3, ErrorMessage = "CVV 3-4 цифры")]
        public string CVV { get; set; } = string.Empty;
    }
}