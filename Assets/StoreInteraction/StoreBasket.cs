using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
public sealed class StoreBasket : MonoBehaviour
{
    [SerializeField] private BoxCollider storageZone;
    [SerializeField] private Transform contents;
    [SerializeField] private string placementSound = "basket";
    private readonly HashSet<StoreProduct> products = new HashSet<StoreProduct>();
    private readonly HashSet<StoreProduct> candidates = new HashSet<StoreProduct>();
    private readonly List<StoreProduct> candidateBuffer = new List<StoreProduct>();
    private Collider[] basketColliders;
    private StoreGrabbable grab;
    private const float Gap = .004f;
    private const float Epsilon = .0001f;
    public Transform Contents => contents;
    public int Count => products.Count;
    public bool IsHeld => grab != null && grab.isSelected;
    public int UnpaidCount
    {
        get
        {
            int count = 0;
            foreach (var product in products)
                if (product != null && !product.IsPaid) count++;
            return count;
        }
    }

    private void Awake()
    {
        basketColliders = GetComponentsInChildren<Collider>();
        grab = GetComponent<StoreGrabbable>();
    }

    public void CopyUnpaidProducts(List<StoreProduct> destination)
    {
        destination.Clear();
        foreach (var product in products)
            if (product != null && !product.IsPaid) destination.Add(product);
    }

    public void Track(Collider other)
    {
        if (other.attachedRigidbody == null) return;
        var product = other.attachedRigidbody.GetComponent<StoreProduct>();
        if (product == null || product.Basket != null ||
            (!product.Grab.isSelected && product.Body.isKinematic)) return;
        bool added = candidates.Add(product);
        // A held incoming item must not be repelled by the existing contents
        // before the release handler can place it in a free position.
        if (added || product.Grab.isSelected) SetContentsCollisions(product, true);
    }

    public void Untrack(Collider other)
    {
        if (other.attachedRigidbody == null) return;
        var product = other.attachedRigidbody.GetComponent<StoreProduct>();
        if (product != null && product.Basket != this && !OverlapsZone(product)) ForgetCandidate(product);
    }

    private void LateUpdate()
    {
        foreach (var product in products)
            if (product != null) product.SyncStoredPose();
        // Keep candidates between physics ticks. XRI can release in a render
        // frame without an OnTriggerStay; waiting for the next tick is too late.
        candidateBuffer.Clear();
        candidateBuffer.AddRange(candidates);
        foreach (var product in candidateBuffer)
        {
            if (product == null) { candidates.Remove(product); continue; }
            if (product.Basket == this) { candidates.Remove(product); continue; }
            if (TryStore(product)) continue;
            if (!OverlapsZone(product)) ForgetCandidate(product);
            else if (!product.Grab.isSelected) SetContentsCollisions(product, false);
        }
    }

    public bool TryStore(StoreProduct product)
    {
        if (product == null || product.Grab.isSelected || product.Basket != null || product.Body.isKinematic) return false;
        Vector3 local = storageZone.transform.InverseTransformPoint(product.Body.worldCenterOfMass);
        if (!new Bounds(storageZone.center, storageZone.size).Contains(local)) return false;
        foreach (var stored in products)
            if (stored != null) stored.SyncStoredPose();
        Bounds item = LocalBounds(product);
        if (!TryFindPosition(item, out var center)) return false;
        Vector3 position = product.transform.position + transform.TransformVector(center - item.center);
        SetBasketCollisions(product, true);
        SetContentsCollisions(product, true);
        // An item can have been held inside before this newly stored one arrived.
        foreach (var incoming in candidates)
            SetPairCollisions(product, incoming, true);
        product.StoreIn(this, position, product.transform.rotation);
        products.Add(product);
        candidates.Remove(product);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayAtPosition(placementSound, product.transform.position);
        return true;
    }

    private bool TryFindPosition(Bounds item, out Vector3 center)
    {
        center = default;
        Vector3 min = storageZone.center - storageZone.size * .5f;
        Vector3 max = storageZone.center + storageZone.size * .5f;
        Vector3 half = item.extents;
        if (item.size.x > storageZone.size.x || item.size.z > storageZone.size.z) return false;
        var occupied = new List<Bounds>(products.Count);
        var xs = new List<float> { Mathf.Clamp(item.center.x, min.x + half.x, max.x - half.x), min.x + half.x, max.x - half.x };
        var zs = new List<float> { Mathf.Clamp(item.center.z, min.z + half.z, max.z - half.z), min.z + half.z, max.z - half.z };
        var levels = new List<float> { min.y };
        foreach (var stored in products)
        {
            if (stored == null) continue;
            Bounds b = LocalBounds(stored);
            occupied.Add(b);
            xs.Add(b.min.x - half.x - Gap); xs.Add(b.max.x + half.x + Gap);
            zs.Add(b.min.z - half.z - Gap); zs.Add(b.max.z + half.z + Gap);
            levels.Add(b.max.y + Gap);
        }
        levels.Sort();
        // First use available floor space; stack only if the lower level is full.
        foreach (float level in levels)
        {
            if (level > max.y + Epsilon) continue;
            bool found = false;
            float bestDistance = float.PositiveInfinity;
            foreach (float x in xs)
                foreach (float z in zs)
                {
                    if (x - half.x < min.x - Epsilon || x + half.x > max.x + Epsilon ||
                        z - half.z < min.z - Epsilon || z + half.z > max.z + Epsilon) continue;
                    var candidate = new Bounds(new Vector3(x, level + half.y, z), item.size);
                    bool overlaps = false;
                    foreach (Bounds b in occupied)
                        if (candidate.min.x < b.max.x + Gap - Epsilon && candidate.max.x > b.min.x - Gap + Epsilon &&
                            candidate.min.y < b.max.y + Gap - Epsilon && candidate.max.y > b.min.y - Gap + Epsilon &&
                            candidate.min.z < b.max.z + Gap - Epsilon && candidate.max.z > b.min.z - Gap + Epsilon)
                        { overlaps = true; break; }
                    if (overlaps) continue;
                    // Pack from one corner so the first item does not split the
                    // remaining floor into two gaps too narrow for the next one.
                    float distance = (new Vector2(x, z) - new Vector2(min.x + half.x, min.z + half.z)).sqrMagnitude;
                    if (distance < bestDistance)
                    { bestDistance = distance; center = candidate.center; found = true; }
                }
            if (found) return true;
        }
        return false;
    }

    public void Remove(StoreProduct product)
    {
        products.Remove(product);
        if (product == null) return;
        SetBasketCollisions(product, false);
        if (OverlapsZone(product))
        {
            candidates.Add(product);
            SetContentsCollisions(product, true);
        }
        else ForgetCandidate(product);
    }

    private bool OverlapsZone(StoreProduct product)
    {
        foreach (var collider in product.Colliders)
            if (collider != null && collider.enabled && collider.bounds.Intersects(storageZone.bounds)) return true;
        return false;
    }

    private void ForgetCandidate(StoreProduct product)
    {
        candidates.Remove(product);
        SetContentsCollisions(product, false);
        foreach (var incoming in candidates) SetPairCollisions(product, incoming, false);
    }

    private void SetContentsCollisions(StoreProduct product, bool ignore)
    {
        foreach (var stored in products) SetPairCollisions(product, stored, ignore);
    }

    private static void SetPairCollisions(StoreProduct first, StoreProduct second, bool ignore)
    {
        if (first == null || second == null || first == second) return;
        foreach (var a in first.Colliders)
            foreach (var b in second.Colliders)
                if (a != null && b != null && !a.isTrigger && !b.isTrigger) Physics.IgnoreCollision(a, b, ignore);
    }

    private void SetBasketCollisions(StoreProduct product, bool ignore)
    {
        foreach (var productCollider in product.Colliders)
            foreach (var basketCollider in basketColliders)
                if (productCollider != null && basketCollider != null && !basketCollider.isTrigger)
                    Physics.IgnoreCollision(productCollider, basketCollider, ignore);
    }

    private Bounds LocalBounds(StoreProduct product)
    {
        Renderer visual = product.GetComponent<Renderer>();
        Bounds mesh = visual.localBounds;
        Bounds result = new Bounds(transform.InverseTransformPoint(visual.transform.TransformPoint(mesh.min)), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3((i & 1) == 0 ? mesh.min.x : mesh.max.x,
                (i & 2) == 0 ? mesh.min.y : mesh.max.y, (i & 4) == 0 ? mesh.min.z : mesh.max.z);
            result.Encapsulate(transform.InverseTransformPoint(visual.transform.TransformPoint(corner)));
        }
        return result;
    }
}
