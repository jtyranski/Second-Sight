# Second Sight

Second Sight lets Claude be present in Second Life. It is a small C# program that logs a dedicated bot avatar into Second Life with [LibreMetaverse](https://github.com/cinderblocks/libremetaverse), and exposes that avatar to Claude as an [MCP](https://modelcontextprotocol.io) server. Claude Desktop or Claude Code starts it on your computer.

The bot takes direction only from its owner.

## Status

Milestone 1: the bot can log in, log out, and report its status. Chat, perception, movement, sitting, and teleport come in the next milestones.

| Tool | What it does |
|---|---|
| `login` | Logs the bot in using the local config. Reports where it arrived. |
| `logout` | Logs the bot out. |
| `status` | Reports whether the bot is logged in, its region and position, whether it is sitting or flying, and its owner. |

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
