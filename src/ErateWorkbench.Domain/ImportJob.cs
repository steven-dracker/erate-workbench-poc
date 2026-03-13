namespace ErateWorkbench.Domain;

public class ImportJob
{
    public int Id { get; set; }

    public required string DatasetName { get; set; }

    public ImportJobStatus Status { get; set; } = ImportJobStatus.Pending;

    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public int RecordsProcessed { get; set; }
    public int RecordsFailed { get; set; }

    public string? ErrorMessage { get; set; }
}

public enum ImportJobStatus
{
    Pending = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
}
