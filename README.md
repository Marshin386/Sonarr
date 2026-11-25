# Sonarr (review location fork)

[![Docker image](https://github.com/Marshin386/Sonarr/actions/workflows/docker.yml/badge.svg?branch=develop)](https://github.com/Marshin386/Sonarr/actions/workflows/docker.yml?query=branch%3Adevelop)
[![Version](https://img.shields.io/badge/dynamic/regex?url=https%3A%2F%2Fraw.githubusercontent.com%2FMarshin386%2FSonarr%2Fdevelop%2Fdistribution%2Ffork-version&search=%5E(%5B%5E%5Cs%5D%2B)&replace=%241&label=version)](https://github.com/Marshin386/Sonarr/blob/develop/docs/review-location.md)
[![License: GPL-3.0](https://img.shields.io/github/license/Marshin386/Sonarr)](https://github.com/Marshin386/Sonarr/blob/develop/LICENSE)
[![Forked from Sonarr/Sonarr](https://img.shields.io/badge/forked%20from-Sonarr%2FSonarr-blue?logo=github)](https://github.com/Sonarr/Sonarr)
[![GHCR](https://img.shields.io/badge/ghcr.io-marshin386%2Fsonarr%3Alatest-blue?logo=github)](https://github.com/Marshin386/Sonarr/pkgs/container/sonarr)

A fork of [Sonarr](https://github.com/Sonarr/Sonarr) that can hold a completed download in a review folder, with an **In review** status, until a series rescan adopts it into the library.

## About this fork

This repository is a fork of [Sonarr/Sonarr](https://github.com/Sonarr/Sonarr), a PVR for Usenet and BitTorrent users.

### What this fork adds

**Review location** (Settings → Media Management → Importing) sends a completed import to a review folder instead of creating an episode file. The download is marked imported, so it leaves the queue. The episode shows an indigo **In review** status until it is adopted.

- Review items do not expire. Remove one from the episode status tooltip, or with `DELETE /api/v3/review/{id}`. That clears the tracking row and does not delete the file.
- RSS sync and automatic searches do not grab another release for an episode that is in review. Interactive Search still can.
- `RescanSeries` adopts a file already in the series folder when its path matches the review item by filename stem. The extension is ignored, so a `.mkv` staged for review can be adopted as `.mp4`. The episode file keeps the grabbed quality, language, release group, and scene name, and stores MediaInfo read from the adopted file.
- **Keep original file on upgrade** is off by default. When it is on, adopting over an existing episode leaves the previous file on disk and stops tracking it.

The fork version is shown in **System → About**. The published image is `ghcr.io/marshin386/sonarr` (linux/amd64 and linux/arm64). The external-tool workflow and API examples are in [docs/review-location.md](docs/review-location.md).

> [!WARNING]
> **Back up your Sonarr config before switching**
>
> This fork upgrades the database (migration 241). Starting official Sonarr on that same database is not supported. Test with a copy of the config folder, and keep a backup of the original.

### Support for this fork

Problems with **review location**, **In review** status, adoption on rescan, this Docker image, or other changes in this fork should be reported on [this repository's Issues](https://github.com/Marshin386/Sonarr/issues), not on the official Sonarr project. Please do not file fork-specific bugs against [Sonarr/Sonarr](https://github.com/Sonarr/Sonarr).

For general Sonarr usage, official docs, and upstream bugs or feature requests, use the [Servarr wiki](https://wiki.servarr.com/sonarr) and [official support channels](https://github.com/Sonarr/Sonarr#support).

## Quick start

The usual image for this fork is `ghcr.io/marshin386/sonarr:latest`, built from the default `develop` branch. Replace the placeholder paths, `PUID`, `PGID`, and `TZ` before you start the stack.

```yaml
services:
  sonarr:
    image: ghcr.io/marshin386/sonarr:latest
    container_name: sonarr
    environment:
      - PUID=1000
      - PGID=1000
      - TZ=Etc/UTC
    ports:
      - "8989:8989"
    volumes:
      - /path/to/config:/config
      - /path/to/tv:/tv
      - /path/to/downloads:/downloads
      - /path/to/review:/data/review
    restart: unless-stopped
```

Then open `http://<host>:8989`. Under **Settings → Media Management → Importing**, turn on **Review location** and set **Review folder** to `/data/review`.

| Image tag | When to use it |
| --- | --- |
| `latest` | Default branch (`develop`) |
| `v<version>` | A published release tag, for example `v5.9.1-review.1` |
| `testing` | Non-default branches only |
| `sha-<commit>` | Pin a specific build |

Before you start, back up the config folder and use a **copy** if you still need official Sonarr. A sample test stack (host port 8990, so it can run beside an existing instance) is in [docs/docker-compose.sample.yml](docs/docker-compose.sample.yml).

---

## Upstream Sonarr

The rest of this document is from the official [Sonarr](https://github.com/Sonarr/Sonarr) project, kept here as a reference. For current docs, see the [Servarr wiki](https://wiki.servarr.com/sonarr). This fork is not affiliated with the Sonarr team.

Sonarr is a PVR for Usenet and BitTorrent users. It can monitor multiple RSS feeds for new episodes of your favorite shows and will grab, sort, and rename them. It can also be configured to automatically upgrade the quality of files already downloaded when a better quality format becomes available.

### Major features

* Support for major platforms: Windows, Linux, macOS, Raspberry Pi, and others.
* Automatically detects new episodes.
* Can scan an existing library and download missing episodes.
* Can watch for a better quality of an episode you already have and upgrade it, for example from DVD to Blu-ray.
* Automatic failed download handling tries another release if one fails.
* Manual search, so you can pick a release or see why one was not grabbed.
* Fully configurable episode renaming.
* Integration with SABnzbd, NZBGet, and other download clients.
* Integration with Kodi and Plex (notifications, library update, and metadata).
* Support for specials and multi-episode releases.

### Official support and docs

[![Wiki](https://img.shields.io/badge/servarr-wiki-181717.svg?maxAge=60)](https://wiki.servarr.com/sonarr)
[![Discord](https://img.shields.io/badge/discord-chat-7289DA.svg?maxAge=60)](https://discord.gg/M6BvZn5)

Note: GitHub Issues on the official project are for bugs and feature requests only. Do not use them for issues that are specific to this fork.

[![GitHub - Bugs and Feature Requests Only](https://img.shields.io/badge/github-issues-red.svg?maxAge=60)](https://github.com/Sonarr/Sonarr/issues)

### Contributors

[API Documentation](https://sonarr.tv/docs/api/)

- [Contribute (GitHub)](CONTRIBUTING.md)
- [Contribution (Wiki Article)](https://wiki.servarr.com/sonarr/contributing)
- [Official contributors](https://github.com/Sonarr/Sonarr/graphs/contributors)

### License

* [GNU GPL v3](http://www.gnu.org/licenses/gpl.html)
* Copyright 2010-2025
