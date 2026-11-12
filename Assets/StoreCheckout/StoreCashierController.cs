using System.Collections.Generic;
using UnityEngine;

public enum StoreCheckoutStatus { Waiting, Processing, Paid }

public sealed class StoreCashierController : MonoBehaviour
{
    [SerializeField] private BoxCollider checkoutZone;
    [SerializeField] private Renderer placementMarker;
    [SerializeField] private TextMesh statusText;
    [SerializeField] private Transform paymentSoundPosition;
    [SerializeField, Min(.1f)] private float processingSeconds = 1.25f;
    [SerializeField] private string paymentSound = "checkout";
    private readonly HashSet<StoreBasket> baskets = new HashSet<StoreBasket>();
    private MaterialPropertyBlock markerProperties;
    private IStoreCashierState state;
    private StoreCashierWaitingState waiting;
    private StoreCashierProcessingState processing;
    private StoreCashierPaidState paid;
    private StoreBasket cancelledBasket;

    public StoreCheckoutStatus Status { get; private set; }
    public int PaymentCount { get; private set; }
    public int LastPurchasedCount { get; private set; }
    public float ProcessingSeconds => processingSeconds;

    private void Awake()
    {
        markerProperties = new MaterialPropertyBlock();
        waiting = new StoreCashierWaitingState(this);
        processing = new StoreCashierProcessingState(this);
        paid = new StoreCashierPaidState(this);
    }

    private void OnEnable() => ChangeState(waiting);
    private void Update() => state?.Update();
    private void OnDisable()
    {
        state?.Exit();
        state = null;
        baskets.Clear();
        cancelledBasket = null;
    }

    public void Track(Collider other)
    {
        if (other.attachedRigidbody == null) return;
        var basket = other.attachedRigidbody.GetComponent<StoreBasket>();
        if (basket != null) baskets.Add(basket);
    }

    public void Untrack(Collider other)
    {
        if (other.attachedRigidbody == null) return;
        var basket = other.attachedRigidbody.GetComponent<StoreBasket>();
        if (basket != null && !Contains(basket)) baskets.Remove(basket);
    }

    public bool Contains(StoreBasket basket)
    {
        if (basket == null || !basket.gameObject.activeInHierarchy || checkoutZone == null) return false;
        Vector3 local = checkoutZone.transform.InverseTransformPoint(basket.transform.position);
        return new Bounds(checkoutZone.center, checkoutZone.size).Contains(local);
    }

    internal StoreBasket FindReadyBasket()
    {
        if (cancelledBasket != null && (!Contains(cancelledBasket) || cancelledBasket.IsHeld))
        {
            cancelledBasket = null;
            ShowWaiting();
        }
        baskets.RemoveWhere(b => b == null);
        foreach (var basket in baskets)
            if (basket != cancelledBasket && Contains(basket) && !basket.IsHeld && basket.UnpaidCount > 0) return basket;
        return null;
    }

    internal bool CanProcess(StoreBasket basket, List<StoreProduct> purchase)
    {
        if (!Contains(basket) || basket.IsHeld || basket.UnpaidCount != purchase.Count || purchase.Count == 0) return false;
        foreach (var product in purchase)
            if (product == null || product.Basket != basket || product.IsPaid || product.Grab.isSelected) return false;
        return true;
    }

    internal void CompletePayment(List<StoreProduct> purchase)
    {
        foreach (var product in purchase) product.MarkPaid();
        LastPurchasedCount = purchase.Count;
        PaymentCount++;
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayAtPosition(paymentSound,
                paymentSoundPosition != null ? paymentSoundPosition.position : transform.position);
    }

    internal void Wait() => ChangeState(waiting);
    internal void Cancel(StoreBasket basket)
    {
        cancelledBasket = basket;
        Wait();
    }
    internal void ShowWaiting() => Show(StoreCheckoutStatus.Waiting,
        cancelledBasket != null ? "Оплата отменена\nПоставьте снова" : "Поставьте корзину\nдля оплаты",
        new Color(.13f, .55f, .7f));
    internal void Process(StoreBasket basket)
    {
        processing.Basket = basket;
        ChangeState(processing);
    }
    internal void Finish(StoreBasket basket)
    {
        paid.Basket = basket;
        ChangeState(paid);
    }

    private void ChangeState(IStoreCashierState next)
    {
        if (state == next) return;
        state?.Exit();
        state = next;
        state?.Enter();
    }

    internal void Show(StoreCheckoutStatus status, string message, Color color)
    {
        Status = status;
        if (statusText != null) statusText.text = message;
        if (placementMarker == null) return;
        placementMarker.GetPropertyBlock(markerProperties);
        markerProperties.SetColor("_BaseColor", color);
        markerProperties.SetColor("_Color", color);
        placementMarker.SetPropertyBlock(markerProperties);
    }
}
