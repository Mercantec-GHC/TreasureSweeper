\# Database design



PostgreSQL stores only what must outlive a round: \*\*players\*\* and the \*\*results of finished rounds\*\*. These feed the leaderboard and the statistics.



Live game state is deliberately \*\*not\*\* stored: lobby, board, tiles, treasures, mines, player positions, freeze timers, holes and game status. It changes many times per second, only matters while a round runs, and lives in memory in `GameRegistry`. If the server restarts mid-round, that round is lost.



\## ER diagram



```mermaid

erDiagram

&#x20;   Players ||--o{ RoundPlayers : "has results"

&#x20;   Rounds  ||--o{ RoundPlayers : "has results"



&#x20;   Players {

&#x20;       uuid Id PK

&#x20;       varchar(40) Name UK "generated, unique"

&#x20;       varchar(64) DeviceId UK "nullable, the Opla controller"

&#x20;       timestamptz CreatedAt

&#x20;   }

&#x20;   Rounds {

&#x20;       uuid Id PK

&#x20;       timestamptz StartedAt

&#x20;       timestamptz EndedAt

&#x20;       int PlayerCount

&#x20;       varchar(32) EndReason "TimeUp or AllTreasuresFound"

&#x20;   }

&#x20;   RoundPlayers {

&#x20;       uuid RoundId PK, FK

&#x20;       uuid PlayerId PK, FK

&#x20;       int Score "can be negative"

&#x20;       int Placement

&#x20;       int TreasuresFound

&#x20;       int FreezeMinesHit

&#x20;       int PointMinesHit

&#x20;       int MinesAvoided "scanned a mine, never dug it"

&#x20;       int TotalSteps

&#x20;       int FewestStepsToTreasure "null if no treasure found"

&#x20;   }

```



\## Tables



\- \*\*Players\*\*: a persistent player. A player is identified by the `DeviceId` of the Oplà and gets a generated unique name the first time that device is seen. A player without a `DeviceId` has no history.

\- \*\*Rounds\*\*: one row per \*\*finished\*\* round.

\- \*\*RoundPlayers\*\*: the result of one player in one round. The key is `(RoundId, PlayerId)`.



\## Rules in the database



\- Deleting a round deletes its results (cascade).

\- Deleting a player with results is refused (restrict), so statistics can't disappear by accident.

\- Check constraints: `Placement >= 1`, and all counts and steps `>= 0`. There is no limit on `Score`, because point mines give negative points.

\- Unique indexes on `Players.Name` and `Players.DeviceId`. Other indexes: `RoundPlayers.PlayerId`, `RoundPlayers.Score` and `Rounds.EndedAt`.



\## Example leaderboard queries



```sql

\-- Highest score in a round

SELECT p."Name", rp."Score"

FROM "RoundPlayers" rp JOIN "Players" p ON p."Id" = rp."PlayerId"

ORDER BY rp."Score" DESC LIMIT 10;



\-- Fewest steps to reach a treasure

SELECT p."Name", MIN(rp."FewestStepsToTreasure") AS best

FROM "RoundPlayers" rp JOIN "Players" p ON p."Id" = rp."PlayerId"

WHERE rp."FewestStepsToTreasure" IS NOT NULL

GROUP BY p."Name" ORDER BY best ASC LIMIT 10;



\-- Most treasures found over all rounds

SELECT p."Name", SUM(rp."TreasuresFound") AS treasures

FROM "RoundPlayers" rp JOIN "Players" p ON p."Id" = rp."PlayerId"

GROUP BY p."Name" ORDER BY treasures DESC LIMIT 10;

```



\## Changing the schema



See \*Migrations\* in the README.

