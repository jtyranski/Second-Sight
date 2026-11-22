using System.ComponentModel;
using ModelContextProtocol.Server;
using SecondSight.Core.Session;

namespace SecondSight.Tools;

/// <summary>MCP tools for connecting the bot to Second Life.</summary>
[McpServerToolType]
public sealed class SessionTools(IBotSession session)
{
    [McpServerTool(Name = "login"), Description(
        "Log the bot avatar into Second Life using the credentials in its local config. " +
        "Returns where the bot arrived. The password is never passed through this tool.")]
    public async Task<string> Login(CancellationToken cancellationToken)
    {
        var result = await session.LoginAsync(cancellationToken);
        return result.Message;
    }

    [McpServerTool(Name = "logout"), Description("Log the bot avatar out of Second Life.")]
    public async Task<string> Logout(CancellationToken cancellationToken)
    {
        var result = await session.LogoutAsync(cancellationToken);
        return result.Message;
    }

    [McpServerTool(Name = "status", ReadOnly = true), Description(
        "Report whether the bot is logged in, which region it is in, its position, " +
        "whether it is sitting or flying, and who its owner is.")]
    public string Status() => session.GetStatus().Describe();
}
