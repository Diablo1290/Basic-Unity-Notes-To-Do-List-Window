# Project Planner — Unity Editor Window

A lightweight, single-script editor tool that adds a full-featured project planner to Unity. Organize your work with categories, prioritized to-do lists, notes, and file-based import/export — all without adding anything to your game builds.

---

## Features

- **Categories** — Create, rename, reorder (▲▼ buttons or ≡ drag handle), and delete groups to organize your work.
- **To-Do Items** — Add checkbox items to any category. Mark them complete, edit inline, reorder with ▲▼ arrows, or delete.
- **Priority System** — Each item has a clickable priority box (0–4) with color coding. Items auto-sort by priority within their category.
- **Completed Section** — Checked-off items move to a collapsible "Completed" section with `*COMPLETED*` appended. Unchecking restores them with their original priority.
- **Notes** — Each category has a freeform text area for general notes.
- **Search** — Filter items across all categories from the toolbar.
- **Import (Manual)** — Bulk-import categories, items, and notes from `.txt` or `.json` files via the Import button.
- **Import (Auto)** — Drop `.txt` or `.json` files into the watch folders and the planner auto-imports and deletes them.
- **Auto-Save** — Every change automatically writes `ProjectPlannerSaveText.txt` and `ProjectPlannerSaveJSON.json` alongside the script.
- **Data Storage** — Primary data is stored in `EditorPrefs` (machine-only). Auto-save files provide a shareable backup.

---

## Requirements

- **Unity 6+** (tested on 6000.3.11f1 LTS). Should work on older versions as well — the APIs used are long-standing.

---

## Installation

1. In your Unity project's `Assets` folder, create a folder called `Editor` if one doesn't already exist:

   ```
   Assets/
   └── Editor/
   ```

2. Copy **`NotesEditorWindow.cs`** into that `Editor` folder:

   ```
   Assets/
   └── Editor/
       └── NotesEditorWindow.cs
   ```

   > **Why an `Editor` folder?** Unity treats any folder named `Editor` as editor-only. Scripts inside are compiled into a separate assembly that is stripped from builds automatically. This means the tool will never end up in your game.

3. Wait for Unity to recompile (you'll see the progress bar at the bottom of the editor).

4. Open the window via the menu bar:

   ```
   Window → General → Project Planner
   ```

5. Dock it wherever you like — next to the Inspector, Console, or as a floating window.

---

## File Layout

Once the planner runs for the first time, the following files and folders are created automatically:

```
Assets/Editor/
├── NotesEditorWindow.cs              ← The script
├── ProjectPlannerSaveText.txt        ← Auto-save (human-readable)
├── ProjectPlannerSaveJSON.json       ← Auto-save (JSON format)
└── ProjectPlannerImport/
    ├── txt/                          ← Drop .txt files here to auto-import
    └── json/                         ← Drop .json files here to auto-import
```

- **ProjectPlannerSaveText.txt** — A human-readable snapshot of all your data. Updates every time you make a change. Includes checkbox state `[x]`/`[ ]`, priority tags, and notes.
- **ProjectPlannerSaveJSON.json** — The same data in JSON format, compatible with the import system. Updates on every change.
- **ProjectPlannerImport/txt/** — Drop `.txt` files here. The planner detects them, imports the data, and deletes the file.
- **ProjectPlannerImport/json/** — Drop `.json` files here. Same auto-import behavior.

---

## Usage

### Creating a Category

Type a name in the **"New Category"** field at the bottom of the window and click **Add Category**.

### Adding a To-Do Item

Inside any category, type your task in the text field and click the **+** button (or press **Enter** while the field is focused). New items are added at **priority 0** by default.

### Priority System

Each to-do item has a colored **priority box** to the left of its text:

- **Left-click** the box to increase priority (0→1→2→3→4→0)
- **Right-click** the box to decrease priority (0→4→3→2→1→0)

| Priority | Color        | Meaning     |
|----------|--------------|-------------|
| **0**    | Grey         | Default     |
| **1**    | Light Blue   | Low         |
| **2**    | Light Green  | Medium      |
| **3**    | Yellow       | High        |
| **4**    | Red          | Highest     |

Items automatically sort by priority within their category — priority 4 items appear at the top, priority 0 items at the bottom.

### Reordering Items

Each active item has **▲▼** arrows on the right side. These move the item up or down **within the same priority level**. You cannot move an item past items of a different priority — use the priority box to change its level instead.

### Completing Items

Click the **checkbox** next to an item to mark it as complete. `*COMPLETED*` is appended to the item's text and it moves to the **Completed** section below the active items. The Completed section is collapsible — click the foldout arrow to show/hide it.

### Restoring Completed Items

- **Uncheck a single item** — Click its checkbox in the Completed section. The `*COMPLETED*` tag is removed and the item returns to the active list at its original priority.
- **Clear Completed** — Bulk-unchecks all completed items in that category, restoring them all to the active list with their original priorities.
- **Delete All** (red button) — Permanently deletes all completed items in that category. A confirmation dialog will appear.

### Editing Items

- Click the **text field** of any item to edit it inline.
- Click **✕** to delete a single item.

### Notes

Each category has a **Notes** text area below the Completed section. Use it for freeform text — context, links, reminders, etc.

### Search

The **Search** field in the toolbar filters items across all categories. Only categories and items matching the search text are shown. Clear the field to show everything again.

### Reordering Categories

Use the **▲** and **▼** buttons on each category header to move it one position at a time. Use the **≡** drag handle to click and drag a category to any position in the list.

---

## Toolbar

| Element                            | Description                                              |
|------------------------------------|----------------------------------------------------------|
| **Project Planner**                | Window title.                                            |
| **Total Categories: #**            | Displays the total number of categories.                 |
| **Total Categories Completed: #**  | Number of categories where every item is completed.      |
| **Total Tasks Completed: #**       | Sum of all completed tasks across every category.        |
| **Search**                         | Filter items across all categories by text.              |
| **Import**                         | Open a file picker to manually import a `.txt` or `.json` file. |
| **Collapse All**                   | Folds every category and completed section closed.       |
| **Expand All**                     | Opens every category and completed section.              |

## Category Header

| Element                      | Description                                                        |
|------------------------------|--------------------------------------------------------------------|
| **Foldout ▶**                | Click to expand or collapse the category.                          |
| **Name**                     | Click to edit the category name inline.                            |
| **Tasks Completed: #/#**     | Shows completed vs total items (e.g. `Tasks Completed: 3/7`).     |
| **▲ ▼**                      | Reorder the category up or down one position.                      |
| **≡**                        | Drag handle — click and drag to move the category to any position. |
| **✕**                        | Delete the category and all its items (with confirmation dialog).  |

## Item Row

| Element          | Description                                                              |
|------------------|--------------------------------------------------------------------------|
| **Checkbox**     | Toggle to mark the item as completed or restore it.                      |
| **Priority Box** | Left-click to increase, right-click to decrease priority (0–4).          |
| **Text Field**   | The item's text. Click to edit inline.                                   |
| **✕**            | Delete the item.                                                         |
| **▲ ▼**          | Move the item up or down within the same priority level.                 |

---

## Importing Data

There are two ways to import data into the Project Planner.

### Manual Import (Import Button)

Click the **Import** button in the toolbar. A file picker opens — select a `.txt` or `.json` file. The data is imported and merged with your existing categories.

### Auto-Import (Watch Folders)

Drop files directly into the import folders inside your project:

- `.txt` files → `Assets/Editor/ProjectPlannerImport/txt/`
- `.json` files → `Assets/Editor/ProjectPlannerImport/json/`

The planner detects new files automatically when Unity reimports assets. On detection:

1. The file is validated for correct format.
2. If valid, the data is merged into the planner and the file is deleted.
3. If invalid, an error dialog appears listing which files failed and why. The files are left in place so you can fix them.

### Merge Behavior

- If an imported category has the **same name** (case-insensitive) as an existing category, the items are **appended** to the existing category and notes are concatenated.
- If the category name is **different**, a new category is created.
- Duplicate item text is **not** filtered — if the file contains the same item twice, both will be added.
- All imported items are set to **priority 0** by default.

### TXT Format

Text files must follow this structure:

```
Category:
Jump Starting Vehicle:

- Grab jumper cables
- Pop the hood
- Connect red cable to positive terminal
* Connect black cable to ground
1. Start the working vehicle
2. Start the dead vehicle

Notes:
Wait 5 minutes before attempting to start.
Make sure both vehicles are in park.

Category:
Starting a Campfire:

- Gather dry wood
* Clear the fire pit area
1. Arrange kindling in a teepee shape

Notes:
Never leave the fire unattended.
```

**Rules:**

- `Category:` must be on its **own line**. The **next non-empty line** after it is the category name (trailing `:` is stripped automatically).
- To-do items are lines starting with any of these prefixes:
  - Dashes: `- ` or `\- `
  - Asterisks: `* ` or `\* `
  - Bullets: `• `
  - Numbers: `1.` `2.` `3)` `12.` etc.
  - The prefix is stripped — only the item text is kept.
- `Notes:` must be on its **own line**. All lines after it are treated as notes until the next `Category:` line.
- Empty lines are ignored.
- Lines before the first `Category:` are ignored.

### JSON Format

JSON files must follow this structure:

```json
{
  "categories": [
    {
      "name": "Jump Starting Vehicle",
      "items": [
        "Grab jumper cables",
        "Pop the hood",
        "Connect red cable to positive terminal"
      ],
      "notes": "Wait 5 minutes before attempting to start."
    },
    {
      "name": "Starting a Campfire",
      "items": [
        "Gather dry wood",
        "Clear the fire pit area",
        "Arrange kindling in a teepee shape"
      ],
      "notes": "Never leave the fire unattended."
    }
  ]
}
```

**Rules:**

- `categories` is an array of objects.
- Each object must have a `name` (string).
- `items` is an array of strings — each string becomes a to-do checkbox item.
- `notes` is a string — becomes the category's notes area content.

---

## Auto-Save

Every change you make in the planner automatically writes two files alongside the script:

- **`ProjectPlannerSaveText.txt`** — Human-readable format. Each item shows `[x]` or `[ ]` for completion status, a priority tag like `(P3)` if priority is above 0, and the item text.
- **`ProjectPlannerSaveJSON.json`** — JSON format matching the import structure. Can be used as a backup or shared with others who can import it.

These files update on every action (adding, editing, completing, deleting, reordering). They are written to the same folder as the script.

---

## Data Storage

Primary data is stored in **`EditorPrefs`** under the key `EditorTools_NotesData`.

**What this means:**

- Data persists between Unity sessions on the **same machine**.
- Data is **not** shared via version control or across machines (use the auto-save files or export for that).
- Data is **not** included in builds.
- Uninstalling the script does not automatically delete saved data from EditorPrefs. To clear it manually, use `EditorPrefs.DeleteKey("EditorTools_NotesData")` in a console or script.

---

## Uninstalling

1. Delete the following from your project:
   - `Assets/Editor/NotesEditorWindow.cs`
   - `Assets/Editor/ProjectPlannerSaveText.txt`
   - `Assets/Editor/ProjectPlannerSaveJSON.json`
   - `Assets/Editor/ProjectPlannerImport/` (entire folder)

2. *(Optional)* Clear stored data by running this in a Unity editor script or the Immediate window:
   ```csharp
   EditorPrefs.DeleteKey("EditorTools_NotesData");
   ```

---

## License

This script is provided freely with no restrictions. Use it, modify it, distribute it however you like.
