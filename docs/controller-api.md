# Controller API

Contract between the Oplà controller ([Arduino.ino](../Arduino/Arduino.ino)) and the backend. Ported from AI-Bomberman's `controller-service.js` (#10).

All endpoints are `POST <GAME_PATH>/api/controller/...` with a JSON body. Every response has `ok`; other fields are omitted when empty.

## Endpoints

### `/join`
Joins a game. A known `deviceId` gets its existing player back with `reconnected: true`.

Request:
```json
{ "pin": "1234", "name": "Mikkel", "deviceId": "OPLA_AABBCCDDEEFF" }
```
Response:
```json
{ "ok": true, "playerId": "…", "name": "Mikkel", "number": 1, "reconnected": true }
```
No name gives `"Arduino <number>"`.

### `/heartbeat`
Keeps the session alive; sent every `HEARTBEAT_MS` (2 s).

Request:
```json
{ "pin": "1234", "playerId": "…", "deviceId": "OPLA_AABBCCDDEEFF" }
```
Response: `{ "ok": true }`

### `/action`
Request:
```json
{ "pin": "1234", "playerId": "…", "deviceId": "OPLA_AABBCCDDEEFF", "action": "rotate", "params": { "direction": "LEFT" } }
```

| `action` | `params.direction` | Meaning |
|---|---|---|
| `move` | ignored | One step forward in the facing direction |
| `rotate` | `LEFT` / `RIGHT` | Turn 90° |
| `scan` | – | Scan the tile in front of the player (see [Scan](#scan)) |
| `dig` | – | Dig the tile in front of the player |

Movement is relative to facing, so the controller needs only three movement buttons (forward, rotate left, rotate right). Values are case-insensitive.

Response on success:

| `action` | Body |
|---|---|
| `move` | `{ "ok": true }`, or `{ "ok": true, "light": "purple" }` / `"blue"` when stepping onto an unscanned mine |
| `rotate` | `{ "ok": true }` |
| `scan` | `{ "ok": true, "light": "red", "count": 2 }` |
| `dig` | `{ "ok": true, "light": "yellow" }` (see [Dig](#dig)) |

A rule rejection (cooldown, frozen, facing the edge, no round running) is always `200` with `{ "ok": false, "message": "…" }` and **never** includes `light` or `count`. The controller keeps its current light.

#### Scan

- **Scanned tile:** the tile directly in front of the player (one step in the facing direction). The player's own tile is not scanned.
- **`light`:** `"yellow"` for a treasure, `"purple"` for an unscanned point mine, `"blue"` for an unscanned freeze mine, otherwise `"off"`. Always present on a successful dig. No `count` is sent.
- A treasure gives points (see [game-rules.md](game-rules.md#scoring-tunable)) and leaves a hole. An unscanned mine goes off and leaves a hole. A scanned mine is harmless and stays. Digging a hole fills it. Digging an empty tile gives nothing.
- **`count`:** mines (freeze + point) in the 8 tiles around the scanned tile, `0`–`8`. The scanned tile itself is not counted. `0` is sent, not omitted.
- The scanned tile is marked as discovered and its count appears on the screen.
- **Cooldown:** 1 s per player (`Game.SCAN_COOLDOWN`), independent of move.
- **Rejections:**

| Case | `message` |
|---|---|
| Scanned again within 1 s | `Player is on cooldown` |
| Facing the board edge | `Player is blocked` |
| Frozen | `Player is frozen` |
| No round running | `No round is running` |

#### Dig

- **Dug tile:** the tile directly in front of the player, the same tile scan uses. The player's own tile is not dug.
- **`light`:** `"yellow"` if the tile held a treasure, otherwise `"off"`. Always present on a successful dig. No `count` is sent.
- A treasure gives points (see [game-rules.md](game-rules.md#scoring-tunable)) and leaves a hole. Digging an empty tile, a hole or (for now) a mine gives nothing.
- **Cooldown:** 1 s per player (`Game.DIG_COOLDOWN`), also on an empty tile. Independent of move and scan.
- **Rejections:** the same cases and messages as scan.

#### Light values

| `light` | Sent by | Oplà |
|---|---|---|
| `off` | scan, safe tile; dig, nothing to collect | LEDs off |
| `red` | scan, mine on the scanned tile | Red for `LIGHT_MS` (1.5 s), then off |
| `yellow` | dig, treasure | Yellow for `LIGHT_MS`, then off |
| `purple` | dig or move, unscanned point mine | Purple for `LIGHT_MS`, then off |
| `blue` | dig or move, unscanned freeze mine | Blue while frozen (`FREEZE_DURATION`, 5 s) |

Reserved for later issues: `green` (won). The firmware treats unknown values as `off`.

## Status codes

| Status | When | Body | Controller reaction |
|---|---|---|---|
| `200` | Success | `{ ok: true }`, plus `light`/`count` for scan, `light` for dig and for a move onto a mine | Sets the light when present |
| `200` | Rule rejection (e.g. no round running) | `{ ok: false, message }` | Shows message, keeps session |
| `400` | Missing PIN on join, unknown action/direction | `{ ok: false, error }` | Shows error |
| `403` | Unknown `playerId`, or `deviceId` doesn't match | `{ ok: false, error }` | Forgets session, rejoins |
| `404` | Unknown PIN | `{ ok: false, error }` | Forgets session, rejoins |
| `409` | Join: game full or round in progress | `{ ok: false, error }` | Shows error |
| `5xx` | Server problem | – | Treated as "not ready" |

PIN and controller are checked before the action, so an unknown controller always gets `403`/`404`.

Heartbeat never returns `200 + ok:false` for a known player, because the firmware forgets its session on that.

## Firmware response filter

`requestJson()` only keeps the fields listed in `fields[]` in [Arduino.ino](../Arduino/Arduino.ino). New response fields must be added there or they are silently dropped.

Required: `ok`, `playerId`, `error`, `message`, `light`, `count`.

Sent by the server but not used by the firmware: `name`, `number`, `reconnected`.

## Security notes

- `playerId` is a random GUID and acts as the session secret for heartbeat/action.
- `deviceId` is derived from the MAC and is **not** secret. Anyone who knows it can call `/join` and receive that player's `playerId`. Accepted for now (local classroom network); see #50 for transport security.
- `deviceId` is **not** truncated to 40 chars as AI-Bomberman does: truncation lets different IDs collide and impersonate each other.
- `light` and `count` are sent only to the controller that scanned or dug. The tile's content also appears on the screen, so all players can see it.