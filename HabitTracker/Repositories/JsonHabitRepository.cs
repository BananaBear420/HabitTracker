using System.Text.Json;
using HabitTracker.Models;

namespace HabitTracker.Repositories;

public class JsonHabitRepository : IHabitRepository, IDisposable
{
    private readonly string _habitsFilePath;
    private readonly string _logsFilePath;
    private readonly ReaderWriterLockSlim _lock = new();

    public JsonHabitRepository(string habitsFilePath, string logsFilePath)
    {
        _habitsFilePath = habitsFilePath;
        _logsFilePath = logsFilePath;
    }

    // This makes it so Guid is saved twice (The id is in the data model, to not save it i can change the model but this seems not necessery)
    private void SaveToFile<T>(string path, Dictionary<Guid, T> data)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(data, options);
        File.WriteAllText(path, json);
    }

    private Dictionary<Guid, T> LoadFromFile<T>(string path)
    {
        if (!File.Exists(path))
            return [];

        var json = File.ReadAllText(path);

        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<Dictionary<Guid, T>>(json) ?? [];
    }

    private Dictionary<Guid, Habit> GetHabitDict()
    {
        return LoadFromFile<Habit>(_habitsFilePath);
    }

    private Dictionary<Guid, HabitLog> GetHabitLogDict()
    {
        return LoadFromFile<HabitLog>(_logsFilePath);
    }

    public void AddHabit(Habit habit)
    {
        _lock.EnterWriteLock();
        try
        {
            var habitDict = GetHabitDict();

            if (habitDict.TryAdd(habit.Id, habit))
                SaveToFile(_habitsFilePath, habitDict);
            else
                throw new InvalidOperationException($"Habit with ID {habit.Id} already exists.");
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public List<Habit> GetAllHabits()
    {
        _lock.EnterReadLock();
        try
        {
            return [.. GetHabitDict().Values];
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Habit? GetHabitById(Guid id)
    {
        _lock.EnterReadLock();
        try
        {
            return GetHabitDict().GetValueOrDefault(id);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public bool UpdateHabit(Habit habit)
    {
        _lock.EnterWriteLock();
        try
        {
            var habitDict = GetHabitDict();

            if (!habitDict.ContainsKey(habit.Id))
                return false;

            habitDict[habit.Id] = habit;
            SaveToFile(_habitsFilePath, habitDict);
            return true;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public bool DeleteHabit(Guid id)
    {
        _lock.EnterWriteLock();
        try
        {
            var habitDict = GetHabitDict();
            if (!habitDict.ContainsKey(id))
                return false;

            habitDict.Remove(id);
            SaveToFile(_habitsFilePath, habitDict);

            var logDict = GetHabitLogDict();
            foreach (HabitLog log in logDict.Values)
            {
                if (log.HabitId == id)
                    logDict[log.Id].IsDeletedHabit = true;
            }
            SaveToFile(_logsFilePath, logDict);

            return true;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public List<HabitLog> GetHabitLogs()
    {
        _lock.EnterReadLock();
        try
        {
            return [.. LoadFromFile<HabitLog>(_logsFilePath).Values];
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void AddOrUpdateHabitLog(HabitLog log)
    {
        _lock.EnterWriteLock();
        try
        {
            var logHabitId = log.HabitId;

            if (GetHabitDict().GetValueOrDefault(logHabitId) is null)
                throw new InvalidOperationException($"Log is associated with HabitId ({logHabitId}) that does not exist");

            var logDict = GetHabitLogDict();
            logDict[log.Id] = log;

            SaveToFile(_logsFilePath, logDict);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Dispose()
    {
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }
}
