# Tutorial: AutoSync

Commit every scene and prefab save to Git, and recover when something breaks them.

## 1. Turn on Sync

Choose **Tools > RiryTools > AutoSync > Sync** (or **Git > Sync**). If Git is not
installed, the dialog links to the download; install it, restart Unity and choose
**Sync** again. Without a repository, one is created in the project folder with the
Unity `.gitignore` and `.gitattributes`, and Git LFS is enabled.

## 2. Save as usual

Every time you save a scene or prefab, it is committed to the branch you have
checked out, titled with the time, for example `2026-10-30T14:05:12+01:00`.
Only the saved files and their `.meta` files go into the commit.

## 3. Push to a server (optional)

1. Choose **Tools > RiryTools > AutoSync > Configuration...**.
2. Enter the server address in **Push to remote** and click **Apply**.
3. Every commit is now pushed. When the server asks you to sign in, Git's
   credential manager opens its own window.

For a server behind Cloudflare, use an SSH address; the window warns about HTTPS.

The same window picks other **.gitignore** and **.gitattributes** templates.
To reuse your own setup in other projects, type a **Template name** and click
**Save .gitignore** or **Save .gitattributes**.

## 4. Restore a file

From a terminal in the project folder, find the commit and check the file out:

```text
git log -- Assets/Scenes/Main.unity
git checkout <commit> -- Assets/Scenes/Main.unity Assets/Scenes/Main.unity.meta
```

## 5. When a script breaks a scene

If a scene or prefab loads with far fewer objects than its file holds, the
**SCRIPT CAUSING FAILED LOAD** window names the suspected script and blocks
saving, so the good file is not overwritten. Remove or update that script, then
choose **Reload From Disk**. **Open Empty Scene** and **Quit Unity Without
Saving** are the other ways out; **The File Loaded Correctly (Allow Saving)** is
only for a false alarm.

Reference: [design/autosync.md](../design/autosync.md)
