using System.Collections.Generic;
using UnityEngine;

internal interface IStoreCashierState
{
    void Enter();
    void Update();
    void Exit();
}

internal sealed class StoreCashierWaitingState : IStoreCashierState
{
    private readonly StoreCashierController cashier;
    public StoreCashierWaitingState(StoreCashierController cashier) => this.cashier = cashier;
    public void Enter() => cashier.ShowWaiting();
    public void Update()
    {
        var basket = cashier.FindReadyBasket();
        if (basket != null) cashier.Process(basket);
    }
    public void Exit() { }
}

internal sealed class StoreCashierProcessingState : IStoreCashierState
{
    private readonly StoreCashierController cashier;
    private readonly List<StoreProduct> purchase = new List<StoreProduct>();
    private float started;
    public StoreBasket Basket { private get; set; }
    public StoreCashierProcessingState(StoreCashierController cashier) => this.cashier = cashier;
    public void Enter()
    {
        Basket.CopyUnpaidProducts(purchase);
        started = Time.time;
        cashier.Show(StoreCheckoutStatus.Processing, "Оплата...\nНе забирайте корзину", new Color(.9f, .6f, .12f));
    }
    public void Update()
    {
        // Taking the basket or changing its contents cancels this transaction.
        if (!cashier.CanProcess(Basket, purchase)) { cashier.Cancel(Basket); return; }
        if (Time.time - started < cashier.ProcessingSeconds) return;
        var basket = Basket;
        cashier.CompletePayment(purchase);
        cashier.Finish(basket);
    }
    public void Exit() { purchase.Clear(); Basket = null; }
}

internal sealed class StoreCashierPaidState : IStoreCashierState
{
    private readonly StoreCashierController cashier;
    public StoreBasket Basket { private get; set; }
    public StoreCashierPaidState(StoreCashierController cashier) => this.cashier = cashier;
    public void Enter() => cashier.Show(StoreCheckoutStatus.Paid,
        "Оплачено!\nЗаберите корзину", new Color(.2f, .7f, .3f));
    public void Update()
    {
        if (!cashier.Contains(Basket) || Basket.UnpaidCount > 0) cashier.Wait();
    }
    public void Exit() => Basket = null;
}
