using System.IO;
using System.IO.Pipes;

namespace ChattyWidgets.Core.Services;

public static class SingleInstanceService
{
    private const string MutexName = "ChattyWidgets_SingleInstance_Mutex_Global";
    private const string PipeName = "ChattyWidgets_SingleInstance_Pipe";
    private static Mutex? _mutex;
    private static CancellationTokenSource? _cts;

    public static bool TryStartAsFirstInstance(Action<string> onCommandLineReceived)
    {
        bool isFirstInstance = false;
        try
        {
            _mutex = new Mutex(true, MutexName, out isFirstInstance);
        }
        catch (AbandonedMutexException)
        {
            // Previous instance process exited abruptly without releasing the mutex. We now own it!
            isFirstInstance = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SingleInstanceService] Mutex init error: {ex.Message}");
            isFirstInstance = true;
        }

        if (!isFirstInstance)
        {
            return false;
        }

        _cts = new CancellationTokenSource();
        Task.Run(() => ListenForSecondaryInstances(onCommandLineReceived, _cts.Token));
        return true;
    }

    public static void ForceResetMutex(Action<string> onCommandLineReceived)
    {
        try
        {
            _cts?.Cancel();
            _mutex?.Dispose();
        }
        catch { }

        try
        {
            _mutex = new Mutex(true, MutexName);
        }
        catch { }

        _cts = new CancellationTokenSource();
        Task.Run(() => ListenForSecondaryInstances(onCommandLineReceived, _cts.Token));
    }

    public static bool SendArgsToRunningInstance(string[] args)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(1000); // 1 second timeout

            using var writer = new StreamWriter(client) { AutoFlush = true };
            string payload = string.Join("\n", args);
            writer.WriteLine(payload);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task ListenForSecondaryInstances(Action<string> onCommandLineReceived, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(ct);

                using var reader = new StreamReader(server);
                string? line;
                var lines = new List<string>();
                while ((line = await reader.ReadLineAsync(ct)) != null)
                {
                    lines.Add(line);
                }

                if (lines.Count > 0)
                {
                    string joined = string.Join("\n", lines);
                    onCommandLineReceived(joined);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SingleInstanceService] Pipe listener error: {ex.Message}");
                await Task.Delay(500, ct);
            }
        }
    }

    public static void Cleanup()
    {
        try
        {
            _cts?.Cancel();
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }
        catch { }
    }
}
