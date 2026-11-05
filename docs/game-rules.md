# Game rules (v1)

The single reference for how Treasure Sweeper works. Values marked *tunable* are constants in code (`BoardSettings`, and a `GameSettings` class for the rest) and may change after playtesting.

## Overview

1–8 players race on a shared board to dig up treasures and avoid mines. Each player steers with an Arduino Oplà controller. The screen shows the board, the scores and the leaderboard. A round lasts 8 minutes or until the last treasure is dug up.

## Setup

| Item | Rule |
| ---- | ---- |
| Players | 1–8. Joining is closed once the round has started. |
| Board | 32 × 32 = 1,024 tiles, generated when the round starts (`Domain.Boards`). |
| Treasures | `max(1, players − 1)`: 1 player → 1, 2 → 1, 3 → 2, 8 → 7. With several players, at least one goes home empty-handed. |
| Mines | 32 freeze mines + 32 point mines, placed randomly on empty tiles. |
| Safe start | No mine within distance 1 of any start tile. Start tiles are always empty. |
| Start positions | Spread out (each new start is the best of 50 random candidates by distance). Each player starts facing the centre of the board. |
| Hidden information | Players only see tiles they have discovered. Treasure and mine positions are never sent to the screen or the controllers. |

Each tile stores its hidden content, a *discovered* flag, the number of mines in the 8 surrounding tiles (0–8) and a *hole* flag.

## Actions

| Action | Effect | Cooldown *(tunable)* |
| ------ | ------ | -------------------- |
| Move | Moves one tile. Players may share a tile. A player cannot enter a hole or leave the board. | 200 ms |
| Scan | The screen shows the number of mines (freeze + point) around the scanned tile. The Oplà lights **red** if the scanned tile itself holds a mine. | 1 s |
| Dig | Digs up the tile's content (see Scoring). The content disappears. | 1 s, also on an empty tile |

A frozen player cannot move, scan or dig. If two players dig the same tile at the same moment, the server processes them in order: the first one gets the content, the second finds an empty tile.

## Scoring *(tunable)*

| Dug up | Points | Extra effect | Oplà light |
| ------ | ------ | ------------ | ---------- |
| First treasure of the round | **+100** | The treasure disappears. Points are permanent and cannot be stolen. | Yellow / orange |
| Every later treasure | **+90** | The treasure disappears. Points are permanent and cannot be stolen. | Yellow / orange |
| Point mine | **−25** | A hole appears. | Purple |
| Freeze mine | 0 | The player is frozen for **5 seconds**. A hole appears. | Blue (while frozen) |
| Empty tile | 0 | None. | — |

A score may go below 0.

**First-treasure bonus:** the first treasure dug up in the round is worth 100 for whoever finds it. Every other treasure is worth 90 for whoever finds it. The extra 10 points exist to reduce ties: of two players with the same number of treasures, the one who found the round's first treasure scores higher. "First" is decided by the order in which the server processes the digs.

**Holes:** after a mine is dug up, the tile is a hole for about 5 seconds. Nobody can enter it. The player who dug it stays on the tile and may leave. After 5 seconds it becomes an ordinary empty tile.

## Round end and winner

The round ends when **8 minutes** have passed or the **last treasure** has been dug up, whichever comes first.

The winner is decided by:
1. the highest score;
2. then the most treasures found;
3. then the fewest mines hit (freeze + point);
4. if still tied, the placement is **shared**: tied players all get the same rank (for example 1, 1, 3) and all of them get the green light.

The first-treasure bonus makes ties less likely, but they can still happen, for example between two players who both found only later treasures, so the rules above stay.

A solo round always ends with the player in rank 1. It ends when the single treasure is found or when time runs out. The only treasure is the first one, so a solo score is 100 minus any mine penalties.

## Feedback

| Event | Oplà | Screen |
| ----- | ---- | ------ |
| Scan: a mine is on the tile | Red | Number of mines around the tile |
| Treasure dug up | Yellow / orange | Score updated, the treasure disappears |
| Freeze mine dug up | Blue while frozen | Frozen message, a hole for about 5 s |
| Point mine dug up | Purple | Minus-points message, a hole for about 5 s |
| Won | Green | Winner and leaderboard |

## Players and controllers

- A player is identified by the `DeviceId` of the Oplà and gets a generated unique name the first time the device is seen.
- A controller that reconnects with the same `DeviceId` keeps its player and score.
- A player who has been silent for about 30 seconds is treated as idle but is not removed from the round.

## Statistics

- Finished rounds are saved, **including solo rounds**. Aborted rounds are not saved.
- Competitive leaderboards use rounds with `PlayerCount >= 2`. Personal bests (for example fewest steps to a treasure) include solo rounds.
- Per player per round: score, placement, treasures found, freeze mines hit, point mines hit, mines avoided, total steps and fewest steps to a treasure.
- **Mines avoided** counts distinct tiles where the player scanned a mine and never dug it.

See [database.md](database.md) for the schema.

## Implementation status

| Done | Not yet |
| ---- | ------- |
| Board generation, treasure formula, safe radius, start positions | Move, scan and dig actions |
| Lobby, joining and reconnecting | Scoring (including the first-treasure bonus), freezing and holes |
| Starting a round from the lobby | Round timer, winner and tie-break |
| Database schema and migrations | Saving round results, player names |

## Open questions

1. **Treasure hint.** Scan only reveals mines, and treasures are under 1% of the tiles. Without a hint ("a treasure within 3 tiles") the game is mostly luck.
2. **Scan semantics.** Confirm that the red light means "this tile is a mine", and that the number is for the 8 surrounding tiles.
3. **Movement model.** `Spawn` stores a facing direction. Decide whether players turn and then step, or move directly in four directions.
4. **Cooldown values** (move, scan, dig) and the idle timeout need a playtest.