using LibreMetaverse;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SecondSight.Core.Config;
using SecondSight.Core.Session;

// stdout carries the MCP protocol, so anything else that writes to Console.Out
// (including library logging) is sent to stderr instead.
Console.SetOut(Console.Error);

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(LogLevel.Information);

var config = ConfigLoader.Load();
builder.Services.AddSingleton(config);
builder.Services.AddSingleton<BotSession>();
builder.Services.AddSingleton<IBotSession>(sp => sp.GetRequiredService<BotSession>());

builder.Services
    .AddMcpServer(o => o.ServerInfo = new() { Name = "second-sight", Version = BotSession.Version })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var host = builder.Build();

Logger.SetLoggerFactory(host.Services.GetRequiredService<ILoggerFactory>(), "LibreMetaverse");

if (config.AutoLogin)
{
    var session = host.Services.GetRequiredService<IBotSession>();
    _ = Task.Run(async () =>
    {
        var result = await session.LoginAsync(CancellationToken.None);
        host.Services.GetRequiredService<ILogger<Program>>().LogInformation("Auto-login: {Message}", result.Message);
    });
}

await host.RunAsync();
