# Syncify

Syncify keeps music playlists in sync across platforms. The first goal is importing YouTube playlists into Spotify and keeping them up to date as the YouTube playlist grows.

This is a personal project, not a public service.

## Repository layout

```
src/
  Syncify.Api              ASP.NET Core API (composition root, endpoints)
  Syncify.Application      Use cases: import, sync, review
  Syncify.Domain           Entities and track-matching rules (no dependencies)
  Syncify.Infrastructure   Postgres, Spotify and YouTube clients, background jobs
tests/
  Syncify.Domain.Tests     xUnit tests for the domain
web/                       Next.js dashboard
```

## Requirements

- .NET 10 SDK
- Node.js 22
- Docker (for Postgres)

## Run locally

```powershell
docker compose up -d
dotnet run --project src/Syncify.Api
```

The API runs on `http://127.0.0.1:5050`. Spotify only accepts loopback IP redirect URIs, not `localhost`, so the API uses `127.0.0.1`.

- Health check: `http://127.0.0.1:5050/health`
- API reference (Development only): `http://127.0.0.1:5050/scalar`

In another terminal:

```powershell
cd web
npm install
npm run dev
```

Open `http://localhost:3000`.

## Secrets

Secrets never go in `appsettings*.json`. Use user secrets for local development:

```powershell
dotnet user-secrets set "Spotify:ClientId" "<value>" --project src/Syncify.Api
```

## Roadmap

1. **Matching engine**: normalize YouTube titles, score Spotify candidates, reject remixes, covers, live versions and similar. Covered by tests built from real playlists.
2. **First import**: log in with Spotify, read a public YouTube playlist using an API key, create the Spotify playlist and add the matched tracks. Unmatched tracks are listed.
3. **One-way sync**: link a YouTube playlist to a Spotify playlist and add new tracks, either on a schedule or with a "Sync now" button. Add-only, no duplicates, with sync history and review of ambiguous matches.
4. **Web dashboard**: live progress, notifications, history and stats.
5. **Spotify to YouTube**: Google OAuth, YouTube quota budgeting, bidirectional sync.
6. **Extras**: MCP server, optional AI-assisted matching.
7. **Mobile app** (Expo).
