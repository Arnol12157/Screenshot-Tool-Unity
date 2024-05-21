using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

public class ScreenshotToolEditorWindow : EditorWindow
{
    private List<Texture2D> _screenshotTextures = new List<Texture2D>();
    private List<Texture2D> _screenshotOriginalTextures = new List<Texture2D>();
    private List<string> _screenshotFileNames = new List<string>();
    private Vector2 _scrollPosition;
    private bool _loadedScreenshots = false;
    private int _selectedResolutionIndex = 0;
    private int _selectedFormatIndex = 0;
    private readonly string[] _resolutions = { "1920x1080", "1280x720", "800x600" };
    private readonly string[] _formats = { "PNG", "JPG" };
    private string _filterText = string.Empty;
    private int _sortOption = 0; // 0: Name, 1: Date
    private Texture2D _activeTexture;
    private Texture2D _originalTexture;
    private Vector2 _drawPosition;
    private bool _isDrawing;
    private Texture2D _drawingMask = null;

    private Rect _cropRect = new Rect(0, 0, 100, 100);
    private bool _isCropping = false;
    private bool _isResizing = false;
    private int _newWidth;
    private int _newHeight;

    [MenuItem("TG Utils/Screenshot Tool")]
    private static void Init()
    {
        ScreenshotToolEditorWindow window = GetWindow<ScreenshotToolEditorWindow>();
        window.titleContent.text = "Screenshot Tool";
        window.Show();
    }

    private void OnGUI()
    {
        CaptureGameScreenButton();
        DisplaySettings();

        if (!_loadedScreenshots)
        {
            LoadAllScreenshots();
            _loadedScreenshots = true;
        }
        
        DisplayFiltersAndSorting();
        ShowAllScreenshots();

        void CaptureGameScreenButton()
        {
            if (GUILayout.Button("Capture Game Screen"))
            {
                var directoryPath = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length) + "/ScreenShots";
                Directory.CreateDirectory(directoryPath);

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string fileExtension = _formats[_selectedFormatIndex].ToLower();
                string fullName = Path.Combine(directoryPath, $"ScreenShot_{timestamp}.{fileExtension}");
                Debug.Log($"Saving screenshot to {fullName}");

                int width = Screen.width;
                int height = Screen.height;
                if (_selectedResolutionIndex == 1) { width = 1280; height = 720; }
                if (_selectedResolutionIndex == 2) { width = 800; height = 600; }

                ScreenCapture.CaptureScreenshot(fullName, 1);
                EditorApplication.delayCall += () => DelayedLoadScreenshot(fullName, 1f);
            }
        }

        void DisplaySettings()
        {
            GUILayout.Label("Settings", EditorStyles.boldLabel);
            _selectedResolutionIndex = EditorGUILayout.Popup("Resolution", _selectedResolutionIndex, _resolutions);
            _selectedFormatIndex = EditorGUILayout.Popup("Format", _selectedFormatIndex, _formats);
        }

        void DelayedLoadScreenshot(string path, float delay)
        {
            EditorApplication.delayCall += () =>
            {
                if (File.Exists(path))
                {
                    LoadScreenshot(path);
                }
                else
                {
                    DelayedLoadScreenshot(path, delay);
                }
            };
        }

        void LoadScreenshot(string path)
        {
            byte[] fileData = File.ReadAllBytes(path);
            var screenshotTexture = new Texture2D(2, 2);
            screenshotTexture.LoadImage(fileData);
            screenshotTexture.Apply();
            
            var screenshotOriginalTexture = new Texture2D(2, 2);
            screenshotOriginalTexture.LoadImage(fileData);
            screenshotOriginalTexture.Apply();
            
            _screenshotTextures.Add(screenshotTexture);
            _screenshotOriginalTextures.Add(screenshotOriginalTexture);

            Repaint();
        }

        void LoadAllScreenshots()
        {
            var directoryPath = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length) + "/ScreenShots";
            if (!Directory.Exists(directoryPath))
            {
                return;
            }

            string[] pngFiles = Directory.GetFiles(directoryPath, "*.png", SearchOption.TopDirectoryOnly);
            string[] jpgFiles = Directory.GetFiles(directoryPath, "*.jpg", SearchOption.TopDirectoryOnly);

            string[] allFiles = new string[pngFiles.Length + jpgFiles.Length];
            pngFiles.CopyTo(allFiles, 0);
            jpgFiles.CopyTo(allFiles, pngFiles.Length);

            foreach (var file in allFiles)
            {
                if (!_screenshotFileNames.Contains(file))
                {
                    _screenshotFileNames.Add(file);
                    LoadScreenshot(file.Replace(@"\", "/"));
                    Debug.Log("-----" + file);
                }
            }
        }
        
        void DisplayFiltersAndSorting()
        {
            GUILayout.Label("Filters and Sorting", EditorStyles.boldLabel);
            _filterText = EditorGUILayout.TextField("Filter by Name:", _filterText);

            _sortOption = EditorGUILayout.Popup("Sort By", _sortOption, new string[] { "Name", "Date" });

            if (GUILayout.Button("Apply Filters and Sorting"))
            {
                ApplyFiltersAndSorting();
            }
        }

        void ApplyFiltersAndSorting()
        {
            if (_sortOption == 0)
            {
                _screenshotFileNames.Sort();
            }
            else
            {
                _screenshotFileNames.Sort((a, b) => File.GetCreationTime(b).CompareTo(File.GetCreationTime(a)));
            }

            if (!string.IsNullOrEmpty(_filterText))
            {
                for (int i = _screenshotFileNames.Count - 1; i >= 0; i--)
                {
                    if (!Path.GetFileName(_screenshotFileNames[i]).Contains(_filterText, StringComparison.OrdinalIgnoreCase))
                    {
                        _screenshotFileNames.RemoveAt(i);
                        _screenshotTextures.RemoveAt(i);
                    }
                }
            }

            Repaint();
        }

        void ShowAllScreenshots()
        {
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            GUILayout.BeginVertical();
            for (int i = 0; i < _screenshotTextures.Count; i++)
            {
                var screen = _screenshotOriginalTextures[i];
                var screenToBeDrawed = _screenshotTextures[i];
                if (screen != null)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(screen, GUILayout.Width(100), GUILayout.Height(100 * screen.height / screen.width)))
                    {
                        _activeTexture = screenToBeDrawed;
                        _originalTexture = screen;
                        _drawingMask = new Texture2D(_activeTexture.width, _activeTexture.height);
                    }
                    GUILayout.Label(Path.GetFileName(_screenshotFileNames[i]));

                    if (GUILayout.Button("Open"))
                    {
                        EditorUtility.OpenWithDefaultApp(_screenshotFileNames[i]);
                    }

                    if (GUILayout.Button("Delete"))
                    {
                        DeleteScreenshot(i);
                    }

                    if (GUILayout.Button("Rename"))
                    {
                        RenameScreenshot(i);
                    }

                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndVertical();
            GUILayout.EndScrollView();

            if (_activeTexture != null)
            {
                GUILayout.Label("Drawing Tools", EditorStyles.boldLabel);
                _isDrawing = GUILayout.Toggle(_isDrawing, "Enable Drawing");

                Rect drawArea = GUILayoutUtility.GetRect(_activeTexture.width, _activeTexture.height);
                EditorGUI.DrawTextureTransparent(drawArea, _activeTexture);

                HandleDrawing(drawArea);
                if (GUILayout.Button("Clear Drawing"))
                {
                    ClearDrawing();
                }
            }
        }

        void HandleDrawing(Rect drawArea)
        {
            if (_isDrawing)
            {
                Event e = Event.current;
                Vector2 mousePos = e.mousePosition;

                if (e.type == EventType.MouseDown || e.type == EventType.MouseDrag)
                {
                    if (drawArea.Contains(mousePos))
                    {
                        Vector2 textureCoord = mousePos - drawArea.position;
                        textureCoord.y = _activeTexture.height - textureCoord.y;

                        textureCoord.x = Mathf.Clamp(textureCoord.x, 0, _activeTexture.width - 1);
                        textureCoord.y = Mathf.Clamp(textureCoord.y, 0, _activeTexture.height - 1);

                        DrawOnTexture(_activeTexture, _drawingMask, (int)textureCoord.x, (int)textureCoord.y, Color.red);
                        Repaint();
                    }
                }
            }
        }

        void DrawOnTexture(Texture2D texture, Texture2D mask, int x, int y, Color color)
        {
            for (int i = -2; i <= 2; i++)
            {
                for (int j = -2; j <= 2; j++)
                {
                    int px = Mathf.Clamp(x + i, 0, texture.width - 1);
                    int py = Mathf.Clamp(y + j, 0, texture.height - 1);

                    texture.SetPixel(px, py, color);
                    mask.SetPixel(px, py, Color.white);
                }
            }
            texture.Apply();
            mask.Apply();
        }
        
        void ClearDrawing()
        {
            if (_activeTexture != null && _drawingMask != null)
            {
                Color[] maskPixels = _drawingMask.GetPixels();
                Color[] drawedPixels = _activeTexture.GetPixels();
                Color[] originalPixels = _originalTexture.GetPixels();

                for (int i = 0; i < maskPixels.Length; i++)
                {
                    if (maskPixels[i] == Color.white)
                    {
                        drawedPixels[i] = originalPixels[i];//Color.clear;
                    }
                }

                _activeTexture.SetPixels(drawedPixels);
                _activeTexture.Apply();
            }
        }

        
        void ClearDrawingMask()
        {
            Color[] clearPixels = new Color[_drawingMask.width * _drawingMask.height];
            for (int i = 0; i < clearPixels.Length; i++)
            {
                clearPixels[i] = Color.clear;
            }
            _drawingMask.SetPixels(clearPixels);
            _drawingMask.Apply();
        }

        void DeleteScreenshot(int index)
        {
            string path = _screenshotFileNames[index];
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            _screenshotTextures.RemoveAt(index);
            _screenshotFileNames.RemoveAt(index);
            Repaint();
        }

        void RenameScreenshot(int index)
        {
            string oldPath = _screenshotFileNames[index];
            string directory = Path.GetDirectoryName(oldPath);
            string newName = EditorUtility.SaveFilePanel("Rename Screenshot", directory, Path.GetFileNameWithoutExtension(oldPath), Path.GetExtension(oldPath).TrimStart('.'));

            if (!string.IsNullOrEmpty(newName))
            {
                File.Move(oldPath, newName);
                _screenshotFileNames[index] = newName;
                Repaint();
            }
        }
    }
}
