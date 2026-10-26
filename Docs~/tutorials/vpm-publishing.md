# Tutorial: Publishing RiryTools as a VPM package

Every push to `main` in this repository asks a second repository (the VPM
repository) to build the package. The VPM repository zips it, publishes the zip
as a release, and adds it to `index.json`, the listing the VRChat Creator
Companion (VCC) reads.

```text
this repository (main)  --dispatch-->  VPM repository
                                        1. checks out that commit
                                        2. zips the package (no Docs~, TestProject~, .github)
                                        3. GitHub release v<version> with the zip and package.json
                                        4. adds the version to index.json (served by GitHub Pages)
```

A version is published once. Pushes that do not change `"version"` in
`package.json` are skipped, so bump it (and add a CHANGELOG entry) to release.

## 1. Create the VPM repository

1. Create a new GitHub repository, for example `rirytools-vpm`. It must be
   **public** on a free plan, so GitHub Pages can serve the listing and the VCC
   can download the zips without signing in. Its releases contain the package's
   source files.
2. Copy everything inside this repository's `VpmRepository~` folder into the root
   of the new repository, including `.github/` and `.nojekyll`.
3. Edit `listing.json`: `name` is what the VCC shows, `id` must be unique (for
   example `com.riry.vpm`), and `url` can stay empty to use
   `https://<owner>.github.io/<repository>/index.json`.
4. Commit and push.

## 2. Configure the VPM repository

In the VPM repository, open **Settings**:

1. **Secrets and variables > Actions > Variables**: add `SOURCE_REPOSITORY` with
   this repository's `owner/name` (for example `Rionquosue/riryunityworkflow`).
2. **Secrets and variables > Actions > Secrets**: add `SOURCE_REPO_TOKEN`, a
   fine-grained personal access token with **Contents: Read-only** on this
   repository. It is needed because this repository is private.
3. **Pages**: Source **Deploy from a branch**, branch `main`, folder `/ (root)`.

## 3. Configure this repository

In this repository, open **Settings > Secrets and variables > Actions**:

1. Variable `VPM_REPOSITORY`: the VPM repository's `owner/name`. While it is
   unset, the notify workflow does nothing.
2. Secret `VPM_DISPATCH_TOKEN`: a fine-grained personal access token with
   **Contents: Read and write** on the VPM repository (GitHub requires that to
   start its workflow).

## 4. Publish

Push to `main` (or run **Notify VPM repository** from this repository's Actions
tab, or **Build VPM package** from the VPM repository's). When the run
finishes, the VPM repository has a release `v<version>` and an updated
`index.json`.

## 5. Add it to the Creator Companion

In the VCC, open **Settings > Packages > Add Repository** and enter the listing
URL, for example `https://rionquosue.github.io/rirytools-vpm/index.json`.
RiryTools then appears in each project's package list.

## Checking a run

- **Skipped:** the version is already released and listed; bump `"version"`.
- **Checkout failed:** check `SOURCE_REPOSITORY` and that `SOURCE_REPO_TOKEN` can
  read this repository.
- **Dispatch failed** (in this repository): check `VPM_REPOSITORY` and that
  `VPM_DISPATCH_TOKEN` has write access to the VPM repository.

A run that failed partway is repeated in full on the next push or manual run;
the same source always produces the same zip.
