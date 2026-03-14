namespace HabitTracker.DTOs;

public class HabitStreak
{
    public Guid HabitId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int Days => (EndDate.Date - StartDate.Date).Days + 1;
}
