using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ConfigureStoreCheckout
{
    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }
    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        Folder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    private static void Move(string from, string to)
    {
        if (AssetDatabase.LoadMainAssetAtPath(from) == null) return;
        string error = AssetDatabase.MoveAsset(from, to);
        if (!string.IsNullOrEmpty(error)) throw new Exception(error);
    }
    private static Material Material(string name, Color color)
    {
        string path = "Assets/Materials/Characters/Cashier/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(material);
        return material;
    }
    private static GameObject Panel(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var existing = parent.Find(name);
        var go = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name; go.transform.SetParent(parent, true);
        go.transform.position = position; go.transform.rotation = Quaternion.identity; go.transform.localScale = scale;
        var collider = go.GetComponent<Collider>();
        if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }
    private static void Ref(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo();
    }
    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play before setup.");
        string models = "Assets/Models/Characters/Cashier", textures = "Assets/Textures/Characters/Cashier",
            materials = "Assets/Materials/Characters/Cashier", animations = "Assets/Animations/Cashier";
        foreach (string folder in new[] { models, textures, materials, animations }) Folder(folder);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene);
        string backup = "/tmp/vr-shop-before-lab7/SampleScene.unity";
        if (!File.Exists(backup)) File.Copy(scene.path, backup);
        string modelPath = models + "/Cashier.fbx";
        Move("Assets/Cashier.fbx", modelPath);
        foreach (string name in new[] { "Body", "Clothes", "Eyebrow", "Hair" })
            Move("Assets/" + name + "_Mat_Base_Color.png", textures + "/" + name + "_Mat_Base_Color.png");
        var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        // Extract materials explicitly; remaps survive moving/reimporting the FBX.
        foreach (var embedded in AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>().ToArray())
        {
            string texture = embedded.mainTexture != null ? embedded.mainTexture.name : embedded.name;
            string name = texture.Split('_')[0];
            string path = materials + "/Cashier_" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(embedded);
                AssetDatabase.CreateAsset(material, path);
            }
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), material);
        }
        importer.importAnimation = true;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
        foreach (var entry in clips)
        {
            entry.name = "Cashier_Idle";
            entry.loopTime = true; entry.loopPose = true;
            entry.lockRootRotation = true; entry.lockRootPositionXZ = true; entry.lockRootHeightY = true;
        }
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        var clip = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
        string controllerPath = animations + "/CashierIdle.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var sm = controller.layers[0].stateMachine;
        var idle = sm.states.FirstOrDefault(s => s.state.name == "Idle").state ?? sm.AddState("Idle");
        idle.motion = clip; sm.defaultState = idle;
        var cashier = GameObject.Find("Cashier") ?? GameObject.Find("Idle");
        if (cashier == null) throw new Exception("Cashier instance not found.");
        cashier.name = "Cashier";
        var npcs = GameObject.Find("_NPC") ?? new GameObject("_NPC");
        cashier.transform.SetParent(npcs.transform, true);
        var animator = GetOrAdd<Animator>(cashier);
        animator.runtimeAnimatorController = controller;
        animator.avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
        animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var station = GameObject.Find("CheckoutStation") ?? new GameObject("CheckoutStation");
        station.transform.SetParent(npcs.transform, true);
        var checkout = GetOrAdd<StoreCashierController>(station);
        var zoneObject = station.transform.Find("PaymentZone")?.gameObject ?? new GameObject("PaymentZone");
        zoneObject.transform.SetParent(station.transform, false);
        zoneObject.transform.position = new Vector3(29.55f, 1.18f, 19.55f);
        var zone = GetOrAdd<BoxCollider>(zoneObject);
        zone.isTrigger = true; zone.size = new Vector3(.84f, .55f, .8f);
        var bridge = GetOrAdd<StoreCheckoutZone>(zoneObject);
        Ref(bridge, "cashier", checkout);
        var marker = Panel(station.transform, "BasketPlacement", new Vector3(29.55f, .927f, 19.55f),
            new Vector3(.64f, .008f, .68f), Material("CheckoutMarker", new Color(.13f, .55f, .7f)));
        Panel(station.transform, "StatusPanel", new Vector3(29.125f, .65f, 19.55f),
            new Vector3(.01f, .28f, .76f), Material("CheckoutPanel", new Color(.025f, .055f, .065f)));
        var textObject = station.transform.Find("CheckoutStatus")?.gameObject ?? new GameObject("CheckoutStatus");
        textObject.transform.SetParent(station.transform, false);
        textObject.transform.position = new Vector3(29.115f, .65f, 19.55f);
        textObject.transform.rotation = Quaternion.Euler(0, 90, 0);
        var text = GetOrAdd<TextMesh>(textObject);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 64; text.characterSize = .009f; text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center; text.color = Color.white;
        text.text = "Поставьте корзину\nдля оплаты";
        text.GetComponent<Renderer>().sharedMaterial = text.font.material;
        Ref(checkout, "checkoutZone", zone); Ref(checkout, "placementMarker", marker.GetComponent<Renderer>());
        Ref(checkout, "statusText", text); Ref(checkout, "paymentSoundPosition", cashier.transform);
        // The old register played checkout on any click, even without purchases.
        var register = GameObject.Find("Cube.3626");
        Ref(checkout, "paymentSoundPosition", register != null ? register.transform : cashier.transform);
        if (register != null && register.GetComponent<InteractiveSound>() != null)
        {
            var sound = register.GetComponent<InteractiveSound>();
            var so = new SerializedObject(sound);
            so.FindProperty("activateSound").stringValue = ""; so.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorUtility.SetDirty(animator); EditorUtility.SetDirty(checkout); EditorUtility.SetDirty(controller);
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        return new { cashier = cashier.name, clip = clip.name, clip.length, clip.isLooping, avatar = animator.avatar?.name,
            materialPaths = cashier.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Select(AssetDatabase.GetAssetPath),
            zone = zone.bounds.ToString(), textBounds = text.GetComponent<Renderer>().bounds.ToString(), backup };
    }
}
