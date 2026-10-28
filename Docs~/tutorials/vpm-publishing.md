# Tutorial: Publishing RiryTools as a VPM package

Every push to `main` starts **Build Package** in
[Rionquosue/rirytools_vpm](https://github.com/Rionquosue/rirytools_vpm), which
reads this repository's `main`. When its `"version"` has no release yet, it
builds the package, publishes the release, and prepares a new listing for the
VRChat Creator Companion (VCC). The listing goes live only after the owner
approves the deployment in GitHub.

```text
this repository (main)  --notify-->  rirytools_vpm
                                           1. Build Package: zips main (no Docs~, TestProject~, dot files),
                                              releases v<version>, refuses releases it did not make
                                           2. Build Repo Listing: builds index.json, verifies every zip,
                                              waits for the owner's approval, then deploys to GitHub Pages
```

The notification carries no input, and only the current commit of `main` is built.
A version is published once, so bump `"version"` in `package.json` (and add a
CHANGELOG entry) to release.

## Setup

Most settings are in `Rionquosue/rirytools_vpm`; see its README. In this
repository, **Settings > Secrets and variables > Actions > Secrets:**
`VPM_DISPATCH_TOKEN`, a fine-grained personal access token for
`Rionquosue/rirytools_vpm` only, with **Actions: Read and write** and nothing else.

## Publish

Merge to `main` with a new `"version"`. Open `rirytools_vpm`'s
Actions tab, read the summary on the **Build Repo Listing** run, and approve its
deployment. To retry, run **Notify VPM repository** here or **Build Package** there by hand.

## Add it to the Creator Companion

In the VCC, open **Settings > Packages > Add Repository** and enter
`https://rionquosue.github.io/rirytools_vpm/index.json`.

## Checking a run

- **Notify failed:** `VPM_DISPATCH_TOKEN` is missing, expired, or lacks Actions: Read and write on `rirytools_vpm`.
- **Build Package failed on an existing release:** a release for that version exists that the build did not make, or its zip differs. Inspect it before deleting it.
- **Not on main** (Build Package): only merged code is released.
- **Verify the listing failed:** a release or zip in `rirytools_vpm` was not made by the build. Do not approve; inspect the releases.
- **Already released:** bump `"version"`.
