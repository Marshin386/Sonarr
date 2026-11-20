#!/bin/sh
# Drop privileges to PUID:PGID and start Sonarr with /config as app data.
# Equivalent to the linuxserver.io Sonarr image environment variables.
set -eu

PUID="${PUID:-1000}"
PGID="${PGID:-1000}"
TZ="${TZ:-Etc/UTC}"
UMASK="${UMASK:-022}"

if [ -f "/usr/share/zoneinfo/${TZ}" ]; then
    ln -snf "/usr/share/zoneinfo/${TZ}" /etc/localtime
    printf '%s\n' "${TZ}" > /etc/timezone
fi

# Inherited by Sonarr across exec. .NET creates new files as 0666 and new
# directories as 0777, then the kernel applies this mask.
umask "${UMASK}"

if getent group sonarr >/dev/null 2>&1; then
    groupmod -o -g "${PGID}" sonarr
else
    groupadd -o -g "${PGID}" sonarr
fi

if id sonarr >/dev/null 2>&1; then
    usermod -o -u "${PUID}" -g sonarr -d /config sonarr
else
    useradd -o -u "${PUID}" -g sonarr -d /config -M -s /usr/sbin/nologin sonarr
fi

mkdir -p /config
# The directory and anything already in it (config.xml, logs, the database)
# have to be owned by PUID:PGID. A non-recursive chown leaves files from a
# previous root run or a different PUID unreadable by the user Sonarr drops to.
chown -R sonarr:sonarr /config

cd /app/sonarr/bin
exec gosu sonarr:sonarr /app/sonarr/bin/Sonarr -nobrowser -data=/config
