# Setting up Second Sight on Linux

These steps mirror the [Windows setup](setup.md). The bot itself works the same on Linux; only the install paths and the Claude app differ.

**What has and hasn't been checked:** the build, `update.sh`, and the program starting as an MCP server and answering tool calls were tested on Ubuntu with .NET 10. Logging in to Second Life wasn't, because the test machine can't reach Second Life's servers, so steps 6 and 7 are your live test. Claude Desktop on Linux wasn't tested either (see step 5).

## 1. Prepare the bot account

Same as on Windows: the bot account (DexterFrost) must be flagged as a scripted agent under **Account > Scripted Agent Status** on [secondlife.com](https://secondlife.com), and multi-factor authentication must be **off** for it. If you already did this for the Windows setup, there's nothing more to do.

## 2. Install .NET 10 and git

On Ubuntu 24.04 or newer:

```bash
sudo apt update
sudo apt install -y dotnet-sdk-10.0 git
dotnet --version   # should print 10.x
```

On other distributions, follow Microsoft's [install guide for Linux](https://learn.microsoft.com/dotnet/core/install/linux) and pick the .NET 10 SDK.

## 3. Build Second Sight

```bash
git clone https://github.com/jtyranski/Second-Sight.git
cd Second-Sight
./update.sh
```

`update.sh` switches to `main`, pulls the latest code, and publishes the program to `~/.local/share/SecondSight/app/SecondSight`. Run it again whenever a change is merged, after quitting Claude.

## 4. Configure the bot

The config file on Linux is `~/.config/SecondSight/config.json`:

```bash
mkdir -p ~/.config/SecondSight
cp config.example.json ~/.config/SecondSight/config.json
chmod 600 ~/.config/SecondSight/config.json   # only you can read it
nano ~/.config/SecondSight/config.json
```

Fill in `Username` (`DexterFrost`), `Owner` (`Jimmi Tiponi`), and the password. The file lives in your home folder, outside the repo, so the password is never committed. If you copy your Windows `config.json` over instead, it works as is.

The environment variables from the Windows guide work too (`SECONDSIGHT_PASSWORD` and the rest). If you use one, export it in the shell you start Claude from.

## 5. Add Second Sight to Claude

### Claude Code (recommended on Linux)

Install [Claude Code](https://docs.claude.com/en/docs/claude-code/setup), then register the server for your user, so it's available in every folder:

```bash
claude mcp add --scope user second-sight -- ~/.local/share/SecondSight/app/SecondSight
```

Start `claude` in any folder and type `/mcp`. `second-sight` should show as connected. When Claude first uses a Second Sight tool, choose the option that stops it asking again for that tool, so chat isn't interrupted.

### Claude Desktop

Anthropic's Claude Desktop is published for Windows and macOS. If you run a Linux build of it, it reads `~/.config/Claude/claude_desktop_config.json`. Add the same entry as on Windows, with the Linux path. Use your real home folder, because `~` isn't expanded there:

```json
{
  "mcpServers": {
    "second-sight": {
      "command": "/home/<you>/.local/share/SecondSight/app/SecondSight"
    }
  }
}
```

This path is unverified, so if Claude Desktop doesn't pick it up, use Claude Code.

## 6. Log in

Ask Claude: "Log the Second Sight bot in and tell me where it is." Claude calls `login`, then `status`. You should see the region, position, and your UUID resolved.

## 7. Live tests

Do these in order with your own avatar logged in nearby. Each line says what to ask Claude and what you should see.

1. **Diagnostics:** "Run the Second Sight diagnostics." The version should match the latest merge, and the config file should be `~/.config/SecondSight/config.json` (not "not found").
2. **Chat:** "Chat with me in Second Life until I say goodbye." Say something in local chat. Claude should answer in local chat. Have a friend (or an object) say something nearby: Claude should see it as background and not answer.
3. **IM:** IM DexterFrost. Claude should get it and can answer with an IM.
4. **Teleport to you:** offer DexterFrost a teleport. He should arrive, and Claude should report where.
5. **Teleport to the bot:** ask Claude to "send me a teleport offer". Accept it in your viewer.
6. **Surroundings:** "What's around DexterFrost?" Check your distance and compass direction (north is up on the map), and that nearby objects have names.
7. **Inspect:** "What is that <object>?" for one listed object. Check the owner and description.
8. **Your avatar:** hold something and ask "What am I doing and holding?", then "What am I wearing?". (Needs the avatar perception update.)
9. **Log out:** "Log the bot out."

## Activity log

On Linux the daily log is `~/.local/share/SecondSight/logs/activity-YYYY-MM-DD.log`. To watch it live:

```bash
tail -f ~/.local/share/SecondSight/logs/activity-$(date +%F).log
```

## Troubleshooting

- **Anything odd:** ask Claude to run the diagnostics tool first. It shows the running version, the config and log paths, and recent IMs and teleport events.
- **`/mcp` shows second-sight failed:** run the program by hand to see the error: `~/.local/share/SecondSight/app/SecondSight`. It should sit waiting for input (press Ctrl+C to quit). "You must install .NET" means the runtime isn't where the program expects. If you installed .NET somewhere other than with `apt`, add `export DOTNET_ROOT=<that folder>` to `~/.profile`, then log out and back in.
- **Config file "not found" in diagnostics:** check the path is exactly `~/.config/SecondSight/config.json`.
- **Login failed: ... password ...:** the username or password is wrong. The username is the account name (`DexterFrost`).
- **update.sh says SecondSight is still running:** quit Claude. If it's still running, `pkill -x SecondSight`.
