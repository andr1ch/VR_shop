using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public sealed class CheckoutTestHand : XRBaseInteractor
{
    public XRBaseInteractable Target;
    public bool Holding;
    public override bool isSelectActive => Holding;
    public override void GetValidTargets(List<IXRInteractable> targets) { targets.Clear(); if (Target != null) targets.Add(Target); }
}
public static class VerifyStoreCheckout
{
    private static readonly List<string> checks = new List<string>();
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks.Add(message); }
    private static async Task Frames(int count = 3)
    {
        int end = Time.frameCount + count;
        while (Time.frameCount < end) await Task.Delay(10);
    }
    private static async Task Seconds(float count)
    {
        float end = Time.time + count;
        while (Time.time < end) await Task.Delay(10);
    }
    private static async Task Grab(CheckoutTestHand hand, StoreGrabbable target)
    {
        hand.transform.position = target.transform.position;
        hand.Target = target; hand.Holding = true;
        target.interactionManager.SelectEnter(hand, (IXRSelectInteractable)target);
        await Frames();
        if (!target.isSelected) throw new Exception("Test hand could not select " + target.name);
    }
    private static void Release(CheckoutTestHand hand, StoreGrabbable target)
    {
        hand.Target = null; hand.Holding = false;
        if (target.isSelected) target.interactionManager.SelectExit(hand, (IXRSelectInteractable)target);
    }
    private static async Task PositionHeldBasket(CheckoutTestHand hand, StoreBasket basket, Vector3 center)
    {
        hand.transform.position += center - basket.transform.position;
        await Frames(8);
    }
    private static async Task AddProduct(CheckoutTestHand hand, StoreBasket basket, StoreProduct product)
    {
        product.Grab.enabled = true;
        product.Body.position = basket.transform.position + Vector3.up * .75f;
        await Frames(); await Grab(hand, product.Grab);
        hand.transform.position += basket.transform.TransformPoint(new Vector3(0, .05f, 0)) - product.Body.worldCenterOfMass;
        await Frames(8); Release(hand, product.Grab); await Frames(10);
        if (product.Basket != basket) throw new Exception("Cannot store " + product.name);
    }
    private static async Task AwaitPaid(StoreCashierController cashier, int expected)
    {
        float end = Time.time + 4;
        while (cashier.PaymentCount < expected && Time.time < end) await Frames(1);
        Check(cashier.PaymentCount == expected && cashier.Status == StoreCheckoutStatus.Paid, "payment " + expected + " completes through real trigger and FSM");
    }
    public static async Task<object> Main()
    {
        checks.Clear();
        var cashier = UnityEngine.Object.FindFirstObjectByType<StoreCashierController>();
        var basket = UnityEngine.Object.FindFirstObjectByType<StoreBasket>();
        var body = basket.GetComponent<Rigidbody>(); var basketGrab = basket.GetComponent<StoreGrabbable>();
        var all = UnityEngine.Object.FindObjectsByType<StoreProduct>(FindObjectsSortMode.None);
        var items = new[] { "Cube.141", "Cube.142", "Cube.143", "Cube.144" }.Select(name => all.First(p => p.name == name)).ToArray();
        UnityEngine.Object.FindFirstObjectByType<StoreInteractionRange>().enabled = false;
        var go = new GameObject("Checkout Test Hand"); var hand = go.AddComponent<CheckoutTestHand>();
        Vector3 counter = new Vector3(29.55f, 1.10f, 19.55f);
        Vector3 far = new Vector3(50, 1, 50);
        try
        {
            Check(basket.Count == 0, "starts with empty basket");
            var animator = GameObject.Find("Cashier").GetComponent<Animator>();
            var skin = animator.GetComponentInChildren<SkinnedMeshRenderer>();
            var rotations = skin.bones.Select(b => b.localRotation).ToArray();
            var root = animator.transform.position;
            await Frames(8);
            Check(skin.bones.Where((b, i) => Quaternion.Angle(b.localRotation, rotations[i]) > .01f).Any(), "idle animation moves skeleton");
            var clip = animator.runtimeAnimatorController.animationClips.First();
            animator.Update(clip.length + .1f);
            Check(animator.GetCurrentAnimatorStateInfo(0).loop && animator.GetCurrentAnimatorStateInfo(0).normalizedTime > 1,
                "idle animation loops past clip boundary");
            Check(Vector3.Distance(root, animator.transform.position) < .001f && !animator.applyRootMotion, "cashier remains at counter without root motion");
            Check(skin.sharedMaterials.All(m => UnityEditor.AssetDatabase.GetAssetPath(m).StartsWith("Assets/Materials/Characters/Cashier/") && m.mainTexture != null), "cashier uses organized external materials with textures");
            body.isKinematic = true; body.position = counter; body.rotation = Quaternion.identity; await Seconds(1.5f);
            Check(cashier.PaymentCount == 0, "empty basket is not paid");
            body.position = far; await Frames();
            for (int i = 0; i < 3; i++) await AddProduct(hand, basket, items[i]);
            Check(basket.Count == 3 && basket.UnpaidCount == 3, "three unpaid products fit in basket");
            var positions = items.Take(3).Select(p => p.transform.localPosition).ToArray();
            await Grab(hand, basketGrab); await PositionHeldBasket(hand, basket, counter); await Seconds(1.5f);
            Check(cashier.PaymentCount == 0 && cashier.Status == StoreCheckoutStatus.Waiting, "held basket cannot trigger payment");
            Release(hand, basketGrab); await Frames(3);
            Check(cashier.Status == StoreCheckoutStatus.Processing, "released basket starts processing");
            await Grab(hand, basketGrab); await PositionHeldBasket(hand, basket, counter + Vector3.up);
            Check(cashier.PaymentCount == 0 && cashier.Status == StoreCheckoutStatus.Waiting, "taking basket during processing cancels payment");
            await PositionHeldBasket(hand, basket, counter); Release(hand, basketGrab); await Frames(3);
            await Grab(hand, items[1].Grab); hand.transform.position += Vector3.up * .8f; await Frames(5);
            Check(cashier.PaymentCount == 0 && !items[1].IsPaid, "removing product during processing cancels payment");
            await Seconds(1.5f); Check(cashier.PaymentCount == 0, "cancelled purchase waits for basket resubmission");
            Release(hand, items[1].Grab); await Frames(2); items[1].Body.isKinematic = true;
            await Grab(hand, basketGrab); await PositionHeldBasket(hand, basket, far); Release(hand, basketGrab);
            body.isKinematic = true; body.position = far; body.rotation = Quaternion.identity; await Frames();
            await AddProduct(hand, basket, items[1]);
            Check(basket.Count == 3, "all products restored before resubmitting cancelled purchase");
            await Grab(hand, basketGrab); await PositionHeldBasket(hand, basket, counter); Release(hand, basketGrab); await AwaitPaid(cashier, 1);
            Check(cashier.LastPurchasedCount == 3 && basket.UnpaidCount == 0 && items.Take(3).All(p => p.IsPaid), "all three products are marked paid");
            Check(basket.Count == 3 && items.Take(3).All(p => p.Basket == basket), "payment keeps products inside basket");
            Check(Vector3.Distance(positions[0], items[0].transform.localPosition) < .002f, "first product does not move during checkout");
            Check(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(a => a.name == "StoreSound_checkout" && a.isPlaying), "successful payment plays supplied spatial checkout sound");
            var label = GameObject.Find("CheckoutStatus").GetComponent<TextMesh>();
            Check(label.text.StartsWith("Оплачено"), "status panel confirms payment");
            await Seconds(1.5f); Check(cashier.PaymentCount == 1, "staying in zone does not charge twice");
            await Grab(hand, basketGrab); await PositionHeldBasket(hand, basket, far); Release(hand, basketGrab);
            body.isKinematic = true; body.position = far; await Frames();
            await Grab(hand, basketGrab); await PositionHeldBasket(hand, basket, counter); Release(hand, basketGrab);
            await Seconds(1.5f); Check(cashier.PaymentCount == 1, "returning paid basket does not charge twice");
            await Grab(hand, basketGrab); await PositionHeldBasket(hand, basket, far); Release(hand, basketGrab);
            body.isKinematic = true; body.position = far; await Frames();
            await Grab(hand, items[0].Grab); hand.transform.position += Vector3.up; await Frames(6);
            Release(hand, items[0].Grab); await Frames(2); items[0].Body.isKinematic = true; items[0].Body.useGravity = false;
            Check(items[0].IsPaid && items[0].Basket == null, "purchase flag survives removing product from basket");
            await AddProduct(hand, basket, items[3]);
            Check(basket.UnpaidCount == 1, "only newly added product needs payment");
            await Grab(hand, basketGrab); await PositionHeldBasket(hand, basket, counter); Release(hand, basketGrab);
            await AwaitPaid(cashier, 2);
            Check(cashier.LastPurchasedCount == 1 && basket.UnpaidCount == 0 && items[3].IsPaid, "next payment includes only new product");
            var result = new { passed = checks.Count, checks, payments = cashier.PaymentCount, remainingProducts = basket.Count, idleClip = clip.name, idleSeconds = clip.length };
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "../AgentScripts/Checks/checkout-results.json"), Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.Indented));
            return result;
        }
        finally { UnityEngine.Object.Destroy(go); }
    }
}
