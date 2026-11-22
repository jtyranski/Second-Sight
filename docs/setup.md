# Setting up Second Sight on Windows 11

## 1. Prepare the bot account

1. Sign in to [secondlife.com](https://secondlife.com) as the bot account (DexterFrost).
2. Open **Account > Scripted Agent Status** and mark the account as a scripted agent. Linden Lab's policy requires this.
3. Make sure multi-factor authentication is **off** for the bot account. The bot can't answer an MFA prompt.
4. Log in once with a normal viewer to accept the Terms of Service, and park the bot somewhere sensible. With the default `"StartLocation": "last"`, the bot logs back in there.

## 2. Install .NET

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), or run this in PowerShell:

```powershell
winget install Microsoft.DotNet.SDK.10
```

## 3. Build Second Sight

```powershell
git clone https://github.com/jtyranski/Second-Sight.git
cd Second-Sight
dotnet publish src/SecondSight -c Release -o C:\SecondSight
```

This puts `SecondSight.exe` in `C:\SecondSight`. Run the same `dotnet publish` command again after you pull updates.

## 4. Configure the bot

Create `%APPDATA%\SecondSight\config.json` by copying [config.example.json](../config.example.json):

```powershell
mkdir $env:APPDATA\SecondSight -Force
copy config.example.json $env:APPDATA\SecondSight\config.json
notepad $env:APPDATA\SecondSight\config.json
```

Fill in `Username`, `Owner`, and the password. This file lives in your user profile, outside the repo, so the password is never committed.

If you'd rather not keep the password in the file, leave `"Password"` empty and set a user environment variable instead. Restart Claude afterwards so it sees the variable.

```powershell
[Environment]::SetEnvironmentVariable("SECONDSIGHT_PASSWORD", "your-password", "User")
```

Message limits are set with `DailyMessageLimit` (default 500) and `MessagesPerMinute` (default 10) in the config file.

Each setting can also be set with an environment variable: `SECONDSIGHT_USERNAME`, `SECONDSIGHT_PASSWORD`, `SECONDSIGHT_OWNER`, `SECONDSIGHT_START`, `SECONDSIGHT_AUTOLOGIN`. `SECONDSIGHT_CONFIG` points to a different config file.

## 5. Add Second Sight to Claude

### Claude Desktop

Claude Desktop reads its MCP servers from `claude_desktop_config.json`. Where that file lives depends on how Claude Desktop was installed:

- Installer from claude.ai: `%APPDATA%\Claude\claude_desktop_config.json`
- Microsoft Store / MSIX install: a folder like `%LOCALAPPDATA%\Claude-Data\claude_<id>\claude_desktop_config.json`

If you're not sure, this finds it:

```powershell
Get-ChildItem $env:APPDATA, $env:LOCALAPPDATA -Recurse -Depth 6 -Filter claude_desktop_config.json -ErrorAction SilentlyContinue | Select-Object FullName
```

Add a `second-sight` entry under `mcpServers`, then fully quit Claude Desktop from the system tray and reopen it:

```json
{
  "mcpServers": {
    "second-sight": {
      "command": "C:\\SecondSight\\SecondSight.exe"
    }
  }
}
```

To add the entry without hand-editing, set `$p` to the path you found and run this. It backs the file up first and keeps everything else in it:

```powershell
$p = "<path to claude_desktop_config.json>"
Copy-Item $p "$p.bak"
$c = Get-Content $p -Raw | ConvertFrom-Json
if ($null -eq $c) { $c = [pscustomobject]@{} }
if (-not $c.PSObject.Properties['mcpServers']) { $c | Add-Member -NotePropertyName mcpServers -NotePropertyValue ([pscustomobject]@{}) }
$c.mcpServers | Add-Member -Force -NotePropertyName 'second-sight' -NotePropertyValue ([pscustomobject]@{ command = 'C:\SecondSight\SecondSight.exe' })
[IO.File]::WriteAllText($p, ($c | ConvertTo-Json -Depth 20))
```

### Claude Code

```powershell
claude mcp add second-sight -- C:\SecondSight\SecondSight.exe
```

## 6. Try it

Ask Claude: "Log the Second Sight bot in and tell me where it is." Claude calls `login`, then `status`.

To chat, stand near the bot in Second Life and ask Claude something like:

> Chat with me in Second Life. Keep waiting for my messages and reply in local chat until I say goodbye.

Claude then loops: it calls `wait_for_message`, reads what you said, answers with `say`, and waits again. You can also IM the bot. Claude Desktop asks before each tool call, so choose **Always allow** for the Second Sight tools to keep the conversation flowing.

### Bringing the bot somewhere

- **Bot to you:** right-click DexterFrost (or open his profile) and choose **Offer Teleport**, or IM him a teleport offer. He accepts offers only from you.
- **You to the bot:** ask Claude to "send me a teleport offer", or use **Request Teleport** on DexterFrost in your viewer. Either way you get an offer to accept.
- **Somewhere specific:** ask Claude to "teleport to Region Name 128 128 30", or paste a SLURL.

Sandboxes are a good place to experiment. Some regions block bots, and the bot will be refused there.

When you're done, ask Claude to log the bot out.

## Activity log

Everything you say to the bot, and everything it says, is logged by day in `%LOCALAPPDATA%\SecondSight\logs\activity-YYYY-MM-DD.log`.

## Troubleshooting

- **Login failed: ... password ...** The username or password is wrong. The username is the account name (`DexterFrost`), not a display name.
- **Could not find the owner avatar** The bot logged in, but the name search for the owner came back empty. Check the spelling of `Owner`, or set it to your avatar's UUID.
- **Logs** The server writes its logs to stderr. Claude Desktop saves them in a `logs` folder next to `claude_desktop_config.json`, in `mcp-server-second-sight.log`.
