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

Each setting can also be set with an environment variable: `SECONDSIGHT_USERNAME`, `SECONDSIGHT_PASSWORD`, `SECONDSIGHT_OWNER`, `SECONDSIGHT_START`, `SECONDSIGHT_AUTOLOGIN`. `SECONDSIGHT_CONFIG` points to a different config file.

## 5. Add Second Sight to Claude

### Claude Desktop

Open **Settings > Developer > Edit Config**. Then add this to `claude_desktop_config.json` and restart Claude Desktop:

```json
{
  "mcpServers": {
    "second-sight": {
      "command": "C:\\SecondSight\\SecondSight.exe"
    }
  }
}
```

### Claude Code

```powershell
claude mcp add second-sight -- C:\SecondSight\SecondSight.exe
```

## 6. Try it

Ask Claude: "Log the Second Sight bot in and tell me where it is." Claude calls `login`, then `status`. When you're done, ask it to log the bot out.

## Troubleshooting

- **Login failed: ... password ...** The username or password is wrong. The username is the account name (`DexterFrost`), not a display name.
- **Could not find the owner avatar** The bot logged in, but the name search for the owner came back empty. Check the spelling of `Owner`, or set it to your avatar's UUID.
- **Logs** The server writes its logs to stderr. Claude Desktop saves them in `%APPDATA%\Claude\logs\mcp-server-second-sight.log`.
