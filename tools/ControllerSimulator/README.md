# Controller Simulator

Pretends to be one or more Arduino Oplà controllers so we can test the game
without hardware (issue #51). It speaks the same HTTP/JSON contract as the
firmware, so the backend cannot tell the difference between this and a real Oplà.

Pair it with the game screen open in a browser: start the simulator, start the
round, and you see the players appear and act on the 32 × 32 board.

## Run

Start the backend first (so it is listening on the plain-HTTP port):

```bash
dotnet run --project src/Blazor --launch-profile http    # listens on http://localhost:5288
```

Then, in another terminal:

```bash
dotnet run --project tools/ControllerSimulator -- --players 4
```

This creates a new game, joins 4 simulated controllers, and prints the PIN.
Open `http://localhost:5288/game-screen` (or your game page) to watch.

### Options

| Option | Meaning | Default |
| ------ | ------- | ------- |
| `--url <base>` | Backend base URL | `http://localhost:5288` |
| `--pin <pin>` | Join an existing game instead of creating one | (creates a new game) |
| `--players <n>` | Number of controllers to join (1–8) | `2` |
| `--prefix <name>` | Player name prefix | `Sim` |

Point it at the deployed site with `--url https://treasuresweeper.mercantec.tech`.

### Keys

| Key | Action |
| --- | ------ |
| `1`–`8` | select the active controller |
| `W` / `Up` | move forward |
| `A` / `Left` | rotate left |
| `D` / `Right` | rotate right |
| `S` | scan the tile in front |
| `F` | dig the tile in front |
| `Enter` | start the round (acts as the host) |
| `R` | reset to the lobby |
| `Tab` | print the status table |
| `Q` / `Esc` | quit |

Movement mirrors the real controller: rotate to face a direction, then move
forward. Scan and dig act on the tile in front.

## What works today vs later

The simulator implements the full controller contract now, but the backend only
has some of it so far:

- **Works today:** create game, join controllers, start/reset the round. You can
  already see the players spawn on the board.
- **Needs the backend endpoints** (`/api/controller/heartbeat`, `/api/controller/action`,
  issues #10 / #21): move, rotate, scan, dig, and live score/light. Until those
  exist the simulator prints "endpoint not implemented yet" for those keys and
  keeps running. No change to the simulator is needed once they land.

## Contract (summary)

All requests are `POST` with JSON and carry `pin`, `deviceId`, and (after joining)
`playerId`.

- `POST /api/controller/join` → `{ ok, playerId, name, number, reconnected? }`
- `POST /api/controller/heartbeat` (every 2 s) → `{ ok, phase, light, frozenMilliseconds, score }`
- `POST /api/controller/action` with `{ action, params: { direction } }` →
  `{ ok, result, light, score, frozenMilliseconds, scan? }`
  - `action`: `move` (direction `UP`), `rotate` (`LEFT`/`RIGHT`), `scan`, `dig`
  - `scan` reply adds `{ mine, around, treasureNear }`

Not in the solution file on purpose — run it with `dotnet run --project` as above,
or add it to the solution in Visual Studio if you want it there.
