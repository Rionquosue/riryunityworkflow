# Tutorial: Clean Animation Keys

Remove the blank shapekey and static bone keys that some exporters write for
every bone and shapekey.

## 1. Open the window with your animations

Select the `.anim` files in the Project window, right-click, and choose
**RiryTools > Clean Animation Keys**. They appear under **Animations**. You can
also open **Tools > RiryTools > Clean Animation Keys** and drag clips into the
empty row, or click **Clean Keys** in the Animation window.

## 2. Protect what must stay

- Type a shapekey or bone name under **Whitelist** and pick a suggestion, or
  click **Add**.
- Or click **Whitelist** beside an animation, tick the keys to keep (only keys
  that would be removed are listed), and click **APPLY**.

## 3. Choose the keep rules

Leave the four **Keep** checkboxes on to keep anything that moves. Turn one off
to remove those curves too.

## 4. Clean

Click **Clean**. The summary shows how many curves were removed. **Edit > Undo**
restores every animation at once.

Reference: [design/clean-animation-keys.md](../design/clean-animation-keys.md)
