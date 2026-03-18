#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            public int priority; // 0=default, 1=light blue, 2=light green, 3=yellow, 4=red
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
        private const float CheckboxWidth = 18f;
        private const float DeleteBtnWidth = 20f;

        private static readonly Color[] PriorityColors = new Color[]
        {
            new Color(0.35f, 0.35f, 0.35f, 1f), // 0: default grey
            new Color(0.4f, 0.7f, 0.9f, 1f),    // 1: light blue
            new Color(0.4f, 0.8f, 0.4f, 1f),    // 2: light green
            new Color(0.9f, 0.85f, 0.3f, 1f),   // 3: yellow
            new Color(0.9f, 0.3f, 0.3f, 1f),    // 4: red
        };

        // ── State ────────────────────────────────────────────────────
        private NotesData _data;
        private Vector2 _scrollPos;
        private string _newCategoryName = "";
        private string _searchFilter = "";
        private readonly Dictionary<string, string> _newItemText = new Dictionary<string, string>();

        // Drag state
        private TodoItem _dragItem;
        private int _dragSourceCatIndex = -1;

        // Styles
        private GUIStyle _completedFieldStyle;
        private GUIStyle _notesStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _sectionLabelStyle;
        private bool _stylesInitialized;

        // ── Menu Entry ───────────────────────────────────────────────
        [MenuItem("Window/General/Notes and To-Do")]
        public static void ShowWindow()
        {
            var window = GetWindow<NotesEditorWindow>("Notes & To-Do");
            window.minSize = new Vector2(320, 250);
        }

        // ── Lifecycle ────────────────────────────────────────────────
        private void OnEnable() => Load();
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

            // Release drag on mouse up anywhere
            if (Event.current.type == EventType.MouseUp)
                ClearDrag();
        }

        // ── Toolbar ──────────────────────────────────────────────────
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("Notes & To-Do", _headerStyle);
            GUILayout.FlexibleSpace();

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

            var activeItems = cat.items.Where(i => !i.completed).OrderByDescending(i => i.priority).ToList();
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

            string newName = EditorGUILayout.TextField(cat.name, EditorStyles.boldLabel);
            if (newName != cat.name) { cat.name = newName; Save(); }

            GUILayout.FlexibleSpace();

            GUILayout.Label($"{completedItems.Count}/{cat.items.Count}", EditorStyles.miniLabel);

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

            if (!cat.foldout) { EditorGUILayout.EndVertical(); return; }

            EditorGUI.indentLevel++;

            // ── Active items (sorted by priority desc) ───────────────
            for (int j = 0; j < filteredActive.Count; j++)
                DrawTodoItem(cat, filteredActive[j], false, index);

            DrawAddItem(cat, catId);

            // ── Completed section ────────────────────────────────────
            if (completedItems.Count > 0)
            {
                EditorGUILayout.Space(6);

                EditorGUILayout.BeginHorizontal();

                cat.completedFoldout = EditorGUILayout.Foldout(cat.completedFoldout,
                    $"Completed ({filteredCompleted.Count})", true);

                GUILayout.FlexibleSpace();

                // Clear Completed = bulk uncheck, move back to active
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

                // Delete All = permanently destroy completed items
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
                        DrawTodoItem(cat, item, true, index);
                }
            }

            EditorGUILayout.Space(4);

            // ── Notes ────────────────────────────────────────────────
            EditorGUILayout.LabelField("Notes", _sectionLabelStyle);
            string newNotes = EditorGUILayout.TextArea(cat.notes, _notesStyle, GUILayout.MinHeight(40));
            if (newNotes != cat.notes) { cat.notes = newNotes; Save(); }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        // ── To-Do Item ───────────────────────────────────────────────
        private void DrawTodoItem(Category category, TodoItem item, bool isCompletedSection, int catIndex)
        {
            Rect rowRect = EditorGUILayout.BeginHorizontal();

            // ── Drag handle (active only) ────────────────────────────
            if (!isCompletedSection)
            {
                var handleRect = GUILayoutUtility.GetRect(14f, 16f, GUILayout.Width(14));
                EditorGUI.LabelField(handleRect, "≡", EditorStyles.miniLabel);
                EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.Pan);

                if (Event.current.type == EventType.MouseDown
                    && handleRect.Contains(Event.current.mousePosition))
                {
                    _dragItem = item;
                    _dragSourceCatIndex = catIndex;
                    Event.current.Use();
                }
            }
            else
            {
                GUILayout.Space(14);
            }

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
                // Clickable — cycles 0→1→2→3→4→0
                if (GUILayout.Button(item.priority.ToString(), EditorStyles.miniButton,
                        GUILayout.Width(PriorityBoxWidth), GUILayout.Height(16)))
                {
                    item.priority = (item.priority + 1) % 5;
                    Save();
                }
            }
            else
            {
                // Read-only display for completed items
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

            EditorGUILayout.EndHorizontal();

            // ── Drop zone for drag reorder ───────────────────────────
            if (!isCompletedSection && _dragItem != null && _dragItem != item
                && _dragSourceCatIndex == catIndex && _dragItem.priority == item.priority)
            {
                if (rowRect.Contains(Event.current.mousePosition)
                    && Event.current.type == EventType.MouseUp)
                {
                    int fromIdx = category.items.IndexOf(_dragItem);
                    int toIdx = category.items.IndexOf(item);
                    if (fromIdx >= 0 && toIdx >= 0 && fromIdx != toIdx)
                    {
                        category.items.RemoveAt(fromIdx);
                        toIdx = category.items.IndexOf(item);
                        category.items.Insert(toIdx, _dragItem);
                        Save();
                    }
                    ClearDrag();
                    Event.current.Use();
                }
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
                category.items.Add(new TodoItem { text = _newItemText[catId].Trim(), priority = 0 });
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

        private void ClearDrag()
        {
            _dragItem = null;
            _dragSourceCatIndex = -1;
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

        // ── Import ────────────────────────────────────────────────
        private void ImportFile()
        {
            string path = EditorUtility.OpenFilePanel("Import Notes & To-Do", "", "txt,json");
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

            // Merge imported categories into existing data
            foreach (var importedCat in imported)
            {
                var existing = _data.categories.Find(c =>
                    string.Equals(c.name, importedCat.name, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    // Same name: append items and notes
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
                    // New category
                    _data.categories.Add(importedCat);
                }
            }

            Save();
            Repaint();

            EditorUtility.DisplayDialog("Import",
                $"Imported {imported.Count} category(s) from {Path.GetFileName(path)}.", "OK");
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

                // Skip empty lines
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                // Check for "Category:" line (case-insensitive)
                if (string.Equals(trimmed, "Category:", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(trimmed, "Category", StringComparison.OrdinalIgnoreCase))
                {
                    // Next non-empty line is the category name
                    string catName = "";
                    for (int j = i + 1; j < lines.Length; j++)
                    {
                        string next = lines[j].Trim();
                        if (!string.IsNullOrWhiteSpace(next))
                        {
                            catName = next.TrimEnd(':');
                            i = j; // advance past the name line
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

                // Check for "Notes:" line
                if (string.Equals(trimmed, "Notes:", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(trimmed, "Notes", StringComparison.OrdinalIgnoreCase))
                {
                    inNotes = true;
                    continue;
                }

                // Must have a current category to add content
                if (current == null) continue;

                if (inNotes)
                {
                    // Append to notes
                    if (!string.IsNullOrWhiteSpace(current.notes))
                        current.notes += "\n" + trimmed;
                    else
                        current.notes = trimmed;
                }
                else if (CheckboxPattern.IsMatch(line))
                {
                    // Strip the bullet/number prefix
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
            // Remove leading: \- \* - * • or 1. 1) 12. 12) etc, plus trailing whitespace after
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

        // ── Persistence ──────────────────────────────────────────────
        private void Save()
        {
            EditorPrefs.SetString(EditorPrefsKey, JsonUtility.ToJson(_data, false));
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
}
#endif
