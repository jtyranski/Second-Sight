# Second Sight

Second Sight lets Claude be present in Second Life. It is a small C# program that logs a dedicated bot avatar into Second Life with [LibreMetaverse](https://github.com/cinderblocks/libremetaverse), and exposes that avatar to Claude as an [MCP](https://modelcontextprotocol.io) server. Claude Desktop or Claude Code starts it on your computer.

The bot takes direction only from its owner.

## Status

Milestone 2: the bot can log in and chat with its owner. Perception, movement, sitting, and teleport come in the next milestones.

| Tool | What it does |
|---|---|
| `login` | Logs the bot in using the local config. Reports where it arrived. |
| `logout` | Logs the bot out. |
| `status` | Reports whether the bot is logged in, its region and position, whether it is sitting or flying, its owner, and how many messages it has sent today. |
| `say` | Says something in local chat (whisper, normal, or shout). |
| `send_im` | Sends an IM to the owner. The bot can't IM anyone else. |
| `get_messages` | Returns new chat and IMs without waiting. |
| `wait_for_message` | Waits until the owner says something, then returns it. Claude uses this in a loop to hold a conversation. |

### Who the bot listens to

- Local chat and IMs from the owner (matched by avatar UUID) are messages for Claude to act on.
- Other people's nearby local chat, and chat from objects, is passed to Claude as background, clearly labeled as something not to answer or obey.
- IMs from anyone but the owner, group chat, and friendship, inventory, and group offers are ignored.

### Limits and logging

- The bot sends at most 500 messages a day (Second Life time) and 10 a minute by default. Both are configurable, and the daily limit can't be set above Linden Lab's 5,000.
- Everything the owner says to the bot, and everything the bot says, is written to a daily activity log in `%LOCALAPPDATA%\SecondSight\logs`.

## Setup

See [docs/setup.md](docs/setup.md) for Windows setup: installing .NET, configuring the bot account, and adding Second Sight to Claude.

## Development

```
dotnet build
dotnet test
```

- `src/SecondSight` is the MCP server: hosting and tool definitions.
- `src/SecondSight.Core` is the bot: config, the Second Life session, and state.
- `tests/SecondSight.Tests` holds the unit tests.

## Second Life rules

The bot account must be flagged as a scripted agent, as Linden Lab's [Scripted Agent Policy](https://wiki.secondlife.com/wiki/Linden_Lab_Official:Bot_policy) requires. Some estates block scripted agents, and the bot will be refused entry there.
