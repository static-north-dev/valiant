# Endpoint samples

The runnable host contains separate Basic, ConditionalRegistration, Configuration, and
QueryBinding examples:

```bash
dotnet run --project endpoints/samples/StaticNorth.Valiant.Endpoints.AspNetCore.Samples
```

Select the startup example near the top of
`StaticNorth.Valiant.Endpoints.AspNetCore.Samples/Program.cs`. Endpoint discovery is assembly-wide,
so every attributed sample route is generated for the host; the selected startup controls
which global options and conventions are applied.

## Conditional registration

Select `Samples.ConditionalRegistration.Startup` to demonstrate:

- `MapEndpointGroupsWhen`, which rejects `BlockedEndpointGroup`;
- `MapEndpointWhen`, which rejects `BlockedStandaloneEndpoint`;
- independent positive and negative cases for group and endpoint policies.

With the HTTP launch profile running on port `5193`:

```bash
curl -i http://localhost:5193/conditional/groups/allowed/hello
curl -i http://localhost:5193/conditional/groups/blocked/hello
curl -i http://localhost:5193/conditional/endpoints/allowed
curl -i http://localhost:5193/conditional/endpoints/blocked
```

Expected statuses:

| Route | Status | Policy demonstrated |
| --- | --- | --- |
| `/conditional/groups/allowed/hello` | `200` | group allowed |
| `/conditional/groups/blocked/hello` | `404` | group rejected |
| `/conditional/endpoints/allowed` | `200` | endpoint allowed |
| `/conditional/endpoints/blocked` | `404` | endpoint rejected |

When a group is rejected, its generated child endpoint registrations are not processed,
so `MapEndpointWhen` is not evaluated for those children. These are startup-time
registration decisions, not per-request feature checks.

The four routes above are the relevant conditional-registration matrix, not the complete
route table, because endpoint discovery includes every attributed type in the sample
assembly.

The implementation is under
`StaticNorth.Valiant.Endpoints.AspNetCore.Samples/ConditionalRegistration`.

## Configuration hooks

Select `Samples.Configuration.Startup` to demonstrate:

- `ConfigureEndpointGroup`, which adds `X-Valiant-Group` to both grouped routes;
- `ConfigureEndpoint`, which adds `X-Valiant-Endpoint` only to `SelectedEndpoint`;
- descriptor-based guards using `GroupType`, `EndpointType`, and endpoint `Group`.

With the HTTP launch profile running on port `5193`:

```bash
curl -i http://localhost:5193/configuration/selected
curl -i http://localhost:5193/configuration/group-only
```

Expected headers:

| Route | `X-Valiant-Group` | `X-Valiant-Endpoint` |
| --- | --- | --- |
| `/configuration/selected` | `ConfigurationEndpointGroup` | `SelectedEndpoint` |
| `/configuration/group-only` | `ConfigurationEndpointGroup` | not present |

The implementation is under
`StaticNorth.Valiant.Endpoints.AspNetCore.Samples/Configuration`.

## Other examples

- Basic: `/hello` and `/private/hello` demonstrate standalone and grouped endpoints.
- QueryBinding: `/query/*` routes demonstrate generated query binding and parsing formats.

Every startup uses the generated registration path through `AddValiantEndpoints()` and
`MapValiantEndpoints()`.
