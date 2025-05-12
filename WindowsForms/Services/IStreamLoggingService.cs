public interface IStreamLoggingService
{
    Task AddLogAsync();
    Task StartStreamingAsync(TextBox logsTextBox);
    Task StopStreamingAsync();
}
