using UnityEngine;

// Register nearby shelf items with XRI; keep moving/held items registered.
// Their colliders stay enabled, so distant shelves still block rays and bodies.
public sealed class StoreInteractionRange : MonoBehaviour
{
    [SerializeField, Min(1f)] private float interactionRadius = 5f;
    [SerializeField, Min(.1f)] private float refreshInterval = .35f;
    private StoreProduct[] products;
    private Transform viewer;
    private float nextRefresh;

    private void Start()
    {
        products = GetComponentsInChildren<StoreProduct>();
        if (Camera.main != null) viewer = Camera.main.transform;
        Refresh();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + refreshInterval;
        if (viewer == null && Camera.main != null) viewer = Camera.main.transform;
        Refresh();
    }

    private void Refresh()
    {
        if (viewer == null) return;
        float squaredRadius = interactionRadius * interactionRadius;
        foreach (var product in products)
        {
            if (product == null) continue;
            var grab = product.Grab;
            bool active = grab.isSelected || grab.isHovered || product.Basket != null || !product.Body.isKinematic ||
                (product.transform.position - viewer.position).sqrMagnitude <= squaredRadius;
            if (grab.enabled != active) grab.enabled = active;
        }
    }
}
