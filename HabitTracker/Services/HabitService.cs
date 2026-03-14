using HabitTracker.DTOs;
using HabitTracker.Models;
using HabitTracker.Repositories;

namespace HabitTracker.Services
{
    public class HabitService : IHabitService
    {
        private readonly IHabitRepository _repository;

        public HabitService(IHabitRepository repository) => _repository = repository;

        public List<Habit> GetAllHabits() => _repository.GetAllHabits();

        public Habit CreateHabit(string name, string? description)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Habit name cannot be empty.");

            var habit = new Habit
            {
                Name = name.Trim(),
                Description = description?.Trim() ?? string.Empty
            };

            _repository.AddHabit(habit);
            return habit;
        }

        public bool DeleteHabit(Guid id) => _repository.DeleteHabit(id);

        public bool MarkHabitCompleted(Guid habitId, DateTime date)
        {
            if (_repository.GetHabitLog(habitId, date) is { } existing)
            {
                if (existing.IsCompleted)
                    return true;

                existing.IsCompleted = true;
                _repository.AddOrUpdateHabitLog(existing);
                return true;
            }

            var log = new HabitLog
            {
                HabitId = habitId,
                Date = date.Date,
                IsCompleted = true
            };

            try
            {
                _repository.AddOrUpdateHabitLog(log);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        public bool UnmarkHabitCompleted(Guid habitId, DateTime date)
        {
            if (_repository.GetHabitLog(habitId, date) is not { IsCompleted: true } existing)
                return false;

            existing.IsCompleted = false;
            _repository.AddOrUpdateHabitLog(existing);
            return true;
        }

        public int GetCompletionCount(Guid habitId) =>
            _repository.GetHabitLogsByHabitId(habitId)
                .Count(log => log.IsCompleted && !log.IsDeletedHabit);

        public bool IsHabitCompletedOnDate(Guid habitId, DateTime date) =>
            _repository.GetHabitLog(habitId, date) is { IsCompleted: true, IsDeletedHabit: false };

        public List<HabitLog> GetHabitLogs(Guid habitId) =>
            _repository.GetHabitLogsByHabitId(habitId);

        public List<HabitStreak> GetHabitStreaks(Guid habitId)
        {
            var completedDates = _repository.GetHabitLogsByHabitId(habitId)
                .Where(log => log.IsCompleted && !log.IsDeletedHabit)
                .Select(log => log.Date.Date)
                .Distinct()
                .OrderBy(day => day)
                .ToList();

            return BuildStreaks(habitId, completedDates);
        }

        public Dictionary<Guid, List<HabitStreak>> GetAllHabitStreaks()
        {
            return _repository.GetAllHabits()
                .ToDictionary(
                    habit => habit.Id,
                    habit => GetHabitStreaks(habit.Id));
        }

        private static List<HabitStreak> BuildStreaks(Guid habitId, List<DateTime> sortedDates)
        {
            if (sortedDates.Count == 0)
                return [];

            var streaks = new List<HabitStreak>();
            var streakStart = sortedDates[0];
            var prev = sortedDates[0];

            for (int i = 1; i < sortedDates.Count; i++)
            {
                if ((sortedDates[i] - prev).Days == 1)
                {
                    prev = sortedDates[i];
                    continue;
                }

                streaks.Add(new HabitStreak { HabitId = habitId, StartDate = streakStart, EndDate = prev });
                streakStart = sortedDates[i];
                prev = sortedDates[i];
            }

            streaks.Add(new HabitStreak { HabitId = habitId, StartDate = streakStart, EndDate = prev });
            return streaks;
        }
    }
}
