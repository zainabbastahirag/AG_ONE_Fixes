using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AgOneSafe.Application.Abstractions;
using AgOneSafe.Domain;
using AgOneSafe.Infrastructure.Emulation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgOneSafe.Infrastructure.Execution;

/// <summary>
/// Runs a control's PowerShell payload in an isolated <c>pwsh</c> process with a hard timeout.
/// An out-of-process host is deliberate: a runaway or hostile payload cannot take the web
/// application down with it, and the process boundary gives a natural place to enforce the timeout.
/// </summary>
public sealed class PowerShellExecutor : IControlExecutor
{
    private readonly PowerShellOptions _options;
    private readonly ITenantEmulator _emulator;
    private readonly IPayloadRenderer _renderer;
    private readonly ILogger<PowerShellExecutor> _logger;

    public PowerShellExecutor(
        IOptions<ExecutorOptions> options,
        ITenantEmulator emulator,
        IPayloadRenderer renderer,
        ILogger<PowerShellExecutor> logger)
    {
        _options = options.Value.PowerShell;
        _emulator = emulator;
        _renderer = renderer;
        _logger = logger;
    }

    public ExecutorType ExecutorType => ExecutorType.PowerShell;

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = _options.ExecutablePath,
                Arguments = "-NoProfile -NonInteractive -Command \"$PSVersionTable.PSVersion.ToString()\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });

            if (process is null)
            {
                return false;
            }

            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "pwsh is not available at {Path}", _options.ExecutablePath);
            return false;
        }
    }

    public async Task<ExecutionResult> ExecuteAsync(ExecutionRequest request)
    {
        var action = request.Action;

        var body = request.DryRun && !string.IsNullOrWhiteSpace(action.DryRunPayload)
            ? action.DryRunPayload!
            : action.Payload;

        var rendered = _renderer.Render(action, body, request.Parameters);

        if (request.DryRun && string.IsNullOrWhiteSpace(action.DryRunPayload))
        {
            // No rehearsal variant is published, so nothing may reach the tenant. Report the exact
            // payload that would run instead of guessing at a safe subset of it.
            return new ExecutionResult
            {
                Succeeded = true,
                Json = JsonSerializer.Serialize(new { dryRun = true, executed = false, payload = rendered }),
                Output = "Dry run: no -WhatIf variant is published for this control. " +
                         "The payload below is what will run once the change is approved." +
                         Environment.NewLine + Environment.NewLine + rendered,
                ExecutedCommand = rendered
            };
        }

        var prologue = request.Tenant.ExecutionMode == ExecutionMode.Simulation
            ? await _emulator.BuildPowerShellPrologueAsync(request.Tenant, request.CancellationToken)
            : BuildLivePrologue(request);

        var script = prologue + Environment.NewLine + rendered + Environment.NewLine;

        Directory.CreateDirectory(_options.ScriptDirectory);
        var scriptPath = Path.Combine(_options.ScriptDirectory, $"ag-{Guid.NewGuid():N}.ps1");
        await File.WriteAllTextAsync(scriptPath, script, request.CancellationToken);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var timeout = TimeSpan.FromSeconds(action.TimeoutSeconds > 0
                ? action.TimeoutSeconds
                : _options.DefaultTimeoutSeconds);

            var (exitCode, stdout, stderr) = await RunProcessAsync(scriptPath, timeout, request.CancellationToken);
            stopwatch.Stop();

            if (exitCode != 0)
            {
                return new ExecutionResult
                {
                    Succeeded = false,
                    Error = string.IsNullOrWhiteSpace(stderr) ? $"pwsh exited with code {exitCode}." : stderr.Trim(),
                    Output = stdout,
                    ExecutedCommand = rendered,
                    DurationMs = (int)stopwatch.ElapsedMilliseconds,
                    Json = "{}"
                };
            }

            var json = TryExtractJson(stdout) ?? "{}";

            return new ExecutionResult
            {
                Succeeded = true,
                Json = json,
                Output = string.IsNullOrWhiteSpace(stderr) ? stdout : stdout + Environment.NewLine + stderr,
                ExecutedCommand = rendered,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ExecutionResult.Failure(ex.Message, rendered, (int)stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            TryDelete(scriptPath);
        }
    }

    private async Task<(int ExitCode, string StdOut, string StdErr)> RunProcessAsync(
        string scriptPath,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _options.ExecutablePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-NonInteractive");
        process.StartInfo.ArgumentList.Add("-ExecutionPolicy");
        process.StartInfo.ArgumentList.Add("Bypass");
        process.StartInfo.ArgumentList.Add("-File");
        process.StartInfo.ArgumentList.Add(scriptPath);

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdout.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new TimeoutException($"The PowerShell payload exceeded its {timeout.TotalSeconds:F0}s budget and was terminated.");
        }

        return (process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private string BuildLivePrologue(ExecutionRequest request)
    {
        // Live mode authenticates with the tenant's app registration. The client secret is read
        // from Key Vault by the host and injected as an environment variable for the child
        // process, so it is never written into the generated script file.
        var builder = new StringBuilder();
        builder.AppendLine("$ErrorActionPreference = 'Stop'");
        builder.AppendLine("$ProgressPreference = 'SilentlyContinue'");

        foreach (var module in _options.LiveModules)
        {
            builder.AppendLine($"Import-Module {module} -ErrorAction SilentlyContinue");
        }

        builder.AppendLine($"$AgTenantId = '{request.Tenant.TenantId}'");
        builder.AppendLine($"$AgClientId = '{request.Tenant.ClientId}'");
        builder.AppendLine("$AgSecret = $env:AG_TENANT_SECRET");

        switch (request.Action.Module)
        {
            case ConnectorModule.ExchangeOnline:
                builder.AppendLine("Connect-ExchangeOnline -AppId $AgClientId -Organization $AgTenantId -ShowBanner:$false");
                break;
            case ConnectorModule.MicrosoftTeams:
                builder.AppendLine("Connect-MicrosoftTeams -TenantId $AgTenantId -ApplicationId $AgClientId");
                break;
            case ConnectorModule.SharePointOnline:
                builder.AppendLine("Connect-SPOService -Url \"https://$($AgTenantId)-admin.sharepoint.com\"");
                break;
            default:
                builder.AppendLine("Connect-MgGraph -TenantId $AgTenantId -ClientId $AgClientId -NoWelcome");
                break;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Pulls the JSON document out of a transcript that may also contain Write-Host lines.
    /// The convention is that a payload emits its result last, so the scan walks backwards.
    /// </summary>
    internal static string? TryExtractJson(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        var trimmed = output.Trim();

        if (IsJson(trimmed))
        {
            return trimmed;
        }

        for (var i = trimmed.Length - 1; i >= 0; i--)
        {
            if (trimmed[i] is not ('{' or '['))
            {
                continue;
            }

            var candidate = trimmed[i..].Trim();
            if (IsJson(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsJson(string text)
    {
        if (text.Length < 2 || text[0] is not ('{' or '['))
        {
            return false;
        }

        try
        {
            using var _ = JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the check and the kill; nothing to clean up.
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "Could not delete scratch script {Path}", path);
        }
    }
}
