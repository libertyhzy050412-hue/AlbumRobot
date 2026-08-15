# Netease probe status

Status: `deterministic-parser-ready-live-metadata-pending`.

The local detector and URL normalizer are implemented in `apps/sync/src/AlbumRobot.Sync.Core`. A live card/metadata sample is still required to verify the real QCE card shape, short-link redirects, cover URL stability, and regional/UA behavior. Until then, the implementation accepts only reliable structured fields or a URL containing a numeric album ID and rejects text-only guesses.
