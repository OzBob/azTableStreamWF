namespace CommonContracts;

public record LogEntry(
    string Uid,
    string EventId,
    string? ParentEventId,
    string CorrelationId,
    string Message,
    string Level,
    string ContextMethod,
    DateTime Timestamp,
    string? ExceptionJson,
    string? ContextJson);
