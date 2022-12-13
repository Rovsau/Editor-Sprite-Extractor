using System.IO;
using UnityEditor;
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
        // Debug
        private static readonly bool TRACE = false;

        #region Info
        private const string __INFO_TEXTURE2D = "Extract all Sprites from a Texture2D with Sprite Mode\nSingle or Multiple.";
        private const string __INFO_TEXTURE2DARRAY = "Extract all elements from a Texture2DArray with predefined Columns and Rows.";
        private const string __INFO_SPRITES = "Extract Sprites from their source Texture2D files.\n";
        private const string __INFO_SPRITEATLAS = "Extract all Sprites in a Sprite Atlas, from their respective Texture2D files.";

        private string _info;
        #endregion

        #region Variables
        private const string __CONFIGURATION = "Configuration";
        private const string __EXTRACTFROM = "From";
        private const string __ENCODETOFORMAT = "Encode to Format";
        private const string __OUTPUTFOLDER = "Destination Folder";


        private static SpriteExtractorWindow _window;
        private static bool _isOpen;

        // Arrays.
        public Texture2D[] _texture2D;
        public Sprite[] _sprites;
        public SpriteAtlas[] _spriteAtlas;
        public Texture2DArray[] _texture2DArray;

        private SerializedObject _serializedObject;
        private SerializedProperty m_texture2D;
        private SerializedProperty m_sprites;
        private SerializedProperty m_spriteAtlas;
        private SerializedProperty m_texture2DArray;

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
            _texture2D = new Texture2D[3];
            _spriteAtlas = new SpriteAtlas[3];
            _texture2DArray = new Texture2DArray[3];
            _arrayCount = new Vector2Int(_texture2D.Length, _sprites.Length);

            _serializedObject = new SerializedObject(this);
            m_texture2D = _serializedObject.FindProperty(nameof(_texture2D));
            m_sprites = _serializedObject.FindProperty(nameof(_sprites));
            m_spriteAtlas = _serializedObject.FindProperty(nameof(_spriteAtlas));
            m_texture2DArray = _serializedObject.FindProperty(nameof(_texture2DArray));

            _encodeToFormat = SpriteExtractorCore.EncodeToFormat.Source;
            _lastOutputFilePath = string.Empty;

            EditorGUIUtility.labelWidth = 128f;

            // Expand the arrays.
            m_texture2D.isExpanded = true;
            m_sprites.isExpanded = true;
            m_spriteAtlas.isExpanded = true;
            m_texture2DArray.isExpanded = true;

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
            UpdateSelectionInfo();

            // Reject selections that are not real folders. (DefaultAsset workaround). 
            if (_outputFolder != null && !AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(_outputFolder))) _outputFolder = null;
        }

        private void OnGUI()
        {
            // GUI Checkpoint. <---
            EditorGUILayout.BeginVertical(SpriteExtractorStyles.MainWrapper);
            EditorGUILayout.BeginVertical(SpriteExtractorStyles.Area_Config);
            EditorGUILayout.LabelField(__CONFIGURATION, SpriteExtractorStyles.Title);

            #region Encode to Format
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(__ENCODETOFORMAT);
            _encodeToFormat = (SpriteExtractorCore.EncodeToFormat)EditorGUILayout.EnumPopup(_encodeToFormat);
            EditorGUILayout.EndHorizontal();
            #endregion

            #region Output Folder
            EditorGUILayout.Space();
            // Can still be a Scene object. Could be fixed. Low prio. 
            _outputFolder = (DefaultAsset)EditorGUILayout.ObjectField(__OUTPUTFOLDER, _outputFolder, typeof(DefaultAsset), false);
            #endregion
            
            EditorGUILayout.LabelField(string.Empty, SpriteExtractorStyles.HorizontalSeparator);

            #region Radio selection
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(SpriteExtractorStyles.Area_Config_Left);
            EditorGUILayout.LabelField(__EXTRACTFROM, SpriteExtractorStyles.Title);
            EditorGUILayout.Space(4f);
            _selected = (ESelection)GUILayout.SelectionGrid((int)_selected, _options, 1, SpriteExtractorStyles.RadioButton);
            EditorGUILayout.Space(4f );
            EditorGUILayout.EndVertical();
            EditorGUILayout.BeginVertical();
            #region Help Box
            EditorGUILayout.LabelField(_info, SpriteExtractorStyles.HelpBox);
            #endregion
            EditorGUILayout.Space(12f);
            #region Button
            if (!_outputFolder) GUI.enabled = false;
            if (GUILayout.Button($"Extract {_options[(int)_selected].Trim()}", SpriteExtractorStyles.Button))
            {
                GetOutputFolderPath();

                switch (_selected)
                {
                    case ESelection.Texture_2D:
                        RemoveDuplicates(m_texture2D);
                        break;
                    case ESelection.Texture_2D_Array:
                        RemoveDuplicates(m_texture2DArray);
                        break;
                    case ESelection.Sprites:
                        RemoveDuplicates(m_sprites);
                        break;
                    case ESelection.Sprite_Atlas:
                        RemoveDuplicates(m_spriteAtlas);
                        break;
                }
                
                _serializedObject.ApplyModifiedProperties();

                switch (_selected)
                {
                    case ESelection.Texture_2D:
                        SpriteExtractorCore.Extract(_texture2D, _outputFolderPath, _encodeToFormat);
                        break;
                    case ESelection.Texture_2D_Array:
                        SpriteExtractorCore.Extract(_texture2DArray, _outputFolderPath, _encodeToFormat);
                        break;
                    case ESelection.Sprites:
                        SpriteExtractorCore.Extract(_sprites, _outputFolderPath, _encodeToFormat);
                        break;
                    case ESelection.Sprite_Atlas:
                        SpriteExtractorCore.Extract(_spriteAtlas, _outputFolderPath, _encodeToFormat);
                        break;
                }

                OnComplete();
            }
            GUI.enabled = true;
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
                    EditorGUILayout.PropertyField(m_texture2D, true);
                    break;
                case ESelection.Texture_2D_Array:
                    EditorGUILayout.PropertyField(m_texture2DArray, true);
                    break;
                case ESelection.Sprites:
                    EditorGUILayout.PropertyField(m_sprites, true);
                    break;
                case ESelection.Sprite_Atlas:
                    EditorGUILayout.PropertyField(m_spriteAtlas, true);
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
            for (int i = 0; i < _texture2D.Length; i++)
            {
                if (_texture2D[i].Equals(texture2D))
                {
                    m_texture2D.DeleteArrayElementAtIndex(i);
                    if (TRACE) Debug.Log($"Post Process Removed:  {texture2D.name}");
                    break;
                }
            }

            _serializedObject.ApplyModifiedProperties();
        }

        private void RemoveFullyProcessed_Texture2DArray(System.Object sender, Texture2DArray texture2DArray)
        {
            for (int i = 0; i < _texture2DArray.Length; i++)
            {
                if (_texture2DArray[i].Equals(texture2DArray))
                {
                    m_texture2DArray.DeleteArrayElementAtIndex(i);
                    if (TRACE) Debug.Log($"Post Process Removed:  {texture2DArray.name}");
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
                    if (TRACE) Debug.Log($"Post Process Removed:  {sprite.name}");
                    break;
                }
            }

            _serializedObject.ApplyModifiedProperties();
        }

        private void RemoveFullyProcessed_SpriteAtlas(System.Object sender, SpriteAtlas atlas)
        {
            for (int i = 0; i < _spriteAtlas.Length; i++)
            {
                if (_spriteAtlas[i].Equals(atlas))
                {
                    m_spriteAtlas.DeleteArrayElementAtIndex(i);
                    if (TRACE) Debug.Log($"Post Process Removed:  {atlas.name}");
                    break;
                }
            }

            _serializedObject.ApplyModifiedProperties();
        }

        private void ConditionallyUpdateSerializedProperties()
        {
            // If the array size has changed.
            if (m_texture2D.arraySize != _arrayCount.x || m_sprites.arraySize != _arrayCount.y)
            {
                _arrayCount.x = m_texture2D.arraySize;
                _arrayCount.y = m_sprites.arraySize;

                // Necessary in order to get the proper context menu when right-clicking array elements. 
                _serializedObject.ApplyModifiedProperties();
            }
        }


        private void UpdateSelectionInfo()
        {
            switch (_selected)
            {
                case ESelection.Texture_2D:
                    _info = __INFO_TEXTURE2D;
                    break;
                case ESelection.Texture_2D_Array:
                    _info = __INFO_TEXTURE2DARRAY;
                    break;
                case ESelection.Sprites:
                    _info = __INFO_SPRITES;
                    break;
                case ESelection.Sprite_Atlas:
                    _info = __INFO_SPRITEATLAS;
                    break;
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
                        if (TRACE) Debug.Log($"Removing Duplicate:  {sp.GetArrayElementAtIndex(r).objectReferenceValue}");
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