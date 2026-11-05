#!/bin/zsh
set -euo pipefail

if [[ $# -ne 1 ]]; then
  print -u2 "usage: $0 <fixture-directory>"
  exit 2
fi

fixture="$1"
root="${0:A:h:h}"
trivy_bin="${TRIVY_BIN:-${root:h:h}/trivy}"

if [[ ! -x "$trivy_bin" ]]; then
  print -u2 "Trivy binary not found or not executable: $trivy_bin"
  exit 2
fi

export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROLL_FORWARD=Major

csharp_output="$(mktemp)"
trivy_output="$(mktemp)"
trap 'rm -f "$csharp_output" "$trivy_output" "$csharp_output.canonical" "$trivy_output.canonical"' EXIT

dotnet run --project "$root/OfflineCycloneDxScanner.csproj" --no-restore -- "$fixture" --format cyclonedx --offline-scan > "$csharp_output"
"$trivy_bin" fs --format cyclonedx --offline-scan "$fixture" > "$trivy_output"

if cmp -s "$csharp_output" "$trivy_output"; then
  print "BYTE_IDENTICAL"
else
  print "BYTE_DIFFERENT"
fi

if jq -S -e . "$csharp_output" > "$csharp_output.canonical" && jq -S -e . "$trivy_output" > "$trivy_output.canonical"; then
  if cmp -s "$csharp_output.canonical" "$trivy_output.canonical"; then
    print "CANONICAL_JSON_IDENTICAL"
  else
    print "CANONICAL_JSON_DIFFERENT"
  fi
else
  print -u2 "Invalid JSON emitted by one or both scanners"
  exit 1
fi
