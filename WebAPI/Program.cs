using Azure.Data.Tables;
using CommonContracts;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var connectionString = builder.Configuration["azLogsConnectionString"];
var tableName = "LogsTable";
var tableClient = new TableClient(connectionString, tableName);
await tableClient.CreateIfNotExistsAsync(); // Ensure the table exists

var subscribers = new Dictionary<string, WebSocket>(); // Active subscribers
var logChannel = Channel.CreateUnbounded<LogEntry>(); // Unbounded channel for log entries

// Background task to process queued logs
_ = Task.Run(async () =>
{
    await foreach (var log in logChannel.Reader.ReadAllAsync())
    {
        if ((DateTime.UtcNow - log.Timestamp).TotalMinutes <= 5) // Only process logs created within the last 5 minutes
        {
            var entity = new TableEntity(log.Uid, log.EventId)
            {
                { "ParentEventId", log.ParentEventId },
                { "CorrelationId", log.CorrelationId },
                { "Message", log.Message },
                { "Level", log.Level },
                { "ContextMethod", log.ContextMethod },
                { "Timestamp", log.Timestamp },
                { "ExceptionJson", log.ExceptionJson },
                { "ContextJson", log.ContextJson }
            };
            await tableClient.AddEntityAsync(entity);

            // Notify subscribers (if needed)
            foreach (var subscriber in subscribers.Values)
            {
                if (subscriber.State == WebSocketState.Open)
                {
                    var logMessage = System.Text.Json.JsonSerializer.Serialize(log);
                    var buffer = Encoding.UTF8.GetBytes(logMessage);
                    await subscriber.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }
    }
});

app.MapPost("/logs", async (LogEntry log) =>
{
    await logChannel.Writer.WriteAsync(log); // Write log to the channel
    return Results.Ok();
});

app.MapGet("/logs", async (int page, int pageSize = 10, string? from = "") =>
{
    var query = tableClient.QueryAsync<TableEntity>();

    DateTimeOffset? fromDateTimeOffset = null;
    if (!string.IsNullOrEmpty(from))
    {
        if (!DateTimeOffset.TryParse(from, out var parsedFrom))
        {
            return Results.BadRequest("Invalid 'from' parameter. Please provide a valid DateTimeOffset.");
        }
        fromDateTimeOffset = parsedFrom;

        var filter = fromDateTimeOffset.HasValue ? $"Timestamp ge datetime'{fromDateTimeOffset.Value.UtcDateTime:O}'" : null;
        query = tableClient.QueryAsync<TableEntity>(filter: filter);
    }
    var logs = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

    return Results.Ok(logs.Select(entity => new LogEntry(
        entity.RowKey,
        entity.PartitionKey,
        entity.GetString("ParentEventId"),
        entity.GetString("CorrelationId"),
        entity.GetString("Message"),
        entity.GetString("Level"),
        entity.GetString("ContextMethod"),
        entity.GetDateTime("Timestamp") ?? DateTime.UtcNow,
        entity.GetString("ExceptionJson"),
        entity.GetString("ContextJson")
    )));
});

app.MapGet("/logs/{id}", async (string id) =>
{
    var entity = await tableClient.GetEntityAsync<TableEntity>(id, id);
    if (entity != null)
    {
        var log = new LogEntry(
            entity.Value.RowKey,
            entity.Value.PartitionKey,
            entity.Value.GetString("ParentEventId"),
            entity.Value.GetString("CorrelationId"),
            entity.Value.GetString("Message"),
            entity.Value.GetString("Level"),
            entity.Value.GetString("ContextMethod"),
            entity.Value.GetDateTime("Timestamp") ?? DateTime.UtcNow,
            entity.Value.GetString("ExceptionJson"),
            entity.Value.GetString("ContextJson")
        );
        return Results.Ok(log);
    }
    return Results.NotFound();
});

app.MapGet("/logs/rows/{rowkey}", async (string rowkey) =>
{
    var query = tableClient.QueryAsync<TableEntity>(filter: $"RowKey eq '{rowkey}'");
    var entity = await query.FirstOrDefaultAsync();

    if (entity != null)
    {
        var log = new LogEntry(
            entity.RowKey,
            entity.PartitionKey,
            entity.GetString("ParentEventId"),
            entity.GetString("CorrelationId"),
            entity.GetString("Message"),
            entity.GetString("Level"),
            entity.GetString("ContextMethod"),
            entity.GetDateTime("Timestamp") ?? DateTime.UtcNow,
            entity.GetString("ExceptionJson"),
            entity.GetString("ContextJson")
        );
        return Results.Ok(log);
    }
    return Results.NotFound();
});

app.MapGet("/subscribe/{uid}", async (string uid, HttpContext context) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var socket = await context.WebSockets.AcceptWebSocketAsync();
        subscribers[uid] = socket;

        var channelReader = logChannel.Reader;

        try
        {
            while (socket.State == WebSocketState.Open)
            {
                if (await channelReader.WaitToReadAsync())
                {
                    while (channelReader.TryRead(out var log))
                    {
                        var logMessage = System.Text.Json.JsonSerializer.Serialize(log);
                        var buffer = Encoding.UTF8.GetBytes(logMessage);
                        await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
            }
        }
        finally
        {
            subscribers.Remove(uid);
        }
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

app.MapPost("/unsubscribe/{uid}", (string uid) =>
{
    if (subscribers.ContainsKey(uid))
    {
        subscribers[uid].Abort();
        subscribers.Remove(uid);
    }
    return Results.Ok();
});

app.Run();
