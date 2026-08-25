using System.ComponentModel;
using System.Diagnostics;
using NaderGorge.Application.Features.LiveSupport.Interfaces;

namespace NaderGorge.Infrastructure.Services;

public sealed class FfmpegWhatsAppAudioProcess : IWhatsAppAudioProcess
{
    private const string FfmpegPath = "/usr/bin/ffmpeg";
    private static readonly TimeSpan TranscodeTimeout = TimeSpan.FromSeconds(45);

    public async Task<byte[]> TranscodeToOggOpusMonoAsync(
        ReadOnlyMemory<byte> source,
        int maximumOutputBytes,
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = CreateStartInfo() };
        Start(process);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TranscodeTimeout);
            using var termination = timeout.Token.Register(() => TryKill(process));
            var execution = BeginExecution(process, source, maximumOutputBytes, timeout.Token);
            await AwaitExecutionAsync(execution, timeout, cancellationToken);
            return ValidatedOutput(process, await execution.Output);
        }
        finally
        {
            TryKill(process);
        }
    }

    private static AudioProcessExecution BeginExecution(
        Process process,
        ReadOnlyMemory<byte> source,
        int maximumOutputBytes,
        CancellationToken cancellationToken) =>
        new(
            WriteInputAsync(process, source, cancellationToken),
            ReadOutputAsync(process.StandardOutput.BaseStream, maximumOutputBytes, cancellationToken),
            DrainAsync(process.StandardError.BaseStream, cancellationToken),
            process.WaitForExitAsync(cancellationToken));

    private static async Task AwaitExecutionAsync(
        AudioProcessExecution execution,
        CancellationTokenSource timeout,
        CancellationToken callerCancellation)
    {
        try
        {
            await Task.WhenAll(execution.Input, execution.Output, execution.Error, execution.Exit);
        }
        catch (OperationCanceledException) when (!callerCancellation.IsCancellationRequested)
        {
            throw Failure("WHATSAPP_MEDIA_TRANSCODE_TIMEOUT", 504, true,
                "WhatsApp audio conversion timed out.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException) when (timeout.IsCancellationRequested && callerCancellation.IsCancellationRequested)
        {
            throw new OperationCanceledException(callerCancellation);
        }
        catch (IOException) when (timeout.IsCancellationRequested)
        {
            throw Failure("WHATSAPP_MEDIA_TRANSCODE_TIMEOUT", 504, true,
                "WhatsApp audio conversion timed out.");
        }
        catch (IOException)
        {
            throw Failure("WHATSAPP_MEDIA_TRANSCODE_FAILED", 422, false,
                "WhatsApp audio conversion failed.");
        }
        if (callerCancellation.IsCancellationRequested)
            throw new OperationCanceledException(callerCancellation);
        if (timeout.IsCancellationRequested)
            throw Failure("WHATSAPP_MEDIA_TRANSCODE_TIMEOUT", 504, true,
                "WhatsApp audio conversion timed out.");
    }

    private static byte[] ValidatedOutput(Process process, BoundedOutput output)
    {
        if (output.TooLarge)
            throw Failure("WHATSAPP_MEDIA_TOO_LARGE", 413, false,
                "WhatsApp audio exceeds the supported size.");
        if (process.ExitCode != 0 || output.Content.Length == 0)
            throw Failure("WHATSAPP_MEDIA_TRANSCODE_FAILED", 422, false,
                "WhatsApp audio conversion failed.");
        return output.Content;
    }

    private static ProcessStartInfo CreateStartInfo()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = FfmpegPath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in new[]
                 {
                     "-hide_banner", "-loglevel", "error", "-nostdin",
                     "-i", "pipe:0", "-vn", "-ac", "1", "-c:a", "libopus",
                     "-ar", "48000", "-b:a", "48k", "-f", "ogg", "pipe:1"
                 })
        {
            startInfo.ArgumentList.Add(argument);
        }
        return startInfo;
    }

    private static void Start(Process process)
    {
        try
        {
            if (!process.Start())
                throw Failure("WHATSAPP_MEDIA_TRANSCODER_UNAVAILABLE", 503, true,
                    "WhatsApp audio conversion is temporarily unavailable.");
        }
        catch (Win32Exception)
        {
            throw Failure("WHATSAPP_MEDIA_TRANSCODER_UNAVAILABLE", 503, true,
                "WhatsApp audio conversion is temporarily unavailable.");
        }
    }

    private static async Task WriteInputAsync(
        Process process,
        ReadOnlyMemory<byte> source,
        CancellationToken cancellationToken)
    {
        try
        {
            await process.StandardInput.BaseStream.WriteAsync(source, cancellationToken);
            await process.StandardInput.BaseStream.FlushAsync(cancellationToken);
        }
        catch (IOException)
        {
            // ffmpeg closes its input pipe early when it rejects the stream; its exit code is authoritative.
        }
        finally
        {
            process.StandardInput.Close();
        }
    }

    private static async Task<BoundedOutput> ReadOutputAsync(
        Stream source,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        using var content = new MemoryStream();
        var buffer = new byte[81_920];
        var tooLarge = false;
        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0) return new(content.ToArray(), tooLarge);
            if (tooLarge || content.Length + bytesRead > maximumBytes)
            {
                tooLarge = true;
                continue;
            }
            await content.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }
    }

    private static async Task DrainAsync(Stream source, CancellationToken cancellationToken)
    {
        var buffer = new byte[16_384];
        while (await source.ReadAsync(buffer, cancellationToken) != 0) { }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }
    }

    private static WhatsAppMediaNormalizationException Failure(
        string errorCode,
        int statusCode,
        bool retryable,
        string message) =>
        new(errorCode, statusCode, retryable, message);

    private sealed record BoundedOutput(byte[] Content, bool TooLarge);

    private sealed record AudioProcessExecution(
        Task Input,
        Task<BoundedOutput> Output,
        Task Error,
        Task Exit);
}
