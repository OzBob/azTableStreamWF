using CommonContracts;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var logs = new List<LogEntry>(); // In-memory log storage
var subscribers = new Dictionary<string, WebSocket>(); // Active subscribers
var logChannel = Channel.CreateUnbounded<LogEntry>(); // Unbounded channel for log entries

// Background task to process queued logs
_ = Task.Run(async () =>
{
    await foreach (var log in logChannel.Reader.ReadAllAsync())
    {
        logs.Add(log);
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
});

app.MapPost("/logs", async (LogEntry log) =>
{
    await logChannel.Writer.WriteAsync(log); // Write log to the channel
    return Results.Ok();
});

app.MapGet("/logs", (int page, int pageSize) =>
{
    var pagedLogs = logs.Skip((page - 1) * pageSize).Take(pageSize);
    return Results.Ok(pagedLogs);
});

app.MapGet("/logs/{id}", (string id) =>
{
    var log = logs.FirstOrDefault(l => l.Uid == id);
    return log is not null ? Results.Ok(log) : Results.NotFound();
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
