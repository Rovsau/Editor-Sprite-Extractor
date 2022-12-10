using System.IO;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

/* 

    # Read Me
    If sprite texture import settings were valid, 
    the sprite array item is deleted,
    so that the invalid ones can be processed after.

    # Notes
    https://docs.unity3d.com/ScriptReference/GUIUtility.GetControlID.html
    https://www.youtube.com/watch?v=GcOs7aOmloU&ab_channel=CryptoGrounds
    https://docs.unity3d.com/ScriptReference/IHasCustomMenu.AddItemsToMenu.html

    https://docs.unity3d.com/ScriptReference/EditorGUI.ChangeCheckScope.html

 */


namespace MyNamespace.EditorSpriteExtractor.Window
{
    internal class SpriteExtractorWindow : EditorWindow
    {
        #region Constants
        private const string __README_HEADER = 
            "Recommended Import Settings\n" +
            "Max Texture Size:  8192\n" +
            "(to prevent downsampling)\n\n\n";

        private const string __README_SPRITE_ATLAS =
            __README_HEADER +
            "\n" +
            "Extract All Sprites from their Sprite Atlas.";

        private const string __README_TEXTURE2D = 
            __README_HEADER + 
            "\n" +
            "Extract All Sprites from multiple Spritesheets.";

        private const string __README_SPRITE =
            __README_HEADER +
            "\n" +
            "Extract Sprites from their source Spritesheets.";

        private const string __helpBoxMessage = 
            "Required Texture2D Import Settings\n" +
            "Texture Type:  Sprite\n" +
            "Read/Write:  True\n\n" +//
            "Recommended Import Settings\n" +
            "Import Mode:  Multiple\n" +
            "Max Texture Size:  8192\n" +
            "(to prevent downsampling)";
        #endregion

        #region Variables
        private static SpriteExtractorWindow _window;
        private static bool _isOpen;

        // Arrays.
        public Texture2D[] _textures;
        public Sprite[] _sprites;
        public SpriteAtlas[] _atlases;
        public Texture2DArray[] _textureArrays;

        private SerializedObject _serializedObject;
        private SerializedProperty m_textures;
        private SerializedProperty m_sprites;
        private SerializedProperty m_atlases;
        private SerializedProperty m_textureArrays;

        // Radio.
        private ESelection _selected;
        private string[] _options;

        // DropDown.
        private SpriteExtractorCore.EncodeToFormat _encodeToFormat;

        // Object field. Drag and drop. Search. 
        private DefaultAsset _outputFolder;
        private protected string _outputFolderPath;
        
        // SerializedProperty Array.
        private Vector2Int _arrayCount;
        private Vector2 _scrollPos;
        
        // Ping last output file after process is complete. 
        private string _lastOutputFilePath;

        private enum ESelection
        {
            Texture_2D,
            Texture_2D_Array,
            Sprites,
            Sprite_Atlas
        }
        #endregion

        #region Window 
        [MenuItem(" Window / 2D / Extract Sprite from Multiple _F12")]
        private static void Window()
        {
            _window = GetWindow<SpriteExtractorWindow>("Sprite Extractor");

            if (_isOpen) _window.Close();
            else
            {
                _window.Show();
                _isOpen = true;
            }
        }

        private void OnEnable()
        {
            // Initialize default values.
            _sprites = new Sprite[3];
            _textures = new Texture2D[3];
            _atlases = new SpriteAtlas[3];
            _textureArrays = new Texture2DArray[3];
            _arrayCount = new Vector2Int(_textures.Length, _sprites.Length);

            _serializedObject = new SerializedObject(this);
            m_textures = _serializedObject.FindProperty(nameof(_textures));
            m_sprites = _serializedObject.FindProperty(nameof(_sprites));
            m_atlases = _serializedObject.FindProperty(nameof(_atlases));
            m_textureArrays = _serializedObject.FindProperty(nameof(_textureArrays));

            _encodeToFormat = SpriteExtractorCore.EncodeToFormat.Source;
            _lastOutputFilePath = string.Empty;

            EditorGUIUtility.labelWidth = 128f;

            // Expand the arrays.
            m_textures.isExpanded = true;
            m_sprites.isExpanded = true;
            m_atlases.isExpanded = true;
            m_textureArrays.isExpanded = true;

            // Prepare Enum Radio selection.
            ESelection[] eSelections = (ESelection[])System.Enum.GetValues(typeof(ESelection));
            _options = new string[eSelections.Length];

            // Parse Enum Names.
            for (int i = 0; i < eSelections.Length; i++)
            {
                _options[i] = $"  {eSelections[i].ToString().Replace('_', ' ')}";
            }

            // Subscribe to receive data for post-processing. 
            SpriteExtractorCore.OnProcessed_Texture -= RemoveFullyProcessed_Texture2D;
            SpriteExtractorCore.OnProcessed_Texture += RemoveFullyProcessed_Texture2D;

            SpriteExtractorCore.OnProcessed_Texture2DArray -= RemoveFullyProcessed_Texture2DArray;
            SpriteExtractorCore.OnProcessed_Texture2DArray += RemoveFullyProcessed_Texture2DArray;

            SpriteExtractorCore.OnProcessed_Sprite -= RemoveFullyProcessed_Sprite;
            SpriteExtractorCore.OnProcessed_Sprite += RemoveFullyProcessed_Sprite;

            SpriteExtractorCore.OnProcessed_SpriteAtlas -= RemoveFullyProcessed_SpriteAtlas;
            SpriteExtractorCore.OnProcessed_SpriteAtlas += RemoveFullyProcessed_SpriteAtlas;

            SpriteExtractorCore.OnProcessed_OutputFilePath -= GetProcessedOutputFilePath;
            SpriteExtractorCore.OnProcessed_OutputFilePath += GetProcessedOutputFilePath;
        }

        private void OnDisable()
        {
            _isOpen = false;

            SpriteExtractorCore.OnProcessed_Texture -= RemoveFullyProcessed_Texture2D;
            SpriteExtractorCore.OnProcessed_Texture2DArray -= RemoveFullyProcessed_Texture2DArray;
            SpriteExtractorCore.OnProcessed_Sprite -= RemoveFullyProcessed_Sprite;
            SpriteExtractorCore.OnProcessed_SpriteAtlas -= RemoveFullyProcessed_SpriteAtlas;
            SpriteExtractorCore.OnProcessed_OutputFilePath -= GetProcessedOutputFilePath;
        }
        #endregion

        private void Update()
        {
            // 
            ConditionallyUpdateSerializedProperties();

            // Reject selections that are not real folders. (DefaultAsset workaround). 
            if (_outputFolder != null && !AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(_outputFolder))) _outputFolder = null;
        }

        private void OnGUI()
        {
            // GUI Checkpoint. <---
            EditorGUILayout.BeginVertical(SpriteExtractorStyles.MainWrapper);
            EditorGUILayout.BeginVertical(SpriteExtractorStyles.Area_Config);
            EditorGUILayout.LabelField("Configuration", SpriteExtractorStyles.Title);

            #region Encode to Format
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Encode to Format");
            _encodeToFormat = (SpriteExtractorCore.EncodeToFormat)EditorGUILayout.EnumPopup(_encodeToFormat);
            EditorGUILayout.EndHorizontal();
            #endregion

            #region Output Folder
            EditorGUILayout.Space();
            // Can still be a Scene object. Could be fixed. Low prio. 
            _outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", _outputFolder, typeof(DefaultAsset), false);
            #endregion
            
            EditorGUILayout.LabelField(string.Empty, SpriteExtractorStyles.HorizontalSeparator);

            #region Radio selection
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(SpriteExtractorStyles.Area_Config_Left);
            EditorGUILayout.LabelField("Extract from", SpriteExtractorStyles.Title);
            EditorGUILayout.Space();
            _selected = (ESelection)GUILayout.SelectionGrid((int)_selected, _options, 1, SpriteExtractorStyles.RadioButton);

            EditorGUILayout.Space();

            #region Button
            if (!_outputFolder) GUI.enabled = false;
            if (GUILayout.Button("Extract All Sprites", SpriteExtractorStyles.Button))
            {
                GetOutputFolderPath();

                switch (_selected)
                {
                    case ESelection.Texture_2D:
                        RemoveDuplicates(m_textures);
                        break;
                    case ESelection.Texture_2D_Array:
                        RemoveDuplicates(m_textureArrays);
                        break;
                    case ESelection.Sprites:
                        RemoveDuplicates(m_sprites);
                        break;
                    case ESelection.Sprite_Atlas:
                        RemoveDuplicates(m_atlases);
                        break;
                }
                
                _serializedObject.ApplyModifiedProperties();

                switch (_selected)
                {
                    case ESelection.Texture_2D:
                        SpriteExtractorCore.Extract(_textures, _outputFolderPath, _encodeToFormat);
                        break;
                    case ESelection.Texture_2D_Array:
                        SpriteExtractorCore.Extract(_textureArrays, _outputFolderPath, _encodeToFormat);
                        break;
                    case ESelection.Sprites:
                        SpriteExtractorCore.Extract(_sprites, _outputFolderPath, _encodeToFormat);
                        break;
                    case ESelection.Sprite_Atlas:
                        SpriteExtractorCore.Extract(_atlases, _outputFolderPath, _encodeToFormat);
                        break;
                }

                OnComplete();
            }
            GUI.enabled = true;
            #endregion 

            EditorGUILayout.EndVertical();
            EditorGUILayout.BeginVertical();
            #region Help Box
            EditorGUILayout.LabelField((_selected == ESelection.Texture_2D) ? __README_TEXTURE2D : __README_SPRITE, SpriteExtractorStyles.HelpBox);
            #endregion
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            #endregion

            EditorGUILayout.EndVertical(); // Area_Config


            #region Property Field
            EditorGUILayout.Space();
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            switch (_selected)
            {
                case ESelection.Texture_2D:
                    EditorGUILayout.PropertyField(m_textures, true);
                    break;
                case ESelection.Texture_2D_Array:
                    EditorGUILayout.PropertyField(m_textureArrays, true);
                    break;
                case ESelection.Sprites:
                    EditorGUILayout.PropertyField(m_sprites, true);
                    break;
                case ESelection.Sprite_Atlas:
                    EditorGUILayout.PropertyField(m_atlases, true);
                    break;
            }
            EditorGUILayout.EndScrollView();
            #endregion

            EditorGUILayout.EndVertical(); // MainWrapper
        }


        #region Utilities
        private void OnComplete()
        {
            if (_lastOutputFilePath == string.Empty)
            {
                Debug.Log($"No Last Output.");
                return;
            }

            AssetDatabase.Refresh();

            FocusPing(_lastOutputFilePath);

            _lastOutputFilePath = string.Empty;
        }

        private void FocusPing(string fullOrRelativeFilePath)
        {
            fullOrRelativeFilePath = fullOrRelativeFilePath.Replace('\\', '/');
            string projectFolder = Application.dataPath.Substring(0, Application.dataPath.LastIndexOf('/'));
            string relativePath = fullOrRelativeFilePath.Replace(projectFolder, string.Empty).Substring(1);

            UnityEngine.Object lastOutputFileObject = (UnityEngine.Object)AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(relativePath);

            EditorUtility.FocusProjectWindow();
            EditorGUIUtility.PingObject(lastOutputFileObject);
        }

        private void GetOutputFolderPath()
        {
            if (!_outputFolder) throw new DirectoryNotFoundException("Output Folder is not assigned.");
            string assets = Application.dataPath;
            string project = assets.Substring(0, assets.LastIndexOf('/') + 1);
            string folderPath = AssetDatabase.GetAssetPath(_outputFolder);
            _outputFolderPath = Path.Combine(project, folderPath);
        }

        private void RemoveFullyProcessed_Texture2D(System.Object sender, Texture2D texture2D)
        {
            for (int i = 0; i < _textures.Length; i++)
            {
                if (_textures[i].Equals(texture2D))
                {
                    m_textures.DeleteArrayElementAtIndex(i);
                    Debug.Log($"Post Process Removed:  {texture2D.name}");
                    break;
                }
            }

            _serializedObject.ApplyModifiedProperties();
        }

        private void RemoveFullyProcessed_Texture2DArray(System.Object sender, Texture2DArray texture2DArray)
        {
            for (int i = 0; i < _textureArrays.Length; i++)
            {
                if (_textureArrays[i].Equals(texture2DArray))
                {
                    m_textureArrays.DeleteArrayElementAtIndex(i);
                    Debug.Log($"Post Process Removed:  {texture2DArray.name}");
                    break;
                }
            }

            _serializedObject.ApplyModifiedProperties();
        }

        private void RemoveFullyProcessed_Sprite(System.Object sender, Sprite sprite)
        {
            for (int i = 0; i < _sprites.Length; i++)
            {
                if (_sprites[i].Equals(sprite))
                {
                    m_sprites.DeleteArrayElementAtIndex(i);
                    Debug.Log($"Post Process Removed:  {sprite.name}");
                    break;
                }
            }

            _serializedObject.ApplyModifiedProperties();
        }

        private void RemoveFullyProcessed_SpriteAtlas(System.Object sender, SpriteAtlas atlas)
        {
            for (int i = 0; i < _atlases.Length; i++)
            {
                if (_atlases[i].Equals(atlas))
                {
                    m_atlases.DeleteArrayElementAtIndex(i);
                    Debug.Log($"Post Process Removed:  {atlas.name}");
                    break;
                }
            }

            _serializedObject.ApplyModifiedProperties();
        }

        private void ConditionallyUpdateSerializedProperties()
        {
            // If the array size has changed.
            if (m_textures.arraySize != _arrayCount.x || m_sprites.arraySize != _arrayCount.y)
            {
                _arrayCount.x = m_textures.arraySize;
                _arrayCount.y = m_sprites.arraySize;

                // Necessary in order to get the proper context menu when right-clicking array elements. 
                _serializedObject.ApplyModifiedProperties();
            }
        }

        public static void RemoveDuplicates(SerializedProperty sp)
        {
            for (int i = 0; i + 1 < sp.arraySize; i++)
            {
                for (int r = sp.arraySize - 1; r >= i + 1; r--)
                {
                    if (SerializedProperty.DataEquals(sp.GetArrayElementAtIndex(i), sp.GetArrayElementAtIndex(r)))
                    {
                        Debug.Log($"Removing Duplicate:  {sp.GetArrayElementAtIndex(r).objectReferenceValue}");
                        sp.DeleteArrayElementAtIndex(r);
                    }
                }
            }
        }

        private void GetProcessedOutputFilePath(System.Object sender, string outputFilePath)
        {
            _lastOutputFilePath = outputFilePath;
        }
        #endregion
    }
}