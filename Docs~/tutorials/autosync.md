# Tutorial: AutoSync (GIT menu)

Back up scenes and prefabs to Git a few seconds after every change, and recover
when something breaks them.

## 1. Install Git

Install Git (Git for Windows includes Git LFS). **GIT > Help** has a link.

## 2. Set up the repository

1. Choose **GIT > Configure Sync...**.
2. Pick a **.gitignore** and a **.gitattributes** template. Without your own
   templates, Unity ones are downloaded for you.
3. Optionally enter a **Remote URL (optional)** to push backups to a server. Sign-in
   is handled by Git, not this tool. For a server behind Cloudflare, use an SSH
   address; the window warns about HTTPS.
4. Click **Apply**. The repository is created in the project folder, Git LFS is
   enabled, and the **Git LFS check** reports large files that are not stored with
   LFS. **Track** adds them.

To reuse your own setup in other projects, type a **Template name** and click
**Save .gitignore** or **Save .gitattributes**.

## 3. Let it run

**GIT > AutoSync Scene∕Prefab** is checked once the project is a repository.
Every scene and prefab save is committed to the `AutoSync` branch, and pushed if
there is a remote. Your own branch and staged files are never touched.
**GIT > Sync Now** runs a backup immediately.

**GIT > AutoSync Settings...** changes the **AutoSync branch**, **Base branch**,
**Push** and **Remote**, and shows the **Last run**. **Install Rebase Workflow**
adds a GitHub job that keeps the backup branch rebased on your main branch.

## 4. Restore a file

From a terminal in the project folder:

```text
git checkout AutoSync -- Assets/Scenes/Main.unity Assets/Scenes/Main.unity.meta
```

## 5. Fix broken references

When a scene or prefab opens with missing references, AutoSync offers **Check
for Broken References** (also in the GIT menu). Choose a redirect for each
missing GUID, filled in from the AutoSync history where possible, and click
**Apply**. See the [GUID Tools tutorial](guid-tools.md).

## 6. When a script breaks a scene

If a scene or prefab loads with far fewer objects than its file holds, the
**SCRIPT CAUSING FAILED LOAD** window names the suspected script and blocks
saving, so the good file is not overwritten. Remove or update that script, then
choose **Reload From Disk**. **Open Empty Scene** and **Quit Unity Without
Saving** are the other ways out; **The File Loaded Correctly (Allow Saving)** is
only for a false alarm.

Reference: [design/autosync.md](../design/autosync.md)
