namespace AgOneSafe.Infrastructure.Execution;

public sealed class ExecutorOptions
{
    public const string SectionName = "Executors";

    public PowerShellOptions PowerShell { get; set; } = new();

    public HttpExecutorOptions Graph { get; set; } = new();

    public HttpExecutorOptions AzureResourceManager { get; set; } = new();

    public EmulatorOptions Emulator { get; set; } = new();
}

public sealed class PowerShellOptions
{
    /// <summary>Path to the pwsh binary. Left as the bare name so PATH resolution works in containers.</summary>
    public string ExecutablePath { get; set; } = "pwsh";

    public int DefaultTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Scratch directory for generated scripts. Each run gets its own file and it is deleted
    /// afterwards, so a payload never lingers on disk.
    /// </summary>
    public string ScriptDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "ag-one-safe", "scripts");

    /// <summary>Modules imported before a live payload runs.</summary>
    public string[] LiveModules { get; set; } =
    {
        "Microsoft.Graph.Authentication",
        "ExchangeOnlineManagement",
        "MicrosoftTeams"
    };
}

public sealed class HttpExecutorOptions
{
    public string BaseAddress { get; set; } = "https://graph.microsoft.com";

    public int DefaultTimeoutSeconds { get; set; } = 60;
}

public sealed class EmulatorOptions
{
    /// <summary>
    /// Root for the deterministic tenant emulator used in Simulation mode. Holds one JSON state
    /// document per tenant plus the generated PowerShell shim module.
    /// </summary>
    public string StateDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "App_Data", "emulator");

    /// <summary>The PowerShell shim module, shipped as content with the Infrastructure assembly.</summary>
    public string ModulePath { get; set; } =
        Path.Combine(AppContext.BaseDirectory, "Emulator", "AgTenantEmulator.psm1");
}
