using UnityEngine;

[RequireComponent(typeof(StoreGrabbable), typeof(Rigidbody))]
public sealed class StoreProduct : MonoBehaviour
{
    public StoreBasket Basket { get; private set; }
    public StoreGrabbable Grab { get; private set; }
    public Rigidbody Body { get; private set; }
    public Collider[] Colliders { get; private set; }
    private Transform shelfParent;
    private RigidbodyInterpolation freeInterpolation;
    private Vector3 storedPosition;
    private Quaternion storedRotation;

    private void Awake()
    {
        Grab = GetComponent<StoreGrabbable>();
        Body = GetComponent<Rigidbody>();
        Colliders = GetComponentsInChildren<Collider>();
        shelfParent = transform.parent;
        freeInterpolation = Body.interpolation;
    }

    public void RemoveFromBasket()
    {
        if (Basket == null) return;
        var oldBasket = Basket;
        Basket = null;
        transform.SetParent(shelfParent, true);
        Body.interpolation = freeInterpolation;
        oldBasket.Remove(this);
    }

    public void StoreIn(StoreBasket basket)
        => StoreIn(basket, transform.position, transform.rotation);

    public void StoreIn(StoreBasket basket, Vector3 position, Quaternion rotation)
    {
        Basket = basket;
        Body.linearVelocity = Vector3.zero;
        Body.angularVelocity = Vector3.zero;
        Body.isKinematic = true;
        Body.useGravity = false;
        // A nested interpolated Rigidbody otherwise keeps a previous world pose
        // when its basket moves. Stored items follow one explicit local pose.
        Body.interpolation = RigidbodyInterpolation.None;
        transform.SetParent(basket.Contents, true);
        storedPosition = basket.Contents.InverseTransformPoint(position);
        storedRotation = Quaternion.Inverse(basket.Contents.rotation) * rotation;
        SyncStoredPose();
    }

    public void SyncStoredPose()
    {
        if (Basket == null || Grab.isSelected) return;
        Body.isKinematic = true;
        Body.useGravity = false;
        transform.localPosition = storedPosition;
        transform.localRotation = storedRotation;
        Body.position = transform.position;
        Body.rotation = transform.rotation;
    }

    private void OnDestroy()
    {
        if (Basket != null) Basket.Remove(this);
    }
}
