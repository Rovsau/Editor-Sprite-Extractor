using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;
using static MyNamespace.EditorSpriteExtractor.SpriteExtractorCore;

namespace MyNamespace.EditorSpriteExtractor
{
    public static class SpriteExtractorCore
    {
        /* Read Me
         * 
         *  This class does not modify any existing files. 
         *  Exception:  Will overwrite files with the same path and name as the output.
         *  
         *  Encoding format is recognized by file extension.
         *  
         *  Texture Import settings such as isReadable are irrelevant, 
         *  because the file bytes are read by System.IO, bypassing TextureImporter settings, 
         *  and loaded directly into a temporary Texture2D.
         *  
         *  Exception: 
         *      Texture2D requires the following importer settings.
         *      Texture Type:   2D Sprite 
         *      Sprite Mode:    Single or Multiple
         * 
         */

        #region Variables
        public static event EventHandler<Texture2D> OnProcessed_Texture;
        public static event EventHandler<Sprite> OnProcessed_Sprite;
        public static event EventHandler<SpriteAtlas> OnProcessed_SpriteAtlas;
        public static event EventHandler<Texture2DArray> OnProcessed_Texture2DArray;
        public static event EventHandler<string> OnProcessed_OutputFilePath;

        public static EncodeToFormat[] EncodingFormats { get; private set; }

        static SpriteExtractorCore()
        {
            EncodingFormats = (EncodeToFormat[])System.Enum.GetValues(typeof(EncodeToFormat));
        }

        public static int derp { get; private set; }

        public enum EncodeToFormat
        {
            Source,
            EXR,
            JPG,
            PNG,
            TGA
        }
        #endregion

        // Extract SpriteAtlas.
        public static void Extract(this SpriteAtlas[] array, string outputFolderPath, EncodeToFormat encodeToFormat)
        {
            foreach (SpriteAtlas atlas in array)
            {
                if (atlas) Extract(atlas, outputFolderPath, encodeToFormat);
            }
        }

        public static void Extract(this SpriteAtlas atlas, string outputFolderPath, EncodeToFormat encodeToFormat)
        {
            string path = AssetDatabase.GetAssetPath(atlas);
            Sprite[] sprites = new Sprite[atlas.spriteCount];
            atlas.GetSprites(sprites);

            foreach (Sprite sprite in sprites)
            {
                string filePath = $"{outputFolderPath}/{sprite.name}.";
                Extract(sprite, filePath, encodeToFormat);
            }

            OnProcessed_SpriteAtlas?.Invoke(nameof(SpriteExtractorCore), atlas);
        }

        // Extract Texture2DArray.
        public static void Extract(this Texture2DArray[] array, string outputFolderPath, EncodeToFormat encodeToFormat)
        {
            foreach (Texture2DArray element in array)
            {
                if (element) Extract(element, outputFolderPath, encodeToFormat);
            }
        }

        public static void Extract(this Texture2DArray texture2dArray, string outputFolderPath, EncodeToFormat encodeToFormat)
        {
            // Get texture element size. 
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture2dArray));
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            // Readability.
            Vector2Int size = new Vector2Int()
            {
                x = texture2dArray.width,
                y = texture2dArray.height
            };

            // Create Rects for each element.
            Rect[] rects = new Rect[texture2dArray.depth];

            int index = 0;
            int x = 0;
            int y = 0;
            for (int row = 0; row < settings.flipbookRows; row++)
            {
                for (int column = 0; column < settings.flipbookColumns; column++)
                {
                    rects[index++] = new Rect(x, y, size.x, size.y);
                    x += size.x;
                }
                x = 0;
                y += size.y;
            }

            // Initialize a temporary container for the original texture file.
            Texture2D rawTexture = RawCopyOf(texture2dArray, ref encodeToFormat);

            // Extract each Rect from the original texture. 
            for (int i = 0; i < rects.Length; i++)
            {
                string filePath = $"{outputFolderPath}/{texture2dArray.name}_{i}.";

                // Write to disk. 
                EncodeAndWriteToDisk(SubTexture(rawTexture, rects[i], encodeToFormat), ref filePath, encodeToFormat);

                OnProcessed_OutputFilePath?.Invoke(typeof(SpriteExtractorCore), filePath);
            }

            OnProcessed_Texture2DArray?.Invoke(typeof(SpriteExtractorCore), texture2dArray);
        }

        // Extract Texture2D.
        public static void Extract(this Texture2D[] array, string outputFolderPath, EncodeToFormat encodeToFormat)
        {
            foreach (Texture2D texture in array)
            {
                if (texture) Extract(texture, outputFolderPath, encodeToFormat);
            }
        }

        public static void Extract(this Texture2D texture2d, string outputFolderPath, EncodeToFormat encodeToFormat)
        {
            // If this validation fails, the Texture2D is not processed.
            TextureImporter importer = (TextureImporter)TextureImporter.GetAtPath(AssetDatabase.GetAssetPath(texture2d));
            if (importer.textureType != TextureImporterType.Sprite) return;

            string assetPath = AssetDatabase.GetAssetPath(texture2d);
            UnityEngine.Object[] subassets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
            Sprite[] sprites = new Sprite[subassets.Length];

            // Get all sprites from Texture2D.
            for (int i = 0; i < subassets.Length; i++)
            {
                sprites[i] = (Sprite)subassets[i];
            }

            Texture2D raw = RawCopyOf(texture2d, ref encodeToFormat);

            //Extract(sprites, outputFolderPath, encodeToFormat);
            Extract(sprites, outputFolderPath, encodeToFormat, raw);

            // Notify that this Texture2D was fully processed.
            OnProcessed_Texture?.Invoke(typeof(SpriteExtractorCore), texture2d);
        }

        // Extract Sprites.
        public static void Extract(this Sprite[] sprites, string outputFolderPath, EncodeToFormat encodeToFormat, Texture2D rawTexture = null)
        {
            foreach (Sprite sprite in sprites)
            {
                if (sprite)
                {
                    string filePath = $"{outputFolderPath}/{sprite.name}.";
                    Extract(sprite, filePath, encodeToFormat, rawTexture);
                }
            }
        }

        public static void Extract(this Sprite sprite, string outputFileNamePath, EncodeToFormat encodeToFormat, Texture2D rawTexture = null)
        {
            // If no raw copy of the texture exists, create one. 
            // A raw copy is passed when extracting multiple sprites from a single Texture2D,
            // so that a texture copy is not made for each sprite extracted. It is an optimization that might be worth while for big tasks. 
            // Further optimization can be achieved, by caching textures in the static.
            if (rawTexture == null) rawTexture = RawCopyOf(sprite.texture, ref encodeToFormat);

            EncodeAndWriteToDisk(SubTexture(rawTexture, sprite.rect, encodeToFormat), ref outputFileNamePath, encodeToFormat);

            // Notify that this Sprite was fully processed.
            OnProcessed_Sprite?.Invoke(typeof(SpriteExtractorCore), sprite);
            OnProcessed_OutputFilePath?.Invoke(typeof(SpriteExtractorCore), outputFileNamePath);
        }

        private static string MaybeGetSourceEncodeToFormat(Texture texture, ref EncodeToFormat encodeToFormat)
        {
            string filePath = AssetDatabase.GetAssetPath(texture);
            FileInfo file = new FileInfo(filePath);
            string extension = file.Extension.Substring(1).ToLower();
            
            // Maybe not.
            if (encodeToFormat != EncodeToFormat.Source) return extension;

            for (int i = 0; i < EncodingFormats.Length; i++)
            {
                if (EncodingFormats[i].ToString().ToLower().EndsWith(extension))
                {
                    encodeToFormat = EncodingFormats[i];
                    return extension;
                }
            }

            // If source format cannot be determined, throw exception. 
            throw new FormatException($"Source file format not supported. ({extension})");
        }

        // If combined with SubTexture(), must include optionally passing in the raw texture.
        private static void EncodeAndWriteToDisk(Texture2D rawTexture2d, ref string outputFileNamePath, EncodeToFormat encodeToFormat)
        {
            // Encode Data.
            byte[] data;
            switch (encodeToFormat)
            {
                case EncodeToFormat.Source:
                    throw new Exception($"{nameof(EncodeToFormat)} format could not be determined, or is unsupported.  Is the filename missing an extension?");
                case EncodeToFormat.EXR:
                    data = rawTexture2d.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat);
                    break;
                case EncodeToFormat.JPG:
                    data = rawTexture2d.EncodeToJPG();
                    break;
                case EncodeToFormat.PNG:
                    data = rawTexture2d.EncodeToPNG();
                    break;
                case EncodeToFormat.TGA:
                    data = rawTexture2d.EncodeToTGA();
                    break;
                default:
                    throw new ArgumentNullException($"{nameof(EncodeToFormat)} value somehow ended up as null.");
            }

            // Add File Extension.
            outputFileNamePath += encodeToFormat.ToString().ToLower();

            // Write Data to Disk.
            File.WriteAllBytes(outputFileNamePath, data);
        }

        public struct byte3 { public byte x, y, z; }

        private static Texture2D RawCopyOf(Texture texture2dMaybeArray, ref EncodeToFormat encodeToFormat)
        {
            // Initialize a temporary container for the original texture. 
            Texture2D rawCopy = new Texture2D(texture2dMaybeArray.width, texture2dMaybeArray.height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                anisoLevel = 0,
                requestedMipmapLevel = 0,
            };

            string fileExtension = MaybeGetSourceEncodeToFormat(texture2dMaybeArray, ref encodeToFormat).ToLower();

            if (fileExtension == "exr")
            {
                Texture2D originalTexture = (Texture2D)texture2dMaybeArray;
                UnityEngine.Debug.Log($"{originalTexture.format}"); // RGB24
                NativeArray<byte3> exr = originalTexture.GetRawTextureData<byte3>();
                NativeArray<byte> bytes = ImageConversion.EncodeNativeArrayToEXR(exr, originalTexture.graphicsFormat, (uint)originalTexture.width, (uint)originalTexture.height);
                rawCopy.LoadRawTextureData(bytes.ToArray());

                // https://docs.unity3d.com/ScriptReference/ImageConversion.EncodeNativeArrayToEXR.html
                // https://answers.unity.com/questions/1817392/why-is-nativearray-always-shorter-than-the-number.html
                Debug.Log($"EXR Passed.");
            }
            else if (fileExtension == "tga")
            {
                Texture2D originalTexture = (Texture2D)texture2dMaybeArray;
                rawCopy.LoadRawTextureData(originalTexture.GetRawTextureData());
            }
            else
            {
                // Read the original file byte array. 
                rawCopy.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(texture2dMaybeArray)), false);
            }

            return rawCopy;
        }

        private static Texture2D SubTexture(Texture2D texture, Rect rect, EncodeToFormat encodeToFormat)
        {
            // Rect values.
            int left = (int)rect.x;
            int top = (int)rect.y;
            int width = (int)rect.width;
            int height = (int)rect.height;

            // Compensate for textures with negative coordinates.
            // Thanks to Artgig @ answers.unity.com.
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

            TextureFormat outputTextureFormat = TextureFormat.RGBA32;

            switch (encodeToFormat)
            {
                case EncodeToFormat.EXR:
                    outputTextureFormat = TextureFormat.RGBAFloat;
                    break;
                case EncodeToFormat.JPG:
                    outputTextureFormat = TextureFormat.RGBA32;
                    break;
                case EncodeToFormat.PNG:
                    outputTextureFormat = TextureFormat.RGBA32;
                    break;
                case EncodeToFormat.TGA:
                    outputTextureFormat = TextureFormat.RGBA32;
                    break;
            }

            // Initialize a container for the subtexture. 
            Texture2D subtexture = new Texture2D(width, height, outputTextureFormat, false)
            {
                filterMode = FilterMode.Point,
                anisoLevel = 0,
                requestedMipmapLevel = 0,
            };

            // Get and set the pixels. 
            subtexture.SetPixels(texture.GetPixels(left, top, width, height, 0));
            subtexture.Apply();

            return subtexture;
        }
    }
}