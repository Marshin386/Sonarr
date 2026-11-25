# Review location

This fork can park a completed download in a review folder. The episode stays **In review** until a series rescan adopts a matching file into the series folder. Review items do not expire. Remove one yourself if you do not want it adopted.

Paths below are examples. Use your own folders. The container paths in the sample Compose file are `/config`, `/tv`, and `/data/review`.

## Turn it on

1. **Settings → Media Management → Importing**
2. Enable **Review location**.
3. Set **Review folder** to a folder that already exists and is writable, for example `/data/review`.

A staged file is saved as:

```text
/data/review/Show Name/Season 01/Show Name - S01E01.mkv
```

That is `{review folder}/{current series folder name}/{season folder}/{episode file name}`.

Sonarr marks the download imported and does not create an episode file yet. The episode shows **In review**. RSS sync and automatic searches will not grab another release for it. Interactive Search still can. Grabbing again replaces that episode's review item.

**Keep original file on upgrade** is off by default. Leave it off to recycle the previous episode file when a reviewed copy is adopted over one you already have. Turn it on to leave that previous file on disk. Sonarr stops tracking it, so a later scan does not import it again.

## After an external tool finishes

An external tool may change the container or the extension (`.mkv` to `.mp4`). It must keep the filename stem. Copy the result into the series folder at the same relative path, then remove the review copy if you no longer need it.

```text
/data/review/Show Name/Season 01/Show Name - S01E01.mp4
        ↓
/tv/Show Name/Season 01/Show Name - S01E01.mp4
```

Tell Sonarr to scan that series. `seriesId` is Sonarr's series id, from the series page or `GET /api/v3/series`. The API key is under **Settings → General**. Replace the host and key.

```bash
curl -sS -X POST "http://sonarr.example:8989/api/v3/command" \
  -H "X-Api-Key: YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"name":"RescanSeries","seriesId":1}'
```

The same command exists on v5:

```bash
curl -sS -X POST "http://sonarr.example:8989/api/v5/command" \
  -H "X-Api-Key: YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"name":"RescanSeries","seriesId":1}'
```

On a match, Sonarr creates one episode file for the episodes on that review item. It keeps the grabbed quality, language, release group, and scene name, and reads MediaInfo from the file in the series folder. The review item is removed. A file in the series folder that does not match a review item is imported the usual way. If nothing in the series folder matches, the review item stays.

## List and remove review items

`GET /api/v3/review` and `GET /api/v5/review` return the current review items, including the series title and episode numbers.

```bash
curl -sS "http://sonarr.example:8989/api/v3/review" \
  -H "X-Api-Key: YOUR_API_KEY"
```

```json
[
  {
    "id": 5,
    "seriesId": 1,
    "seriesTitle": "Show Name",
    "episodeIds": [101],
    "reviewPath": "/data/review/Show Name/Season 01/Show Name - S01E01.mkv",
    "episodes": [
      {
        "id": 101,
        "seasonNumber": 1,
        "episodeNumber": 1,
        "title": "Pilot",
        "hasFile": false
      }
    ]
  }
]
```

`DELETE /api/v3/review/5` and `DELETE /api/v5/review/5` remove that tracking row. They do not delete the file in the review folder.

```bash
curl -sS -X DELETE "http://sonarr.example:8989/api/v3/review/5" \
  -H "X-Api-Key: YOUR_API_KEY"
```

```bash
curl -sS -X DELETE "http://sonarr.example:8989/api/v5/review/5" \
  -H "X-Api-Key: YOUR_API_KEY"
```

You can do the same from the episode status tooltip. The confirmation says the file is left where it is.
