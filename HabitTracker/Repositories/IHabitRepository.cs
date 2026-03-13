using HabitTracker.Models;

namespace HabitTracker.Repositories
{
    public interface IHabitRepository
    {
        // Create
        void AddHabit(Habit habit);
        // Read
        List<Habit> GetAllHabits();
        Habit? GetHabitById(Guid id);
        // Update
        bool UpdateHabit(Habit habit);
        // Delete
        bool DeleteHabit(Guid id);
        // Create or Update Logs
        void AddOrUpdateHabitLog(HabitLog log);
        // Read Logs
        List<HabitLog> GetHabitLogs();
    }
}