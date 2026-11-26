#!/bin/bash
# The droplet half of deploy.ps1, which copies this to /root/starfold and runs
#   bash /root/starfold/deploy-remote.sh <tag>
# It points .env's STARFOLD_TAG at the new tag, pulls, restarts, and
# health-checks the site. If the check fails, it puts the previous tag back.
# Old images are removed, except the new and previous tags (for a rollback)
# and initial-web (the stopped pre-Ezra site).
# Keep LF line endings (.gitattributes); bash fails on CRLF.
set -u
TAG="${1:?usage: deploy-remote.sh <tag>}"
REPO=renaiparagonic/renai-repo
cd /root/starfold || exit 1

OLD=$(sed -n 's/^STARFOLD_TAG=//p' .env)
echo "running: $OLD   deploying: $TAG"
if [ "$OLD" = "$TAG" ]; then echo "$TAG is already running."; exit 0; fi

set_tag() { sed -i "s/^STARFOLD_TAG=.*/STARFOLD_TAG=$1/" .env; }

# The app on its loopback port, then the public site through nginx.
healthy() {
    local code n
    for n in $(seq 1 40); do
        code=$(curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:8081/ezra || true)
        if [ "$code" = 200 ]; then
            code=$(curl -s -o /dev/null -w '%{http_code}' https://renaiparagonic.live/ezra || true)
            [ "$code" = 200 ] && return 0
        fi
        sleep 1
    done
    echo "health check: /ezra answered $code"
    return 1
}

set_tag "$TAG"
if ! docker compose pull -q; then
    echo "pull failed; nothing changed"
    set_tag "$OLD"
    exit 1
fi
docker compose up -d

if healthy; then
    echo "healthy on $TAG"
    docker images "$REPO" --format '{{.Tag}}' | grep -vxE "$TAG|$OLD|initial-web" \
        | while read -r stale; do docker rmi -f "$REPO:$stale" >/dev/null && echo "removed old image $stale"; done
    docker image prune -f >/dev/null
    exit 0
fi

echo "$TAG is not healthy; rolling back to $OLD. Its last log lines:"
docker compose logs --no-color --tail 30
set_tag "$OLD"
docker compose up -d
if healthy; then echo "rolled back: $OLD is running and healthy"; else echo "ROLLBACK ALSO UNHEALTHY: check the droplet now"; fi
exit 1
