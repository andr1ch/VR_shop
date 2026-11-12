using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public static class ConfigureStoreInteraction
{
    private const string MaterialPath = "Assets/StoreInteraction/Physics";
    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before configuring.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var root = GameObject.Find("_InteractiveObjects").transform;
        EditorSceneManager.SaveScene(scene);
        string backup = "/tmp/VR_shop-before-lab6.unity";
        if (!File.Exists(backup)) File.Copy(scene.path, backup);
        Directory.CreateDirectory(MaterialPath);
        AssetDatabase.Refresh();
        var wood = Material("Wood", .6f, .6f, .1f);
        var rubber = Material("Rubber", .8f, .9f, .15f);
        var ice = Material("Ice", .02f, .03f, 0f);
        int productCount = 0;
        foreach (Transform t in root.Find("Products"))
        {
            var mesh = t.GetComponent<MeshFilter>();
            if (mesh == null || mesh.sharedMesh == null) continue;
            t.gameObject.isStatic = false;
            var existing = t.GetComponent<XRGrabInteractable>();
            if (existing != null && !(existing is StoreGrabbable)) UnityEngine.Object.DestroyImmediate(existing);
            var legacyAudio = t.GetComponent<InteractiveSound>();
            if (legacyAudio != null) UnityEngine.Object.DestroyImmediate(legacyAudio);
            var box = GetOrAdd<BoxCollider>(t.gameObject);
            box.center = mesh.sharedMesh.bounds.center;
            box.size = mesh.sharedMesh.bounds.size;
            box.sharedMaterial = wood;
            box.isTrigger = false;
            var body = GetOrAdd<Rigidbody>(t.gameObject);
            ConfigureBody(body, .35f);
            body.centerOfMass = mesh.sharedMesh.bounds.center;
            var grab = GetOrAdd<StoreGrabbable>(t.gameObject);
            ConfigureGrab(grab, box);
            grab.useDynamicAttach = true;
            grab.matchAttachPosition = true;
            grab.matchAttachRotation = true;
            if (t.GetComponent<StoreProduct>() == null) t.gameObject.AddComponent<StoreProduct>();
            if (t.GetComponent<StoreInteractionFeedback>() == null) t.gameObject.AddComponent<StoreInteractionFeedback>();
            productCount++;
        }
        if (root.Find("Products").GetComponent<StoreInteractionRange>() == null)
            root.Find("Products").gameObject.AddComponent<StoreInteractionRange>();
        var basket = root.Find("basket");
        if (basket.GetComponent<StoreBasket>() == null)
        {
            var model = basket;
            basket = new GameObject("basket").transform;
            basket.SetParent(root, false);
            basket.position = model.GetComponent<Renderer>().bounds.center;
            basket.rotation = Quaternion.Euler(0f, model.eulerAngles.y, 0f);
            model.name = "BasketVisual";
            model.SetParent(basket, true);
            model.gameObject.isStatic = false;
            var meshBounds = model.GetComponent<MeshFilter>().sharedMesh.bounds;
            // Mesh local Z is up. The basket is deliberately hollow: five boxes,
            // not one convex hull that would seal off the opening.
            var body = basket.gameObject.AddComponent<Rigidbody>();
            ConfigureBody(body, .7f);
            BoxCollider bottom = BasketBox(basket, "Bottom", new Vector3(0f,-.115f,0f), new Vector3(.43f,.025f,.55f), rubber);
            BasketBox(basket, "LeftWall", new Vector3(-.205f,0f,0f), new Vector3(.025f,.23f,.55f), rubber);
            BasketBox(basket, "RightWall", new Vector3(.205f,0f,0f), new Vector3(.025f,.23f,.55f), rubber);
            BasketBox(basket, "FrontWall", new Vector3(0f,0f,.265f), new Vector3(.39f,.23f,.025f), rubber);
            BasketBox(basket, "BackWall", new Vector3(0f,0f,-.265f), new Vector3(.39f,.23f,.025f), rubber);
            var attach = new GameObject("Grip").transform;
            attach.SetParent(basket, false);
            attach.localPosition = new Vector3(0f,.14f,0f);
            var grab = basket.gameObject.AddComponent<StoreGrabbable>();
            ConfigureGrab(grab, null);
            grab.colliders.Clear();
            grab.colliders.AddRange(basket.GetComponentsInChildren<Collider>());
            grab.attachTransform = attach;
            grab.useDynamicAttach = false;
            grab.attachEaseInTime = .15f;
            grab.throwOnDetach = false;
            var contents = new GameObject("Contents").transform;
            contents.SetParent(basket, false);
            var zone = new GameObject("StorageZone");
            zone.transform.SetParent(basket, false);
            var trigger = zone.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f,.045f,0f);
            trigger.size = new Vector3(.38f,.28f,.49f);
            var container = basket.gameObject.AddComponent<StoreBasket>();
            SetObject(container,"storageZone",trigger);
            SetObject(container,"contents",contents);
            zone.AddComponent<StoreBasketZone>();
            basket.gameObject.AddComponent<StoreInteractionFeedback>();
        }
        basket.Find("Grip").localPosition = new Vector3(0f,.14f,-.265f);
        var doors = root.Find("RfDoors");
        foreach (var t in doors.Cast<Transform>().ToArray())
        {
            if (t.GetComponent<StoreFridgeDoor>() != null) continue;
            var mesh = t.GetComponent<MeshFilter>();
            if (mesh == null) continue;
            var hinge = new GameObject(t.name + "_Hinge").transform;
            hinge.SetParent(doors, false);
            hinge.position = t.position;
            t.SetParent(hinge,true);
            t.gameObject.isStatic = false;
            var box = t.gameObject.AddComponent<BoxCollider>();
            box.center = mesh.sharedMesh.bounds.center;
            box.size = mesh.sharedMesh.bounds.size;
            box.sharedMaterial = ice;
            var body = hinge.gameObject.AddComponent<Rigidbody>();
            ConfigureBody(body, 8f);
            body.useGravity = false;
            var door = hinge.gameObject.AddComponent<StoreFridgeDoor>();
            door.colliders.Clear();
            door.colliders.Add(box);
            door.selectMode = InteractableSelectMode.Single;
            var feedback = hinge.gameObject.AddComponent<StoreInteractionFeedback>();
            var serialized = new SerializedObject(feedback);
            serialized.FindProperty("pickupSound").stringValue = "";
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        int staticCount=0;
        foreach (string group in new[]{"_Environment","_Props"})
            foreach (var mesh in GameObject.Find(group).GetComponentsInChildren<MeshFilter>())
            {
                if (mesh.sharedMesh == null || mesh.GetComponent<Collider>() != null) continue;
                var collider = mesh.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh.sharedMesh;
                collider.sharedMaterial = wood;
                staticCount++;
            }
        foreach (var interactor in UnityEngine.Object.FindObjectsByType<XRBaseInputInteractor>(FindObjectsSortMode.None))
        {
            // StateChange is a single grab per press, held until the grip releases.
            interactor.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.StateChange;
            EditorUtility.SetDirty(interactor);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return new{products=productCount,doors=doors.childCount,newStaticColliders=staticCount,basket=basket.name,backup};
    }
    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }
    private static PhysicsMaterial Material(string name,float dynamicFriction,float staticFriction,float bounce)
    {
        string path=MaterialPath+"/"+name+".physicMaterial";
        var material=AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if(material==null){material=new PhysicsMaterial(name);AssetDatabase.CreateAsset(material,path);}
        material.dynamicFriction=dynamicFriction;material.staticFriction=staticFriction;material.bounciness=bounce;
        material.frictionCombine=PhysicsMaterialCombine.Average;material.bounceCombine=PhysicsMaterialCombine.Minimum;
        EditorUtility.SetDirty(material);
        return material;
    }
    private static void ConfigureBody(Rigidbody body,float mass)
    {
        body.mass=mass;body.linearDamping=.1f;body.angularDamping=.15f;
        body.useGravity=true;body.isKinematic=true;
        body.interpolation=RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
    }
    private static void ConfigureGrab(StoreGrabbable grab,Collider collider)
    {
        grab.selectMode=InteractableSelectMode.Single;
        grab.movementType=XRBaseInteractable.MovementType.Kinematic;
        grab.throwOnDetach=true;grab.forceGravityOnDetach=true;
        grab.retainTransformParent=true;
        if(collider!=null){grab.colliders.Clear();grab.colliders.Add(collider);}
        EditorUtility.SetDirty(grab);
    }
    private static BoxCollider BasketBox(Transform root,string name,Vector3 position,Vector3 size,PhysicsMaterial material)
    {
        var go=new GameObject(name);go.transform.SetParent(root,false);go.transform.localPosition=position;
        var collider=go.AddComponent<BoxCollider>();collider.size=size;collider.sharedMaterial=material;return collider;
    }
    private static void SetObject(UnityEngine.Object target,string field,UnityEngine.Object value)
    {
        var obj=new SerializedObject(target);obj.FindProperty(field).objectReferenceValue=value;obj.ApplyModifiedPropertiesWithoutUndo();
    }
}
