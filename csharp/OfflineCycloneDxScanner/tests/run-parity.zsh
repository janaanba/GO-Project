#!/bin/zsh
set -euo pipefail

root="${0:A:h:h}"
repo="${root:h:h}"
trivy_bin="${TRIVY_BIN:-${repo}/trivy}"
fixture_root="${root}/tests/fixtures/parity"
work_dir="$(mktemp -d)"
trap 'rm -rf "$work_dir"' EXIT

if [[ ! -x "$trivy_bin" ]]; then
  print -u2 "Trivy binary not found or not executable: $trivy_bin"
  exit 2
fi

export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROLL_FORWARD=Major

project_assets="${root}/obj/project.assets.json"
if [[ ! -f "$project_assets" ]]; then
  dotnet restore "$root/OfflineCycloneDxScanner.csproj"
fi

dotnet build "$root/OfflineCycloneDxScanner.csproj" --no-restore --nologo -v:q

parity_failed=0

normalize() {
  local input="$1"
  local output="$2"
  jq -S '
    {
      components: [
        .components[] |
        {
          type,
          name,
          version,
          purl,
          properties: ([.properties[]? |
            select(.name != "FilePath" and
                   .name != "StartLine" and
                   .name != "EndLine")
          ] | sort_by(.name, .value))
        }
      ] | sort_by(.type, .name, (.version // ""), (.purl // "")),
      packageDependencies: [
        .dependencies[] |
        select(.ref | startswith("pkg:")) |
        {
          ref,
          dependsOn: ([.dependsOn[] | select(startswith("pkg:"))] | sort)
        }
      ] | sort_by(.ref)
    }
  ' "$input" > "$output"
}

for ecosystem in composer conan dart gradle maven; do
  fixture="${fixture_root}/${ecosystem}"
  csharp_output="${work_dir}/${ecosystem}-csharp.json"
  trivy_output="${work_dir}/${ecosystem}-trivy.json"
  csharp_normalized="${work_dir}/${ecosystem}-csharp.normalized.json"
  trivy_normalized="${work_dir}/${ecosystem}-trivy.normalized.json"

  print "=== ${ecosystem} ==="
  dotnet run --project "$root/OfflineCycloneDxScanner.csproj" --no-build --no-restore -- \
    "$fixture" --format cyclonedx --offline-scan > "$csharp_output"
  "$trivy_bin" fs --format cyclonedx --offline-scan "$fixture" > "$trivy_output"

  normalize "$csharp_output" "$csharp_normalized"
  normalize "$trivy_output" "$trivy_normalized"

  if cmp -s "$csharp_normalized" "$trivy_normalized"; then
    print "NORMALIZED_IDENTICAL"
  else
    parity_failed=1
    print "NORMALIZED_DIFFERENT"
    diff -u "$trivy_normalized" "$csharp_normalized" || true
  fi

done

exit "$parity_failed"
