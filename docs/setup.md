# Setting up Second Sight on Windows 11

On Linux, see [setup-linux.md](setup-linux.md).

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

This puts `SecondSight.exe` in `C:\SecondSight`.

To update later, quit Claude Desktop (from the system tray icon), then run this in the `Second-Sight` folder and reopen Claude Desktop:

```powershell
powershell -ExecutionPolicy Bypass -File .\update.ps1
```

It pulls the latest `main` and rebuilds `C:\SecondSight`.

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

Claude then loops: it reads what you said, answers with `reply_and_wait` (which sends the answer and waits for your next message in one step), and repeats. You can also IM the bot.

### Making chat feel quick

Each reply Dexter gives is one Claude turn, so a few habits keep the gap short:

- **Allow all Second Sight tools.** In Claude Code, add `mcp__second-sight` to `permissions.allow` in `~/.claude/settings.json` (or `/permissions` > Allow > add `mcp__second-sight`, saved to user settings). In Claude Desktop, choose **Always allow** the first time each tool is used. Without this, every reply waits for you to approve it. Allowing everything also lets Claude remove friends or set home without asking; leave `remove_friend` out of the rule if you'd rather approve that one.
- **Start a fresh session for each chat.** A long session carries everything before it, and each turn gets slower as it grows.
- **Ask for `reply_and_wait`.** It sends Claude's reply and waits for your next message in one step, instead of `say` then `wait_for_message`. A good opening prompt:

  > Log in Dexter if needed, then chat with me in Second Life. Use reply_and_wait for every answer, keep replies short, and stop when I say goodbye.

While Claude is working out an answer, Dexter turns to face you and plays the typing animation, so you can see he heard you. The typing stops when he speaks, or after 45 seconds.


### Bringing the bot somewhere

- **Bot to you:** right-click DexterFrost (or open his profile) and choose **Offer Teleport**, or IM him a teleport offer. He accepts offers only from you.
- **You to the bot:** ask Claude to "send me a teleport offer", or use **Request Teleport** on DexterFrost in your viewer. Either way you get an offer to accept.
- **Somewhere specific:** ask Claude to "teleport to Region Name 128 128 30", or paste a SLURL.

Sandboxes are a good place to experiment. Some regions block bots, and the bot will be refused there.

### Asking what the bot sees

Ask Claude "What's around DexterFrost?" or "Where am I compared to the bot?". Claude calls `describe_surroundings`, which reports the region and parcel, where you are (for example "8 m northeast, 3 m above"), other avatars, and nearby objects. For a closer look at one thing, ask "What is that chair?" and Claude calls `inspect_object` with its id. Asking "What am I wearing?" or "What am I doing?" inspects your avatar: your animations, what you hold, and your attachments.

Object names come from the region a moment after the bot asks for them, so the first description in a new place can list a few objects as unnamed. Asking again fills them in.

### Home and friends

- **Set home:** stand the bot somewhere it's allowed to set home (your land, or land of a group it belongs to) and ask Claude to "set DexterFrost's home here". Then "teleport home" from somewhere else to check.
- **Friend request to you:** ask Claude to "send me a friend request". Accept it in your viewer.
- **Friend request from you:** if you're not friends yet, offer DexterFrost friendship. He accepts right away.
- **Someone else's request:** have an alt or a friend offer DexterFrost friendship. You get an IM from him asking whether to accept. Tell Claude "accept it" or "decline it".
- **Friends list:** ask "Who are DexterFrost's friends?".
- **Remove a friend:** ask Claude to "remove <name> from DexterFrost's friends".

When you're done, ask Claude to log the bot out.

## Activity log

Everything you say to the bot, and everything it says, is logged by day in `%LOCALAPPDATA%\SecondSight\logs\activity-YYYY-MM-DD.log`.

## Troubleshooting

- **Something isn't working** Ask Claude to "run the Second Sight diagnostics tool". It shows the running version, where the config and activity log are, whether your UUID was found, and the last IMs and teleport offers the bot received and what it did with them.

- **Login failed: ... password ...** The username or password is wrong. The username is the account name (`DexterFrost`), not a display name.
- **Could not find the owner avatar** The bot logged in, but the name search for the owner came back empty. Check the spelling of `Owner`, or set it to your avatar's UUID.
- **Logs** The server writes its logs to stderr. Claude Desktop saves them in a `logs` folder next to `claude_desktop_config.json`, in `mcp-server-second-sight.log`.
