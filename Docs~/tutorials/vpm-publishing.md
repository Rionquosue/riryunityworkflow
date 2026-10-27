# Tutorial: Publishing RiryTools as a VPM package

Every hour, [Rionquosue/rirytools_vpm](https://github.com/Rionquosue/rirytools_vpm)
checks this repository's `main`. When its `"version"` has no release yet, it
builds the package, publishes the release, and prepares a new listing for the
VRChat Creator Companion (VCC). The listing goes live only after the owner
approves the deployment in GitHub.

```text
this repository (main)  <--hourly read--  rirytools_vpm
                                           1. Build Package: zips main (no Docs~, TestProject~, dot files),
                                              releases v<version>, refuses releases it did not make
                                           2. Build Repo Listing: builds index.json, verifies every zip,
                                              waits for the owner's approval, then deploys to GitHub Pages
```

No token connects the two repositories, and only commits on `main` are built.
A version is published once, so bump `"version"` in `package.json` (and add a
CHANGELOG entry) to release.

## Setup

All settings are in `Rionquosue/rirytools_vpm`; see its README. This repository
needs no secrets for publishing. Delete any `VPM_DISPATCH_TOKEN` secret here and
revoke that token.

## Publish

Merge to `main` with a new `"version"`. Within the hour, open `rirytools_vpm`'s
Actions tab, read the summary on the **Build Repo Listing** run, and approve its
deployment. To skip the wait, run **Build Package** there by hand.

## Add it to the Creator Companion

In the VCC, open **Settings > Packages > Add Repository** and enter
`https://rionquosue.github.io/rirytools_vpm/index.json`.

## Checking a run

- **Build Package failed on an existing release:** a release for that version exists that the build did not make, or its zip differs. Inspect it before deleting it.
- **Not on main** (Build Package): only merged code is released.
- **Verify the listing failed:** a release or zip in `rirytools_vpm` was not made by the build. Do not approve; inspect the releases.
- **Already released:** bump `"version"`.
