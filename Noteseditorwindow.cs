#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    public class NotesEditorWindow : EditorWindow
    {
        // ── Data Models ──────────────────────────────────────────────
        [Serializable]
        private class TodoItem
        {
            public string text = "";
            public bool completed;
            public int priority;   // 0=default, 1=light blue, 2=light green, 3=yellow, 4=red
            public int sortOrder;  // sub-order within same priority
        }

        [Serializable]
        private class Category
        {
            public string name = "New Category";
            public bool foldout = true;
            public bool completedFoldout = true;
            public List<TodoItem> items = new List<TodoItem>();
            public string notes = "";
        }

        [Serializable]
        private class NotesData
        {
            public List<Category> categories = new List<Category>();
        }

        // ── Constants ────────────────────────────────────────────────
        private const string EditorPrefsKey = "EditorTools_NotesData";
        private const float PriorityBoxWidth = 26f;
        private const float CheckboxWidth = 28f;
        private const float DeleteBtnWidth = 20f;

        private static readonly Color[] PriorityColors = new Color[]
        {
            new Color(0.35f, 0.35f, 0.35f, 1f), // 0: default grey
            new Color(0.4f, 0.7f, 0.9f, 1f),    // 1: light blue
            new Color(0.4f, 0.8f, 0.4f, 1f),    // 2: light green
            new Color(0.9f, 0.85f, 0.3f, 1f),   // 3: yellow
            new Color(0.9f, 0.3f, 0.3f, 1f),    // 4: red
        };

        // ── File Paths ───────────────────────────────────────────────
        private static string ScriptFolder
        {
            get
            {
                // Find where this script lives
                string[] guids = AssetDatabase.FindAssets("t:MonoScript NotesEditorWindow");
                if (guids.Length > 0)
                {
                    string scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    return Path.GetDirectoryName(scriptPath);
                }
                return "Assets/Editor";
            }
        }

        private static string SaveTxtPath => Path.Combine(ScriptFolder, "ProjectPlannerSaveText.txt");
        private static string SaveJsonPath => Path.Combine(ScriptFolder, "ProjectPlannerSaveJSON.json");
        private static string ImportTxtFolder => Path.Combine(ScriptFolder, "ProjectPlannerImport", "txt");
        private static string ImportJsonFolder => Path.Combine(ScriptFolder, "ProjectPlannerImport", "json");

        // ── State ────────────────────────────────────────────────────
        private NotesData _data;
        private Vector2 _scrollPos;
        private string _newCategoryName = "";
        private string _searchFilter = "";
        private readonly Dictionary<string, string> _newItemText = new Dictionary<string, string>();

        // Category drag state
        private int _dragCatIndex = -1;

        // Styles
        private GUIStyle _completedFieldStyle;
        private GUIStyle _notesStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _sectionLabelStyle;
        private bool _stylesInitialized;

        // ── Menu Entry ───────────────────────────────────────────────
        [MenuItem("Window/General/Project Planner")]
        public static void ShowWindow()
        {
            var window = GetWindow<NotesEditorWindow>("Project Planner");
            window.minSize = new Vector2(320, 250);
        }

        // ── Lifecycle ────────────────────────────────────────────────
        private void OnEnable()
        {
            Load();
            EnsureImportFolders();
            ProcessImportFolders();
        }

        private void OnDisable() => Save();

        // ── Style Init ───────────────────────────────────────────────
        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _completedFieldStyle = new GUIStyle(EditorStyles.textField)
            {
                fontStyle = FontStyle.Italic,
                normal = { textColor = new Color(0.5f, 0.5f, 0.5f) },
                focused = { textColor = new Color(0.55f, 0.55f, 0.55f) }
            };

            _notesStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                padding = new RectOffset(4, 0, 0, 0)
            };

            _sectionLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) },
                padding = new RectOffset(4, 0, 4, 2)
            };

            _stylesInitialized = true;
        }

        // ── Main GUI ─────────────────────────────────────────────────
        private void OnGUI()
        {
            InitStyles();
            DrawToolbar();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            for (int i = 0; i < _data.categories.Count; i++)
            {
                DrawCategory(i);
                if (i < _data.categories.Count - 1)
                    EditorGUILayout.Space(4);
            }

            EditorGUILayout.Space(8);
            DrawAddCategory();

            EditorGUILayout.EndScrollView();

            if (Event.current.type == EventType.MouseUp && _dragCatIndex >= 0)
                _dragCatIndex = -1;
        }

        // ── Toolbar ──────────────────────────────────────────────────
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("Project Planner", _headerStyle);
            GUILayout.FlexibleSpace();

            int totalCategories = _data.categories.Count;
            int totalCompleted = _data.categories.Sum(c => c.items.Count(i => i.completed));
            int totalCategoriesCompleted = _data.categories.Count(c => c.items.Count > 0 && c.items.All(i => i.completed));

            GUILayout.Label($"Total Categories: {totalCategories}", _headerStyle);
            GUILayout.Label($"Total Categories Completed: {totalCategoriesCompleted}", _headerStyle);
            GUILayout.Label($"Total Tasks Completed: {totalCompleted}", _headerStyle);
            GUILayout.Label("Search:", EditorStyles.miniLabel, GUILayout.Width(42));
            _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField,
                GUILayout.Width(140));

            if (GUILayout.Button("Import", EditorStyles.toolbarButton))
                ImportFile();

            if (GUILayout.Button("Collapse All", EditorStyles.toolbarButton))
                foreach (var c in _data.categories) { c.foldout = false; c.completedFoldout = false; }

            if (GUILayout.Button("Expand All", EditorStyles.toolbarButton))
                foreach (var c in _data.categories) { c.foldout = true; c.completedFoldout = true; }

            EditorGUILayout.EndHorizontal();
        }

        // ── Category ─────────────────────────────────────────────────
        private void DrawCategory(int index)
        {
            var cat = _data.categories[index];
            string catId = index.ToString();

            var activeItems = cat.items.Where(i => !i.completed)
                .OrderByDescending(i => i.priority).ThenBy(i => i.sortOrder).ToList();
            var completedItems = cat.items.Where(i => i.completed).ToList();

            bool hasSearch = !string.IsNullOrWhiteSpace(_searchFilter);
            var filteredActive = hasSearch
                ? activeItems.Where(i => MatchesSearch(i.text)).ToList() : activeItems;
            var filteredCompleted = hasSearch
                ? completedItems.Where(i => MatchesSearch(i.text)).ToList() : completedItems;

            if (hasSearch && filteredActive.Count == 0 && filteredCompleted.Count == 0
                && !MatchesSearch(cat.notes ?? "") && !MatchesSearch(cat.name))
                return;

            EditorGUILayout.BeginVertical("box");

            // ── Header ───────────────────────────────────────────────
            EditorGUILayout.BeginHorizontal();

            cat.foldout = EditorGUILayout.Foldout(cat.foldout, "", true);

            string newName = EditorGUILayout.TextField(cat.name, _headerStyle);
            if (newName != cat.name) { cat.name = newName; Save(); }

            GUILayout.FlexibleSpace();

            GUILayout.Label($"Tasks Completed: {completedItems.Count}/{cat.items.Count}", _headerStyle);

            GUI.enabled = index > 0;
            if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(22)))
            {
                SwapCategories(index, index - 1);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            GUI.enabled = index < _data.categories.Count - 1;
            if (GUILayout.Button("▼", EditorStyles.miniButtonRight, GUILayout.Width(22)))
            {
                SwapCategories(index, index + 1);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            GUI.enabled = true;

            // ── Drag handle for category ─────────────────────────────
            if (GUILayout.Button("≡", EditorStyles.miniButtonLeft, GUILayout.Width(22)))
            {
                // Visual only — drag handled below
            }
            Rect catHandleRect = GUILayoutUtility.GetLastRect();
            EditorGUIUtility.AddCursorRect(catHandleRect, MouseCursor.Pan);

            if (Event.current.type == EventType.MouseDown
                && catHandleRect.Contains(Event.current.mousePosition))
            {
                _dragCatIndex = index;
                Event.current.Use();
            }

            if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22)))
            {
                if (EditorUtility.DisplayDialog("Delete Category",
                        $"Delete \"{cat.name}\" and all its items?", "Delete", "Cancel"))
                {
                    _data.categories.RemoveAt(index);
                    Save();
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    GUIUtility.ExitGUI();
                    return;
                }
            }

            EditorGUILayout.EndHorizontal();

            if (!cat.foldout)
            {
                EditorGUILayout.EndVertical();
                HandleCategoryDrop(index);
                return;
            }

            EditorGUI.indentLevel++;

            // ── Active items ─────────────────────────────────────────
            for (int j = 0; j < filteredActive.Count; j++)
                DrawTodoItem(cat, filteredActive[j], false, filteredActive, j);

            DrawAddItem(cat, catId);

            // ── Completed section ────────────────────────────────────
            if (completedItems.Count > 0)
            {
                EditorGUILayout.Space(6);

                EditorGUILayout.BeginHorizontal();

                cat.completedFoldout = EditorGUILayout.Foldout(cat.completedFoldout,
                    $"Completed ({filteredCompleted.Count})", true);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Clear Completed", EditorStyles.miniButton, GUILayout.Width(110)))
                {
                    foreach (var item in completedItems)
                    {
                        item.completed = false;
                        item.text = StripCompletedTag(item.text);
                    }
                    Save();
                    GUIUtility.ExitGUI();
                }

                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.9f, 0.35f, 0.35f, 1f);
                if (GUILayout.Button("Delete All", EditorStyles.miniButton, GUILayout.Width(70)))
                {
                    if (EditorUtility.DisplayDialog("Delete Completed",
                            $"Permanently delete all completed items in \"{cat.name}\"?",
                            "Delete", "Cancel"))
                    {
                        cat.items.RemoveAll(i => i.completed);
                        Save();
                        GUI.backgroundColor = oldBg;
                        GUIUtility.ExitGUI();
                    }
                }
                GUI.backgroundColor = oldBg;

                EditorGUILayout.EndHorizontal();

                if (cat.completedFoldout)
                {
                    foreach (var item in filteredCompleted)
                        DrawTodoItem(cat, item, true, null, -1);
                }
            }

            EditorGUILayout.Space(4);

            // ── Notes ────────────────────────────────────────────────
            EditorGUILayout.LabelField("Notes", _sectionLabelStyle);
            string newNotes = EditorGUILayout.TextArea(cat.notes, _notesStyle, GUILayout.MinHeight(40));
            if (newNotes != cat.notes) { cat.notes = newNotes; Save(); }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();

            HandleCategoryDrop(index);
        }

        private void HandleCategoryDrop(int index)
        {
            if (_dragCatIndex >= 0 && _dragCatIndex != index)
            {
                Rect catRect = GUILayoutUtility.GetLastRect();
                if (catRect.Contains(Event.current.mousePosition)
                    && Event.current.type == EventType.MouseUp)
                {
                    var dragged = _data.categories[_dragCatIndex];
                    _data.categories.RemoveAt(_dragCatIndex);
                    int insertAt = index > _dragCatIndex ? index - 1 : index;
                    _data.categories.Insert(insertAt, dragged);
                    _dragCatIndex = -1;
                    Save();
                    Event.current.Use();
                    GUIUtility.ExitGUI();
                }
            }
        }

        // ── To-Do Item ───────────────────────────────────────────────
        private void DrawTodoItem(Category category, TodoItem item, bool isCompletedSection,
            List<TodoItem> sortedList, int sortedIndex)
        {
            EditorGUILayout.BeginHorizontal();

            // ── Checkbox ─────────────────────────────────────────────
            bool newCompleted = EditorGUILayout.Toggle(item.completed, GUILayout.Width(CheckboxWidth));
            if (newCompleted != item.completed)
            {
                item.completed = newCompleted;
                if (item.completed)
                    item.text = StripCompletedTag(item.text) + "  *COMPLETED*";
                else
                    item.text = StripCompletedTag(item.text);
                Save();
                GUIUtility.ExitGUI();
            }

            GUILayout.Space(16);

            // ── Priority box ─────────────────────────────────────────
            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = PriorityColors[item.priority];

            if (!isCompletedSection)
            {
                Rect priorityRect = GUILayoutUtility.GetRect(PriorityBoxWidth, 16,
                    GUILayout.Width(PriorityBoxWidth), GUILayout.Height(16));
                GUI.Box(priorityRect, item.priority.ToString(), EditorStyles.miniButton);

                if (Event.current.type == EventType.MouseDown && priorityRect.Contains(Event.current.mousePosition))
                {
                    if (Event.current.button == 0) // Left-click: increase
                        item.priority = (item.priority + 1) % 5;
                    else if (Event.current.button == 1) // Right-click: decrease
                        item.priority = (item.priority + 4) % 5;

                    NormalizeSortOrders(category);
                    Save();
                    Event.current.Use();
                }
            }
            else
            {
                GUILayout.Box(item.priority.ToString(), EditorStyles.miniButton,
                    GUILayout.Width(PriorityBoxWidth), GUILayout.Height(16));
            }

            GUI.backgroundColor = oldBg;

            // ── Text field ───────────────────────────────────────────
            GUIStyle textStyle = isCompletedSection ? _completedFieldStyle : EditorStyles.textField;
            string newText = EditorGUILayout.TextField(item.text, textStyle);
            if (newText != item.text) { item.text = newText; Save(); }

            // ── Delete button ────────────────────────────────────────
            if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(DeleteBtnWidth)))
            {
                category.items.Remove(item);
                Save();
                GUIUtility.ExitGUI();
            }

            // ── Up/Down arrows (active items only) ───────────────────
            if (!isCompletedSection && sortedList != null)
            {
                GUILayout.Space(4);

                bool canMoveUp = CanMoveUpInPriority(sortedList, sortedIndex);
                bool canMoveDown = CanMoveDownInPriority(sortedList, sortedIndex);

                GUI.enabled = canMoveUp;
                if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(18)))
                {
                    SwapSortOrders(sortedList[sortedIndex], sortedList[sortedIndex - 1]);
                    Save();
                    GUIUtility.ExitGUI();
                }

                GUI.enabled = canMoveDown;
                if (GUILayout.Button("▼", EditorStyles.miniButtonRight, GUILayout.Width(18)))
                {
                    SwapSortOrders(sortedList[sortedIndex], sortedList[sortedIndex + 1]);
                    Save();
                    GUIUtility.ExitGUI();
                }

                GUI.enabled = true;
            }

            EditorGUILayout.EndHorizontal();
        }

        private bool CanMoveUpInPriority(List<TodoItem> sorted, int index)
        {
            if (index <= 0) return false;
            return sorted[index - 1].priority == sorted[index].priority;
        }

        private bool CanMoveDownInPriority(List<TodoItem> sorted, int index)
        {
            if (index >= sorted.Count - 1) return false;
            return sorted[index + 1].priority == sorted[index].priority;
        }

        private void SwapSortOrders(TodoItem a, TodoItem b)
        {
            int temp = a.sortOrder;
            a.sortOrder = b.sortOrder;
            b.sortOrder = temp;
        }

        private void NormalizeSortOrders(Category category)
        {
            var groups = category.items.Where(i => !i.completed)
                .GroupBy(i => i.priority);
            foreach (var group in groups)
            {
                int order = 0;
                foreach (var item in group.OrderBy(i => i.sortOrder))
                    item.sortOrder = order++;
            }
        }

        // ── Add Item ─────────────────────────────────────────────────
        private void DrawAddItem(Category category, string catId)
        {
            if (!_newItemText.ContainsKey(catId))
                _newItemText[catId] = "";

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(17);

            GUI.SetNextControlName($"newItem_{catId}");
            _newItemText[catId] = EditorGUILayout.TextField(_newItemText[catId]);

            bool addPressed = GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(22));
            bool enterPressed = Event.current.type == EventType.KeyDown
                                && Event.current.keyCode == KeyCode.Return
                                && GUI.GetNameOfFocusedControl() == $"newItem_{catId}";

            if ((addPressed || enterPressed) && !string.IsNullOrWhiteSpace(_newItemText[catId]))
            {
                int maxOrder = category.items.Where(i => !i.completed && i.priority == 0)
                    .Select(i => i.sortOrder).DefaultIfEmpty(-1).Max() + 1;
                category.items.Add(new TodoItem
                {
                    text = _newItemText[catId].Trim(),
                    priority = 0,
                    sortOrder = maxOrder
                });
                _newItemText[catId] = "";
                Save();
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndHorizontal();
        }

        // ── Add Category ─────────────────────────────────────────────
        private void DrawAddCategory()
        {
            EditorGUILayout.BeginHorizontal();
            _newCategoryName = EditorGUILayout.TextField("New Category", _newCategoryName);

            if (GUILayout.Button("Add Category", GUILayout.Width(100)))
            {
                if (!string.IsNullOrWhiteSpace(_newCategoryName))
                {
                    _data.categories.Add(new Category { name = _newCategoryName.Trim() });
                    _newCategoryName = "";
                    Save();
                    GUI.FocusControl(null);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        // ── Helpers ──────────────────────────────────────────────────
        private void SwapCategories(int a, int b)
        {
            var temp = _data.categories[a];
            _data.categories[a] = _data.categories[b];
            _data.categories[b] = temp;
            Save();
        }

        private bool MatchesSearch(string text)
        {
            return text != null && text.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string StripCompletedTag(string text)
        {
            if (text == null) return "";
            return text
                .Replace("  *COMPLETED*", "")
                .Replace(" *COMPLETED*", "")
                .Replace("*COMPLETED*", "")
                .TrimEnd();
        }

        // ── Import (Manual via button) ───────────────────────────────
        private void ImportFile()
        {
            string path = EditorUtility.OpenFilePanel("Import Project Planner", "", "txt,json");
            if (string.IsNullOrEmpty(path)) return;

            string ext = Path.GetExtension(path).ToLowerInvariant();
            string content = File.ReadAllText(path);

            List<Category> imported = null;

            try
            {
                if (ext == ".json")
                    imported = ParseJson(content);
                else
                    imported = ParseTxt(content);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Import Error",
                    $"Failed to parse file:\n{e.Message}", "OK");
                return;
            }

            if (imported == null || imported.Count == 0)
            {
                EditorUtility.DisplayDialog("Import", "No categories found in the file.", "OK");
                return;
            }

            MergeImported(imported);

            EditorUtility.DisplayDialog("Import",
                $"Imported {imported.Count} category(s) from {Path.GetFileName(path)}.", "OK");
        }

        // ── Import (Auto from watch folders) ─────────────────────────
        public void ProcessImportFolders()
        {
            bool anyImported = false;

            anyImported |= ProcessImportFolder(ImportTxtFolder, ".txt");
            anyImported |= ProcessImportFolder(ImportJsonFolder, ".json");

            if (anyImported)
            {
                Save();
                Repaint();
            }
        }

        private bool ProcessImportFolder(string folder, string expectedExt)
        {
            if (!Directory.Exists(folder)) return false;

            string[] files = Directory.GetFiles(folder, "*" + expectedExt);
            if (files.Length == 0) return false;

            bool anyProcessed = false;
            List<string> errorFiles = new List<string>();

            foreach (string filePath in files)
            {
                string content = File.ReadAllText(filePath);
                List<Category> imported = null;

                try
                {
                    if (expectedExt == ".json")
                        imported = ParseJson(content);
                    else
                        imported = ParseTxt(content);
                }
                catch (Exception e)
                {
                    errorFiles.Add($"{Path.GetFileName(filePath)}: {e.Message}");
                    continue;
                }

                if (imported == null || imported.Count == 0)
                {
                    errorFiles.Add($"{Path.GetFileName(filePath)}: No categories found. Check file format.");
                    continue;
                }

                MergeImported(imported);
                anyProcessed = true;

                // Delete the file after successful import
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Project Planner] Could not delete imported file {filePath}: {e.Message}");
                }
            }

            if (errorFiles.Count > 0)
            {
                string errorMsg = "The following files could not be imported (incorrect format):\n\n"
                    + string.Join("\n", errorFiles)
                    + "\n\nThese files have been left in place so you can fix them.";
                EditorUtility.DisplayDialog("Project Planner Import Error", errorMsg, "OK");
            }

            if (anyProcessed)
            {
                // Clean up .meta files for deleted imports
                AssetDatabase.Refresh();
            }

            return anyProcessed;
        }

        // ── Merge Logic ──────────────────────────────────────────────
        private void MergeImported(List<Category> imported)
        {
            foreach (var importedCat in imported)
            {
                var existing = _data.categories.Find(c =>
                    string.Equals(c.name, importedCat.name, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    existing.items.AddRange(importedCat.items);
                    if (!string.IsNullOrWhiteSpace(importedCat.notes))
                    {
                        if (!string.IsNullOrWhiteSpace(existing.notes))
                            existing.notes += "\n" + importedCat.notes;
                        else
                            existing.notes = importedCat.notes;
                    }
                }
                else
                {
                    _data.categories.Add(importedCat);
                }
            }

            Save();
            Repaint();
        }

        // ── Ensure Import Folders Exist ──────────────────────────────
        private static void EnsureImportFolders()
        {
            if (!Directory.Exists(ImportTxtFolder))
                Directory.CreateDirectory(ImportTxtFolder);
            if (!Directory.Exists(ImportJsonFolder))
                Directory.CreateDirectory(ImportJsonFolder);
        }

        // ── TXT Parser ───────────────────────────────────────────────
        private static readonly Regex CheckboxPattern = new Regex(
            @"^(\s*[-*•]|\s*\\[-*]|\s*\d+[.)]\s)", RegexOptions.Compiled);

        private List<Category> ParseTxt(string content)
        {
            var categories = new List<Category>();
            Category current = null;
            bool inNotes = false;

            string[] lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.Trim();

                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                if (string.Equals(trimmed, "Category:", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(trimmed, "Category", StringComparison.OrdinalIgnoreCase))
                {
                    string catName = "";
                    for (int j = i + 1; j < lines.Length; j++)
                    {
                        string next = lines[j].Trim();
                        if (!string.IsNullOrWhiteSpace(next))
                        {
                            catName = next.TrimEnd(':');
                            i = j;
                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(catName))
                    {
                        current = new Category { name = catName };
                        categories.Add(current);
                        inNotes = false;
                    }
                    continue;
                }

                if (string.Equals(trimmed, "Notes:", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(trimmed, "Notes", StringComparison.OrdinalIgnoreCase))
                {
                    inNotes = true;
                    continue;
                }

                if (current == null) continue;

                if (inNotes)
                {
                    if (!string.IsNullOrWhiteSpace(current.notes))
                        current.notes += "\n" + trimmed;
                    else
                        current.notes = trimmed;
                }
                else if (CheckboxPattern.IsMatch(line))
                {
                    string itemText = StripListPrefix(trimmed);
                    if (!string.IsNullOrWhiteSpace(itemText))
                    {
                        current.items.Add(new TodoItem
                        {
                            text = itemText,
                            priority = 0,
                            completed = false
                        });
                    }
                }
            }

            return categories;
        }

        private static string StripListPrefix(string text)
        {
            string result = Regex.Replace(text, @"^(\\?[-*•]|\d+[.)])\s*", "").Trim();
            return result;
        }

        // ── JSON Parser ──────────────────────────────────────────────
        [Serializable]
        private class JsonImportData
        {
            public List<JsonImportCategory> categories = new List<JsonImportCategory>();
        }

        [Serializable]
        private class JsonImportCategory
        {
            public string name = "";
            public List<string> items = new List<string>();
            public string notes = "";
        }

        private List<Category> ParseJson(string content)
        {
            var jsonData = JsonUtility.FromJson<JsonImportData>(content);
            var categories = new List<Category>();

            if (jsonData?.categories == null) return categories;

            foreach (var jc in jsonData.categories)
            {
                var cat = new Category
                {
                    name = string.IsNullOrWhiteSpace(jc.name) ? "Imported" : jc.name,
                    notes = jc.notes ?? ""
                };

                if (jc.items != null)
                {
                    foreach (var itemText in jc.items)
                    {
                        if (!string.IsNullOrWhiteSpace(itemText))
                        {
                            cat.items.Add(new TodoItem
                            {
                                text = itemText.Trim(),
                                priority = 0,
                                completed = false
                            });
                        }
                    }
                }

                categories.Add(cat);
            }

            return categories;
        }

        // ── Auto-Save to .txt and .json ──────────────────────────────
        private void SaveToFiles()
        {
            try
            {
                // Save as .txt
                var sb = new StringBuilder();
                foreach (var cat in _data.categories)
                {
                    sb.AppendLine("Category:");
                    sb.AppendLine($"{cat.name}:");
                    sb.AppendLine();

                    foreach (var item in cat.items)
                    {
                        string prefix = item.completed ? "[x]" : "[ ]";
                        string priorityTag = item.priority > 0 ? $" (P{item.priority})" : "";
                        sb.AppendLine($"- {prefix}{priorityTag} {StripCompletedTag(item.text)}");
                    }

                    sb.AppendLine();

                    if (!string.IsNullOrWhiteSpace(cat.notes))
                    {
                        sb.AppendLine("Notes:");
                        sb.AppendLine(cat.notes);
                    }

                    sb.AppendLine();
                }

                File.WriteAllText(SaveTxtPath, sb.ToString());

                // Save as .json (using the import-friendly format)
                var jsonExport = new JsonImportData();
                foreach (var cat in _data.categories)
                {
                    var jc = new JsonImportCategory
                    {
                        name = cat.name,
                        items = cat.items.Select(i => StripCompletedTag(i.text)).ToList(),
                        notes = cat.notes ?? ""
                    };
                    jsonExport.categories.Add(jc);
                }

                string json = JsonUtility.ToJson(jsonExport, true);
                File.WriteAllText(SaveJsonPath, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Project Planner] Auto-save to files failed: {e.Message}");
            }
        }

        // ── Persistence ──────────────────────────────────────────────
        private void Save()
        {
            EditorPrefs.SetString(EditorPrefsKey, JsonUtility.ToJson(_data, false));
            SaveToFiles();
        }

        private void Load()
        {
            string json = EditorPrefs.GetString(EditorPrefsKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { _data = JsonUtility.FromJson<NotesData>(json); }
                catch { _data = new NotesData(); }
            }
            else
            {
                _data = new NotesData();
            }
        }
    }

    // ── Asset Postprocessor (watches import folders) ──────────────
    public class ProjectPlannerImportWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            bool shouldProcess = false;

            foreach (string asset in importedAssets)
            {
                if (asset.Contains("ProjectPlannerImport"))
                {
                    string ext = Path.GetExtension(asset).ToLowerInvariant();
                    if (ext == ".txt" || ext == ".json")
                    {
                        shouldProcess = true;
                        break;
                    }
                }
            }

            if (shouldProcess)
            {
                // Delay to ensure file is fully written
                EditorApplication.delayCall += () =>
                {
                    var window = EditorWindow.GetWindow<NotesEditorWindow>("Project Planner", false);
                    if (window != null)
                    {
                        window.ProcessImportFolders();
                    }
                };
            }
        }
    }
}
#endif
