using Modding;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UObject = UnityEngine.Object;
using Satchel.BetterMenus;
using static Satchel.IoUtils;

namespace CustomPlayerModel {
    public class CustomPlayerModel: Mod, ICustomMenuMod, IGlobalSettings<GlobalSettings> {
        new public string GetName() => "CustomPlayerModel";
        public override string GetVersion() => "0.8.0.0";

        private Menu MenuRef;
        public static GlobalSettings gs { get; set; } = new();

        public static Dictionary<string, AssetBundle> StoredBundles = new();
        public static AssetBundle ControllerBundle;
        public static Dictionary<string, string> ModelList = new();
        private static List<string> modelNames = new();

        public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects) {
            On.HeroController.Awake += createModel;
            On.tk2dSpriteAnimator.Play_tk2dSpriteAnimationClip_float_float += animatorPlayClipFloat;

            On.HeroController.EnableRenderer += hideKnight1;
            foreach(string method in new string[] { "EnterScene", "HazardRespawn", "Respawn" }) {
                new ILHook(typeof(HeroController).GetMethod(method, BindingFlags.Public | BindingFlags.Instance).GetStateMachineTarget(), hideKnight2);
            }
            On.HutongGames.PlayMaker.Actions.SetMeshRenderer.OnEnter += hideKnight3;
            new ILHook(typeof(HeroController).GetMethod("DieFromHazard", BindingFlags.NonPublic | BindingFlags.Instance).GetStateMachineTarget(), despawnHazardKnight);

            ControllerBundle = AssetBundle.LoadFromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream("CustomPlayerModel.Resources.controller.unity3d"));
            registerModels();
        }

        private void createModel(On.HeroController.orig_Awake orig, HeroController self) {
            orig(self);
            if(gs.isEnabled) {
                if(string.IsNullOrEmpty(gs.chosenModel) || !modelNames.Contains(gs.chosenModel)) {
                    try {
                        gs.chosenModel = modelNames[0];
                    }
                    catch(Exception) {
                        gs.isEnabled = false;
                        return;
                    }
                }
                spawnModel(gs.chosenModel);
            }
        }

        private void registerModels() {
            string filepath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Models");
            EnsureDirectory(filepath);
            string[] filenames = Directory.GetFiles(filepath);
            int folderLength = "CustomPlayerModel/Models/".Length;
            int extensionLength = ".unity3d".Length;
            foreach(string file in filenames) {
                int startIndex = file.IndexOf("CustomPlayerModel") + folderLength;
                string name = file.Substring(startIndex, file.Length - startIndex - extensionLength);
                ModelList.Add(name, file);
                modelNames.Add(name);
            }
            modelNames.Sort();
        }

        private void spawnModel(string bundleName) {
            if(AnimationStateController.instance != null && AnimationStateController.instance.parent != null) {
                GameObject.Destroy(AnimationStateController.instance.parent);
            }
            var model = findFbx(bundleName);
            if(model == null)
                return;
            Bounds heroBounds = HeroController.instance.gameObject.GetComponent<Collider2D>().bounds;
            float rotationFlip = HeroController.instance.cState.facingRight ? 90 : 270;
            GameObject modelGO = GameObject.Instantiate(model, heroBounds.center - new Vector3(0, heroBounds.extents.y, 0), Quaternion.Euler(0, rotationFlip, 0), HeroController.instance.transform) as GameObject;
            modelGO.transform.localScale = modelGO.transform.localScale.MultiplyElements(HeroController.instance.gameObject.transform.localScale).MultiplyElements(new Vector3(0.1f, 1, 1));
            GameObject armature = findArmature(modelGO);
            if(armature == null)
                return;
            AnimationStateController asc = armature.AddComponent<AnimationStateController>();
            asc.parent = modelGO;
            asc.nonSwimHeight = modelGO.transform.localPosition.y;
            armature.AddComponent<Animator>().runtimeAnimatorController = ControllerBundle.LoadAsset("HK controller.controller") as RuntimeAnimatorController;

            modelGO.SetActive(true);
            HeroController.instance.gameObject.GetComponent<MeshRenderer>().enabled = false;
        }

        private void animatorPlayClipFloat(On.tk2dSpriteAnimator.orig_Play_tk2dSpriteAnimationClip_float_float orig, tk2dSpriteAnimator self, tk2dSpriteAnimationClip clip, float clipStartTime, float overrideFps) {
            orig(self, clip, clipStartTime, overrideFps);
            if(gs.isEnabled) {
                if(self.gameObject.GetComponentInParent<HeroController>() == null) {
                    return;
                }
                if(clip != null) {
                    AnimationStateController.updateState(clip.name);
                }
            }
        }

        private void despawnHazardKnight(ILContext il) {
            ILCursor cursor = new ILCursor(il).Goto(0);
            cursor.GotoNext(i => i.MatchCallvirt<GameObject>("set_layer"));
            cursor.GotoNext(i => i.Match(OpCodes.Bne_Un_S));
            cursor.EmitDelegate<Func<Int32, Int32>>(j => { return gs.isEnabled ? -1 : j; });
            cursor.GotoNext(i => i.MatchLdstr("Spike Direction"));
            cursor.GotoNext(i => i.Match(OpCodes.Bne_Un_S));
            cursor.EmitDelegate<Func<Int32, Int32>>(j => { return gs.isEnabled ? -1 : j; });
        }

        private GameObject findArmature(GameObject model) {
            Transform[] transforms = model.GetComponentsInChildren<Transform>();
            foreach(Transform transform in transforms) {
                if(transform.gameObject.name == "Armature") {
                    return transform.gameObject;
                }
            }
            LogError("Could not find \"Armature\" in model transforms");
            return null;
        }

        private UObject findFbx(string bundleName) {
            AssetBundle bundle;
            if(StoredBundles.ContainsKey(bundleName)) {
                bundle = StoredBundles[bundleName];
            }
            else {
                bundle = AssetBundle.LoadFromFile(ModelList[bundleName]);
                StoredBundles.Add(bundleName, bundle);
            }
            string[] assetNames = bundle.GetAllAssetNames();
            foreach(string name in assetNames) {
                if(name.EndsWith(".fbx")) {
                    return bundle.LoadAsset(name);
                }
            }
            Log($"No fbx found in AssetBundle \"{bundleName}\"");
            return null;
        }

        private void hideKnight1(On.HeroController.orig_EnableRenderer orig, HeroController self) {
            orig(self);
            if(gs.isEnabled) {
                ((MeshRenderer)typeof(HeroController).GetField("renderer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self)).enabled = false;
            }
        }

        private void hideKnight2(ILContext il) {
            ILCursor cursor = new ILCursor(il).Goto(0);
            cursor.GotoNext(i => i.MatchLdfld<HeroController>("renderer"),
                            i => i.MatchLdcI4(1));
            cursor.GotoNext(i => i.MatchCallvirt<Renderer>("set_enabled"));
            cursor.EmitDelegate<Func<Int32, Int32>>(j => { return gs.isEnabled ? 0 : j; });
        }

        private void hideKnight3(On.HutongGames.PlayMaker.Actions.SetMeshRenderer.orig_OnEnter orig, HutongGames.PlayMaker.Actions.SetMeshRenderer self) {
            if(gs.isEnabled) {
                if(self.gameObject != null && self.gameObject.GameObject != null && self.gameObject.GameObject.Value != null) {
                    if(self.active != null) {
                        if(new List<string> { "Knight", "Hero Death", "Knight Cutscene Animator", "Knight Lift", "Knight Dummy" }.Contains(self.gameObject.GameObject.Value.name)) {
                            self.active.Value = false;
                        }
                    }
                }
            }
            orig(self);
        }

        public MenuScreen GetMenuScreen(MenuScreen modListMenu, ModToggleDelegates? modtoggledelegates) {
            MenuRef ??= new Menu(
                name: "Custom Player Model",
                elements: new Element[] {
                    new HorizontalOption(
                        name: "Enabled",
                        description: "",
                        values: new string[] { "On", "Off" },
                        applySetting: index => {
                            gs.isEnabled = index == 0;
                            if(GameManager.instance.IsGameplayScene()) {//is GameManager the best check here?
                                if(gs.isEnabled) {
                                    spawnModel(gs.chosenModel);
                                }
                                else {
                                    HeroController.instance.gameObject.GetComponent<MeshRenderer>().enabled = true;
                                    if(AnimationStateController.instance != null && AnimationStateController.instance.parent != null) {
                                        GameObject.Destroy(AnimationStateController.instance.parent);
                                    }
                                }
                            }
                        },
                        loadSetting: () => gs.isEnabled ? 0 : 1
                    ),
                    new HorizontalOption(
                        name: "Model",
                        description: "",
                        values: modelNames.ToArray(),
                        applySetting: index => {
                            gs.chosenModel = modelNames[index];
                            if(gs.isEnabled && GameManager.instance.IsGameplayScene()) {//ditto
                                spawnModel(gs.chosenModel);
                            }
                        },
                        loadSetting: () => modelNames.IndexOf(gs.chosenModel)
                    )
                }
            );
            return MenuRef.GetMenuScreen(modListMenu);
        }

        public bool ToggleButtonInsideMenu {
            get;
        }

        public void OnLoadGlobal(GlobalSettings s) {
            gs = s;
        }

        public GlobalSettings OnSaveGlobal() {
            return gs;
        }
    }

    public class GlobalSettings {
        public bool isEnabled = true;
        public string chosenModel = "";
    }
}