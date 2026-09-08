using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace LeaderShip.EditorTools
{
    /// <summary>
    /// ฟอนต์ TMP ที่มากับ Unity (Liberation Sans) **ไม่มีสระและพยัญชนะไทย**
    /// ถ้าไม่ทำอะไร ข้อความไทยทั้งเกมจะกลายเป็นสี่เหลี่ยมเปล่า
    ///
    /// เครื่องมือนี้สร้าง TMP Font Asset แบบ Dynamic จากไฟล์ .ttf ที่อยู่ใน Assets/0Game/Art/Fonts
    /// Dynamic แปลว่า atlas จะสร้าง glyph ตอนที่เจอตัวอักษรจริง จึงไม่ต้องระบุชุดตัวอักษรไทยล่วงหน้า
    /// </summary>
    public static class ThaiFontTool
    {
        const string FontFolder = "Assets/0Game/Art/Fonts";

        [MenuItem("LeaderShip/Art/สร้าง TMP Font Asset จากฟอนต์ในโปรเจค", priority = 110)]
        public static void CreateFromProjectFont()
        {
            EnsureFontFolder();

            var fontGuids = AssetDatabase.FindAssets("t:Font", new[] { FontFolder });
            if (fontGuids.Length == 0)
            {
                EditorUtility.DisplayDialog("ยังไม่มีฟอนต์",
                    $"ไม่พบไฟล์ฟอนต์ใน {FontFolder}\n\n" +
                    "ใส่ไฟล์ .ttf ที่มีตัวอักษรไทยลงในโฟลเดอร์นั้นก่อน แล้วสั่งเมนูนี้อีกครั้ง\n" +
                    "ถ้าจะเผยแพร่งานนี้ต่อ ควรเลือกฟอนต์ที่อนุญาตให้แจกจ่ายได้ เช่นฟอนต์ลิขสิทธิ์ SIL OFL",
                    "เข้าใจแล้ว");
                return;
            }

            string fontPath = AssetDatabase.GUIDToAssetPath(fontGuids[0]);
            var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);

            if (font == null)
            {
                Debug.LogError($"[ThaiFontTool] โหลดฟอนต์ที่ {fontPath} ไม่ได้");
                return;
            }

            string outputPath = $"{FontFolder}/{Path.GetFileNameWithoutExtension(fontPath)} SDF.asset";

            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outputPath) != null &&
                !EditorUtility.DisplayDialog("มีอยู่แล้ว",
                    $"มี {outputPath} อยู่แล้ว จะสร้างทับไหม", "สร้างทับ", "ยกเลิก"))
            {
                return;
            }

            var fontAsset = TMP_FontAsset.CreateFontAsset(
                font,
                samplingPointSize: 90,
                atlasPadding: 9,
                renderMode: GlyphRenderMode.SDFAA,
                atlasWidth: 1024,
                atlasHeight: 1024,
                atlasPopulationMode: AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);

            if (fontAsset == null)
            {
                Debug.LogError("[ThaiFontTool] สร้าง TMP Font Asset ไม่สำเร็จ");
                return;
            }

            AssetDatabase.CreateAsset(fontAsset, outputPath);

            // atlas กับ material ต้องถูกฝังเป็น sub-asset ไม่งั้นจะหายเมื่อรีโหลดโปรเจค
            if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0)
            {
                fontAsset.atlasTextures[0].name = fontAsset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            }

            if (fontAsset.material != null)
            {
                fontAsset.material.name = fontAsset.name + " Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = fontAsset;
            EditorGUIUtility.PingObject(fontAsset);

            Debug.Log($"[ThaiFontTool] สร้าง {outputPath} แล้ว — สั่ง 'LeaderShip/สร้างหน้าจอเกม' อีกครั้งเพื่อให้ UI ใช้ฟอนต์นี้");
        }

        /// <summary>
        /// คัดลอกฟอนต์ไทยจาก Windows เข้ามาในโปรเจค
        /// จงใจให้เป็นการกดยืนยันของคนทำเกม ไม่ใช่ทำให้อัตโนมัติ เพราะฟอนต์ระบบของ Windows
        /// ส่วนใหญ่ไม่อนุญาตให้แจกจ่ายต่อ ซึ่งสำคัญถ้างานนี้จะถูกเผยแพร่เป็น portfolio
        /// </summary>
        [MenuItem("LeaderShip/Art/คัดลอกฟอนต์ไทยจากระบบ Windows", priority = 111)]
        public static void CopySystemThaiFont()
        {
            string[] candidates =
            {
                @"C:\Windows\Fonts\LeelaUIb.ttf",
                @"C:\Windows\Fonts\leelawui.ttf",
                @"C:\Windows\Fonts\LeelaUI.ttf",
                @"C:\Windows\Fonts\tahoma.ttf",
                @"C:\Windows\Fonts\upcjl.ttf"
            };

            string found = null;
            foreach (var c in candidates)
            {
                if (File.Exists(c)) { found = c; break; }
            }

            if (found == null)
            {
                EditorUtility.DisplayDialog("ไม่พบฟอนต์ไทยในระบบ",
                    "หาไฟล์ฟอนต์ไทยของ Windows ไม่เจอ ใส่ไฟล์ .ttf เองใน " + FontFolder + " แทน",
                    "ตกลง");
                return;
            }

            bool ok = EditorUtility.DisplayDialog(
                "คัดลอกฟอนต์เข้าโปรเจค",
                $"จะคัดลอก:\n{found}\n\nเข้ามาที่ {FontFolder}\n\n" +
                "หมายเหตุเรื่องสิทธิ์: ฟอนต์ที่มากับ Windows ส่วนใหญ่ใช้งานบนเครื่องได้ " +
                "แต่ไม่อนุญาตให้แจกจ่ายต่อพร้อมกับงาน\n" +
                "ถ้างานนี้จะถูกเผยแพร่ ควรเปลี่ยนไปใช้ฟอนต์ลิขสิทธิ์เปิด เช่น Noto Sans Thai หรือ Sarabun แทน\n\n" +
                "จะคัดลอกไหม",
                "คัดลอก", "ยกเลิก");

            if (!ok) return;

            EnsureFontFolder();

            string destination = $"{FontFolder}/{Path.GetFileName(found)}";
            File.Copy(found, destination, true);
            AssetDatabase.Refresh();

            Debug.Log($"[ThaiFontTool] คัดลอก {Path.GetFileName(found)} แล้ว — ต่อไปสั่งเมนู 'สร้าง TMP Font Asset จากฟอนต์ในโปรเจค'");
        }

        static void EnsureFontFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/0Game/Art"))
                AssetDatabase.CreateFolder("Assets/0Game", "Art");

            if (!AssetDatabase.IsValidFolder(FontFolder))
                AssetDatabase.CreateFolder("Assets/0Game/Art", "Fonts");
        }
    }
}
