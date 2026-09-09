#!/usr/bin/env bash
#
# The frontend rules a compiler cannot see. Both of these were broken in code
# that built cleanly and passed every test, which is why they run in CI.
#
#   1. No colour literal in a component stylesheet  (docs/DESIGN_SYSTEM.md, §6
#      and "Rules for anyone editing the frontend"). Only var(--*).
#   2. Wall-clock DateTime renders with the 'UTC' argument, an instant renders
#      without it (docs/PROJECT_OVERVIEW.md §4, "Two kinds of DateTime").
#      Getting it backwards silently shifts a booking by the reader's offset.
#
# Run from the repository root. Exits non-zero on the first rule that fails, so
# read the whole output rather than only the last line.

set -uo pipefail

APP="src/Web/ClientApp/src/app"
failed=0

# Fields the server stores verbatim as the calendar day someone picked. They
# MUST carry :'UTC'.
WALL_CLOCK="startDate|endDate|firstCirculationDate|birthDate|expenseDate|payementDate|periodStart|periodEnd"

# Fields recorded from GetUtcNow(). They MUST NOT carry it — "when did this
# happen" means local time to the person reading it.
INSTANTS="expiresAt|sentAt|submittedAt|reviewedAt|issuedAt|createdAt|generatedAt|personalDataErasedAt"

# These two templates render DateTimeOffset properties (AgencySubscriptionDto,
# PlatformDashboardDto). An offset is already an unambiguous instant, so the
# local pipe is correct and adding 'UTC' would shift a subscription boundary.
# Both files carry a comment saying so.
WALL_CLOCK_ALLOWED="agency/agency-detail.component.html|platform-dashboard/platform-dashboard.component.html"

echo "==> Colour literals in component styles"
# Comments are stripped first: DESIGN_SYSTEM.md and several sheets legitimately
# name a hex value in prose while explaining why a token exists.
literals=$(
  find "$APP" -type f \( -name '*.css' -o -name '*.scss' \) -print0 |
    xargs -0 -I{} sh -c '
      perl -0777 -pe "s{/\*.*?\*/}{}gs; s{//[^\n]*}{}g" "$1" |
        grep -nE "#[0-9a-fA-F]{3,8}\b|rgba?\([0-9]" |
        grep -v "var(--" |
        sed "s|^|$1:|"
    ' _ {}
)

if [ -n "$literals" ]; then
  echo "$literals"
  echo "FAIL: a component stylesheet hard-codes a colour. Use var(--token), or add a token deliberately."
  failed=1
else
  echo "ok"
fi

echo "==> Wall-clock dates missing the 'UTC' argument"
missing=$(
  grep -rnE "($WALL_CLOCK) *\| *date:'[^']*'" "$APP" --include='*.html' |
    grep -v ":'UTC'" |
    grep -vE "$WALL_CLOCK_ALLOWED"
)

if [ -n "$missing" ]; then
  echo "$missing"
  echo "FAIL: a wall-clock field renders in local time, so it shows the wrong day west of UTC."
  failed=1
else
  echo "ok"
fi

echo "==> Instants wrongly forced to UTC"
forced=$(grep -rnE "($INSTANTS) *\| *date:'[^']*':'UTC'" "$APP" --include='*.html')

if [ -n "$forced" ]; then
  echo "$forced"
  echo "FAIL: an instant renders in UTC, so it disagrees with every other screen showing the same event."
  failed=1
else
  echo "ok"
fi

exit "$failed"
