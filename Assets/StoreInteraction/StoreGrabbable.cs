using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Shelf objects sleep until their first grab, then behave as throwable rigidbodies.
public sealed class StoreGrabbable : XRGrabInteractable
{
    private Rigidbody body;

    protected override void Awake()
    {
        base.Awake();
        body = GetComponent<Rigidbody>();
    }

    protected override void OnSelectEntering(SelectEnterEventArgs args)
    {
        var product = GetComponent<StoreProduct>();
        if (product != null) product.RemoveFromBasket();
        // XRI records this state during Grab(), and restores it on release.
        body.isKinematic = false;
        body.useGravity = true;
        base.OnSelectEntering(args);
    }
}
