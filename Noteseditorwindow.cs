#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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
        }

        [Serializable]
        private class Category
        {
            public string name = "New Category";
            public bool foldout = true;
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

        // ── State ────────────────────────────────────────────────────
        private NotesData _data;
        private Vector2 _scrollPos;
        private string _newCategoryName = "";
        private readonly Dictionary<string, string> _newItemText = new Dictionary<string, string>();
        private GUIStyle _completedStyle;
        private GUIStyle _notesStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _categoryBarStyle;
        private bool _stylesInitialized;

        // ── Menu Entry ───────────────────────────────────────────────
        [MenuItem("Window/General/Notes and To-Do")]
        public static void ShowWindow()
        {
            var window = GetWindow<NotesEditorWindow>("Notes & To-Do");
            window.minSize = new Vector2(300, 200);
        }

        // ── Lifecycle ────────────────────────────────────────────────
        private void OnEnable()
        {
            Load();
        }

        private void OnDisable()
        {
            Save();
        }

        // ── GUI ──────────────────────────────────────────────────────
        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _completedStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Italic,
                normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
            };

            _notesStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true
            };

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                padding = new RectOffset(4, 0, 4, 4)
            };

            _categoryBarStyle = new GUIStyle(EditorStyles.foldoutHeader)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12
            };

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitStyles();

            // Toolbar
            DrawToolbar();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            for (int i = 0; i < _data.categories.Count; i++)
            {
                DrawCategory(i);
                if (i < _data.categories.Count - 1)
                    EditorGUILayout.Space(4);
            }

            EditorGUILayout.Space(8);

            // Add category section
            DrawAddCategory();

            EditorGUILayout.EndScrollView();
        }

        // ── Toolbar ──────────────────────────────────────────────────
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("Notes & To-Do", _headerStyle);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Collapse All", EditorStyles.toolbarButton))
            {
                foreach (var cat in _data.categories) cat.foldout = false;
            }

            if (GUILayout.Button("Expand All", EditorStyles.toolbarButton))
            {
                foreach (var cat in _data.categories) cat.foldout = true;
            }

            if (GUILayout.Button("Clear Completed", EditorStyles.toolbarButton))
            {
                if (EditorUtility.DisplayDialog("Clear Completed",
                        "Remove all completed items from every category?", "Yes", "Cancel"))
                {
                    foreach (var cat in _data.categories)
                        cat.items.RemoveAll(item => item.completed);
                    Save();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        // ── Category ─────────────────────────────────────────────────
        private void DrawCategory(int index)
        {
            var category = _data.categories[index];
            string catId = index.ToString();

            EditorGUILayout.BeginVertical("box");

            // Category header row
            EditorGUILayout.BeginHorizontal();

            category.foldout = EditorGUILayout.Foldout(category.foldout, "", true);

            // Editable category name
            string newName = EditorGUILayout.TextField(category.name, EditorStyles.boldLabel);
            if (newName != category.name)
            {
                category.name = newName;
                Save();
            }

            GUILayout.FlexibleSpace();

            // Item count badge
            int doneCount = category.items.FindAll(i => i.completed).Count;
            GUILayout.Label($"{doneCount}/{category.items.Count}", EditorStyles.miniLabel);

            // Move up / down
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
                        $"Delete \"{category.name}\" and all its items?", "Delete", "Cancel"))
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

            if (!category.foldout)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUI.indentLevel++;

            // To-do items
            for (int j = 0; j < category.items.Count; j++)
            {
                DrawTodoItem(category, j);
            }

            // Add new item
            DrawAddItem(category, catId);

            EditorGUILayout.Space(4);

            // Notes text area
            EditorGUILayout.LabelField("Notes", EditorStyles.miniLabel);
            string newNotes = EditorGUILayout.TextArea(category.notes, _notesStyle,
                GUILayout.MinHeight(40));
            if (newNotes != category.notes)
            {
                category.notes = newNotes;
                Save();
            }

            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();
        }

        // ── To-Do Item ───────────────────────────────────────────────
        private void DrawTodoItem(Category category, int itemIndex)
        {
            var item = category.items[itemIndex];

            EditorGUILayout.BeginHorizontal();

            // Checkbox
            bool newCompleted = EditorGUILayout.Toggle(item.completed, GUILayout.Width(16));
            if (newCompleted != item.completed)
            {
                item.completed = newCompleted;
                Save();
            }

            // Text — strikethrough-ish style if completed
            GUIStyle style = item.completed ? _completedStyle : EditorStyles.label;
            string displayText = item.completed ? $"<i>{item.text}</i>" : item.text;

            // Editable text field
            string newText = EditorGUILayout.TextField(item.text,
                item.completed ? _completedStyle : EditorStyles.textField);
            if (newText != item.text)
            {
                item.text = newText;
                Save();
            }

            // Delete item
            if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(20)))
            {
                category.items.RemoveAt(itemIndex);
                Save();
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.EndHorizontal();
        }

        // ── Add Item ─────────────────────────────────────────────────
        private void DrawAddItem(Category category, string catId)
        {
            if (!_newItemText.ContainsKey(catId))
                _newItemText[catId] = "";

            EditorGUILayout.BeginHorizontal();

            GUILayout.Space(20);

            _newItemText[catId] = EditorGUILayout.TextField(_newItemText[catId]);

            bool addPressed = GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(22));
            bool enterPressed = Event.current.type == EventType.KeyDown
                                && Event.current.keyCode == KeyCode.Return
                                && GUI.GetNameOfFocusedControl() == $"newItem_{catId}";

            GUI.SetNextControlName($"newItem_{catId}");

            if ((addPressed || enterPressed) && !string.IsNullOrWhiteSpace(_newItemText[catId]))
            {
                category.items.Add(new TodoItem { text = _newItemText[catId].Trim() });
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

        // ── Persistence ──────────────────────────────────────────────
        private void Save()
        {
            string json = JsonUtility.ToJson(_data, false);
            EditorPrefs.SetString(EditorPrefsKey, json);
        }

        private void Load()
        {
            string json = EditorPrefs.GetString(EditorPrefsKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    _data = JsonUtility.FromJson<NotesData>(json);
                }
                catch
                {
                    _data = new NotesData();
                }
            }
            else
            {
                _data = new NotesData();
            }
        }
    }
}
#endif