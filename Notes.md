# Notes

## What this is

A small pipeline for casino wagers. The API takes a wager and puts it on a RabbitMQ queue, a background consumer reads the queue and saves each wager to SQL Server, and two GET endpoints read the data back out.

```
Tester -> POST api/player/casinowager -> RabbitMQ (casino-wager) -> Consumer -> SQL Server
                                                                                 ^
                                        GET api/player/{id}/casino  --------------|
                                        GET api/player/topSpenders  --------------|
```

## How to run it

You need the .NET 8 SDK, RabbitMQ and SQL Server.

1. **RabbitMQ** on localhost with guest/guest (port 5672, management UI on 15672). I ran it locally. If you prefer Docker:
   `docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management`
2. **SQL Server** (I used Developer Edition with Windows authentication). Build the database with:
   `sqlcmd -S localhost -E -C -i DatabaseGenerate.sql`
   The script creates `OT_Assessment_DB` if it doesn't exist, and it can be run more than once safely.
3. **API:**
   `dotnet run --launch-profile http --project src/OT.Assessment.App`
   Swagger is at http://localhost:5021/swagger
4. **Consumer** (separate terminal):
   `dotnet run --project src/OT.Assessment.Consumer`
5. **Tester** (third terminal, after the first two are up):
   `dotnet run --project test/OT.Assessment.Tester`

Connection strings and RabbitMQ settings are in each project's `appsettings.json`. The connection strings have `TrustServerCertificate=True` added, because the current SQL client encrypts by default and a local SQL Server has a self-signed certificate.

## Database design

Four tables:

- **Player** (AccountId, Username)
- **Provider** (ProviderId, Name)
- **Game** (GameId, ProviderId, Name, Theme)
- **CasinoWager** (WagerId, AccountId, GameId, amounts, dates and so on)

Every message repeats the player name, provider and game, so I split those out and the wager table only holds keys. That keeps the big table narrow and means a username or game name is stored once.

Things I decided on purpose:

- **`SessionData` is not stored.** It's a long free-text field and neither GET endpoint needs it. Keeping it would have made every row much bigger for no benefit.
- **WagerId is the primary key, and it's nonclustered.** The Tester sends random GUIDs, and a clustered index on a random GUID causes page splits on every insert.
- **The clustered index is (AccountId, CreatedDateTime DESC).** The main read is "latest wagers for one player", so a player's wagers sit together on disk, newest first, and paging doesn't need a sort.
- **Amount is DECIMAL(18,2)**, not float, because it's money. The Tester sends doubles with many decimals and SQL rounds them to 2.
- **The timestamp is DATETIMEOFFSET**, so the +02:00 from the message is kept.
- I did keep BrandId, TransactionId, ExternalReferenceId and TransactionTypeId even though no endpoint uses them. They look like they'd matter for auditing and reconciliation. If you wanted the table smaller, these are the first columns I'd question.

All reads and writes go through three stored procedures (`usp_InsertCasinoWager`, `usp_GetPlayerWagers`, `usp_GetTopSpenders`), so the SQL lives in the database and the C# only calls them.

The insert procedure checks whether the WagerId already exists and does nothing if it does. RabbitMQ can deliver a message more than once, so I wanted a repeat to be harmless.

## API

- `POST api/player/casinowager` validates the body and publishes it to the queue. It returns 200, not 202, because the Tester only counts a response as successful when it is exactly `OK`.
- The request model uses real `Guid`, `decimal` and `DateTimeOffset` types, so a bad GUID is rejected with a 400 at the door instead of failing later in the consumer.
- `GET api/player/{playerId}/casino` takes `page` and `pageSize`. I cap `pageSize` at 100 so nobody can ask for a million rows. The response has the shape from the brief (`data`, `page`, `pageSize`, `total`, `totalPages`).
- `GET api/player/topSpenders?count=10` is capped at 1000.
- Publishing sits behind an `IWagerPublisher` interface and the SQL reads sit behind `IPlayerQueryRepository`, so the controller knows nothing about RabbitMQ or SQL Server and either piece can be swapped.
- The queue is durable and messages are marked persistent, so they survive a RabbitMQ restart.
- The publisher keeps one connection and one channel, with a lock around the publish, because RabbitMQ channels aren't thread-safe.

## Consumer

A `BackgroundService` that reads the queue with manual acknowledgements and a prefetch of 100. A message is only acked after it's saved. If a message can't be read as JSON, it's logged and discarded. If saving fails, it's requeued.

## Results of the load test

The Tester sent 7000 requests at about 250 per second: 7000 OK, 0 failed, median latency about 5 ms, p99 about 640 ms (probably the first requests opening the RabbitMQ connection).

**Known issue:** in my run the queue drained to zero but the database ended with **6398** wagers, not 7000. 

## Things that went wrong along the way

- **SQL Server installation.** PolyBase, full-text and the machine learning features failed to install; I didn't need them. After that the engine was stuck in single-user mode and refused my connection. Restarting the service fixed it.
- **sqlcmd.** I didn't have enough disk space for SSMS, so I used the small standalone `sqlcmd`.
- **The boilerplate had a few mismatches.** The API model had `TransactionTypeId` as an `int` while the Tester sends a GUID, and `BogusGenerator.cs` had a stray `/` that stopped the Tester compiling. I fixed both.
- **The Tester calls HTTPS on port 7120.** I couldn't get Windows to trust the ASP.NET dev certificate on the pc I did this assessment on (the trust step errored), so I changed the Tester to call `http://localhost:5021`. If you run it over HTTPS with a trusted certificate, change the URL back.
- **The consumer's config path.** The boilerplate loaded `appsettings.json` from the current directory, which fails when you run from the solution root. I load it from the app's own folder instead.
- **NBomber** prints a warning that it's free for personal use only. I only used it to run this assessment.

## The Challenges I faced and What I would do to improve

- **Batch the inserts.** The consumer saves one message per database call. Reading in batches and writing them with a table-valued parameter or `SqlBulkCopy` would be much faster, and it's the first thing I'd do.
- **Publisher confirms** on the API side, so a 200 really means RabbitMQ has the message.
- **A dead-letter queue** for messages that can't be processed, and a retry limit instead of requeueing forever.
- **More than one consumer**, once inserts are batched.
- **Top spenders** recalculates a SUM over every wager on each call. That's fine at this size, but I'd keep a running total per player (or an indexed view) instead.
- **Tests.** I tested by hand with Swagger, the Tester and SQL queries. I'd add unit tests for the controllers and repositories and an integration test with the queue and database in containers.
- A shared contracts project for the message type, because the API and the consumer each have their own copy of the class today.
- Health checks, structured logging, secrets out of `appsettings.json`, HTTPS, a Docker Compose file for RabbitMQ and SQL Server, and a proper migrations tool instead of one SQL script.