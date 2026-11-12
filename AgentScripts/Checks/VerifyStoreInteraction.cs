using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Object=UnityEngine.Object;

public sealed class StoreCheckHand : XRBaseInteractor
{
    public IXRInteractable Target;
    public bool Holding;
    public override bool isSelectActive => Holding;
    public override void GetValidTargets(List<IXRInteractable> targets)
    { targets.Clear();if(Target!=null)targets.Add(Target); }
}
public static class VerifyStoreInteraction
{
    private static readonly List<string> checks=new List<string>();
    private static void Check(bool value,string name){if(!value)throw new Exception("FAIL: "+name);checks.Add(name);}
    private static async Task Frames(int count=3)
    {int frame=Time.frameCount+count;while(Time.frameCount<frame)await Task.Delay(10);}
    private static void Select(StoreCheckHand hand,XRBaseInteractable target)
    {hand.Target=target;hand.Holding=true;target.interactionManager.SelectEnter(hand,(IXRSelectInteractable)target);}
    private static void Release(StoreCheckHand hand,XRBaseInteractable target)
    {hand.Holding=false;hand.Target=null;target.interactionManager.SelectExit(hand,(IXRSelectInteractable)target);}
    public static async Task<object> Main()
    {
        if(!Application.isPlaying)throw new Exception("Play mode required");
        checks.Clear();
#if UNITY_EDITOR_LINUX
        Check(UnityEngine.XR.Management.XRGeneralSettings.Instance != null &&
            !UnityEngine.XR.Management.XRGeneralSettings.Instance.InitManagerOnStart &&
            UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(UnityEditor.BuildTargetGroup.Standalone).InitManagerOnStart,
            "Linux simulation skips native OpenXR without changing build settings");
#endif
        var products=Object.FindObjectsByType<StoreProduct>(FindObjectsSortMode.None);
        Check(products.Length==5202,"all 5202 products configured");
        Check(products.All(p=>p.Body.isKinematic),"shelf products sleep before first grab");
        int registered=products.Count(p=>p.Grab.enabled);
        Check(registered>0&&registered<products.Length,"XRI registers only nearby shelf products");
        Object.FindFirstObjectByType<StoreInteractionRange>().enabled=false;
        foreach(var p in products)p.Grab.enabled=true;
        var product=products.OrderBy(p=>p.GetComponent<Renderer>().bounds.size.magnitude).First(p=>p.GetComponent<Renderer>().bounds.size.magnitude>.12f);
        var handGo=new GameObject("StoreCheckHand");var hand=handGo.AddComponent<StoreCheckHand>();
        var basket=Object.FindFirstObjectByType<StoreBasket>();
        product.Body.position=new Vector3(50,2,50);
        await Frames();
        hand.transform.position=product.transform.position;
        Check(Vector3.Distance(product.transform.position,new Vector3(50,2,50))<.02f,"test product teleports to isolated physics area");
        await Frames();
        Physics.SyncTransforms();
        var visual=product.GetComponent<Renderer>();
        RaycastHit hit;
        Check(Physics.Raycast(visual.bounds.center+Vector3.forward*.6f,Vector3.back,out hit,1f)&&hit.collider.GetComponent<StoreProduct>()==product,"raycast identifies product collider");
        hand.Target=product.Grab;await Frames(2);
        var hoverBlock=new MaterialPropertyBlock();visual.GetPropertyBlock(hoverBlock);
        Check(hoverBlock.GetColor(Shader.PropertyToID("_BaseColor")).b>.9f,"hover applies visible color feedback");
        int selectEvents=0;product.Grab.selectEntered.AddListener(_=>selectEvents++);
        Select(hand,product.Grab);
        Check(Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(a=>a.name=="StoreSound_pickup"&&a.clip!=null&&a.spatialBlend==1f),"pickup plays supplied 3D sound");
        await Frames();
        Check(product.Grab.isSelected,"XRI product selection");
        Check(product.GetComponent<StoreInteractionFeedback>()!=null,"hover and pickup feedback attached");
        Vector3 before=product.transform.position;
        for(int i=0;i<8;i++){hand.transform.position+=Vector3.right*.035f;await Frames(1);}
        Check(product.transform.position.x>before.x+.1f,"product follows controller");
        Check(selectEvents==1,"holding grip does not repeat selection");
        Release(hand,product.Grab);await Frames(1);
        Check(!product.Body.isKinematic&&product.Body.useGravity,"release restores gravity and dynamics");
        Check(product.Body.linearVelocity.magnitude>.01f,"throw has nonzero velocity");
        var ground=new GameObject("StoreCheckGround");ground.transform.position=new Vector3(50,.5f,50);var floor=ground.AddComponent<BoxCollider>();floor.size=new Vector3(4,.1f,4);
        product.Body.linearVelocity=Vector3.zero;product.Body.angularVelocity=Vector3.zero;
        product.Body.position=new Vector3(50,1.1f,50);
        await Task.Delay(1300);
        Check(product.transform.position.y>.5f&&product.transform.position.y<.9f,"released product falls and collides with surface");
        basket.GetComponent<Rigidbody>().position=new Vector3(50,1.1f,50);basket.GetComponent<Rigidbody>().rotation=Quaternion.identity;
        await Frames(3);
        var untouched=products.First(p=>p!=product);
        var originalStockPosition=untouched.Body.position;
        untouched.Body.position=new Vector3(50,1.2f,50);await Frames(5);
        Check(untouched.Basket==null&&basket.Count==0,"basket cannot collect untouched shelf stock");
        untouched.Body.position=originalStockPosition;
        hand.transform.position=product.transform.position;
        Select(hand,product.Grab);await Frames(2);
        hand.transform.position+=new Vector3(50,1.2f,50)-product.transform.position;
        await Frames(10);
        Check(product.Basket==null&&basket.Count==0,"held item is not captured by basket trigger");
        Release(hand,product.Grab);
        for(int i=0;i<20&&basket.Count==0;i++)await Frames(1);
        Check(product.Basket==basket,"release inside basket is detected by real trigger");
        Check(Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(a=>a.name=="StoreSound_basket"&&a.clip!=null&&a.spatialBlend==1f),"placement plays supplied 3D basket sound");
        await Frames(6);
        Check(product.Basket==basket&&basket.Count==1,"trigger stores released product exactly once");
        Check(product.Body.isKinematic&&product.transform.IsChildOf(basket.Contents),"stored product stays attached to basket");
        Check(product.GetComponent<Renderer>().bounds.min.y>basket.transform.position.y-.11f,"stored product settles on basket floor");
        var bottom=basket.transform.Find("Bottom").GetComponent<Collider>();
        Check(Physics.GetIgnoreCollision(product.Colliders[0],bottom),"stored item cannot push basket through its own walls");
        int count=basket.Count;await Frames(8);Check(basket.Count==count,"trigger stay does not repeat placement");
        var basketGrab=basket.GetComponent<StoreGrabbable>();
        hand.transform.position=basketGrab.attachTransform.position;
        Select(hand,basketGrab);await Frames();
        Vector3 itemBefore=product.transform.position;
        hand.transform.position+=Vector3.right*.5f;await Frames(8);
        Check(product.transform.position.x>itemBefore.x+.3f,"contents travel with held basket");
        var otherGo=new GameObject("StoreCheckOtherHand");var other=otherGo.AddComponent<StoreCheckHand>();
        other.transform.position=product.transform.position;Select(other,product.Grab);await Frames();
        Check(basketGrab.isSelected&&product.Grab.isSelected,"two hands can hold basket and product simultaneously");
        Check(product.Basket==null&&basket.Count==0&&!product.transform.IsChildOf(basket.Contents),"product can be removed from basket");
        Check(!Physics.GetIgnoreCollision(product.Colliders[0],bottom),"removing product restores basket collisions");
        other.transform.position+=Vector3.forward*.8f;await Frames(5);
        Release(other,product.Grab);Release(hand,basketGrab);await Frames(2);Object.Destroy(otherGo);
        var door=Object.FindObjectsByType<StoreFridgeDoor>(FindObjectsSortMode.None).OrderBy(d=>d.transform.position.x).First();
        Vector3 closed=door.transform.rotation*Vector3.back*.7f;
        hand.transform.position=door.transform.position+closed;
        Select(hand,door);await Frames(2);
        Vector3 hinge=door.transform.position;
        for(int i=1;i<=7;i++){hand.transform.position=hinge+Quaternion.AngleAxis(-20*i,Vector3.up)*closed;await Frames(3);}
        Check(door.Angle<-95f&&door.Angle>=-105.01f,"door opens on hinge and clamps at 105 degrees");
        Check(Vector3.Distance(hinge,door.transform.position)<.001f,"hinge position stays fixed");
        Release(hand,door);await Frames(5);
        Check(door.Angle<-95f,"released door stays open for taking products");
        hand.transform.position=hinge+Quaternion.AngleAxis(door.Angle,Vector3.up)*closed;
        Select(hand,door);await Frames(2);
        for(int i=1;i<=7;i++){hand.transform.position=hinge+Quaternion.AngleAxis(-105+20*i,Vector3.up)*closed;await Frames(3);}
        Check(Mathf.Abs(door.Angle)<1f,"door can be closed by hand");Release(hand,door);
        Object.Destroy(handGo);Object.Destroy(ground);
        var result=new{passed=checks.Count,checks,registeredAtStart=registered,frame=Time.frameCount,frameMs=Time.unscaledDeltaTime*1000};
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath,"../AgentScripts/Checks/latest-results.json"),Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        return result;
    }
}
