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
| `scan` | – | Scan surroundings |
| `dig` | – | Dig the current tile |

Movement is relative to facing, so the controller needs only three movement buttons (forward, rotate left, rotate right). Values are case-insensitive.

## Status codes

| Status | When | Body | Controller reaction |
|---|---|---|---|
| `200` | Success | `{ ok: true }` | – |
| `200` | Rule rejection (e.g. no round running) | `{ ok: false, message }` | Shows message, keeps session |
| `400` | Missing PIN on join, unknown action/direction | `{ ok: false, error }` | Shows error |
| `403` | Unknown `playerId`, or `deviceId` doesn't match | `{ ok: false, error }` | Forgets session, rejoins |
| `404` | Unknown PIN | `{ ok: false, error }` | Forgets session, rejoins |
| `409` | Join: game full or round in progress | `{ ok: false, error }` | Shows error |
| `5xx` | Server problem | – | Treated as "not ready" |

PIN and controller are checked before the action, so an unknown controller always gets `403`/`404`.

Heartbeat never returns `200 + ok:false` for a known player, because the firmware forgets its session on that.

## Firmware response filter

`requestJson()` only keeps the fields listed in `fields[]`. New response fields must be added there or they are silently dropped. Currently needed: `ok`, `playerId`, `name`, `number`, `reconnected`, `error`, `message`.

## Security notes

- `playerId` is a random GUID and acts as the session secret for heartbeat/action.
- `deviceId` is derived from the MAC and is **not** secret. Anyone who knows it can call `/join` and receive that player's `playerId`. Accepted for now (local classroom network); see #50 for transport security.
- `deviceId` is **not** truncated to 40 chars as AI-Bomberman does: truncation lets different IDs collide and impersonate each other.