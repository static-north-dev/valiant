#!/usr/bin/env bash
set -euo pipefail

release_tag="${1:-}"
output_file="${2:-}"
package_version="${release_tag#v}"
semver='^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-[0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?$'

if [[ ! "$package_version" =~ $semver ]]; then
  echo "Release tag '$release_tag' must be SemVer without build metadata and with an optional leading v." >&2
  exit 1
fi

docs_major="${BASH_REMATCH[1]}"
if [[ "$package_version" == *-* ]]; then
  prerelease="${package_version#*-}"
  IFS='.' read -r -a identifiers <<< "$prerelease"
  for identifier in "${identifiers[@]}"; do
    if [[ "$identifier" =~ ^[0-9]+$ && "$identifier" != "0" && "$identifier" == 0* ]]; then
      echo "Numeric prerelease identifier '$identifier' must not contain a leading zero." >&2
      exit 1
    fi
  done
fi

if [ -n "$output_file" ]; then
  {
    echo "package_version=$package_version"
    echo "docs_major=$docs_major"
  } >> "$output_file"
else
  echo "package_version=$package_version"
  echo "docs_major=$docs_major"
fi

echo "Preparing Valiant $package_version (docs major $docs_major)."
