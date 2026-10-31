# Tutorial: VPM Publishing

See the [design notes](../design/vpm-publishing.md) for how it works.

## Setup

In `Rionquosue/riryunityworkflow`, add the secret `VPM_DISPATCH_TOKEN`: a
fine-grained personal access token with access to `Rionquosue/rirytools_vpm` only
and the single permission **Actions: Read and write**. The rest is in
`rirytools_vpm`'s README.

## Publish

- **Stable:** push to `main`. The next patch version is released.
- **Beta:** push to `beta`. The next beta (`<next patch>-beta.N`) is released as a prerelease.
- **Minor or major:** run **Build Package** in `rirytools_vpm` by hand with the `version` input.

The listing is then rebuilt and deployed by itself; the VCC sees the new version
once **Build Repo Listing** finishes.

## Add it to the Creator Companion

In the VCC, open **Settings > Packages > Add Repository** and enter
`https://rionquosue.github.io/rirytools_vpm/index.json`. Betas show only with
pre-release packages turned on.

## Checking a run

- **No change** (Build Package): the package is identical to the previous release on that channel; nothing was published.
- **Verify the listing failed:** the listing or landing page is not what the template and releases give, so nothing was deployed; read the errors.
