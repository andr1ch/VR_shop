using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
public sealed class MultiBasketHand : XRBaseInteractor
{
    public XRBaseInteractable Target;
    public bool Holding;
    public override bool isSelectActive=>Holding;
    public override void GetValidTargets(List<IXRInteractable> targets){targets.Clear();if(Target!=null)targets.Add(Target);}
}
public static class VerifyBasketMultipleProducts
{
    private static async Task Frames(int n=3){int end=Time.frameCount+n;while(Time.frameCount<end)await Task.Delay(10);}
    public static async Task<object> Main()
    {
        var basket=UnityEngine.Object.FindFirstObjectByType<StoreBasket>();
        var bbody=basket.GetComponent<Rigidbody>();bbody.isKinematic=true;bbody.position=new Vector3(50,1,50);bbody.rotation=Quaternion.identity;
        var all=UnityEngine.Object.FindObjectsByType<StoreProduct>(FindObjectsSortMode.None);
        var items=new[]{"Cube.141","Cube.142","Cube.143"}.Select(name=>all.First(p=>p.name==name)).ToArray();
        UnityEngine.Object.FindFirstObjectByType<StoreInteractionRange>().enabled=false;
        foreach(var p in items)p.Grab.enabled=true;
        var go=new GameObject("Multiple Product Test Hand");var hand=go.AddComponent<MultiBasketHand>();
        var report=new List<object>();Vector3 firstLocal=Vector3.zero;
        try
        {
            foreach(var p in items)
            {
                p.Body.position=new Vector3(50,1.7f,50);await Frames();hand.transform.position=p.transform.position;
                hand.Target=p.Grab;hand.Holding=true;p.Grab.interactionManager.SelectEnter(hand,(IXRSelectInteractable)p.Grab);await Frames();
                hand.transform.position+=basket.transform.TransformPoint(new Vector3(0,.05f,0))-p.Body.worldCenterOfMass;await Frames(10);
                hand.Holding=false;hand.Target=null;p.Grab.interactionManager.SelectExit(hand,(IXRSelectInteractable)p.Grab);await Frames(12);
                report.Add(new{item=p.name,count=basket.Count,stored=p.Basket==basket,local=basket.transform.InverseTransformPoint(p.transform.position).ToString("F4"),first=items[0].transform.localPosition.ToString("F4"),firstKinematic=items[0].Body.isKinematic});
                if(p==items[0])firstLocal=p.transform.localPosition;
            }
            if(basket.Count!=3||items.Any(p=>p.Basket!=basket))throw new Exception("All three products must fit side by side; count="+basket.Count);
            for(int i=0;i<5;i++)
            {
                bbody.position+=new Vector3(.15f,.03f,.07f);bbody.rotation=Quaternion.Euler(8*i,12*i,3*i);await Frames(5);
                foreach(var p in items)
                    if(!p.Body.isKinematic||Vector3.Distance(p.Body.position,p.transform.position)>.002f)
                        throw new Exception("Stored body drifted from basket: "+p.name);
            }
            if(Vector3.Distance(firstLocal,items[0].transform.localPosition)>.002f)throw new Exception("First product moved while adding or carrying other products");
            for(int i=0;i<items.Length;i++)for(int j=i+1;j<items.Length;j++)
            {
                var a=items[i].Colliders[0];var b=items[j].Colliders[0];Vector3 direction;float distance;
                if(Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out direction,out distance)&&distance>.002f)
                    throw new Exception("Stored products overlap: "+items[i].name+" / "+items[j].name);
            }
            var middle=items[1];hand.transform.position=middle.transform.position;hand.Target=middle.Grab;hand.Holding=true;
            middle.Grab.interactionManager.SelectEnter(hand,(IXRSelectInteractable)middle.Grab);await Frames();
            if(basket.Count!=2||middle.Basket!=null)throw new Exception("Cannot remove middle product");
            hand.transform.position+=Vector3.up*.8f;await Frames(8);
            if(Physics.GetIgnoreCollision(middle.Colliders[0],items[0].Colliders[0]))throw new Exception("Product collisions were not restored after leaving basket");
            hand.transform.position+=basket.transform.TransformPoint(new Vector3(0,.05f,0))-middle.Body.worldCenterOfMass;await Frames(10);
            hand.Holding=false;hand.Target=null;middle.Grab.interactionManager.SelectExit(hand,(IXRSelectInteractable)middle.Grab);await Frames(10);
            if(basket.Count!=3||middle.Basket!=basket)throw new Exception("Cannot return middle product to basket");
            if(Vector3.Distance(firstLocal,items[0].transform.localPosition)>.002f)throw new Exception("First product moved during removal/reinsertion");
            var result=new{count=basket.Count,allStored=items.All(p=>p.Basket==basket),firstDrift=Vector3.Distance(firstLocal,items[0].transform.localPosition),movingAndTiltingStable=true,noProductOverlap=true,removalAndReinsertion=true,report};
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath,"../AgentScripts/Checks/multiple-products-results.json"),Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
            return result;
        }
        finally{UnityEngine.Object.Destroy(go);}
    }
}
