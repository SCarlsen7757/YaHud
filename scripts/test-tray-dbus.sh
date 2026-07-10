#!/usr/bin/env bash
# Headless protocol test for the Linux tray icon (org.kde.StatusNotifierItem +
# com.canonical.dbusmenu). Verifies the D-Bus surface without a desktop panel:
# runs a private session bus with a StatusNotifierWatcher stub, starts YaHud,
# and asserts registration, introspection, properties, menu layout, and Quit.
#
# Requires: dbus (dbus-run-session), busctl (systemd), python3-dbus, python3-gi.
# Usage: scripts/test-tray-dbus.sh <path to YaHud executable or .dll>
set -euo pipefail

APP=${1:?usage: $0 <path to YaHud executable or .dll>}

# Re-exec inside a private session bus.
if [ -z "${TRAY_TEST_IN_SESSION:-}" ]; then
    exec dbus-run-session -- env TRAY_TEST_IN_SESSION=1 bash "$0" "$APP"
fi

BUS="--address=${DBUS_SESSION_BUS_ADDRESS}"
WORKDIR=$(mktemp -d)
trap 'kill $(jobs -p) 2>/dev/null; rm -rf "$WORKDIR"' EXIT

fail() { echo "FAIL: $*" >&2; exit 1; }
pass() { echo "PASS: $*"; }

# --- StatusNotifierWatcher stub ---------------------------------------------
python3 - "$WORKDIR/registered" <<'PYEOF' &
import sys
import dbus
import dbus.service
from dbus.mainloop.glib import DBusGMainLoop
from gi.repository import GLib

outfile = sys.argv[1]
DBusGMainLoop(set_as_default=True)

class Watcher(dbus.service.Object):
    def __init__(self, bus):
        name = dbus.service.BusName('org.kde.StatusNotifierWatcher', bus)
        super().__init__(name, '/StatusNotifierWatcher')

    @dbus.service.method('org.kde.StatusNotifierWatcher', in_signature='s')
    def RegisterStatusNotifierItem(self, service):
        with open(outfile, 'w') as f:
            f.write(service)

Watcher(dbus.SessionBus())
GLib.MainLoop().run()
PYEOF

for _ in $(seq 1 50); do
    busctl "$BUS" list --no-legend 2>/dev/null | grep -q org.kde.StatusNotifierWatcher && break
    sleep 0.1
done
busctl "$BUS" list --no-legend | grep -q org.kde.StatusNotifierWatcher \
    || fail "StatusNotifierWatcher stub did not come up"

# --- Start YaHud --------------------------------------------------------------
case "$APP" in
    *.dll) dotnet "$APP" --urls http://127.0.0.1:0 >"$WORKDIR/app.log" 2>&1 & ;;
    *)     "$APP" --urls http://127.0.0.1:0 >"$WORKDIR/app.log" 2>&1 & ;;
esac
APP_PID=$!

for _ in $(seq 1 100); do
    [ -s "$WORKDIR/registered" ] && break
    kill -0 "$APP_PID" 2>/dev/null || { cat "$WORKDIR/app.log"; fail "app exited early"; }
    sleep 0.1
done
[ -s "$WORKDIR/registered" ] || { cat "$WORKDIR/app.log"; fail "app never called RegisterStatusNotifierItem"; }
NAME=$(cat "$WORKDIR/registered")
pass "RegisterStatusNotifierItem called with '$NAME'"

# --- Assertions ----------------------------------------------------------------
busctl "$BUS" introspect "$NAME" /StatusNotifierItem >/dev/null \
    || fail "introspection of /StatusNotifierItem"
pass "introspect /StatusNotifierItem"

GETALL=$(busctl "$BUS" call "$NAME" /StatusNotifierItem org.freedesktop.DBus.Properties GetAll s org.kde.StatusNotifierItem) \
    || fail "Properties.GetAll on StatusNotifierItem"
echo "$GETALL" | grep -q '"IconPixmap"' || fail "GetAll reply is missing IconPixmap: $GETALL"
echo "$GETALL" | grep -q '"Menu"' || fail "GetAll reply is missing Menu: $GETALL"
pass "Properties.GetAll marshals correctly"

LAYOUT=$(busctl "$BUS" -- call "$NAME" /MenuBar com.canonical.dbusmenu GetLayout iias 0 -1 0) \
    || fail "dbusmenu GetLayout"
echo "$LAYOUT" | grep -q 'Quit' || fail "GetLayout reply is missing the Quit item: $LAYOUT"
pass "dbusmenu GetLayout returns the Quit menu"

busctl "$BUS" call "$NAME" /MenuBar com.canonical.dbusmenu Event isvu 1 clicked s "" 0 \
    || fail "dbusmenu Event(clicked)"

for _ in $(seq 1 100); do
    kill -0 "$APP_PID" 2>/dev/null || break
    sleep 0.1
done
if kill -0 "$APP_PID" 2>/dev/null; then
    fail "app did not stop after Quit was clicked"
fi
pass "Quit menu event stopped the application"

echo "ALL TRAY D-BUS TESTS PASSED"
