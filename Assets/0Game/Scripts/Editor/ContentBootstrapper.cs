using System.Collections.Generic;
using LeaderShip.Data;
using LeaderShip.Model;
using UnityEditor;
using UnityEngine;

namespace LeaderShip.EditorTools
{
    /// <summary>
    /// สร้างไฟล์ ScriptableObject ตั้งต้นทั้งชุดจาก DefaultContent
    ///
    /// หลังจากนี้ **ไฟล์ .asset คือแหล่งความจริง** ไม่ใช่โค้ด (ADR-0001)
    /// การกดซ้ำจะเขียนทับค่าที่แก้ไว้ในไฟล์ จึงถามยืนยันก่อนเสมอ
    /// </summary>
    public static class ContentBootstrapper
    {
        const string DataRoot = "Assets/0Game/Data";

        [MenuItem("LeaderShip/Content/สร้างไฟล์ข้อมูลตั้งต้น", priority = 0)]
        public static void CreateAll()
        {
            bool overwriteConfirmed = !AssetDatabase.IsValidFolder(DataRoot + "/Members") ||
                EditorUtility.DisplayDialog(
                    "สร้างข้อมูลตั้งต้นใหม่",
                    "มีไฟล์ข้อมูลอยู่แล้ว การทำต่อจะเขียนทับค่าที่ปรับไว้ทั้งหมดด้วยค่าตั้งต้นใน DefaultContent\n\nจะทำต่อไหม",
                    "เขียนทับ", "ยกเลิก");

            if (!overwriteConfirmed) return;

            EnsureFolder("Assets/0Game", "Data");
            EnsureFolder(DataRoot, "Members");
            EnsureFolder(DataRoot, "Commands");
            EnsureFolder(DataRoot, "Events");

            var defaults = DefaultContent.Create();

            var balance = CreateOrLoad<BalanceConfigSO>($"{DataRoot}/BalanceConfig.asset");
            balance.EditorSetData(defaults.Balance);
            EditorUtility.SetDirty(balance);

            var members = new List<MemberDefinitionSO>();
            foreach (var def in defaults.Members)
            {
                var so = CreateOrLoad<MemberDefinitionSO>($"{DataRoot}/Members/Member_{def.Id}.asset");
                so.EditorSetData(def);
                EditorUtility.SetDirty(so);
                members.Add(so);
            }

            var commands = new List<CommandDefinitionSO>();
            foreach (var def in defaults.Commands)
            {
                var so = CreateOrLoad<CommandDefinitionSO>($"{DataRoot}/Commands/Command_{def.Type}.asset");
                so.EditorSetData(def);
                EditorUtility.SetDirty(so);
                commands.Add(so);
            }

            var events = new List<EventDefinitionSO>();
            foreach (var def in defaults.Events)
            {
                var so = CreateOrLoad<EventDefinitionSO>($"{DataRoot}/Events/Event_{def.Id}.asset");
                so.EditorSetData(def);
                EditorUtility.SetDirty(so);
                events.Add(so);
            }

            var content = CreateOrLoad<GameContentSO>($"{DataRoot}/GameContent.asset");
            content.EditorAssign(balance, members.ToArray(), commands.ToArray(), events.ToArray());
            EditorUtility.SetDirty(content);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = content;
            EditorGUIUtility.PingObject(content);

            Debug.Log($"[ContentBootstrapper] สร้างข้อมูลตั้งต้นแล้ว: ลูกทีม {members.Count} · คำสั่ง {commands.Count} · เหตุการณ์ {events.Count}\n" +
                      $"ไฟล์รวมอยู่ที่ {DataRoot}/GameContent.asset");
        }

        static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
