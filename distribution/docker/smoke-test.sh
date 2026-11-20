#!/bin/sh
# Start a locally loaded image and fail unless the UI and /ping respond, the
# Linux platform assembly is present, and /api/v3/system/status reports the fork version.
set -eu

image="${1:-sonarr:smoke}"
expected="${2:-}"
name=sonarr-smoke
log=/tmp/sonarr-smoke.log

if [ -z "${expected}" ]; then
    echo 'expected fork version is required' >&2
    exit 1
fi

# Run as an arbitrary non-root uid, with the entrypoint skipped, so a mode
# that only root can use fails the build. NuGet's creat(0766) plus umask 022
# leaves bundled binaries at 0744, which is the ffprobe import failure.
audit_non_root() {
    uid="$1"
    gid="$2"
    echo "checking /app as ${uid}:${gid}"
    docker run --rm --user "${uid}:${gid}" --entrypoint sh "${image}" -c '
        set -eu
        fail=0
        is_exec() {
            case "$1" in
                1|3|5|7) return 0 ;;
                *) return 1 ;;
            esac
        }
        if ! find /app /entrypoint.sh -print > /tmp/sonarr-paths 2>/tmp/sonarr-find.err; then
            echo "cannot list /app" >&2
            cat /tmp/sonarr-find.err >&2
            exit 1
        fi
        while IFS= read -r path; do
            [ -n "${path}" ] || continue
            if [ -d "${path}" ]; then
                if [ ! -r "${path}" ] || [ ! -x "${path}" ]; then
                    echo "directory not accessible: ${path}" >&2
                    fail=1
                fi
                continue
            fi
            if [ ! -r "${path}" ]; then
                echo "file not readable: ${path}" >&2
                fail=1
                continue
            fi
            if [ ! -f "${path}" ]; then
                continue
            fi
            bits=$(stat -c %a "${path}")
            while [ "${#bits}" -gt 3 ]; do
                bits=${bits#?}
            done
            user=${bits%??}
            rest=${bits#?}
            group=${rest%?}
            other=${bits#??}
            if is_exec "${user}" || is_exec "${group}"; then
                if ! is_exec "${other}"; then
                    echo "executable is not runnable by other users: ${path} (mode $(stat -c %a "${path}"))" >&2
                    fail=1
                fi
            fi
        done < /tmp/sonarr-paths
        if [ "${fail}" -ne 0 ]; then
            exit 1
        fi
        probe=$(/app/sonarr/bin/ffprobe -version 2>&1) || {
            echo "ffprobe failed: ${probe}" >&2
            exit 1
        }
        printf "%s" "${probe}" | grep -q ffprobe
        test -x /app/sonarr/bin/Sonarr
        test -r /app/sonarr/package_info
        test -d /app/sonarr/bin/UI
    '
}

audit_non_root 1000 1000
audit_non_root 1001 100

# Entrypoint: a non-default PUID/PGID must own /config, and UMASK must apply
# to files Sonarr creates. Stop this container before the full smoke test.
perm_name=sonarr-perms
docker rm -f "${perm_name}" >/dev/null 2>&1 || true
docker run -d --name "${perm_name}" -e PUID=1001 -e PGID=100 -e UMASK=027 "${image}"
cleanup_perm() {
    docker logs "${perm_name}" >&2 || true
    docker rm -f "${perm_name}" >/dev/null 2>&1 || true
}
trap cleanup_perm EXIT

i=0
while [ "${i}" -lt 40 ]; do
    if docker exec "${perm_name}" test -s /config/config.xml; then
        break
    fi
    i=$((i + 1))
    sleep 2
done

if ! docker exec "${perm_name}" test -s /config/config.xml; then
    echo 'Sonarr did not create /config/config.xml as PUID 1001' >&2
    exit 1
fi

config_stat="$(docker exec "${perm_name}" stat -c '%u %g %a' /config/config.xml)"
echo "config.xml as 1001:100 umask 027: ${config_stat}"
if [ "${config_stat}" != "1001 100 640" ]; then
    echo 'expected config.xml to be owned by 1001:100 with mode 640' >&2
    exit 1
fi

app_uid="$(docker exec "${perm_name}" stat -c %u /proc/1)"
if [ "${app_uid}" != "1001" ]; then
    echo "Sonarr is running as uid ${app_uid}, expected 1001" >&2
    exit 1
fi

docker exec --user 1001:100 "${perm_name}" /app/sonarr/bin/ffprobe -version >/dev/null
docker exec --user 1001:100 "${perm_name}" test -r /config/config.xml
trap - EXIT
docker rm -f "${perm_name}" >/dev/null 2>&1 || true

docker rm -f "${name}" >/dev/null 2>&1 || true
docker run -d --name "${name}" -p 8989:8989 -e PUID=1000 -e PGID=1000 "${image}"

cleanup() {
    echo '--- container logs ---'
    docker logs "${name}" > "${log}" 2>&1 || true
    tail -n 200 "${log}" || true
    docker rm -f "${name}" >/dev/null 2>&1 || true
}
trap cleanup EXIT

docker exec "${name}" test -s /app/sonarr/bin/Sonarr.Mono.dll
docker exec "${name}" sh -c 'ls /app/sonarr/bin/Openur.Mono.Unix.* /app/sonarr/bin/libMono.Unix.* >/dev/null'

i=0
while [ "${i}" -lt 40 ]; do
    if curl -fsS http://127.0.0.1:8989/ping | grep -q '"status": *"OK"'; then
        break
    fi
    i=$((i + 1))
    sleep 3
done

if ! curl -fsS http://127.0.0.1:8989/ping | grep -q '"status": *"OK"'; then
    echo 'Sonarr did not answer /ping with status OK' >&2
    exit 1
fi

# Forms authentication is on by default, so / redirects to the login page.
ui_code="$(curl -sS -o /dev/null -w '%{http_code}' http://127.0.0.1:8989/)"
echo "ui status: ${ui_code}"
case "${ui_code}" in
    200|302) ;;
    *)
        echo 'Sonarr web UI did not respond' >&2
        exit 1
        ;;
esac

curl -fsSL -o /tmp/sonarr-ui.html http://127.0.0.1:8989/
if ! grep -qi sonarr /tmp/sonarr-ui.html; then
    echo 'Sonarr web UI response did not look like the Sonarr page' >&2
    exit 1
fi

app_uid="$(docker exec "${name}" stat -c %u /proc/1)"
if [ "${app_uid}" != "1000" ]; then
    echo "Sonarr is running as uid ${app_uid}, expected 1000" >&2
    exit 1
fi

docker exec --user 1000:1000 "${name}" /app/sonarr/bin/ffprobe -version >/dev/null

config_stat="$(docker exec "${name}" stat -c '%u %g %a' /config/config.xml)"
echo "config.xml as 1000:1000 umask 022: ${config_stat}"
if [ "${config_stat}" != "1000 1000 644" ]; then
    echo 'expected config.xml to be owned by 1000:1000 with mode 644' >&2
    exit 1
fi

api_key="$(docker exec "${name}" sed -n 's:.*<ApiKey>\([^<]*\)</ApiKey>.*:\1:p' /config/config.xml)"
if [ -z "${api_key}" ]; then
    echo 'could not read the API key from /config/config.xml' >&2
    exit 1
fi

status="$(curl -fsS -H "X-Api-Key: ${api_key}" http://127.0.0.1:8989/api/v3/system/status)"
echo "system status: ${status}"

if ! printf '%s' "${status}" | grep -Eq "\"version\": *\"${expected}\""; then
    echo "system status version is not ${expected}" >&2
    exit 1
fi

if ! printf '%s' "${status}" | grep -Eq "\"packageVersion\": *\"${expected}\""; then
    echo "system status packageVersion is not ${expected}" >&2
    exit 1
fi

docker logs "${name}" > "${log}" 2>&1 || true
if grep -E 'Could not load file or assembly|FileNotFoundException|DllNotFoundException' "${log}"; then
    echo 'Sonarr log contains a missing-assembly error' >&2
    exit 1
fi

if ! grep -q "Version ${expected}" "${log}"; then
    echo "startup log did not report Version ${expected}" >&2
    exit 1
fi

echo "smoke test passed: UI and /ping responded, version is ${expected}"
