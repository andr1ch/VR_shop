using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class StoreCheckoutZone : MonoBehaviour
{
    [SerializeField] private StoreCashierController cashier;
    private void OnTriggerEnter(Collider other) => cashier.Track(other);
    private void OnTriggerStay(Collider other) => cashier.Track(other);
    private void OnTriggerExit(Collider other) => cashier.Untrack(other);
}
