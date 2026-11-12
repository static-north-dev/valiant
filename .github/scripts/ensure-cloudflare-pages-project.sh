#!/usr/bin/env bash

set -euo pipefail

account_id="${CLOUDFLARE_ACCOUNT_ID:-}"
api_token="${CLOUDFLARE_API_TOKEN:-}"
project_name="${CLOUDFLARE_PAGES_PROJECT:-}"
production_branch="${CLOUDFLARE_PAGES_PRODUCTION_BRANCH:-}"
curl_bin="${CURL_BIN:-curl}"
api_origin="${CLOUDFLARE_API_ORIGIN:-https://api.cloudflare.com/client/v4}"

if [ -z "$account_id" ]; then
  echo "CLOUDFLARE_ACCOUNT_ID secret is required." >&2
  exit 1
fi

if [ -z "$api_token" ]; then
  echo "CLOUDFLARE_API_TOKEN secret is required." >&2
  exit 1
fi

if [ -z "$project_name" ]; then
  echo "CLOUDFLARE_PAGES_PROJECT is required." >&2
  exit 1
fi

if [ -z "$production_branch" ]; then
  echo "CLOUDFLARE_PAGES_PRODUCTION_BRANCH is required." >&2
  exit 1
fi

response_file="$(mktemp)"
trap 'rm -f "$response_file"' EXIT

project_url="$api_origin/accounts/$account_id/pages/projects/$project_name"
projects_url="$api_origin/accounts/$account_id/pages/projects"
http_status=""

request() {
  local method="$1"
  local url="$2"
  local body="${3:-}"
  local curl_arguments=(
    --silent
    --show-error
    --output "$response_file"
    --write-out "%{http_code}"
    --request "$method"
    --header "Authorization: Bearer $api_token"
    --header "Content-Type: application/json"
    "$url"
  )

  if [ -n "$body" ]; then
    curl_arguments+=(--data "$body")
  fi

  if http_status="$("$curl_bin" "${curl_arguments[@]}")"; then
    if [[ ! "$http_status" =~ ^[0-9]{3}$ ]]; then
      echo "Cloudflare returned an invalid HTTP status: $http_status" >&2
      exit 1
    fi
  else
    local curl_exit_code="$?"
    echo "Cloudflare request failed before receiving an HTTP response (curl exit $curl_exit_code)." >&2
    exit "$curl_exit_code"
  fi
}

print_api_errors() {
  node - "$response_file" <<'NODE'
const fs = require('node:fs');

const responsePath = process.argv[2];
let response;

try {
  response = JSON.parse(fs.readFileSync(responsePath, 'utf8'));
} catch {
  console.error('Cloudflare returned a malformed JSON response.');
  process.exit(0);
}

const errors = [
  ...(Array.isArray(response.errors) ? response.errors : []),
  ...(Array.isArray(response.messages) ? response.messages : []),
].slice(0, 5);

if (errors.length === 0) {
  console.error('Cloudflare did not include an API error message.');
  process.exit(0);
}

for (const error of errors) {
  const code = error && error.code !== undefined ? ` (${error.code})` : '';
  const message = error && error.message ? error.message : 'Unknown Cloudflare API error';
  console.error(`Cloudflare API${code}: ${message}`);
}
NODE
}

validate_project_response() {
  node - "$response_file" "$project_name" "$production_branch" <<'NODE'
const fs = require('node:fs');

const [responsePath, expectedName, expectedBranch] = process.argv.slice(2);
let response;

try {
  response = JSON.parse(fs.readFileSync(responsePath, 'utf8'));
} catch {
  console.error('Cloudflare returned a malformed JSON response.');
  process.exit(1);
}

if (response.success !== true) {
  console.error('Cloudflare returned an unsuccessful API response.');
  process.exit(1);
}

if (!response.result || response.result.name !== expectedName) {
  console.error(`Cloudflare returned an unexpected Pages project; expected "${expectedName}".`);
  process.exit(1);
}

if (response.result.production_branch !== expectedBranch) {
  console.error(
    `Cloudflare Pages project "${expectedName}" uses production branch ` +
      `"${response.result.production_branch}", expected "${expectedBranch}".`,
  );
  process.exit(1);
}
NODE
}

fail_with_access_guidance() {
  local operation="$1"

  echo "Cloudflare Pages $operation failed with HTTP $http_status." >&2
  print_api_errors
  echo "Confirm CLOUDFLARE_ACCOUNT_ID is the account ID, not a zone ID." >&2
  echo "The API token must target that Cloudflare account (not a domain/zone) and grant Account > Cloudflare Pages > Edit." >&2
  exit 1
}

verify_existing_project() {
  request GET "$project_url"

  if [ "$http_status" != "200" ]; then
    fail_with_access_guidance "project lookup after create conflict"
  fi

  if ! validate_project_response; then
    print_api_errors
    exit 1
  fi
}

request GET "$project_url"

case "$http_status" in
  200)
    if ! validate_project_response; then
      print_api_errors
      exit 1
    fi

    echo "Cloudflare Pages project \"$project_name\" is ready."
    ;;
  404)
    echo "Cloudflare Pages project \"$project_name\" was not found; creating it."
    create_body="$(node -e \
      'process.stdout.write(JSON.stringify({name: process.argv[1], production_branch: process.argv[2]}))' \
      "$project_name" "$production_branch")"
    request POST "$projects_url" "$create_body"

    case "$http_status" in
      2??)
        if ! validate_project_response; then
          print_api_errors
          exit 1
        fi

        echo "Created Cloudflare Pages project \"$project_name\"."
        ;;
      409)
        echo "Cloudflare Pages project creation raced with another request; verifying the existing project."
        verify_existing_project
        echo "Cloudflare Pages project \"$project_name\" is ready."
        ;;
      *)
        fail_with_access_guidance "project creation"
        ;;
    esac
    ;;
  *)
    fail_with_access_guidance "project lookup"
    ;;
esac
