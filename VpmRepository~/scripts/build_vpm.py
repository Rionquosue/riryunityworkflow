#!/usr/bin/env python3
"""Builds a VPM package from a checkout of the RiryTools source repository.

Steps:
  version   Print the source package's version (to check for an existing release).
  listed    Exit 0 when that version is already in the listing (index.json).
  build     Zip the package, write the release package.json, and add the version
            to the VPM listing (index.json).

The zip holds the package folder's contents at its root, as the VRChat Creator
Companion expects. Entries are written in a fixed order with fixed timestamps,
so the same source always gives the same zip and SHA-256.
"""
import argparse
import hashlib
import json
import os
import sys
import zipfile

# Top-level entries of the source repository that are not part of the package.
EXCLUDED_TOP_LEVEL = {"Docs~", "TestProject~", "VpmRepository~"}
FIXED_TIME = (2020, 1, 1, 0, 0, 0)


def read_manifest(source):
    with open(os.path.join(source, "package.json"), encoding="utf-8") as handle:
        return json.load(handle)


def package_files(source):
    """Relative paths of every file that belongs in the package, sorted."""
    files = []
    for root, directories, names in os.walk(source):
        relative_root = os.path.relpath(root, source)
        if relative_root == ".":
            # Dot entries (.git, .github, .gitignore) and the repository-only folders.
            directories[:] = [d for d in directories if not d.startswith(".") and d not in EXCLUDED_TOP_LEVEL]
            names = [n for n in names if not n.startswith(".")]
        else:
            directories[:] = [d for d in directories if d != ".git"]
        for name in names:
            files.append(os.path.normpath(os.path.join(relative_root, name)).replace(os.sep, "/"))
    return sorted(files)


def write_zip(source, files, manifest_bytes, path):
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as archive:
        for relative in files:
            info = zipfile.ZipInfo(relative, FIXED_TIME)
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o644 << 16
            if relative == "package.json":
                data = manifest_bytes
            else:
                with open(os.path.join(source, relative), "rb") as handle:
                    data = handle.read()
            archive.writestr(info, data)


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for block in iter(lambda: handle.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def build(args):
    manifest = read_manifest(args.source)
    name, version = manifest["name"], manifest["version"]
    tag = "v" + version
    zip_name = "%s-%s.zip" % (name, version)
    manifest["url"] = "https://github.com/%s/releases/download/%s/%s" % (args.repository, tag, zip_name)
    manifest_bytes = (json.dumps(manifest, indent=2, ensure_ascii=False) + "\n").encode("utf-8")

    os.makedirs(args.dist, exist_ok=True)
    zip_path = os.path.join(args.dist, zip_name)
    files = package_files(args.source)
    if "package.json" not in files:
        sys.exit("package.json is missing from the package.")
    write_zip(args.source, files, manifest_bytes, zip_path)
    with open(os.path.join(args.dist, "package.json"), "wb") as handle:
        handle.write(manifest_bytes)

    with open(args.listing, encoding="utf-8") as handle:
        listing = json.load(handle)
    if os.path.exists(args.index):
        with open(args.index, encoding="utf-8") as handle:
            index = json.load(handle)
    else:
        index = {}
    owner, repository = args.repository.split("/", 1)
    index["name"] = listing["name"]
    index["id"] = listing["id"]
    index["author"] = listing["author"]
    index["url"] = listing.get("url") or "https://%s.github.io/%s/index.json" % (owner.lower(), repository)
    entry = dict(manifest)
    entry["zipSHA256"] = sha256(zip_path)
    index.setdefault("packages", {}).setdefault(name, {}).setdefault("versions", {})[version] = entry
    with open(args.index, "w", encoding="utf-8") as handle:
        json.dump(index, handle, indent=2, ensure_ascii=False)
        handle.write("\n")

    print("Built %s with %d files (SHA-256 %s)." % (zip_name, len(files), entry["zipSHA256"]))
    outputs = os.environ.get("GITHUB_OUTPUT")
    if outputs:
        with open(outputs, "a", encoding="utf-8") as handle:
            handle.write("tag=%s\nzip=%s\nname=%s\nversion=%s\n" % (tag, zip_path, name, version))


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("step", choices=["version", "listed", "build"])
    parser.add_argument("--source", required=True, help="Checkout of the source repository.")
    parser.add_argument("--repository", help="owner/name of the VPM repository (build).")
    parser.add_argument("--dist", default="dist", help="Folder for the zip and package.json (build).")
    parser.add_argument("--listing", default="listing.json", help="Listing name, id and author (build).")
    parser.add_argument("--index", default="index.json", help="The VPM listing (listed, build).")
    args = parser.parse_args()
    if args.step == "version":
        print(read_manifest(args.source)["version"])
        return
    if args.step == "listed":
        manifest = read_manifest(args.source)
        index = {}
        if os.path.exists(args.index):
            with open(args.index, encoding="utf-8") as handle:
                index = json.load(handle)
        versions = index.get("packages", {}).get(manifest["name"], {}).get("versions", {})
        sys.exit(0 if manifest["version"] in versions else 1)
    if not args.repository:
        parser.error("--repository is required for build")
    build(args)


if __name__ == "__main__":
    main()
