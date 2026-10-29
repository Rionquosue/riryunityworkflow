# VPM Publishing

How RiryTools reaches the VRChat Creator Companion (VCC), and which repository
owns what. For the steps, see the [tutorial](../tutorials/vpm-publishing.md).

## Terms

- **Source:** `Rionquosue/riryunityworkflow`, this repository. The package code only.
- **Destination:** `Rionquosue/rirytools_vpm`. Packages the source, versions and
  releases it, and deploys the listing at
  `https://rionquosue.github.io/rirytools_vpm/index.json`.
- **Package folder:** `com.riry.rirytools/` in the source.
- **Channel:** `stable` (from `main`) or `beta` (from `beta`).
- **Staging forks:** `Wyrmatron/riryunityworkflow_dev` and `Wyrmatron/rirytools_vpm_dev`.
  Changes reach the Rionquosue repositories through pull requests. No workflow
  runs there: every job requires the Rionquosue repository name.

## Branches

- **Source `main`:** stable code. A push publishes a stable version.
- **Source `beta`:** beta code. A push publishes a beta version.
- **Destination `main`:** runs every build, for both channels.
- **Destination `beta`:** for trying out changes to the destination's scripts
  before merging. Runs nothing.

## Source layout

```text
com.riry.rirytools/        the package; everything in it is zipped
  Editor/, Editor.meta
  ProjectFolder~/
  README.md, README.md.meta
  CHANGELOG.md, CHANGELOG.md.meta
  package.json.meta        for the package.json the destination adds
docs/                      not zipped
README.md
.github/workflows/notify-vpm.yml
.gitignore
```

The source holds no `package.json`, no version, no tests and no test project.
`.meta` files exist only inside `com.riry.rirytools/`: every file and folder
Unity imports needs one, and its GUID must never change (see Package).

## Trigger: `notify-vpm.yml` (source)

- **On:** `push` to `main` or `beta`. Nothing else triggers it.
- **Guard:** `if: github.repository == 'Rionquosue/riryunityworkflow'`.
- **Permissions:** `contents: read`.
- **No checkout:** no code from the repository is run.
- **Step:** `gh workflow run build-package.yml --repo Rionquosue/rirytools_vpm --ref main -f channel=<stable|beta> -f sha=<pushed commit>`.
- **Token:** secret `VPM_DISPATCH_TOKEN`.
  - A fine-grained token for `Rionquosue/rirytools_vpm` only, with only **Actions: Read and write**.
  - It can start workflows. It cannot change code, releases or settings.
  - It is passed only to that one step.

## Build: `build-package.yml` (destination)

**On:** `workflow_dispatch` only, with inputs:

| Input | Required | Allowed |
|---|---|---|
| `channel` | yes | `stable` or `beta` |
| `sha` | no | 40 lowercase hex; empty means the head of the channel's branch |
| `version` | no | stable `X.Y.Z`, beta `X.Y.Z-beta.N`; empty means automatic |

An input that doesn't match its pattern fails the run before anything is checked
out. Inputs reach scripts only through environment variables, never pasted into
shell text.

- **Concurrency:** group `build-package`, never cancelled.
- **Permissions:** `contents: write`, `actions: write`.

Steps:

1. Check out the destination, and the source at `sha` or at the head of the
   channel's branch. Both use `persist-credentials: false`; the source also
   checks out LFS files.
2. Find the previous release on the channel:
   - stable: the highest `vX.Y.Z`
   - beta: the highest `vX.Y.Z-beta.N`
3. Work out the version (see Versions).
4. If there is a previous release, build the package with the previous
   release's version. If that zip's SHA-256 equals the previous release's zip,
   **abort**: `No change in com.riry.rirytools since v<previous>; nothing published.`
5. Build the package with the new version.
6. Create release `v<version>`:
   - target: destination `main`
   - title: `com.riry.rirytools <version>`
   - assets: the zip and `package.json`
   - notes: `Built from Rionquosue/riryunityworkflow@<full sha>.`
   - betas are marked prerelease
7. Start `build-listing.yml`.

The run fails only at step 4, or when a step can't complete (checkout, network,
invalid input, an existing tag).

## Versions

Only the destination's releases and the `version` input decide versions.
Nothing is read from the source.

- **Stable:** the `version` input; otherwise the previous stable release plus
  one patch (`0.7.2` to `0.7.3`); with no stable release, `0.0.1`.
- **Beta:** the `version` input; otherwise `<next stable>-beta.<n>`.
  - Next stable is the highest stable release plus one patch, or `0.0.1`.
  - `n` is one more than the highest existing `-beta.N` for that next stable, starting at `1`.
  - With stable `0.7.2`: `0.7.3-beta.1`, then `0.7.3-beta.2`. After `0.7.3`: `0.7.4-beta.1`.
- **Minor and major:** a manual run with the `version` input, for example
  `0.8.0`. Automatic versions continue from it.

## Package

- **Zip contents:** the contents of `com.riry.rirytools/` at the zip root.
  - Dot files and dot folders are left out.
  - A `package.json` in the folder is ignored.
- **`package.json`:** the destination's `package.json` fields, with `version`
  after `name` and `url` at the end
  (`https://github.com/Rionquosue/rirytools_vpm/releases/download/v<version>/com.riry.rirytools-<version>.zip`).
- **`.meta` files:** zipped from the source as they are.
  - The VCC installs into the project's `Packages/` folder, where Unity would
    create a missing `.meta` with a random GUID on every install or update.
  - A `.meta` holds the asset's GUID, which every reference to the asset uses.
    Keeping the same GUID across versions keeps those references working
    after an update; `AutoSync.cs` finds its workflow template by GUID.
- **Deterministic:** fixed file order, timestamps and mode, so the same input
  gives the same SHA-256.
- **Refused:** symbolic links and Git LFS pointers fail the build.

## Listing: `build-listing.yml` (destination)

1. **Build:** package-list-action, pinned to a commit, builds `index.json` and
   the landing page from all releases, betas included.
2. **Verify:** `verify_listing.py --artifact` checks:
   - the artifact holds only `Website/` plus `index.json`
   - static files are byte-identical to `Website/`
   - pages load only the template's scripts and link only to known addresses
   - the listing's `name`, `id`, `url` and `author` match `source.json`
   - the only package is `com.riry.rirytools`
   - each version's `url` points at its release
3. **Deploy:** waits in the `github-pages` environment for the owner's approval.

Betas show in the VCC only for users who turn on pre-release packages.

## Not checked

The destination is the builder, so it does not re-check its own output:

- who made a release
- release assets
- rebuilding releases from their notes
- whether the source commit is still on `main`
- whether a deployed SHA-256 changed

There is no daily audit.

## Tests

Neither repository holds tests. Checks are run locally only and are never
committed.
