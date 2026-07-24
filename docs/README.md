# Valiant Docs

This directory contains the Docusaurus documentation site for Valiant.

The site uses one docs plugin instance per Valiant subproject so Validation,
Endpoints, Mapping, and Settings can have independent version histories.

## Commands

```bash
npm install
npm run start
npm run build
npm run serve
```

## Cloudflare Pages

Use `docs` as the project root, `npm run build` as the build command, and
`build` as the build output directory.

The site builds static files, so no Cloudflare adapter is required.

The CI workflow builds the documentation for every push, pull request, and
manual run. After the complete CI pipeline succeeds, a non-pull-request run on
`main` deploys `docs/build` to the `valiant-docs` Cloudflare Pages project.
Other branches never receive Cloudflare credentials and do not create preview
deployments.

Create the Pages project before enabling the workflow:

- project name: `valiant-docs`;
- production branch: `main`;
- build output: `docs/build`.

Add these GitHub Actions repository secrets:

- `CLOUDFLARE_ACCOUNT_ID`: the account that owns the Pages project;
- `CLOUDFLARE_API_TOKEN`: a custom account token with
  **Cloudflare Pages: Edit** permission.

The deployment job also grants its generated `GITHUB_TOKEN`
`deployments: write`, allowing Wrangler Action to create a GitHub Deployment
and attach the Cloudflare Pages URL. The first successful `main` run is the
integration check for the external project, credentials, and production-branch
configuration.

## Versioning

Each product has its own docs plugin instance:

- Validation: default docs instance in `validation`
- Endpoints: `endpoints` docs instance in `endpoints`
- Mapping: `mapping` docs instance in `mapping`
- Settings: `settings` docs instance in `settings`

Create product-specific snapshots with the docs version command. For example:

```bash
npm run docusaurus docs:version 1.0.0
npm run docusaurus docs:version 1.0.0 -- --plugin-id endpoints
npm run docusaurus docs:version 1.0.0 -- --plugin-id mapping
npm run docusaurus docs:version 1.0.0 -- --plugin-id settings
```
