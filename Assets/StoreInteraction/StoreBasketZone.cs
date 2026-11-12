using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class StoreBasketZone : MonoBehaviour
{
    private StoreBasket basket;
    private void Awake() => basket = GetComponentInParent<StoreBasket>();
    private void OnTriggerEnter(Collider other) => basket.Track(other);
    private void OnTriggerStay(Collider other) => basket.Track(other);
    private void OnTriggerExit(Collider other) => basket.Untrack(other);
}
