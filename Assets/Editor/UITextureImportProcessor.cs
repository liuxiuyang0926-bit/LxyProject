using System;
using UnityEditor;
using UnityEngine;

namespace LxyDemo.EditorTools
{
    /// <summary>
    /// 统一设置 UI 图集源图片和 UI 独立图片的导入参数。
    /// </summary>
    internal sealed class UITextureImportProcessor : AssetPostprocessor
    {
        private const string UIAtlasFolder =
            "Assets/GameResources/UIAtlas";

        private const string UITextureFolder =
            "Assets/GameResources/UITexture";

        private void OnPreprocessTexture()
        {
            bool isUIAtlasTexture = IsInFolder(
                assetPath,
                UIAtlasFolder);
            bool isUITexture = IsInFolder(
                assetPath,
                UITextureFolder);

            if (!isUIAtlasTexture && !isUITexture)
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = isUIAtlasTexture
                ? TextureImporterCompression.Compressed
                : TextureImporterCompression.CompressedLQ;
        }

        private static bool IsInFolder(
            string path,
            string folder)
        {
            return path.StartsWith(
                folder + "/",
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
