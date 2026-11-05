# Spotify fixtures

- `search-strict.json`, `search-free-text.json`: real `GET /v1/search?type=track&limit=10&market=RO` responses for the two queries `SpotifyTrackFinder` builds for "Leo de la Izvoare - Dacă dragostea era pe bani". Each was recorded with the field-filtered query and the free-text query for that song.
- `token.json`: the client-credentials token response format, with a fake token.
- `Errors/invalid-limit.json`: recorded (`limit=50`). `Errors/invalid-client.json`: the documented accounts-service error for wrong credentials.

Rate limiting (429 with `Retry-After`) has no body worth recording; tests set the header directly.
