namespace MusicStreamingService.Models
{
    public class Subscription
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActivated { get; set; }

        public virtual User User { get; set; } = null!;
    }
}