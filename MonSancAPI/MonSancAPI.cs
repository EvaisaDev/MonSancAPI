using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Rewired;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace MonSancAPI
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class MonSancAPI : BaseUnityPlugin
    {
        public const string ModGUID = "evaisa.MonSancAPI";
        public const string ModName = "Monster Sanctuary API";
        public const string ModVersion = "0.2.0";

        public static int version_number = 0;
        internal new static ManualLogSource Logger { get; set; }

        public static OptionsMenu optionsMenu;

        public static List<GameObject> monsters;

        public static Dictionary<string, bool> gameplayOptions;

        public static int selectedPage = 0;
        public static int totalPages = 0;

        //public static int itemsPerPage = 8;

        public static MenuListItem nextPageButton;
        public static MenuListItem previousPageButton;

        public static List<MenuOption> menuOptions;
        public static Dictionary<optionType, List<List<MenuOption>>> paginatedMenuOptions;

        public MenuListItem previousItem;

        public static List<InputOption> customInputs = new List<InputOption>();
        public static List<InputOption> inputOptions = new List<InputOption>();
        public static List<List<InputOption>> paginatedInputs = new List<List<InputOption>>();

        public static Dictionary<string, KeyBind> keybinds = new Dictionary<string, KeyBind>();

        public static OptionsMenu currentMenu;

        public static ConfigFile keyBinds = new ConfigFile(Path.Combine(Paths.ConfigPath, "MonSancAPIKeybinds.cfg"), true);

        public static List<LanguageToken> localizations = new List<LanguageToken>();

        public static Dictionary<optionType, int> itemsPerPage = new Dictionary<optionType, int>
        {
            {optionType.gameplay, 9},
            {optionType.audio, 9},
            {optionType.video, 9},
            {optionType.input, 14}
        };

        public enum optionType
        {
            gameplay,
            audio,
            video,
            input
        }

        public class KeyBind
        {
            public int bindIndex = -1;
            public List<ConfigEntry<KeyCode>> bindings = new List<ConfigEntry<KeyCode>>();
            public string name;
            public List<KeyCode> defaultButtons;

            public void SetBinding(KeyCode key)
            {
                if (bindIndex != -1)
                {
                    bindings[bindIndex].Value = key;
                    bindIndex = -1;
                    
                }
            }

            public KeyCode GetBinding(int index)
            {
                return bindings[index].Value;
            }

            public KeyBind(string keyName, List<KeyCode> defaultButtons)
            {
                this.defaultButtons = defaultButtons;
                this.name = keyName;
                bindings.Add(keyBinds.Bind("General", keyName + "1", KeyCode.None, ""));
                bindings.Add(keyBinds.Bind("General", keyName + "2", KeyCode.None, ""));
            }
        }

        public class LanguageToken
        {
            public LocaDataEntry locaData;
            public LanguageToken(string Key, string English, string French = null, string Spanish = null, string SimplifiedChinese = null, string Russian = null, string Italian = null, string German = null, string Japanese = null )
            {
                this.locaData = new LocaDataEntry();
                this.locaData.Key = Key;
                this.locaData.String = English;
                this.locaData.French = French != null ? French : English;
                this.locaData.Spanish = Spanish != null ? Spanish : English;
                this.locaData.SimplifiedChinese = SimplifiedChinese != null ? SimplifiedChinese : English;
                this.locaData.Russian = Russian != null ? Russian : English;
                this.locaData.Italian = Italian != null ? Italian : English;
                this.locaData.German = German != null ? German : English;
                this.locaData.Japanese = Japanese != null ? Japanese : English;

                this.locaData.StringFemale = English;
                this.locaData.FrenchFemale = French != null ? French : English;
                this.locaData.SpanishFemale = Spanish != null ? Spanish : English;
                this.locaData.SimplifiedChineseFemale = SimplifiedChinese != null ? SimplifiedChinese : English;
                this.locaData.RussianFemale = Russian != null ? Russian : English;
                this.locaData.ItalianFemale = Italian != null ? Italian : English;
                this.locaData.GermanFemale = German != null ? German : English;
                this.locaData.JapaneseFemale = Japanese != null ? Japanese : English;
            }
        }



        public class MenuOption
        {
            public optionType optionType;
            public string optionName;
            public Func<OptionsMenu, string> captionFunction;
            public Func<OptionsMenu, string> display;
            public bool isValue;
            public Func<OptionsMenu, bool> disableRule;
            public MenuOption(optionType optionType,  string optionName, Func<OptionsMenu, string> captionFunction, Func<OptionsMenu, string> display, bool isValue, Func<OptionsMenu, bool> disableRule)
            {
                this.optionType = optionType;
                this.optionName = optionName;
                this.captionFunction = captionFunction;
                this.display = display;
                this.isValue = isValue;
                this.disableRule = disableRule;
            }

            public override string ToString()
            {
                return "";//$"\noptionType: {this.optionType}\noptionName: {this.optionName}\ncaption: {this.caption}";
            }
        }

        public class InputOption
        {
            public string optionName;
            public Func<OptionsMenu, string> captionFunction;
            public AxisRange axisRange;
            public EInputType inputType;
            public int column = -1;
            public List<KeyCode> defaultButtons;
            public InputOption(string optionName, Func<OptionsMenu, string> captionFunction, AxisRange axisRange, EInputType inputType, List<KeyCode> defaultButtons)
            {
                this.optionName = optionName;
                this.captionFunction = captionFunction;
                this.axisRange = axisRange;
                this.inputType = inputType;
                this.defaultButtons = defaultButtons;
            }
            public override string ToString()
            {
                return "";//$"\ninputType: {this.inputType}\noptionName: {this.optionName}\ncaption: {this.caption}\naxisRange: {this.axisRange}";
            }
        }


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

            menuOptions = new List<MenuOption>();

            paginatedMenuOptions = new Dictionary<optionType, List<List<MenuOption>>>();


            AddDefaultOptions();

            RegisterLanguageToken(new LanguageToken("Previous", "Previous", "Précédente", "Anterior", "以前", "Предыдущий", "Precedente", "Vorig", "前"));

            new ILHook(typeof(StackTrace).GetMethod("AddFrames", BindingFlags.Instance | BindingFlags.NonPublic), IlHook);

            GameController.GameVersionBuild = version_number;

            Logger.LogInfo("Game build version updated to "+version_number);



            On.OptionsMenu.Start += OptionsMenu_Start;

            On.OptionsMenu.ShowGameplayOptions += OptionsMenu_ShowGameplayOptions;
            On.OptionsMenu.ShowAudioOptions += OptionsMenu_ShowAudioOptions;
            On.OptionsMenu.ShowVideoOptions += OptionsMenu_ShowVideoOptions;

            On.OptionsMenu.OnFooterSelected += OptionsMenu_OnFooterSelected;

            On.OptionsMenu.OnCategoryHovered += OptionsMenu_OnCategoryHovered;
            /*
            

            On.OptionsMenu.OnCategorySelected += OptionsMenu_OnCategorySelected;
            */

            On.OptionsMenu.ShowInputOptions += OptionsMenu_ShowInputOptions;

            On.OptionsMenu.OnOptionsSelected += OptionsMenu_OnOptionsSelected;

            On.OptionsMenu.AddInputLine += OptionsMenu_AddInputLine;

            On.OptionsMenu.OnConflictFound += OptionsMenu_OnConflictFound;

            On.GameController.Awake += GameController_Awake; ;
        }

        private void GameController_Awake(On.GameController.orig_Awake orig, GameController self)
        {
            localizations.ForEach(token =>
            {
                if (self.WorldData.LocaData.Any(item => item.Key == token.locaData.Key))
                {
                    self.WorldData.LocaData.RemoveAll(item => item.Key == token.locaData.Key);
                    self.WorldData.LocaData.Add(token.locaData);
                }
                else
                {

                    self.WorldData.LocaData.Add(token.locaData);
                }
            });
            orig(self);



        }



        private void Update()
        {
            foreach (KeyCode vKey in System.Enum.GetValues(typeof(KeyCode)))
            {
                if (Input.GetKey(vKey))
                {
                    
                    List<string> keys = new List<string>(keybinds.Keys);
                    foreach (string key in keys)
                    {
                        
                        var keybind = keybinds[key];

                        //Debug.Log(keybind.bindIndex);

                        if (keybind.bindIndex != -1)
                        {
                            //Debug.Log("rawr");
                            keybind.SetBinding(vKey);
                            keyBinds.Save();
                            Debug.Log(keybind.name + " was bound to key: " + vKey.ToString());
                            currentMenu.BaseOptions.SetLocked(false);
                            currentMenu.RefreshPage();
                        }
                    }
                }
            }
        }

        private void OptionsMenu_OnConflictFound(On.OptionsMenu.orig_OnConflictFound orig, OptionsMenu self, InputMapper.ConflictFoundEventData data)
        {
            data.responseCallback(InputMapper.ConflictResponse.Add);
        }

        private void OptionsMenu_ShowInputOptions(On.OptionsMenu.orig_ShowInputOptions orig, OptionsMenu self)
        {
            OrganizeInputs();
            self.ClearOptions();
            customInputs.Clear();

            HandlePageButtons();

            self.Captions.transform.localPosition = self.CaptionsOriginalPos + Vector3.right * 60f;


            //Debug.Log("Rawr0");
            paginatedInputs[selectedPage].ForEach(item =>
            {
                //Debug.Log("Rawr1");
                self.AddInputLine(item.optionName, item.captionFunction(self), item.inputType, item.column, item.axisRange);
                //Debug.Log("Rawr2");
            });

            self.Captions.UpdateItemPositions(false);
            self.BaseOptions.UpdateItemPositions(false);



            // Debug.Log("Rawr3");
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
    
                    foreach (MenuListItem menuListItem in self.BaseOptions.Lists[i * 2 + j])
                    {
                        //Debug.Log("Rawr5");
                        Vector3 localPosition = menuListItem.transform.localPosition;
                        localPosition.x += (float)(42 * ((i == 0) ? -1 : 1));
                        menuListItem.transform.localPosition = localPosition;
                    }
                }
            }
        }

        public static void OrganizeInputs()
        {
            paginatedInputs.Clear();
            totalPages = 0;
            var index = 0;

            for (var i = 0; i < inputOptions.Count; i++)
            {
                if (inputOptions[i].column == -1)
                {
                    if (index > (itemsPerPage[optionType.input] - 1) / 2)
                    {
                        inputOptions[i].column = 1;
                    }
                    else
                    {
                        inputOptions[i].column = 0;
                    }
                }

                if (index > itemsPerPage[optionType.input] - 1)
                {
                    index = 0;
                    if (menuOptions.ElementAtOrDefault(i + 1) != null)
                    {
                        totalPages++;
                    }
                }

                if (index <= itemsPerPage[optionType.input] - 1)
                {
                    if (paginatedInputs.ElementAtOrDefault(totalPages) == null)
                    {
                        paginatedInputs.Add(new List<InputOption>());
                        paginatedInputs[totalPages].Add(inputOptions[i]);
                    }
                    else
                    {
                        paginatedInputs[totalPages].Add(inputOptions[i]);
                    }
                    index++;
                }


            }
        }

        private void OptionsMenu_AddInputLine(On.OptionsMenu.orig_AddInputLine orig, OptionsMenu self, string optionName, string caption, EInputType input, int column, AxisRange axisRange)
        {

            self.optionNames.Add(optionName);
            var captionsMenuItem = self.Captions.AddTextItem(caption, column);
            captionsMenuItem.SetDisabled(false);
            //self.inputs.Add(input);

            customInputs.Add(new InputOption(optionName, delegate (OptionsMenu self2) { return caption; }, axisRange, input, new List<KeyCode>() { KeyCode.None, KeyCode.None }));
            self.inputs.Add(input);

            if ((int)input == -1)
            {
                if (!keybinds.ContainsKey(optionName))
                {
                    paginatedInputs[selectedPage].ForEach(item =>
                    {
                        if (item.optionName == optionName)
                        {
                            if (item.defaultButtons.Count == 0)
                            {
                                item.defaultButtons = new List<KeyCode>() { KeyCode.None, KeyCode.None };
                            }
                            else if (item.defaultButtons.Count == 1)
                            {
                                item.defaultButtons.Add(KeyCode.None);
                            }


                            keybinds.Add(optionName, new KeyBind(optionName, item.defaultButtons));
                            if (keybinds[optionName].GetBinding(0) == KeyCode.None)
                            {
                                keybinds[optionName].bindIndex = 0;
                                keybinds[optionName].SetBinding(item.defaultButtons[0]);
                            }
                            if (keybinds[optionName].GetBinding(1) == KeyCode.None)
                            {
                                keybinds[optionName].bindIndex = 1;
                                keybinds[optionName].SetBinding(item.defaultButtons[1]);
                            }
                        }
                    });
                    keyBinds.Save();
                }
            }

            int num = 0;
            if ((int)input != -1)
            {
                foreach (ActionElementMap actionElementMap in self.ControllerMap.ElementMapsWithAction(InputController.GetActionMapping(input)))
                {
                    if (actionElementMap.ShowInField(axisRange))
                    {
                        if (num > 1)
                        {
                            break;
                        }
                        string text = actionElementMap.elementIdentifierName;
                        if (text.Length > 12)
                        {
                            text = text.Substring(0, 11) + ".";
                        }
                        self.mapIDs.Add(actionElementMap.id);
                       
                        self.BaseOptions.AddTextItem(text, column * 2 + num).SetDisabled(false);
                        num++;
                    }
                }
            }
            for (int i = num; i < 2; i++)
            {
                
                if ((int)input != -1) self.mapIDs.Add(-1);
                var list_index = column * 2 + i;
                var row = list_index == 0 || list_index == 2 ? 1 : 2;
                //Debug.Log("Row: " + row);

                

                if ((int)input == -1)
                {
                    self.BaseOptions.AddTextItem(keybinds[optionName].GetBinding(row - 1).ToString(), list_index).SetDisabled(false);
                }
                else
                {
                    self.BaseOptions.AddTextItem("None", list_index).SetDisabled(false);
                }

                


            }
            if ((int)input != -1) self.axisRanges.Add(axisRange);
            
        }

        private void OptionsMenu_OnOptionsSelected(On.OptionsMenu.orig_OnOptionsSelected orig, OptionsMenu self, MenuListItem menuItem)
        {
            if (self.GetCurrentCategory() == OptionsMenu.ECategory.Input)
            {
                string text = self.optionNames[self.GetCurrentOptionIndex()];

                int num = self.BaseOptions.CurrentIndex * 4 + self.BaseOptions.CurrentListIndex;

                //Debug.Log("Index: " + self.BaseOptions.CurrentIndex);
                //Debug.Log("List Index: " + self.BaseOptions.CurrentListIndex);


                
                

                /*
                int index = 0;

                customInputs.ForEach(input =>
                {
                    Debug.Log($"[{index}]"+input.ToString());
                    index++;
                });

                */

                

                //Debug.Log("Input: "+inputStuff.inputType);
                //Debug.Log("Input index: " + num / 4);
                if ((int)self.inputs[num / 2] != -1)
                {
                    ActionElementMap actionElementMapToReplace = (self.mapIDs[num] >= 0) ? self.ControllerMap.GetElementMap(self.mapIDs[num]) : null;

                    //Debug.Log("Selected Keybind: "+ customInputs[num / 2].captionFunction(self));
                    
                    self.inputMapper.Start(new InputMapper.Context
                    {
                        actionName = InputController.GetActionMapping(self.inputs[num / 2]),
                        controllerMap = self.ControllerMap,
                        actionRange = self.axisRanges[num / 2],
                        actionElementMapToReplace = actionElementMapToReplace
                    });

                    /*
                    self.InputPopup.SetActive(true);
                    UIController.Instance.ShadeLayer.Show(self.InputPopup);
                    
                    */

                    self.BaseOptions.SetLocked(true);

                    
     
                    //Debug.Log(inputStuff.inputType.ToString());
                }
                else
                {
                    var fixedNum = self.BaseOptions.CurrentListIndex <= 1 ? (num / 4) : (num / 4) + 7;
                    var row = self.BaseOptions.CurrentListIndex == 0 || self.BaseOptions.CurrentListIndex == 2 ? 1 : 2;
                    var currentInput = customInputs[fixedNum];

                    //Debug.Log("Selected Keybind: " + currentInput.captionFunction(self));
                    //Debug.Log("Selected Row: " + row);


                    /*
                    self.inputMapper.Start(new InputMapper.Context
                    {
                        actionName = inputStuff.optionName,
                        controllerMap = self.ControllerMap,
                        actionRange = self.axisRanges[num / 2],
                        actionElementMapToReplace = actionElementMapToReplace
                    });
                    */
                    //Debug.Log("Waiting for input..");

                    currentMenu = self;

                    keybinds[currentInput.optionName].bindIndex = row - 1;

                    //Debug.Log(keybinds[currentInput.optionName].bindIndex);

                    self.BaseOptions.SetLocked(true);


                }

                self.RefreshPage();
            }
            else
            {
                orig(self, menuItem);
            }
        }

        private void OptionsMenu_OnCategoryHovered(On.OptionsMenu.orig_OnCategoryHovered orig, OptionsMenu self, MenuListItem menuItem)
        {
            if (menuItem == self.GameplayCategory || menuItem == self.InputCategory || menuItem == self.AudioCategory || menuItem == self.VideoCategory)
            {

                if (menuItem != previousItem)
                {
                    selectedPage = 0;
                }
                previousItem = menuItem;
            }

            orig(self, menuItem);
        }

        private void OptionsMenu_OnCategorySelected(On.OptionsMenu.orig_OnCategorySelected orig, OptionsMenu self, MenuListItem menuItem)
        {
            if (menuItem == self.GameplayCategory || menuItem == self.InputCategory || menuItem == self.AudioCategory || menuItem == self.VideoCategory)
            {

                if (menuItem != previousItem)
                {
                    selectedPage = 0;
                }
                previousItem = menuItem;
            }


            orig(self, menuItem);
        }

        private void OptionsMenu_ShowVideoOptions(On.OptionsMenu.orig_ShowVideoOptions orig, OptionsMenu self)
        {
            OrganizeOptions(optionType.video);

            self.ClearOptions();

            HandlePageButtons();

            if (paginatedMenuOptions.ContainsKey(optionType.video))
            {
                paginatedMenuOptions[optionType.video][selectedPage].ForEach(item =>
                {
                    self.AddOption(item.optionName, item.captionFunction(self), item.display(self), item.isValue).SetDisabled(item.disableRule(self));
                });
            }
            self.Captions.UpdateItemPositions(false);
            self.BaseOptions.UpdateItemPositions(false);
        }

        private void OptionsMenu_ShowAudioOptions(On.OptionsMenu.orig_ShowAudioOptions orig, OptionsMenu self)
        {

            OrganizeOptions(optionType.audio);

            self.ClearOptions();

            HandlePageButtons();

            if (paginatedMenuOptions.ContainsKey(optionType.audio))
            {
                paginatedMenuOptions[optionType.audio][selectedPage].ForEach(item =>
                {
                    self.AddOption(item.optionName, item.captionFunction(self), item.display(self), item.isValue).SetDisabled(item.disableRule(self));
                });
            }
            self.Captions.UpdateItemPositions(false);
            self.BaseOptions.UpdateItemPositions(false);
        }

        private void OptionsMenu_OnFooterSelected(On.OptionsMenu.orig_OnFooterSelected orig, OptionsMenu self, MenuListItem menuItem)
        {
            //Debug.Log("Item selected.");


            if (menuItem == nextPageButton)
            {
                if(selectedPage + 1 > totalPages)
                {
                    selectedPage = 0;
                }
                else
                {
                    selectedPage++;
                }
                self.RefreshPage();
            }
            if (menuItem == previousPageButton)
            {
                if (selectedPage - 1 < 0)
                {
                    selectedPage = totalPages;
                }
                else
                {
                    selectedPage--;
                }
                self.RefreshPage();
            }

            if (menuItem == self.DefaultsButton)
            {
                OptionsManager instance = OptionsManager.Instance;
                OptionsMenu.ECategory currentCategory = self.GetCurrentCategory();

                if(currentCategory == OptionsMenu.ECategory.Input)
                {
                    List<string> keys = new List<string>(keybinds.Keys);
                    foreach (string key in keys)
                    {

                        var keybind = keybinds[key];

                        //Debug.Log(keybind.bindIndex);

                        keybind.bindIndex = 0;
                        keybind.SetBinding(keybind.defaultButtons[0]);
                        keybind.bindIndex = 1;
                        keybind.SetBinding(keybind.defaultButtons[1]);

                    }
                    keyBinds.Save();
                    self.RefreshPage();
                }
            }



            //Debug.Log("Current page: " + selectedPage);
            orig(self, menuItem);
        }

        private void OptionsMenu_Start(On.OptionsMenu.orig_Start orig, OptionsMenu self)
        {
            orig(self);

            self.BackButton.transform.position += new Vector3(39, 0, 0);
            self.BackButton.text.text = "Close";
            self.UndoButton.transform.position += new Vector3(39, 0, 0);
            self.DefaultsButton.transform.position += new Vector3(39, 0, 0);

            var root = self.BackButton.gameObject.transform.parent;
            var nextPageButtonObject = Object.Instantiate<GameObject>(self.BackButton.gameObject, self.DefaultsButton.transform.position + new Vector3(78, 0, 0), Quaternion.identity);
            nextPageButtonObject.name = "NextPage";
            nextPageButtonObject.transform.parent = root;
            nextPageButton = nextPageButtonObject.GetComponent<MenuListItem>();
            nextPageButton.text.text = "Next";
            self.FooterMenu.AddMenuItem(nextPageButton, -1);
            //nextPageButton.
 

            var previousPageButtonObject = Object.Instantiate<GameObject>(self.BackButton.gameObject, self.BackButton.transform.position + new Vector3(-78, 0, 0), Quaternion.identity);
            previousPageButtonObject.name = "PreviousPage";
            previousPageButtonObject.transform.parent = root;
            previousPageButton = previousPageButtonObject.GetComponent<MenuListItem>();
            previousPageButton.text.text = "Previous";
            self.FooterMenu.AddMenuItem(previousPageButton, -1);


           // self.CaptionsOriginalPos = self.Captions.transform.localPosition;
        }



        private void AddDefaultOptions()
        {
            RegisterOption(optionType.gameplay, "Language", delegate (OptionsMenu self) { return Utils.LOCA("Language", ELoca.UI); }, delegate(OptionsMenu self) { return Utils.LocalizeString(OptionsManager.Instance.GetLanguage().ToString(), true); }, false, delegate (OptionsMenu self) { return false; });
            
            RegisterOption(optionType.gameplay, "Difficulty", delegate (OptionsMenu self) { return Utils.LOCA("Difficulty", ELoca.UI); }, delegate (OptionsMenu self) {
                if (self.ingameMenu)
                {
                    return GameDefines.GetDifficultyName(PlayerController.Instance.Difficulty);
                }
                else
                {
                    return Utils.LOCA("Ingame", ELoca.UI);
                }
            }, false, delegate(OptionsMenu self) {
                return !self.ingameMenu;
            });

            RegisterOption(optionType.gameplay, "Combat Speed", delegate (OptionsMenu self) { return Utils.LOCA("Combat Speed", ELoca.UI); }, delegate (OptionsMenu self) { return "x " + OptionsManager.Instance.GetCombatSpeedMultiplicator(); }, true, delegate (OptionsMenu self) { return false; });
            RegisterOption(optionType.gameplay, "Axis Threshold", delegate (OptionsMenu self) { return Utils.LOCA("Gamepad Axis Threshold", ELoca.UI); }, delegate (OptionsMenu self) { return OptionsManager.Instance.OptionsData.AxisTreshold * 100f + "%"; }, true, delegate (OptionsMenu self) { return false; });
            RegisterOption(optionType.gameplay, "Screen Shake", delegate (OptionsMenu self) { return Utils.LOCA("Screen Shake", ELoca.UI); }, delegate (OptionsMenu self) { return self.GetBoolString(!OptionsManager.Instance.OptionsData.DisableScreenShake); }, true, delegate (OptionsMenu self) { return false; });
            RegisterOption(optionType.gameplay, "Preview Dodge Chance", delegate (OptionsMenu self) { return Utils.LOCA("Preview Dodge Chance", ELoca.UI); }, delegate (OptionsMenu self) { return self.GetBoolString(OptionsManager.Instance.OptionsData.ShowDodgeChance); }, true, delegate (OptionsMenu self) { return false; });
            RegisterOption(optionType.gameplay, "Controller Icons", delegate (OptionsMenu self) { return Utils.LOCA("Controller Icons", ELoca.UI); }, delegate (OptionsMenu self) { return Utils.LocalizeString(OptionsManager.Instance.GetControllerGlyphPreference().ToString(), true); }, false, delegate (OptionsMenu self) { return false; });
            RegisterOption(optionType.gameplay, "Timer", delegate (OptionsMenu self) { return Utils.LOCA("Timer", ELoca.UI); }, delegate (OptionsMenu self) {
                if (self.ingameMenu)
                {
                    return self.GetBoolString(PlayerController.Instance.TimerEnabled);
                }
                else
                {
                    return Utils.LOCA("Ingame", ELoca.UI);
                }
            }, true, delegate (OptionsMenu self) {
                return !self.ingameMenu;
            });

            RegisterOption(optionType.gameplay, "NGOptions", delegate (OptionsMenu self) { return Utils.LOCA("Advanced Game Modes", ELoca.UI); }, delegate (OptionsMenu self) {
                return self.GetBoolString(OptionsManager.Instance.OptionsData.AlternateGameModes);
            }, true, delegate (OptionsMenu self) {
                return self.ingameMenu;
            });

            RegisterOption(optionType.audio, "Music Volume", delegate (OptionsMenu self) { return Utils.LOCA("Music Volume", ELoca.UI); }, delegate (OptionsMenu self) { return OptionsManager.Instance.OptionsData.VolumeBGM * 100f + "%"; }, true, delegate (OptionsMenu self) { return false; });
            RegisterOption(optionType.audio, "SFX Volume", delegate (OptionsMenu self) { return Utils.LOCA("SFX Volume", ELoca.UI); }, delegate (OptionsMenu self) { return OptionsManager.Instance.OptionsData.VolumeSFX * 100f + "%"; }, true, delegate (OptionsMenu self) { return false; });
            RegisterOption(optionType.audio, "Battle Theme", delegate (OptionsMenu self) { return Utils.LOCA("Alternative Battle Theme", ELoca.UI); }, delegate (OptionsMenu self) { return self.GetBoolString(OptionsManager.Instance.OptionsData.AlternativeBattleTheme); }, true, delegate (OptionsMenu self) { return false; });
            RegisterOption(optionType.audio, "Snowy Theme", delegate (OptionsMenu self) { return Utils.LOCA("Alt. 'Snowy Peaks' Theme", ELoca.UI); }, delegate (OptionsMenu self) { return self.GetBoolString(OptionsManager.Instance.OptionsData.AlternativeSnowyPeaksTheme); }, true, delegate (OptionsMenu self) { return false; });
            RegisterOption(optionType.audio, "Stronghold Theme", delegate (OptionsMenu self) { return Utils.LOCA("Alt. 'Keeper's Stronghold' Theme", ELoca.UI); }, delegate (OptionsMenu self) { return self.GetBoolString(OptionsManager.Instance.OptionsData.AlternativeStrongholdTheme); }, true, delegate (OptionsMenu self) { return false; });


            RegisterOption(optionType.video, "Resolution", delegate (OptionsMenu self) { return Utils.LOCA("Resolution", ELoca.UI); }, delegate (OptionsMenu self) { return Screen.width + " x " + Screen.height; }, false, delegate (OptionsMenu self) { return false; });
            RegisterOption(optionType.video, "Screen Mode", delegate (OptionsMenu self) { return Utils.LOCA("Screen Mode", ELoca.UI); }, delegate (OptionsMenu self) { return self.GetScreenModeString(OptionsManager.Instance.GetCurrentScreenMode()); }, false, delegate (OptionsMenu self) { return false; });


            /*
            for(var i = 0; i < 23; i++)
            {
                RegisterOption(optionType.gameplay, "Gameplay Option "+i, "Gameplay Option " + i, delegate (OptionsMenu self) { return "Enabled"; }, false, delegate (OptionsMenu self) { return false; });
            }

            for (var i = 0; i < 23; i++)
            {
                RegisterOption(optionType.audio, "Audio Option " + i, "Audio Option " + i, delegate (OptionsMenu self) { return "Enabled"; }, false, delegate (OptionsMenu self) { return false; });
            }

            for (var i = 0; i < 23; i++)
            {
                RegisterOption(optionType.video, "Video Option " + i, "Video Option " + i, delegate (OptionsMenu self) { return "Enabled"; }, false, delegate (OptionsMenu self) { return false; });
            }
            */


            var item = new InputOption("Confirm", delegate(OptionsMenu self) { return Utils.LOCA("Confirm", ELoca.UI); }, AxisRange.Full, EInputType.Confirm, new List<KeyCode>() { KeyCode.None, KeyCode.None });
            item.column = 0;
            inputOptions.Add(item);

            item = new InputOption("Cancel", delegate(OptionsMenu self) { return Utils.LOCA("Cancel", ELoca.UI); }, AxisRange.Full, EInputType.Cancel, new List<KeyCode>() { KeyCode.None, KeyCode.None });
            item.column = 1;
            inputOptions.Add(item);

            item = new InputOption("Jump", delegate(OptionsMenu self) { return Utils.LOCA("Jump", ELoca.UI); }, AxisRange.Full, EInputType.Jump, new List<KeyCode>() { KeyCode.None, KeyCode.None });
            item.column = 0;
            inputOptions.Add(item);

            item = new InputOption("Menu", delegate(OptionsMenu self) { return Utils.LOCA("Menu", ELoca.UI); }, AxisRange.Full, EInputType.Menu, new List<KeyCode>() { KeyCode.None, KeyCode.None });
            item.column = 1;
            inputOptions.Add(item);

            item = new InputOption("Up", delegate(OptionsMenu self) { return Utils.LOCA("Up", ELoca.UI); }, AxisRange.Positive, EInputType.Up, new List<KeyCode>() { KeyCode.None, KeyCode.None });
            item.column = 0;
            inputOptions.Add(item);

            item = new InputOption("Down", delegate(OptionsMenu self) { return Utils.LOCA("Down", ELoca.UI); }, AxisRange.Negative, EInputType.Down, new List<KeyCode>() { KeyCode.None, KeyCode.None });
            item.column = 1;
            inputOptions.Add(item);

            item = new InputOption("Left", delegate(OptionsMenu self) { return Utils.LOCA("Left", ELoca.UI); }, AxisRange.Negative, EInputType.Left, new List<KeyCode>() { KeyCode.None, KeyCode.None });
            item.column = 0;
            inputOptions.Add(item);

            item = new InputOption("Right", delegate(OptionsMenu self) { return Utils.LOCA("Right", ELoca.UI); }, AxisRange.Positive, EInputType.Right, new List<KeyCode>() { KeyCode.None, KeyCode.None });
            item.column = 1;
            inputOptions.Add(item);

            item = new InputOption("Left Monster", delegate(OptionsMenu self) { return Utils.LOCA("Left Monster", ELoca.UI); }, AxisRange.Full, EInputType.LeftMonster, new List<KeyCode>() { KeyCode.None, KeyCode.None });
            item.column = 0;
            inputOptions.Add(item);

            item = new InputOption("Right Monster", delegate(OptionsMenu self) { return Utils.LOCA("Right Monster", ELoca.UI); }, AxisRange.Full, EInputType.RightMonster, new List<KeyCode>() { KeyCode.None, KeyCode.None });
            item.column = 1;
            inputOptions.Add(item);

            item = new InputOption("Interact", delegate(OptionsMenu self) { return Utils.LOCA("Interact", ELoca.UI); }, AxisRange.Full, EInputType.Interact, new List<KeyCode>() { KeyCode.None, KeyCode.None });
            item.column = 0;
            inputOptions.Add(item);

            item = new InputOption("Buff Info", delegate(OptionsMenu self) { return Utils.LOCA("Buff Info", ELoca.UI); }, AxisRange.Full, EInputType.BuffInfo, new List<KeyCode>() { KeyCode.None, KeyCode.None });
            item.column = 1;
            inputOptions.Add(item);

            item = new InputOption("Map", delegate(OptionsMenu self) { return Utils.LOCA("Map", ELoca.UI); }, AxisRange.Full, EInputType.Map, new List<KeyCode>() { KeyCode.None, KeyCode.None });
            item.column = 0;
            inputOptions.Add(item);

            item = new InputOption("Keyboard", delegate(OptionsMenu self) { return Utils.LOCA("Text Input", ELoca.UI); }, AxisRange.Full, EInputType.ToggleDirectKeyboardInput, new List<KeyCode>() {KeyCode.None, KeyCode.None});
            item.column = 1;
            inputOptions.Add(item);

            /*
            for (var i = 0; i < 23; i++)
            {
               RegisterInput("Input Option " + i, "Input Option " + i, new List<KeyCode>() { KeyCode.A, KeyCode.None }); 
            }
            */

        }



        public static void RegisterOption(optionType optionType, string optionName, Func<OptionsMenu, string> captionFunction, Func<OptionsMenu, string > displayFunction, bool isValue, Func<OptionsMenu, bool> disableRule)
        {
            var option = new MenuOption(optionType, optionName, captionFunction, displayFunction, isValue, disableRule);

            menuOptions.Add(option);
        }

        public static void RegisterInput(string optionName, Func<OptionsMenu, string> captionFunction, List<KeyCode> defaultButtons)
        {
            var option = new InputOption(optionName, captionFunction, AxisRange.Full, (EInputType)(-1), defaultButtons);


            inputOptions.Add(option);
        }

        public static void RegisterLanguageToken(LanguageToken token)
        {
            localizations.Add(token);

        }

        private void HandlePageButtons()
        {
            if (totalPages < 1)
            {
                nextPageButton.SetDisabled(true);
                previousPageButton.SetDisabled(true);
            }
            else
            {
                nextPageButton.SetDisabled(false);
                previousPageButton.SetDisabled(false);
            }
        }

        private void OptionsMenu_ShowGameplayOptions(On.OptionsMenu.orig_ShowGameplayOptions orig, OptionsMenu self)
        {
            
            OrganizeOptions(optionType.gameplay);

            self.ClearOptions();

            HandlePageButtons();

            if (paginatedMenuOptions.ContainsKey(optionType.gameplay))
            {
                paginatedMenuOptions[optionType.gameplay][selectedPage].ForEach(item =>
                {
                    self.AddOption(item.optionName, item.captionFunction(self), item.display(self), item.isValue).SetDisabled(item.disableRule(self));
                });
            }
            self.Captions.UpdateItemPositions(false);
            self.BaseOptions.UpdateItemPositions(false);
        }

        public static void OrganizeOptions(optionType type)
        {
            if (!paginatedMenuOptions.ContainsKey(type))
            {
                paginatedMenuOptions.Add(type, new List<List<MenuOption>>());
            }

            paginatedMenuOptions[type].Clear();
            totalPages = 0;
            var index = 0;

            for (var i = 0; i < menuOptions.Count; i++)
            {
               
                if (menuOptions[i].optionType == type)
                {
                    if( index > itemsPerPage[type] - 1)
                    {
                        index = 0;
                        if (menuOptions.ElementAtOrDefault(i + 1) != null)
                        {
                            totalPages++;
                        }
                    }
                    if (index <= itemsPerPage[type] - 1)
                    {
                        // Debug.Log("rawr");
                        //Debug.Log(menuOptions[i].optionName);
                        if (paginatedMenuOptions[type].ElementAtOrDefault(totalPages) == null)
                        {
                            // Debug.Log("rawr2");
                            paginatedMenuOptions[type].Add(new List<MenuOption>());
                            // Debug.Log("rawr3");
                            paginatedMenuOptions[type][totalPages].Add(menuOptions[i]);
                        }
                        else
                        {
                            // Debug.Log("rawr4");
                            paginatedMenuOptions[type][totalPages].Add(menuOptions[i]);
                        }
                        index++;
                    }
                }
            }
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

        /*
        public static MenuListItem RegisterConfigCategory(string menuName)
        {
            var newOptionsMenu = DuplicateComponent<MenuListItem>(optionsMenu.GameplayCategory);

            newOptionsMenu.text.text = menuName;

            optionsMenu.CategoryMenu.AddMenuItem(newOptionsMenu);

            return newOptionsMenu;
        }
        */
    }
}
