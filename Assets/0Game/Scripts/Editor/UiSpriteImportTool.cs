using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LeaderShip.EditorTools
{
    /// <summary>
    /// ตั้งค่าอิมพอร์ตให้สไปรต์ UI ทั้งชุดในคลิกเดียว
    ///
    /// อาร์ตที่ได้มาเป็น pixel art 9-slice สีขาวล้วน ซึ่งต้องการ 3 อย่างที่ค่าเริ่มต้นของ Unity ให้ไม่ถูก:
    ///   1. filterMode = Point — ไม่งั้นขอบพิกเซลจะเบลอ
    ///   2. ไม่บีบอัด — สีขาวล้วนที่โดน DXT จะเกิดขอบสกปรกตอน tint
    ///   3. spriteBorder ที่ถูกต้อง — ค่าเริ่มต้นเป็น 0 ซึ่งทำให้ลายมุมยืดเละตอนขยายกรอบ
    ///
    /// ขนาด border ของแต่ละแบบ **ไม่เท่ากัน** (วัดได้ 4 ถึง 24 พิกเซลในชุดเดียวกัน)
    /// จึงคำนวณจากพิกเซลจริงทีละไฟล์ แทนที่จะเดาค่าเดียวใช้ทั้งชุด
    /// </summary>
    public static class UiSpriteImportTool
    {
        const string UiRoot = "Assets/0Game/Art/UI";

        [MenuItem("LeaderShip/Art/ตั้งค่าอิมพอร์ตสไปรต์ UI ทั้งชุด", priority = 100)]
        public static void ApplyAll()
        {
            if (!AssetDatabase.IsValidFolder(UiRoot))
            {
                Debug.LogError($"ไม่พบโฟลเดอร์ {UiRoot}");
                return;
            }

            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { UiRoot });
            int changed = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                    EditorUtility.DisplayProgressBar("ตั้งค่าสไปรต์ UI",
                        Path.GetFileName(path), (float)i / Mathf.Max(1, guids.Length));

                    if (ApplyTo(path)) changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[UiSpriteImportTool] ตั้งค่าเสร็จ {changed}/{guids.Length} ไฟล์ใน {UiRoot}");
        }

        static bool ApplyTo(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return false;

            Vector4 border = ComputeBorder(path);

            bool needsChange =
                importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                importer.filterMode != FilterMode.Point ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.mipmapEnabled ||
                !importer.alphaIsTransparency ||
                importer.spriteBorder != border;

            if (!needsChange) return false;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spriteBorder = border;

            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            return true;
        }

        /// <summary>
        /// หา border ที่เล็กที่สุดที่ยังทำให้ส่วนกลางยืดได้โดยลายไม่เพี้ยน
        ///
        /// คิดแกนนอนกับแกนตั้งแยกกัน เพราะของอย่างเส้นคั่น (192x44) ยืดได้แค่แนวนอน
        /// ถ้าบังคับให้ทั้งสองแกนต้องเจอค่าเดียวกัน เส้นคั่นจะหา border ไม่เจอเลย
        /// </summary>
        static Vector4 ComputeBorder(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);

            try
            {
                if (!tex.LoadImage(bytes)) return Vector4.zero;

                int w = tex.width;
                int h = tex.height;
                var pixels = tex.GetPixels32();

                int horizontal = FindHorizontalBorder(pixels, w, h);
                int vertical = FindVerticalBorder(pixels, w, h);

                return new Vector4(horizontal, vertical, horizontal, vertical);
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        /// <summary>คอลัมน์ตั้งแต่ b ถึง w-1-b ต้องเหมือนกันทุกคอลัมน์ จึงจะยืดแนวนอนได้</summary>
        static int FindHorizontalBorder(Color32[] pixels, int w, int h)
        {
            int limit = (w / 2) - 1;
            for (int b = 1; b <= limit; b++)
            {
                if (ColumnsIdenticalFrom(pixels, w, h, b)) return b;
            }
            return 0;
        }

        static int FindVerticalBorder(Color32[] pixels, int w, int h)
        {
            int limit = (h / 2) - 1;
            for (int b = 1; b <= limit; b++)
            {
                if (RowsIdenticalFrom(pixels, w, h, b)) return b;
            }
            return 0;
        }

        static bool ColumnsIdenticalFrom(Color32[] pixels, int w, int h, int b)
        {
            for (int x = b + 1; x < w - b; x++)
                for (int y = 0; y < h; y++)
                    if (!Same(pixels[y * w + x], pixels[y * w + b])) return false;

            return true;
        }

        static bool RowsIdenticalFrom(Color32[] pixels, int w, int h, int b)
        {
            for (int y = b + 1; y < h - b; y++)
                for (int x = 0; x < w; x++)
                    if (!Same(pixels[y * w + x], pixels[b * w + x])) return false;

            return true;
        }

        static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        [MenuItem("LeaderShip/Art/รายงานขนาด 9-slice ของสไปรต์ที่เลือก", priority = 101)]
        public static void ReportSelection()
        {
            var lines = new List<string>();

            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".png")) continue;
                lines.Add($"{Path.GetFileName(path)} -> border {ComputeBorder(path)}");
            }

            Debug.Log(lines.Count == 0
                ? "ไม่ได้เลือกไฟล์ .png ไว้"
                : string.Join("\n", lines));
        }
    }
}
