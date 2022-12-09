using System;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;

namespace MyNamespace.EditorSpriteExtractor
{

    public static class SpriteExtractorCore
    {
        /* To Do
         * 
         *  Texture2DArray
         * 
         */

        /* Read Me
         * 
         *  This class does not modify any existing files. 
         *  Exception:  Will overwrite files with the same path and name as the output.
         * 
         */

        #region Variables
        public static event EventHandler<Texture2D> OnProcessed_Texture;
        public static event EventHandler<Sprite> OnProcessed_Sprite;
        public static event EventHandler<SpriteAtlas> OnProcessed_SpriteAtlas;
        public static event EventHandler<string> OnProcessed_OutputFilePath;

        public static EncodeToFormat[] EncodingFormats { get; private set; }

        static SpriteExtractorCore()
        {
            EncodingFormats = (EncodeToFormat[])System.Enum.GetValues(typeof(EncodeToFormat));
        }

        public enum EncodeToFormat
        {
            Source,
            EXR,
            JPG,
            PNG,
            TGA
        }
        #endregion

        #region Sprite Atlas (untested, unfinished)
        // Extract SpriteAtlas Array.
        private static void Extract(SpriteAtlas[] spriteAtlas_array, string outputFolderPath, EncodeToFormat encodeToFormat, bool useAtlasTextureImportSettings)
        {
            foreach (SpriteAtlas atlas in spriteAtlas_array)
            {
                //if (atlas) Extract(atlas);
            }
        }

        // Extract SpriteAtlas.
        private static void Extract(SpriteAtlas atlas, string outputFolderPath, EncodeToFormat encodeToFormat, bool useAtlasTextureImportSettings)
        {
            // Validate

            string path = AssetDatabase.GetAssetPath(atlas);
            Sprite[] sprites = (Sprite[])AssetDatabase.LoadAllAssetRepresentationsAtPath(path);

            foreach (Sprite sprite in sprites)
            {
                string filePath = $"{outputFolderPath}/{sprite.name}.";
                Extract(sprite, filePath, encodeToFormat);
            }
        }
        #endregion

        #region Texture 2D Array
        // Extract Texture2DArray.
        private static void Extract(Texture2DArray source, string outputFolderPath, EncodeToFormat encodeToFormat, bool useAtlasTextureImportSettings)
        {
            //source.
        }
        #endregion

        #region Texture2D
        // Extract Texture2D Array.
        public static void Extract(Texture2D[] array, string outputFolderPath, EncodeToFormat encodeToFormat)
        {
            foreach (Texture2D texture in array)
            {
                if (texture) Extract(texture, outputFolderPath, encodeToFormat);
            }
        }

        // Extract Texture2D.
        public static void Extract(Texture2D texture2d, string outputFolderPath, EncodeToFormat encodeToFormat)
        {
            // Validation
            if (!HasRequiredImportSettings(texture2d)) return;

            string assetPath = AssetDatabase.GetAssetPath(texture2d);
            UnityEngine.Object[] subassets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
            Sprite[] sprites = new Sprite[subassets.Length];

            // Get all sprites from Texture2D.
            for (int i = 0; i < subassets.Length; i++)
            {
                sprites[i] = (Sprite)subassets[i];
            }

            Extract(sprites, outputFolderPath, encodeToFormat);

            // Notify that this Texture2D was fully processed.
            OnProcessed_Texture?.Invoke(typeof(SpriteExtractorCore), texture2d);
        }
        #endregion

        #region Sprite
        // Extract Sprite Array.
        public static void Extract(Sprite[] sprites, string outputFolderPath, EncodeToFormat encodeToFormat)
        {
            foreach (Sprite sprite in sprites)
            {
                if (sprite)
                {
                    string filePath = $"{outputFolderPath}/{sprite.name}.";
                    Extract(sprite, filePath, encodeToFormat);
                }
            }
        }

        // Extract Sprite.
        public static void Extract(Sprite sprite, string outputFileNamePath, EncodeToFormat encodeToFormat)
        {
            string filePath = AssetDatabase.GetAssetPath(sprite.texture);
            FileInfo file = new FileInfo(filePath);
            string extension = file.Extension.Substring(1);

            Debug.Log("File Path:  " + filePath);
            Debug.Log("File Extension:  " + file.Extension);

            for (int i = 0; i < EncodingFormats.Length; i++)
            {
                if (EncodingFormats[i].ToString().ToLower().EndsWith(extension))
                {
                    encodeToFormat = EncodingFormats[i];
                    break;
                }
            }

            // Extract Sprite Texture.
            Texture2D spriteSubTexture = CropTexture(sprite.texture, sprite.rect);

            byte[] data;

            // Encode Data.
            switch (encodeToFormat)
            {
                case EncodeToFormat.Source:
                    throw new Exception($"{nameof(EncodeToFormat)} format could not be determined, or is unsupported.  Is the filename missing an extension?");
                case EncodeToFormat.EXR:
                    data = spriteSubTexture.EncodeToEXR();
                    break;
                case EncodeToFormat.JPG:
                    data = spriteSubTexture.EncodeToJPG();
                    break;
                case EncodeToFormat.PNG:
                    data = spriteSubTexture.EncodeToPNG();
                    break;
                case EncodeToFormat.TGA:
                    data = spriteSubTexture.EncodeToTGA();
                    break;
                default:
                    throw new ArgumentNullException($"{nameof(EncodeToFormat)} value cannot be null.");
            }

            // Add File Extension.
            outputFileNamePath += encodeToFormat.ToString().ToLower();

            // Write Data to Disk.
            File.WriteAllBytes(outputFileNamePath, data);

            // Notify that this Sprite was fully processed.
            OnProcessed_Sprite?.Invoke(typeof(SpriteExtractorCore), sprite);
            OnProcessed_OutputFilePath?.Invoke(typeof(SpriteExtractorCore), outputFileNamePath);
        }
        #endregion

        #region Utilities
        // Validation. 
        public static bool HasRequiredImportSettings(Texture2D spriteTexture)
        {
            TextureImporter importer = (TextureImporter)TextureImporter.GetAtPath(AssetDatabase.GetAssetPath(spriteTexture));
            //if (importer.isReadable == false) return false; // Not required?
            if (importer.textureType != TextureImporterType.Sprite) return false;
            //if (importer.spriteImportMode != SpriteImportMode.Multiple) return false;
            //if (importer.maxTextureSize != 8192) return false;
            return true;
        }

        private static void CropTextureRAW(Sprite sprite)
        {
            CropTextureRAW(sprite.texture, sprite.rect);
        }

        private static Texture2D CropTextureRAW(Texture2D texture, Rect rect)
        {
            int top = (int)rect.y;
            int left = (int)rect.x;
            int width = (int)rect.width;
            int height = (int)rect.height;

            // Compensate for textures with negative coordinates. 
            // Thanks to Artgig @ answers.unity.com
            // Source: https://answers.unity.com/questions/683772/export-sprite-sheets.html
            if (left < 0)
            {
                width += left;
                left = 0;
            }
            if (top < 0)
            {
                height += top;
                top = 0;
            }
            if (left + width > texture.width)
            {
                width = texture.width - left;
            }
            if (top + height > texture.height)
            {
                height = texture.height - top;
            }
            if (width <= 0 || height <= 0)
            {
                throw new Exception($"{rect}. Sprite position or size does not correspond with Texture coordinates or dimensions. {new Rect(0, 0, texture.width, texture.height)}");
            }

            // Initialize a temporary container for the original texture. 
            Texture2D rawCopy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                anisoLevel = 0,
                requestedMipmapLevel = 0,
            };

            // Read the original file byte array. 
            rawCopy.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(texture)));

            // Initialize a container for the subtexture. 
            Texture2D subtexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                anisoLevel = 0,
                requestedMipmapLevel = 0,
            };

            // Read and write directly to memory in the GPU. 
            NativeArray<Color> nativeRawCopy = rawCopy.GetRawTextureData<Color>();
            NativeArray<Color> nativeSubTexture = subtexture.GetRawTextureData<Color>();

            int write = 0;
            for (int y = 0; y < height; y++)
            {
                int read = (y + top) * texture.width + left;
                for (int x = 0; x < width; x++)
                {
                    // Get subtexture pixels. 
                    nativeSubTexture[write++] = nativeRawCopy[read++];
                }
            }

            return subtexture;
        }

        // Extract Subtexture. 
        private static Texture2D CropTexture(Texture2D pSource, Rect rect)
        {
            // Source:  https://answers.unity.com/questions/683772/export-sprite-sheets.html
            // Thanks to Artgig @ answers.unity.com!

            // <Modification>
            int left = (int)rect.x;
            int top = (int)rect.y;
            int width = (int)rect.width;
            int height = (int)rect.height;
            // </Modification>

            if (left < 0)
            {
                width += left;
                left = 0;
            }
            if (top < 0)
            {
                height += top;
                top = 0;
            }
            if (left + width > pSource.width)
            {
                width = pSource.width - left;
            }
            if (top + height > pSource.height)
            {
                height = pSource.height - top;
            }

            if (width <= 0 || height <= 0)
            {
                throw new Exception($"{rect}. Sprite position or size does not correspond with Texture coordinates or dimensions. {new Rect(0, 0, pSource.width, pSource.height)}");
            }

            Color[] aSourceColor = pSource.GetPixels(0);


            //*** Make New 
            Texture2D oNewTex = new Texture2D(width, height, pSource.format, false); // Swapped TextureFormat.RGBA32 for pSource.format

            //*** Make destination array
            int xLength = width * height;
            Color[] aColor = new Color[xLength];
            
            int i = 0;
            for (int y = 0; y < height; y++)
            {
                int sourceIndex = (y + top) * pSource.width + left;
                for (int x = 0; x < width; x++)
                {
                    aColor[i++] = aSourceColor[sourceIndex++];
                }
            }

            //*** Set Pixels
            oNewTex.SetPixels(aColor);
            oNewTex.Apply();

            //*** Return
            return oNewTex;
        }
        #endregion
    }
}