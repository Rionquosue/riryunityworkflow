# Tutorial: Publishing RiryTools as a VPM package

Every push to `main` in this repository asks
[Rionquosue/rirytools_vpm](https://github.com/Rionquosue/rirytools_vpm) to build
the package. That repository zips it, publishes the zip as a release, and rebuilds
its listing, which the VRChat Creator Companion (VCC) reads.

```text
this repository (main)  --dispatch-->  rirytools_vpm
                                        1. Build Package: checks out that commit, zips the package
                                           (no Docs~, TestProject~, dot files), releases v<version>
                                        2. Build Repo Listing: index.json and landing page on GitHub Pages
```

A version is published once. Pushes that do not change `"version"` in
`package.json` are skipped, so bump it (and add a CHANGELOG entry) to release.

## Setup

In `Rionquosue/rirytools_vpm`:

1. **Settings > Pages > Build and deployment > Source:** GitHub Actions.
2. **Settings > Secrets and variables > Actions > Secrets:** `SOURCE_REPO_TOKEN`,
   a fine-grained personal access token with **Contents: Read-only** on this
   repository. It is needed because this repository is private.

In this repository:

3. **Settings > Secrets and variables > Actions > Secrets:** `VPM_DISPATCH_TOKEN`,
   a fine-grained personal access token with **Contents: Read and write** on
   `Rionquosue/rirytools_vpm` (GitHub requires that to start its workflow).

The release zips in `rirytools_vpm` are public and contain the package's source files.

## Publish

Push to `main`, run **Notify VPM repository** from this repository's Actions tab,
or run **Build Package** from `rirytools_vpm`'s Actions tab.

## Add it to the Creator Companion

In the VCC, open **Settings > Packages > Add Repository** and enter
`https://rionquosue.github.io/rirytools_vpm/index.json`.

## Checking a run

- **Notify failed:** `VPM_DISPATCH_TOKEN` is missing or cannot write to `rirytools_vpm`.
- **Checkout failed** (Build Package): `SOURCE_REPO_TOKEN` is missing or cannot read this repository.
- **Already released** (Build Package): bump `"version"`.
- **Listing not updated:** run **Build Repo Listing** in `rirytools_vpm`.
