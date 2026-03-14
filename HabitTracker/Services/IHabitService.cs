using HabitTracker.DTOs;
using HabitTracker.Models;

namespace HabitTracker.Services
{
    public interface IHabitService
    {
        List<Habit> GetAllHabits();
        Habit CreateHabit(string name, string? description);
        bool DeleteHabit(Guid id);
        bool MarkHabitCompleted(Guid habitId, DateTime date);
        bool UnmarkHabitCompleted(Guid habitId, DateTime date);
        int GetCompletionCount(Guid habitId);
        bool IsHabitCompletedOnDate(Guid habitId, DateTime date);
        List<HabitLog> GetHabitLogs(Guid habitId);
        List<HabitStreak> GetHabitStreaks(Guid habitId);
        Dictionary<Guid, List<HabitStreak>> GetAllHabitStreaks();
    }
}
