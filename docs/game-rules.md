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
| Hidden information | Unscanned tiles are sent to the screen as empty. A scanned tile shows its content (treasure, freeze mine or point mine) or, if it has none, the number of mines around it. |

Each tile stores its hidden content and a *discovered* flag. A discovered tile without content also stores the number of mines in its 8 surrounding tiles (0–8). A hole is a separate tile value.

## Actions

| Action | Effect | Cooldown *(tunable)* |
| ------ | ------ | -------------------- |
| Move | Moves one tile. Players may share a tile. A player cannot enter a hole or leave the board. Stepping onto an unscanned mine sets it off (see Scoring), and the player is left standing in the new hole. A scanned mine is safe to walk over. | 200 ms |
| Scan | Scans the tile **in front of** the player (one step in the facing direction). The tile is marked as discovered and the screen shows its content or the number of mines (freeze + point) in its 8 surrounding tiles. The Oplà lights **red** if the scanned tile itself holds a mine. Scanning while facing the board edge is rejected. | 1 s |
| Dig | Digs the tile **in front of** the player (see Scoring). A dug empty tile (including one showing a nearby-mine count), treasure or unscanned mine leaves a hole, a scanned mine stays, and a hole is filled. Digging while facing the board edge is rejected. | 1 s, also on an empty tile |

**Scan details:** the scanned tile's own mine is not part of the count. A tile with content shows the content instead of a count; the Oplà still gets the count. A scanned tile keeps its count until it is scanned again or dug, so the number can be out of date after a nearby mine is dug. Scanning a hole shows nothing new. Everyone watches the same screen, so a scanned treasure or mine is visible to all players.

A frozen player cannot move, scan or dig. A player standing in a hole may still leave it. If two players dig the same tile at the same moment, the server processes them in order: the first one gets the content, the second fills the hole it left.

## Scoring *(tunable)*

| Dug up or stepped on | Points | Extra effect | Oplà light |
| -------------------- | ------ | ------------ | ---------- |
| First treasure of the round | **+1000** | The treasure disappears and leaves a hole. Points are permanent and cannot be stolen. | Yellow / orange |
| Every later treasure | **100 less than the previous one** (900, 800, …) | The treasure disappears and leaves a hole. Points are permanent and cannot be stolen. | Yellow / orange |
| Point mine | **−25** | A hole appears. | Red |
| Freeze mine | 0 | The player is frozen for **5 seconds**. A hole appears. | Cyan (while frozen) |
| Scanned mine | 0 | Nothing. The mine stays. | — |
| Empty tile | 0 | A hole appears when dug. | — |
| Hole (dig only) | 0 | The hole is filled. | — |

Treasures are only collected by digging. Stepping onto a treasure does nothing. Digging or stepping onto an unscanned mine sets it off. A scanned mine is harmless and stays on the board; it cannot be removed.

A score may go below 0.

**Falling treasure value:** the first treasure dug up in the round is worth 1000, and each later treasure is worth 100 less than the one before (at most 7 treasures, so never below 400). The count is for the whole round, not per player. Early finds are worth more, which also reduces ties: of two players with the same number of treasures, the one who found theirs first scores higher. "First" is decided by the order in which the server processes the digs.

**Holes:** digging an empty tile or treasure, or digging or stepping onto an unscanned mine, leaves a hole. Holes stay until someone fills them. Nobody can enter a hole, but a player standing in one may leave. Digging a hole fills it, and the tile becomes an ordinary empty tile.

## Round end and winner

The round ends when **8 minutes** have passed or the **last treasure** has been dug up, whichever comes first.

The winner is decided by:
1. the highest score;
2. then the most treasures found;
3. then the fewest mines hit (freeze + point);
4. if still tied, the placement is **shared**: tied players all get the same rank (for example 1, 1, 3) and all of them get the green light.

Falling treasure values make ties less likely, but they can still happen, for example between two players with no treasures and the same mine penalties, so the rules above stay.

A solo round always ends with the player in rank 1. It ends when the single treasure is found or when time runs out. The only treasure is the first one, so a solo score is 1000 minus any mine penalties.

## Feedback

| Event | Oplà | Screen |
| ----- | ---- | ------ |
| Scan | Red radar sweep if a mine is on the tile in front, otherwise green | The scanned tile's content, or the number of mines around it |
| Empty tile dug | — | A hole appears |
| Treasure dug up | Yellow / orange | Score updated, the treasure disappears |
| Freeze mine dug up or stepped on | Cyan while frozen | Frozen message, a hole |
| Point mine dug up or stepped on | Red | Minus-points message, a hole |
| Won | Green | Winner and leaderboard |
| Treasure dug up | Yellow / orange | Score updated, the treasure becomes a hole |

## Players and controllers

- A player is identified by the `DeviceId` of the Oplà and gets a generated unique name the first time the device is seen.
- A controller that reconnects with the same `DeviceId` keeps its player and score.
- A player who has been silent for about 30 seconds is treated as idle but is not removed from the round.

## Statistics

- Finished rounds are saved, **including solo rounds**. Aborted rounds are not saved.
- Competitive leaderboards use rounds with `PlayerCount >= 2`. Personal bests (for example fewest steps to a treasure) include solo rounds.
- Per player per round: score, placement, treasures found, freeze mines hit, point mines hit, mines avoided, total steps and fewest steps to a treasure.
- **Mines avoided** counts distinct tiles where the player scanned a mine (the tile in front of them) and never dug it.

See [database.md](database.md) for the schema.

## Implementation status

| Done | Not yet |
| ---- | ------- |
| Board generation, treasure formula, safe radius, start positions | Tie-break and shared placement |
| Lobby, joining and reconnecting | Saving finished rounds (only dev seed data so far) |
| Starting a round from the lobby | Green "won" light on the controller |
| Round timer (8 minutes) and ending when the last treasure is dug | |
| Database schema, migrations, leaderboard and history pages | |
| Move (turn, then step) and scan (tile in front, red light, count) | |
| Dig (tile in front, treasure scoring, holes, yellow light, score on screen) | |
| Mines (dig or step on unscanned: point penalty, freeze, red/cyan light, holes) | |
| Live scoreboard, countdown and treasures left on the game screen | |
| Game over screen with winner and leaderboard | |

## Open questions

1. **Treasure hint.** Scan only shows a treasure when it is on the scanned tile, and treasures are under 1% of the tiles. Without a hint ("a treasure within 3 tiles") the game is mostly luck.
2. **Cooldown values** (move, scan, dig) and the idle timeout need a playtest.
3. **Permanent holes.** Holes from digging and mines can cut off parts of the board until filled. Playtest whether filling by digging is enough.