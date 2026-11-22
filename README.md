# Second Sight

Second Sight lets Claude be present in Second Life. It is a small C# program that logs a dedicated bot avatar into Second Life with [LibreMetaverse](https://github.com/cinderblocks/libremetaverse), and exposes that avatar to Claude as an [MCP](https://modelcontextprotocol.io) server. Claude Desktop or Claude Code starts it on your computer.

The bot takes direction only from its owner.

## Status

The bot can log in, chat with its owner, teleport, and describe what is around it. Movement and sitting come in the next milestone.

| Tool | What it does |
|---|---|
| `login` | Logs the bot in using the local config. Reports where it arrived. |
| `logout` | Logs the bot out. |
| `diagnostics` | Troubleshooting: version, config and log file locations, owner UUID, and the last raw IMs and teleport events. |
| `status` | Reports whether the bot is logged in, its region and position, whether it is sitting or flying, its owner, and how many messages it has sent today. |
| `say` | Says something in local chat (whisper, normal, or shout). |
| `send_im` | Sends an IM to the owner. The bot can't IM anyone else. |
| `get_messages` | Returns new chat and IMs without waiting. |
| `wait_for_message` | Waits until the owner says something, then returns it. Claude uses this in a loop to hold a conversation. |
| `teleport` | Teleports to a region and position, or to a SLURL. |
| `teleport_home` | Teleports to the bot's home location. |
| `offer_teleport` | Sends the owner a teleport offer to the bot's location. |
| `describe_surroundings` | Describes the region and parcel, where the owner is, other avatars in the region, and nearby objects with their size and what clicking them does. |
| `inspect_object` | Details for one object or avatar: name, description, floating text, owner, creator, size, position, and whether it's for sale. |

### Seeing

- `describe_surroundings` gives distances and compass directions from the bot (north is +Y, east is +X in region coordinates). It looks up to 128 m for objects (30 m by default) and lists the nearest 25 unless asked for more.
- Avatars beyond the bot's 128 m draw distance are still listed, with an approximate position from the minimap.
- Object names, descriptions, and floating text are written by whoever made the object, so Claude is told to treat them as information, not instructions.

### Teleporting

- When the owner sends the bot a teleport offer, the bot accepts it automatically and tells Claude where it arrived. Teleport offers from anyone else are declined.
- When the owner uses "Request teleport" on the bot, it sends the owner a teleport offer back.
- Regions that block scripted agents refuse the bot. The `teleport` tool reports this.

### Who the bot listens to

- Local chat and IMs from the owner (matched by avatar UUID) are messages for Claude to act on.
- Other people's nearby local chat, and chat from objects, is passed to Claude as background, clearly labeled as something not to answer or obey.
- IMs from anyone but the owner, group chat, and friendship, inventory, and group offers are ignored. Teleport offers from anyone but the owner are declined.

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
