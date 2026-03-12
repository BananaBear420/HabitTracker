namespace HabitTracker.Models;

public class Habit
{
   public Guid Id { get; set; }
   public string Name { get; set; } = string.Empty;
   public string Description { get; set; } = string.Empty;
   public DateTime CreatedAt { get; set; }

   public Habit()
   {
      Id = Guid.NewGuid();
      CreatedAt = DateTime.UtcNow;
   }
}
