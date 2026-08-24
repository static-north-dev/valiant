# Contributing to Valiant

Thank you for helping improve Valiant.

Participation is governed by [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

## Before opening a pull request

- Target the `main` branch.
- Keep changes focused and preserve public API compatibility unless a breaking
  change is explicitly agreed first.
- Add or update tests for behavior changes.
- Update user documentation for public API, diagnostics, package, or workflow
  changes.
- Regenerate committed lock files when dependencies or the pinned SDK change.
- Consider trimming and Native AOT behavior for runtime and source-generator
  changes.

## Local verification

Install the exact SDK declared in `global.json`, then run:

```bash
dotnet restore StaticNorth.Valiant.sln --locked-mode
dotnet build StaticNorth.Valiant.sln --configuration Debug --no-restore
dotnet test --solution StaticNorth.Valiant.sln --configuration Debug --no-build
dotnet build StaticNorth.Valiant.sln --configuration Release --no-restore
dotnet test --solution StaticNorth.Valiant.sln --configuration Release --no-build
```

Documentation changes should also run:

```bash
cd docs
npm ci
npm run audit:dependencies
npm run test:docs-versions
npm run build
```

Package and Native AOT changes should run the relevant verification commands
documented in the root README and `AGENT_GUIDE.md`.

## Pull requests

Explain the problem and why the proposed change belongs in Valiant. Include
the verification commands you ran and call out public API, generated output,
package layout, dependency, and documentation effects.

Commit names and email addresses become public repository metadata. Use an
address you are comfortable publishing, such as GitHub's `noreply` address.

## Licensing

Valiant is licensed under Apache-2.0. By intentionally submitting a
contribution for inclusion, you agree that it may be distributed under the
same license, consistent with the contribution terms in the repository's
Apache License 2.0.
