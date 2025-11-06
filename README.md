# Worldwide Rush Infixo's RouteEdit

[Worldwide Rush](https://store.steampowered.com/app/3325500/Worldwide_Rush/) mod. Vehicles don't teleport when editing routes.

## Features
After editing a line, vehicles will continue their way to the next destination. The mod will place the vehicles on a new route, trying to find the optimal positon for them.
- For loading vehicles - it will be the same city if it still exists on a new route, or the city closest to it.
- For moving vehicles - it will find the matching route segment by taking existing cities and/or closest ones, and maintain vehicles progress.
  - E.g. If a vehicle travelled 100km from A to B, and the new segment is A-C, then it will be placed 100km from A and continue travel to C.
- Passengers that cannot be delivered will be removed from the vehicles and refunded, others will continue to their destinations.
- There must be a physical connection (road, rails, sea path) between old route and a new route. If there is none detected, vehicles will teleport the vanilla-way. There will be a message logged in the log file when this happens.

### Troubleshooting
- Output messages are logged into RouteEditLog.txt in the %TEMP% dir. Please consult me on Discord to understand the log.
- Vehicles will often turn around in place to find a proper direction. This is a byproduct of the relocation process, not a bug.

## Technical

### Requirements and Compatibility
- [WWR ModLoader](https://github.com/Infixo/WWR-ModLoader).
- [Harmony v2.4.1 for .net 8.0](https://github.com/pardeike/Harmony/releases/tag/v2.4.1.0). The correct dll is provided in the release files.

### Known Issues
- None atm.

### Changelog
- v1.0.1 (2025-11-06)
  - Removed redundant logging.
- v1.0.0 (2025-11-05)
  - Better placement of vehicles when adding mid-stops.
- v0.9.1 (2025-11-03)
  - Added extra check to make sure the vehicle is not sold or destroyed between edit confirmation and command execution.
- v0.9.0 (2025-10-31)
  - Initial version.

### Support
- Please report bugs and issues on [GitHub](https://github.com/Infixo/WWR-RouteEdit).
- You may also leave comments on [Discord](https://discord.com/channels/1342565384066170964/1421898965556920342).
