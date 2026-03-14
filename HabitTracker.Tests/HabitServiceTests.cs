using HabitTracker.Models;
using HabitTracker.Repositories;
using HabitTracker.Services;

namespace HabitTracker.Tests;

public class HabitServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly JsonHabitRepository _repository;
    private readonly HabitService _service;

    public HabitServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"HabitServiceTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        _repository = new JsonHabitRepository(
            Path.Combine(_tempDir, "habits.json"),
            Path.Combine(_tempDir, "logs.json"));
        _service = new HabitService(_repository);
    }

    public void Dispose()
    {
        _repository.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    #region CreateHabit

    [Fact]
    public void CreateHabit_ReturnsHabitWithTrimmedFields()
    {
        var habit = _service.CreateHabit("  Running  ", "  Daily run  ");

        Assert.Equal("Running", habit.Name);
        Assert.Equal("Daily run", habit.Description);
        Assert.NotEqual(Guid.Empty, habit.Id);
    }

    [Fact]
    public void CreateHabit_NullDescription_DefaultsToEmpty()
    {
        var habit = _service.CreateHabit("Reading", null);

        Assert.Equal(string.Empty, habit.Description);
    }

    [Fact]
    public void CreateHabit_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() => _service.CreateHabit("  ", "desc"));
    }

    #endregion

    #region DeleteHabit

    [Fact]
    public void DeleteHabit_ExistingHabit_ReturnsTrue()
    {
        var habit = _service.CreateHabit("Running", null);

        Assert.True(_service.DeleteHabit(habit.Id));
    }

    [Fact]
    public void DeleteHabit_NonExistentHabit_ReturnsFalse()
    {
        Assert.False(_service.DeleteHabit(Guid.NewGuid()));
    }

    #endregion

    #region MarkHabitCompleted

    [Fact]
    public void MarkHabitCompleted_ValidHabit_ReturnsTrue()
    {
        var habit = _service.CreateHabit("Running", null);

        Assert.True(_service.MarkHabitCompleted(habit.Id, new DateTime(2025, 3, 1)));
    }

    [Fact]
    public void MarkHabitCompleted_NonExistentHabit_ReturnsFalse()
    {
        Assert.False(_service.MarkHabitCompleted(Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void MarkHabitCompleted_SameDateTwice_IsIdempotent()
    {
        var habit = _service.CreateHabit("Running", null);
        var date = new DateTime(2025, 3, 1);

        _service.MarkHabitCompleted(habit.Id, date);
        _service.MarkHabitCompleted(habit.Id, date);

        Assert.Equal(1, _service.GetCompletionCount(habit.Id));
    }

    #endregion

    #region UnmarkHabitCompleted

    [Fact]
    public void UnmarkHabitCompleted_PreviouslyCompleted_ReturnsTrue()
    {
        var habit = _service.CreateHabit("Running", null);
        var date = new DateTime(2025, 3, 1);
        _service.MarkHabitCompleted(habit.Id, date);

        Assert.True(_service.UnmarkHabitCompleted(habit.Id, date));
        Assert.False(_service.IsHabitCompletedOnDate(habit.Id, date));
    }

    [Fact]
    public void UnmarkHabitCompleted_NeverCompleted_ReturnsFalse()
    {
        var habit = _service.CreateHabit("Running", null);

        Assert.False(_service.UnmarkHabitCompleted(habit.Id, new DateTime(2025, 3, 1)));
    }

    #endregion

    #region GetCompletionCount

    [Fact]
    public void GetCompletionCount_CountsOnlyCompletedLogs()
    {
        var habit = _service.CreateHabit("Running", null);
        _service.MarkHabitCompleted(habit.Id, new DateTime(2025, 3, 1));
        _service.MarkHabitCompleted(habit.Id, new DateTime(2025, 3, 2));
        _service.MarkHabitCompleted(habit.Id, new DateTime(2025, 3, 3));
        _service.UnmarkHabitCompleted(habit.Id, new DateTime(2025, 3, 2));

        Assert.Equal(2, _service.GetCompletionCount(habit.Id));
    }

    [Fact]
    public void GetCompletionCount_ExcludesDeletedHabitLogs()
    {
        var habit = _service.CreateHabit("Running", null);
        _service.MarkHabitCompleted(habit.Id, new DateTime(2025, 3, 1));
        _service.DeleteHabit(habit.Id);

        Assert.Equal(0, _service.GetCompletionCount(habit.Id));
    }

    #endregion

    #region IsHabitCompletedOnDate

    [Fact]
    public void IsHabitCompletedOnDate_CompletedDate_ReturnsTrue()
    {
        var habit = _service.CreateHabit("Running", null);
        var date = new DateTime(2025, 3, 1);
        _service.MarkHabitCompleted(habit.Id, date);

        Assert.True(_service.IsHabitCompletedOnDate(habit.Id, date));
    }

    [Fact]
    public void IsHabitCompletedOnDate_NotCompletedDate_ReturnsFalse()
    {
        var habit = _service.CreateHabit("Running", null);

        Assert.False(_service.IsHabitCompletedOnDate(habit.Id, new DateTime(2025, 3, 1)));
    }

    #endregion

    #region GetHabitLogs

    [Fact]
    public void GetHabitLogs_ReturnsLogsForSpecificHabit()
    {
        var h1 = _service.CreateHabit("Running", null);
        var h2 = _service.CreateHabit("Reading", null);
        _service.MarkHabitCompleted(h1.Id, new DateTime(2025, 3, 1));
        _service.MarkHabitCompleted(h1.Id, new DateTime(2025, 3, 2));
        _service.MarkHabitCompleted(h2.Id, new DateTime(2025, 3, 1));

        var logs = _service.GetHabitLogs(h1.Id);

        Assert.Equal(2, logs.Count);
        Assert.All(logs, log => Assert.Equal(h1.Id, log.HabitId));
    }

    [Fact]
    public void GetHabitLogs_NoLogs_ReturnsEmptyList()
    {
        var habit = _service.CreateHabit("Running", null);

        Assert.Empty(_service.GetHabitLogs(habit.Id));
    }

    #endregion

    #region GetHabitStreaks

    [Fact]
    public void GetHabitStreaks_ConsecutiveDays_ReturnsSingleStreak()
    {
        var habit = _service.CreateHabit("Running", null);
        _service.MarkHabitCompleted(habit.Id, new DateTime(2025, 3, 1));
        _service.MarkHabitCompleted(habit.Id, new DateTime(2025, 3, 2));
        _service.MarkHabitCompleted(habit.Id, new DateTime(2025, 3, 3));

        var streaks = _service.GetHabitStreaks(habit.Id);

        Assert.Single(streaks);
        Assert.Equal(new DateTime(2025, 3, 1), streaks[0].StartDate);
        Assert.Equal(new DateTime(2025, 3, 3), streaks[0].EndDate);
        Assert.Equal(3, streaks[0].Days);
    }

    [Fact]
    public void GetHabitStreaks_GapInDays_ReturnsMultipleStreaks()
    {
        var habit = _service.CreateHabit("Running", null);
        _service.MarkHabitCompleted(habit.Id, new DateTime(2025, 3, 1));
        _service.MarkHabitCompleted(habit.Id, new DateTime(2025, 3, 2));
        _service.MarkHabitCompleted(habit.Id, new DateTime(2025, 3, 5));
        _service.MarkHabitCompleted(habit.Id, new DateTime(2025, 3, 6));

        var streaks = _service.GetHabitStreaks(habit.Id);

        Assert.Equal(2, streaks.Count);
        Assert.Equal(2, streaks[0].Days);
        Assert.Equal(2, streaks[1].Days);
        Assert.Equal(new DateTime(2025, 3, 5), streaks[1].StartDate);
    }

    [Fact]
    public void GetHabitStreaks_NoCompletions_ReturnsEmptyList()
    {
        var habit = _service.CreateHabit("Running", null);

        Assert.Empty(_service.GetHabitStreaks(habit.Id));
    }

    [Fact]
    public void GetHabitStreaks_ExcludesDeletedHabitLogs()
    {
        var habit = _service.CreateHabit("Running", null);
        _service.MarkHabitCompleted(habit.Id, new DateTime(2025, 3, 1));
        _service.MarkHabitCompleted(habit.Id, new DateTime(2025, 3, 2));
        _service.DeleteHabit(habit.Id);

        Assert.Empty(_service.GetHabitStreaks(habit.Id));
    }

    #endregion

    #region GetAllHabitStreaks

    [Fact]
    public void GetAllHabitStreaks_ReturnsStreaksForAllHabits()
    {
        var h1 = _service.CreateHabit("Running", null);
        var h2 = _service.CreateHabit("Reading", null);
        _service.MarkHabitCompleted(h1.Id, new DateTime(2025, 3, 1));
        _service.MarkHabitCompleted(h1.Id, new DateTime(2025, 3, 2));
        _service.MarkHabitCompleted(h2.Id, new DateTime(2025, 3, 5));

        var allStreaks = _service.GetAllHabitStreaks();

        Assert.Equal(2, allStreaks.Count);
        Assert.Single(allStreaks[h1.Id]);
        Assert.Equal(2, allStreaks[h1.Id][0].Days);
        Assert.Single(allStreaks[h2.Id]);
        Assert.Equal(1, allStreaks[h2.Id][0].Days);
    }

    [Fact]
    public void GetAllHabitStreaks_NoHabits_ReturnsEmptyDictionary()
    {
        Assert.Empty(_service.GetAllHabitStreaks());
    }

    #endregion
}
