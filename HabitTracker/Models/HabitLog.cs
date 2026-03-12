namespace HabitTracker.Models;

public class HabitLog
{
    public Guid Id { get; set; }
    public Guid HabitId { get; set; }
    public DateTime Date { get; set; }
    public bool IsCompleted { get; set; }

    public HabitLog()
    {
        Id = Guid.NewGuid();
        Date = DateTime.UtcNow;
    }
}