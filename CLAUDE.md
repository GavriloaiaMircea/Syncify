# Syncify

Personal portfolio project: import YouTube playlists into Spotify and keep them in sync (later also Spotify → YouTube). Not a public service. Code quality and test coverage matter more than speed.

## Layout

- `src/Syncify.Domain` — entities and matching rules. Plain C#, **no package or project references**.
- `src/Syncify.Application` — use cases and the interfaces they need (`IYouTubeClient`, `ISpotifyClient`, repositories). References Domain only.
- `src/Syncify.Infrastructure` — EF Core/Postgres, Spotify and YouTube HTTP clients, background jobs. Implements Application interfaces.
- `src/Syncify.Api` — composition root and minimal-API endpoints. Endpoints only validate input and call a use case; no business logic.
- `tests/Syncify.<Project>.Tests` — one test project per source project.
- `web/` — Next.js 16 + Tailwind 4 dashboard.

## Commands

```bash
dotnet build Syncify.slnx
dotnet test Syncify.slnx
docker compose up -d                      # Postgres
dotnet run --project src/Syncify.Api      # http://127.0.0.1:5050, docs at /scalar
cd web && npm run lint && npm run build
```

The build treats warnings as errors. Run build + tests before every commit.

## Code conventions

- File-scoped namespaces, one public type per file, `sealed` classes and `record`s by default. Small enums that only describe one type live in that type's file (e.g. `MatchStatus` and `MatchReason` in `MatchResult.cs`).
- Expected outcomes (song not found, private playlist, invalid link) are return values, not exceptions. Exceptions are for bugs and infrastructure failures.
- Async methods take a `CancellationToken`. Use `TimeProvider` instead of `DateTime.UtcNow`.
- Matching is platform-agnostic: it compares one source track against candidates, whichever platform each side is from.
- No comments that restate the code; comment only the non-obvious "why".

## Tests

- xUnit + Shouldly. Name tests `Method_Scenario_ExpectedResult`.
- Every PR adds or updates tests. Prefer `[Theory]` tables for parsing and matching rules.
- External HTTP APIs are faked with WireMock.Net using recorded real responses in `tests/**/Fixtures/`. Tests never call real APIs.
- Database tests use Testcontainers (real Postgres), not the EF in-memory provider.

## Workflow

- `main` is protected: work on `feature/<name>`, `fix/<name>` or `chore/<name>` branches, merge via squash PR after CI (`backend`, `web`) passes.
- Small PRs: one roadmap step each. PR description: what, why, how it was tested.
- Commit messages: imperative summary line, then bullet points if needed.

## Secrets and external APIs

- Secrets live in `dotnet user-secrets` (project `src/Syncify.Api`): `Spotify:ClientId`, `Spotify:ClientSecret`, `YouTube:ApiKey`. Never print, log or commit them.
- YouTube is read with the API key (no Google login). Quota is 10,000 units/day: list calls cost 1, `search.list` costs 100.
- Spotify redirect URI: `http://127.0.0.1:5050/auth/spotify/callback` (Spotify rejects `localhost`).

## Matching notes (from the real playlists)

Titles are mostly Romanian pop/manele, uploaded by label channels, so the channel name rarely identifies the artist. Expect `Artist - Song` with artist joiners `feat.`, `feat`, `x`, `&`, `+`, `,`, `si`/`și`, producer credits `(by X)`, Romanian and English noise (`Videoclip Oficial`, `video oficial`, `Official Video HD`), emojis, ALL CAPS and diacritics. "Not found on Spotify" is a common, valid result.
