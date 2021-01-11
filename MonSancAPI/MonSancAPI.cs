using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace MonSancAPI
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class MonSancAPI : BaseUnityPlugin
    {
        public const string ModGUID = "evaisa.MonSancAPI";
        public const string ModName = "Monster Sanctuary API";
        public const string ModVersion = "0.1.0";

        public static int version_number = 0;
        internal new static ManualLogSource Logger { get; set; }

        public static OptionsMenu optionsMenu;

        public static List<GameObject> monsters;

        public static Dictionary<string, bool> gameplayOptions;

        public MonSancAPI()
        {
            Logger = base.Logger;

            foreach (KeyValuePair<string, PluginInfo> pluginInfoPair in BepInEx.Bootstrap.Chainloader.PluginInfos) {
                var pluginInfo = pluginInfoPair.Value;
                try
                {
                    version_number += (pluginInfo.Metadata.GUID + pluginInfo.Metadata.Version).GetHashCode();
                }
                catch (Exception e)
                {
                    Logger.LogError($"Exception while scanning plugin {pluginInfo.Metadata.GUID}");
                    Logger.LogError("MonSancAPI Failed to properly scan the assembly." + Environment.NewLine +
                                          "Please make sure you are compiling against .NET framework 4.6" +
                                          "and not anything else when making a plugin for Monster Sanctuary!" +
                                          Environment.NewLine + e);
                }
            };

            new ILHook(typeof(StackTrace).GetMethod("AddFrames", BindingFlags.Instance | BindingFlags.NonPublic), IlHook);

            GameController.GameVersionBuild = version_number;

            Logger.LogInfo("Game build version updated to "+version_number);

        }



        public static List<Monster> SpawnMonsterEncounter(Vector2 Position, List<GameObject> Monsters, bool IsChampion)
        {


            var encounterObject = Instantiate(new GameObject());

            var boxCollider = encounterObject.AddComponent<BoxCollider2D>();

            boxCollider.size = new Vector2(16, 200);
            boxCollider.isTrigger = true;

            encounterObject.transform.position = Position;

            var mimicEncounter = encounterObject.AddComponent<MonsterEncounter>();

            if (IsChampion)
            {
                mimicEncounter.EncounterType = EEncounterType.Champion;
            }
            else
            {
                mimicEncounter.EncounterType = EEncounterType.Normal;
            }

            mimicEncounter.PredefinedMonsters = new MonsterEncounter.EncounterConfig();

            mimicEncounter.PredefinedMonsters.Monster = new GameObject[Monsters.Count];

            mimicEncounter.PredefinedMonsters.level = 1;

            mimicEncounter.PredefinedMonsters.weight = 1;

            mimicEncounter.PredefinedMonsters.Monster = Monsters.ToArray();

            mimicEncounter.EncounterType = EEncounterType.Normal;

            mimicEncounter.VariableLevel = true;

            mimicEncounter.AutoStart = true;

            mimicEncounter.CanRetreat = true;

            mimicEncounter.MonsterBoundsRange = 130;

            mimicEncounter.ContrahentsDistance = 160;

            mimicEncounter.SetupEnemies(Position, false);

            var monsters = mimicEncounter.DeterminedEnemies;

            return monsters;
        }

        private void IlHook(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(
                x => x.MatchCallvirt(typeof(StackFrame).GetMethod("GetFileLineNumber", BindingFlags.Instance | BindingFlags.Public))
            );

            cursor.RemoveRange(2);
            cursor.EmitDelegate<Func<StackFrame, string>>(GetLineOrIL);
        }

        private static string GetLineOrIL(StackFrame instace)
        {
            var line = instace.GetFileLineNumber();
            if (line == StackFrame.OFFSET_UNKNOWN || line == 0)
            {
                return "IL_" + instace.GetILOffset().ToString("X4");
            }

            return line.ToString();
        }

        public static T CopyComponent<T>(T original, GameObject destination) where T : Component
        {
            System.Type type = original.GetType();
            Component copy = destination.AddComponent(type);
            System.Reflection.FieldInfo[] fields = type.GetFields();
            foreach (System.Reflection.FieldInfo field in fields)
            {
                field.SetValue(copy, field.GetValue(original));
            }
            return copy as T;
        }

        public static T DuplicateComponent<T>(T original) where T : Component
        {
            System.Type type = original.GetType();
            Component copy = (T)Activator.CreateInstance(type);
            System.Reflection.FieldInfo[] fields = type.GetFields();
            foreach (System.Reflection.FieldInfo field in fields)
            {
                field.SetValue(copy, field.GetValue(original));
            }
            return copy as T;
        }

        public static MenuListItem RegisterConfigCategory(string menuName)
        {
            var newOptionsMenu = DuplicateComponent<MenuListItem>(optionsMenu.GameplayCategory);

            newOptionsMenu.text.text = menuName;

            optionsMenu.CategoryMenu.AddMenuItem(newOptionsMenu);

            return newOptionsMenu;
        }
    }
}
