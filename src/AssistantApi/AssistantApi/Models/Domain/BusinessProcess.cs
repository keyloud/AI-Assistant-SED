namespace AssistantApi.Models.Domain;

public class BusinessProcess
{
    public string ProcessId { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string CurrentStage { get; set; } = string.Empty;
    public string? PreviousStage { get; set; }
    public string? NextPossibleStage { get; set; }
    public List<string> AvailableActions { get; set; } = new();
    public List<ProcessStage> AllStages { get; set; } = new();
    public List<ProcessHistoryEntry> History { get; set; } = new();
}

public class ProcessStage
{
    public int Order { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public bool IsCurrent { get; set; }
}

public class ProcessHistoryEntry
{
    public string Stage { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public DateTime Timestamp { get; set; }
}
