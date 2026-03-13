using System.Text.Json;
using HabitTracker.Models;

namespace HabitTracker.Repositories;

public class JsonHabitRepository : IHabitRepository
{
    private readonly string _habitsFilePath;
    private readonly string _logsFilePath;

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
    public void AddHabit(Habit habit)
    {
        var habitDict = GetHabitDict();

        if (habitDict.TryAdd(habit.Id, habit))
            SaveToFile(_habitsFilePath, habitDict);
        else
            throw new InvalidOperationException($"Habit with ID {habit.Id} already exists.");

    }
    public List<Habit> GetAllHabits()
    {
        return [.. GetHabitDict().Values];
    }

    public Habit? GetHabitById(Guid id)
    {
        return GetHabitDict().GetValueOrDefault(id);
    }

    public bool UpdateHabit(Habit habit)
    {
        var habitDict = GetHabitDict();

        if (!habitDict.ContainsKey(habit.Id))
            return false;

        habitDict[habit.Id] = habit;
        SaveToFile(_habitsFilePath, habitDict);
        return true;
    }

    public bool DeleteHabit(Guid id)
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

    public Dictionary<Guid, HabitLog> GetHabitLogDict()
    {
        return LoadFromFile<HabitLog>(_logsFilePath);
    }
    public List<HabitLog> GetHabitLogs()
    {
        return [.. LoadFromFile<HabitLog>(_logsFilePath).Values];
    }

    public void AddOrUpdateHabitLog(HabitLog log)
    {
        var logId = log.Id;
        var logHabitId = log.HabitId;

        if (GetHabitById(logHabitId) is null)
            throw new InvalidOperationException($"Log is associated with HabitId ({logHabitId}) that does not exist");

        var logDict = GetHabitLogDict();
        logDict[log.Id] = log;

        SaveToFile(_logsFilePath, logDict);
    }
}
