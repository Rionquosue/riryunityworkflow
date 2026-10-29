# VPM publishing

How RiryTools reaches the VRChat Creator Companion (VCC), and which repository
owns what. For the steps to publish, see the
[tutorial](../tutorials/vpm-publishing.md).

## Repositories

- **`Rionquosue/riryunityworkflow`** (this repository): the package code only.
  It has no `package.json`, no version, no workflows, no tests and no test
  project, and the package is not installed from it by Git URL.
- **`Rionquosue/rirytools_vpm`**: everything else. It holds the `package.json`
  template and every version, builds the zip, publishes the releases, and builds
  and deploys the listing at `https://rionquosue.github.io/rirytools_vpm/index.json`.
- **`Wyrmatron/riryunityworkflow_dev`, `Wyrmatron/rirytools_vpm_dev`**: staging
  forks. Changes are pushed there and reach the Rionquosue repositories through
  pull requests. No workflow runs there (each job requires
  `github.repository == 'Rionquosue/rirytools_vpm'`), and they have no secrets.

No token connects the repositories: `rirytools_vpm` reads this repository's
`main` every hour.

## Versions

The purpose of `rirytools_vpm` is to deploy whatever is on this repository's
`main`, so versions are bumped automatically:

1. Take the highest `vX.Y.Z` release of `rirytools_vpm`.
2. If the `"version"` in its `package.json` template is higher, publish that
   version. This is how a minor or major version is made.
3. Otherwise build `main` with the latest release's version and compare the zip
   with that release. If it differs, publish the next patch version; if not,
   do nothing.

The version exists only in the release: the tag, the zip name and the
`package.json` inside the zip. Nothing in this repository records it.

## Verification

- A release is only reused or compared if it was made by `github-actions[bot]`
  and holds exactly the zip and `package.json`.
- Release notes name the commit of this repository and the commit of
  `rirytools_vpm` (whose `package.json` template was used). Before the listing
  deploys, and every day after, each version is rebuilt from those commits and
  must give the identical zip. Both commits must be on their repository's `main`.
- v0.7.0 was built before the template and cannot be rebuilt the same way. It is
  checked against its deployed listing entry and SHA-256 instead.
- A version already deployed with a different SHA-256 is rejected: a published
  zip never changes.
- The listing deploys only after the owner approves the `github-pages`
  environment.

## Tests

Neither repository holds tests. Checks of the scripts are run locally only and
are never committed.
