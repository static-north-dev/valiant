# Valiant Docs

This directory contains the Docusaurus documentation site for StaticNorth.Valiant.

The site uses one docs plugin instance for the complete Valiant release.
Validation, Endpoints, Mapping, and Settings remain separate navigation
sections, but share one version inventory, snapshot, and version selector.

## Commands

```bash
npm install
npm run start
npm run build
npm run serve
```

## LLM and crawler files

The static site publishes these root files:

- `/llms.txt`: a concise, curated entry point for language models and agents;
- `/llms-full.txt`: the complete **current** documentation in one generated
  Markdown context; it deliberately excludes `versioned_docs` so it does not
  mix historical APIs with the current package family;
- `/robots.txt`: allows indexing and points at Docusaurus's generated
  `/sitemap.xml`.

`static/llms-full.txt` is generated from `content` in the same Validation →
Endpoints → Mapping → Settings order as `sidebars.js`. It is committed rather
than regenerated during builds, so a production build is reproducible from its
pinned source commit. Run these commands before committing a content or sidebar
change:

```bash
npm run docs:generate-llms
npm run docs:check-llms
```

## Cloudflare Pages

The site builds static files, so no Cloudflare adapter is required. CI builds
the site for pushes and pull requests targeting `main`, but never deploys it.

Documentation publication is independent from NuGet releases. Run the
**Publish Docs** workflow manually whenever the site should be refreshed. It
pins the current `main` commit, snapshots the major tracked in
`current-major.json`, merges it into the complete history on `main`, ensures
the `valiant-docs` Cloudflare Pages project exists, and deploys the exact
persisted commit.

The workflow creates a missing Pages project with production branch `main`.
The deployed static output is `docs/build`.

Add these GitHub Actions repository secrets:

- `CLOUDFLARE_ACCOUNT_ID`: the Cloudflare **account ID**, not the zone ID;
- `CLOUDFLARE_API_TOKEN`: a custom token with
  **Account → Cloudflare Pages → Edit** permission. Its resource scope must
  include the account identified by `CLOUDFLARE_ACCOUNT_ID`; selecting a
  domain or zone such as `staticnorth.dev` does not grant access to the
  account-level Pages API.

The deployment job also grants its generated `GITHUB_TOKEN`
`deployments: write`, allowing Wrangler Action to create a GitHub Deployment
and attach the Cloudflare Pages URL.

The snapshot persistence job grants its `GITHUB_TOKEN` only `contents: write`.
Repository rules for `main` must allow `github-actions[bot]` to push this
generated documentation commit. If `main` requires pull requests, signed
commits, status checks, or another rule the bot cannot satisfy, add a narrow
ruleset bypass for GitHub Actions or replace the token with a narrowly scoped
GitHub App credential. A broad personal access token is not recommended.

## Versioning

The default docs plugin reads all release documentation from `content`:

- Validation: `content/validation`
- Endpoints: `content/endpoints`
- Mapping: `content/mapping`
- Settings: `content/settings`

Documentation versions are SemVer majors only. Major `1` represents the latest
published `1.x` documentation, not only the original `1.0.0` content.
`current-major.json` is the reviewed source of truth for the content currently
being authored. The **Publish Docs** workflow refreshes one snapshot containing
all four sections. Change that file to `"2"` before the first major 2
publication; later major 2 runs refresh only major 2 and preserve every older
major.

The local commands remain useful for previewing or diagnosing versioning, but
they are not a publication prerequisite:

```bash
npm run docs:current-major
npm run docs:snapshot-major -- 1
npm run docs:validate-major -- 1
npm run build
```

The snapshot command refreshes an existing major without reordering it, creates
a newer major when needed, preserves all other majors, and rolls back partial
changes if the unified snapshot fails. A valid snapshot must contain all four
section indexes and all four sidebar definitions. The publication workflow's
import command accepts the constrained snapshot artifact, rejects unexpected
paths and symlinks, replaces only the requested major, and preserves the
remaining history.

Production builds set `DOCS_INCLUDE_CURRENT=false`, so only released numeric
major versions appear. Normal local and CI builds retain the `current` docs
version for previewing changes before they merge to `main`.

The newest released major uses `/validation/`, `/endpoints/`, `/mapping/`, and
`/settings/`. Older majors use `/<major>/<section>/`, such as
`/1/endpoints/`. Section navigation follows the active shared version.

The navbar version selector intentionally remains a dropdown when only one
released major exists. Docusaurus normally renders a single version as a plain
link, so `src/theme/NavbarItem/DocsVersionDropdownNavbarItem` contains a narrow
override of the Docusaurus 3.10.2 component. When upgrading Docusaurus, diff
that override against the new upstream component before changing the pinned
version.

## Publishing documentation

The **Publish Docs** workflow is manually triggered and always publishes the
latest `main` content for the major in `current-major.json`. No GitHub
Release is required. The workflow must exist on the repository's default
branch. To publish:

1. Merge the desired documentation and, when beginning a new major, the
   `current-major.json` change into `main`.
2. Open **Actions → Publish Docs → Run workflow**.
3. The workflow pins the starting `main` SHA, tests and audits the docs,
   creates the shared major snapshot, and validates every generated route.
4. It verifies or creates the Cloudflare Pages project, commits only
   `versions.json`, `versioned_docs`, and `versioned_sidebars` to `main`,
   rebuilds the exact persisted commit, and deploys that verified artifact.

If `main` moves before the generated snapshot is persisted, the workflow
fails instead of publishing stale documentation on top of newer content. Run it
again to publish the new head. A no-change rerun still verifies and deploys the
existing persisted commit.

Repository rules for `main` must allow `github-actions[bot]` to push the
generated snapshot commit. The job has only `contents: write`; it cannot bypass
required pull requests, signed commits, or status checks. Add a narrow ruleset
bypass for GitHub Actions or replace the token with a narrowly scoped GitHub App
credential when those protections are enabled.

Commits pushed with the repository `GITHUB_TOKEN` do not recursively trigger
the `push` CI workflow. The manual workflow performs its own versioning tests,
audit, production build, and route checks before deployment.

## Publishing NuGet releases

Publishing a GitHub Release only publishes NuGet packages. The release tag—not
the release title—sets the package version. Tags must be SemVer without build
metadata and with an optional `v` prefix, such as `v1.2.0` or
`1.2.0-preview.1`. The workflow checks out the immutable tag, restores, builds,
tests, audits, verifies package consumers, runs Native AOT smoke verification,
and publishes the verified packages through NuGet trusted publishing. Those
commands use `StaticNorth.Valiant.Release.slnf`, which contains every .NET project but
deliberately excludes the documentation `.esproj`; a release cannot build or
deploy the site indirectly.

Update the NuGet.org trusted-publishing policy to use
`.github/workflows/release.yml`; trusted-publishing policies can be bound to the
workflow filename. Configure the repository variable `NUGET_USER` for releases.
Configure `CLOUDFLARE_ACCOUNT_ID` and `CLOUDFLARE_API_TOKEN` only for the
separate documentation workflow.

Rerunning a partially failed release is safe because NuGet publication uses
`--skip-duplicate`. CI and documentation publication fail on high or critical
npm advisories, with no advisory exceptions. Compatible security patches for
transitive dependencies are pinned in `package.json` overrides and the lockfile,
including `image-size` 2.0.4, which replaces the previously exempt vulnerable
image parsers. Every workflow job that invokes a Docusaurus build has a
20-minute timeout. The development-server
dependency graph also reports a moderate `uuid` advisory with no upstream fix;
it is build tooling and is not shipped in the static site.
