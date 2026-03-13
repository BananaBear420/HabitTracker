using HabitTracker.Models;
using HabitTracker.Repositories;

namespace HabitTracker.Tests;

public class JsonHabitRepositoryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _habitsFile;
    private readonly string _logsFile;

    public JsonHabitRepositoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"HabitTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        _habitsFile = Path.Combine(_tempDir, "habits.json");
        _logsFile = Path.Combine(_tempDir, "logs.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private JsonHabitRepository CreateRepo() => new(_habitsFile, _logsFile);

    private Habit CreateHabit(string name = "Exercise", string description = "Daily workout") =>
        new() { Name = name, Description = description };

    private HabitLog CreateLog(Guid habitId, DateTime? date = null) =>
        new() { HabitId = habitId, Date = date ?? DateTime.UtcNow, IsCompleted = true };

    #region AddHabit

    /// <summary>
    /// Verifies the write-then-read round-trip: a habit can be stored and
    /// later retrieved with the correct data.
    /// </summary>
    [Fact]
    public void AddHabit_PersistsHabitToFile()
    {
        using var repo = CreateRepo();
        var habit = CreateHabit();

        repo.AddHabit(habit);

        var retrieved = repo.GetHabitById(habit.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(habit.Name, retrieved.Name);
        Assert.Equal(habit.Description, retrieved.Description);
    }

    /// <summary>
    /// Duplicate IDs would silently overwrite data, so the repository
    /// must reject them.
    /// </summary>
    [Fact]
    public void AddHabit_ThrowsOnDuplicateId()
    {
        using var repo = CreateRepo();
        var habit = CreateHabit();
        repo.AddHabit(habit);

        var duplicate = new Habit { Id = habit.Id, Name = "Duplicate" };

        var ex = Assert.Throws<InvalidOperationException>(() => repo.AddHabit(duplicate));
        Assert.Contains(habit.Id.ToString(), ex.Message);
    }

    #endregion

    #region GetAllHabits

    /// <summary>
    /// Verifies that all added habits are returned.
    /// </summary>
    [Fact]
    public void GetAllHabits_ReturnsAllAddedHabits()
    {
        using var repo = CreateRepo();
        var h1 = CreateHabit("Running");
        var h2 = CreateHabit("Reading");
        repo.AddHabit(h1);
        repo.AddHabit(h2);

        var habits = repo.GetAllHabits();

        Assert.Equal(2, habits.Count);
    }

    /// <summary>
    /// On first launch no file exists yet — the repository should return
    /// an empty list rather than throwing.
    /// </summary>
    [Fact]
    public void GetAllHabits_ReturnsEmptyListWhenNoFileExists()
    {
        using var repo = CreateRepo();

        var habits = repo.GetAllHabits();

        Assert.Empty(habits);
    }

    #endregion

    #region GetHabitById

    /// <summary>
    /// Callers need to distinguish "not found" from a real habit, so the
    /// repository must return null for unknown IDs.
    /// </summary>
    [Fact]
    public void GetHabitById_ReturnsNullWhenNotFound()
    {
        using var repo = CreateRepo();

        var result = repo.GetHabitById(Guid.NewGuid());

        Assert.Null(result);
    }

    #endregion

    #region UpdateHabit

    /// <summary>
    /// A successful update must persist the new values and return true.
    /// </summary>
    [Fact]
    public void UpdateHabit_ReturnsTrueAndPersistsChanges()
    {
        using var repo = CreateRepo();
        var habit = CreateHabit("Old Name");
        repo.AddHabit(habit);

        habit.Name = "New Name";
        habit.Description = "Updated description";
        var result = repo.UpdateHabit(habit);

        Assert.True(result);
        var updated = repo.GetHabitById(habit.Id);
        Assert.NotNull(updated);
        Assert.Equal("New Name", updated.Name);
        Assert.Equal("Updated description", updated.Description);
    }

    /// <summary>
    /// Updating a non-existent habit must return false to signal a no-op.
    /// </summary>
    [Fact]
    public void UpdateHabit_ReturnsFalseWhenHabitDoesNotExist()
    {
        using var repo = CreateRepo();
        var habit = CreateHabit();

        var result = repo.UpdateHabit(habit);

        Assert.False(result);
    }

    #endregion

    #region DeleteHabit

    /// <summary>
    /// After deletion the habit must no longer be retrievable.
    /// </summary>
    [Fact]
    public void DeleteHabit_RemovesHabitAndReturnsTrue()
    {
        using var repo = CreateRepo();
        var habit = CreateHabit();
        repo.AddHabit(habit);

        var result = repo.DeleteHabit(habit.Id);

        Assert.True(result);
        Assert.Null(repo.GetHabitById(habit.Id));
    }

    /// <summary>
    /// Deleting a non-existent ID should be a safe no-op returning false.
    /// </summary>
    [Fact]
    public void DeleteHabit_ReturnsFalseWhenHabitDoesNotExist()
    {
        using var repo = CreateRepo();

        var result = repo.DeleteHabit(Guid.NewGuid());

        Assert.False(result);
    }

    /// <summary>
    /// Logs are historical and must not be removed on deletion, but they
    /// need to be flagged with IsDeletedHabit so the UI can handle them.
    /// </summary>
    [Fact]
    public void DeleteHabit_MarksAssociatedLogsAsDeletedHabit()
    {
        using var repo = CreateRepo();
        var habit = CreateHabit();
        repo.AddHabit(habit);

        var log1 = CreateLog(habit.Id, new DateTime(2025, 1, 1));
        var log2 = CreateLog(habit.Id, new DateTime(2025, 1, 2));
        repo.AddOrUpdateHabitLog(log1);
        repo.AddOrUpdateHabitLog(log2);

        repo.DeleteHabit(habit.Id);

        var logs = repo.GetHabitLogs();
        Assert.All(logs, log => Assert.True(log.IsDeletedHabit));
    }

    #endregion

    #region AddOrUpdateHabitLog

    /// <summary>
    /// A log referencing a non-existent habit is invalid — the repository
    /// must reject it to maintain referential integrity.
    /// </summary>
    [Fact]
    public void AddOrUpdateHabitLog_ThrowsWhenHabitDoesNotExist()
    {
        using var repo = CreateRepo();
        var orphanLog = CreateLog(Guid.NewGuid());

        var ex = Assert.Throws<InvalidOperationException>(() => repo.AddOrUpdateHabitLog(orphanLog));
        Assert.Contains("does not exist", ex.Message);
    }

    /// <summary>
    /// Happy path: a new log is persisted for an existing habit.
    /// </summary>
    [Fact]
    public void AddOrUpdateHabitLog_AddsNewLogForExistingHabit()
    {
        using var repo = CreateRepo();
        var habit = CreateHabit();
        repo.AddHabit(habit);

        var log = CreateLog(habit.Id, new DateTime(2025, 3, 15));
        repo.AddOrUpdateHabitLog(log);

        var logs = repo.GetHabitLogsByHabitId(habit.Id);
        Assert.Single(logs);
        Assert.Equal(log.Id, logs[0].Id);
    }

    /// <summary>
    /// Passing a log with the same Id should overwrite the old entry
    /// (upsert semantics).
    /// </summary>
    [Fact]
    public void AddOrUpdateHabitLog_UpdatesExistingLogById()
    {
        using var repo = CreateRepo();
        var habit = CreateHabit();
        repo.AddHabit(habit);

        var log = CreateLog(habit.Id);
        log.IsCompleted = false;
        repo.AddOrUpdateHabitLog(log);

        log.IsCompleted = true;
        repo.AddOrUpdateHabitLog(log);

        var logs = repo.GetHabitLogsByHabitId(habit.Id);
        Assert.Single(logs);
        Assert.True(logs[0].IsCompleted);
    }

    #endregion

    #region GetHabitLogs

    /// <summary>
    /// Verifies that all logs across all habits are returned.
    /// </summary>
    [Fact]
    public void GetHabitLogs_ReturnsAllLogs()
    {
        using var repo = CreateRepo();
        var h1 = CreateHabit("A");
        var h2 = CreateHabit("B");
        repo.AddHabit(h1);
        repo.AddHabit(h2);

        repo.AddOrUpdateHabitLog(CreateLog(h1.Id, new DateTime(2025, 1, 1)));
        repo.AddOrUpdateHabitLog(CreateLog(h1.Id, new DateTime(2025, 1, 2)));
        repo.AddOrUpdateHabitLog(CreateLog(h2.Id, new DateTime(2025, 1, 1)));

        var logs = repo.GetHabitLogs();

        Assert.Equal(3, logs.Count);
    }

    /// <summary>
    /// When no log file exists, the repository should return an empty list.
    /// </summary>
    [Fact]
    public void GetHabitLogs_ReturnsEmptyListWhenNoLogsExist()
    {
        using var repo = CreateRepo();

        var logs = repo.GetHabitLogs();

        Assert.Empty(logs);
    }

    #endregion

    #region GetHabitLogsByHabitId

    /// <summary>
    /// Filtering by HabitId must return only the logs belonging to that
    /// specific habit.
    /// </summary>
    [Fact]
    public void GetHabitLogsByHabitId_ReturnsOnlyMatchingLogs()
    {
        using var repo = CreateRepo();
        var h1 = CreateHabit("A");
        var h2 = CreateHabit("B");
        repo.AddHabit(h1);
        repo.AddHabit(h2);

        repo.AddOrUpdateHabitLog(CreateLog(h1.Id, new DateTime(2025, 1, 1)));
        repo.AddOrUpdateHabitLog(CreateLog(h1.Id, new DateTime(2025, 1, 2)));
        repo.AddOrUpdateHabitLog(CreateLog(h2.Id, new DateTime(2025, 1, 1)));

        var logsForH1 = repo.GetHabitLogsByHabitId(h1.Id);

        Assert.Equal(2, logsForH1.Count);
        Assert.All(logsForH1, l => Assert.Equal(h1.Id, l.HabitId));
    }

    #endregion

    #region GetHabitLog (by habitId + date)

    /// <summary>
    /// The date-based lookup must find the correct log for the given
    /// habit + date combination.
    /// </summary>
    [Fact]
    public void GetHabitLog_ReturnsMatchingLog()
    {
        using var repo = CreateRepo();
        var habit = CreateHabit();
        repo.AddHabit(habit);

        var targetDate = new DateTime(2025, 6, 15);
        var log = CreateLog(habit.Id, targetDate);
        repo.AddOrUpdateHabitLog(log);

        var result = repo.GetHabitLog(habit.Id, targetDate);

        Assert.NotNull(result);
        Assert.Equal(log.Id, result.Id);
    }

    /// <summary>
    /// Returns null when no log exists for the requested habit+date pair.
    /// </summary>
    [Fact]
    public void GetHabitLog_ReturnsNullWhenNoMatch()
    {
        using var repo = CreateRepo();

        var result = repo.GetHabitLog(Guid.NewGuid(), new DateTime(2025, 1, 1));

        Assert.Null(result);
    }

    /// <summary>
    /// The lookup uses Date-only comparison. A log stored at 10:30 AM must
    /// be found when queried at 11:45 PM of the same day.
    /// </summary>
    [Fact]
    public void GetHabitLog_MatchesByDateIgnoringTimeComponent()
    {
        using var repo = CreateRepo();
        var habit = CreateHabit();
        repo.AddHabit(habit);

        var logDate = new DateTime(2025, 6, 15, 10, 30, 0);
        repo.AddOrUpdateHabitLog(CreateLog(habit.Id, logDate));

        var queryDate = new DateTime(2025, 6, 15, 23, 45, 59);
        var result = repo.GetHabitLog(habit.Id, queryDate);

        Assert.NotNull(result);
    }

    #endregion

    #region Persistence

    /// <summary>
    /// Data written by one repository instance must be readable by a new
    /// instance pointing at the same files — simulates an app restart.
    /// </summary>
    [Fact]
    public void Persistence_DataSurvivesAcrossRepositoryInstances()
    {
        var habit = CreateHabit("Persistent");
        HabitLog log;

        using (var repo1 = CreateRepo())
        {
            repo1.AddHabit(habit);
            log = CreateLog(habit.Id, new DateTime(2025, 5, 1));
            repo1.AddOrUpdateHabitLog(log);
        }

        using var repo2 = CreateRepo();
        var loadedHabit = repo2.GetHabitById(habit.Id);
        var loadedLogs = repo2.GetHabitLogsByHabitId(habit.Id);

        Assert.NotNull(loadedHabit);
        Assert.Equal("Persistent", loadedHabit.Name);
        Assert.Single(loadedLogs);
        Assert.Equal(log.Id, loadedLogs[0].Id);
    }

    #endregion
}
