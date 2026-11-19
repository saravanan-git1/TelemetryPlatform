#!/usr/bin/env bash
set -euo pipefail

if [[ "${EUID}" -ne 0 ]]; then
  echo "Run this installer as root (for example, with sudo)." >&2
  exit 1
fi

publish_dir="${1:-}"
if [[ -z "${publish_dir}" || ! -f "${publish_dir}/TelemetryPlatform.Worker.dll" ]]; then
  echo "Usage: $0 <directory-containing-published-worker>" >&2
  exit 2
fi

id telemetry >/dev/null 2>&1 || useradd --system --home-dir /var/lib/telemetry-platform --shell /usr/sbin/nologin telemetry
install -d -o telemetry -g telemetry -m 0750 /opt/telemetry-platform
install -d -o root -g telemetry -m 0750 /etc/telemetry-platform
cp -a "${publish_dir}/." /opt/telemetry-platform/
chown -R root:telemetry /opt/telemetry-platform
chmod -R u=rwX,g=rX,o= /opt/telemetry-platform

if [[ ! -f /etc/telemetry-platform/worker.env ]]; then
  install -o root -g telemetry -m 0640 /dev/null /etc/telemetry-platform/worker.env
  echo "Create /etc/telemetry-platform/worker.env with the required ConnectionStrings__Postgres and Redis__ConnectionString values before starting the service." >&2
fi

install -o root -g root -m 0644 "$(dirname "$0")/telemetry-platform-worker.service" /etc/systemd/system/
systemctl daemon-reload
systemctl enable telemetry-platform-worker.service
echo "Worker installed. Configure /etc/telemetry-platform/worker.env, then run: systemctl start telemetry-platform-worker"
