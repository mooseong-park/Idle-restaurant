#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using Project.Utils;

namespace Project.EditorTools.Builders
{
    // SF_Square.png / SF_Circle.png / SF_RoundedSquare.png 절차적 생성 + 외부 PNG (Staff_Chef, Customer_*) import 동기화.
    // - createIfMissing=true: 파일 없을 때만 생성 (Sync Missing Only 모드)
    // - createIfMissing=false: 항상 재생성 + import settings 강제 (Full Rebuild)
    public static class SpriteAssetBuilder
    {
        public static void EnsureAll(bool createIfMissing)
        {
            if (!Directory.Exists(BuildPaths.ArtFolder)) Directory.CreateDirectory(BuildPaths.ArtFolder);

            EnsureSpriteAsset(BuildPaths.SquarePath, () => SpriteFactory.MakeSquareTex(), 1f, FilterMode.Point, createIfMissing);
            EnsureSpriteAsset(BuildPaths.CirclePath, () => SpriteFactory.MakeCircleTex(), 64f, FilterMode.Bilinear, createIfMissing);
            EnsureSpriteAsset(BuildPaths.RoundedSquarePath, () => SpriteFactory.MakeRoundedSquareTex(256, 36), 256f, FilterMode.Bilinear, createIfMissing);

            // 사용자 PNG: import boolean 만 동기화 (PPU/filter는 사용자 튜닝 보존)
            ConfigureExternalSprite(BuildPaths.StaffChefPath);
            foreach (var path in Directory.GetFiles(BuildPaths.ArtFolder, "Customer_*.png"))
                ConfigureExternalSprite(path);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // PNG 자체와 PPU/filterMode 안 건드림 — Sprite type, alphaIsTransparency 같은 필수 boolean 만 동기화.
        public static void ConfigureExternalSprite(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[SpriteAssetBuilder] {path} 없음 — 캐릭터 sprite 사용 불가. 파일 복사 후 재실행.");
                return;
            }
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;
            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
            if (dirty) importer.SaveAndReimport();
        }

        static void EnsureSpriteAsset(string path, System.Func<Texture2D> texFactory, float ppu, FilterMode filter, bool createIfMissing)
        {
            if (createIfMissing && File.Exists(path)) return;

            var tex = texFactory();
            var bytes = tex.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = filter;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }
}
#endif
