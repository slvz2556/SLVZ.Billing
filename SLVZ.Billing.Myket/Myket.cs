using Android.Content;
using Library.Slvz.Myketbridge;
using SLVZ.Billing.Core;
using System.Text.Json;

namespace SLVZ.Billing;

public class InAppBilling
{
    internal static bool Initialized;
    internal static string RsaKey = string.Empty;
    internal static List<string> ProductIDs = new List<string>();
    internal static Context? Context;
    internal static TaskCompletionSource<bool>? InitTcs;

    public static void Initialize(Context context, string Key, List<string> skus)
    {
        InitTcs = new TaskCompletionSource<bool>();
        if (Initialized) return;

        Context = context;
        RsaKey = Key;
        ProductIDs = skus;

        RegisterReceiver();
        MyketBridge.Initialize(context, Key);

        purchaseTcs = new TaskCompletionSource<(bool Status, PurchaseModel Product)>();
        subscriptionTcs = new TaskCompletionSource<(bool Status, PurchaseModel Product)>();
        consumePurchaseTcs = new TaskCompletionSource<(bool Status, string ProductID)>();
        allItemsTcs = new TaskCompletionSource<(bool Status, List<ItemModel> Products)>();
    }



    internal static TaskCompletionSource<(bool Status, PurchaseModel Product)>? purchaseTcs;
    internal static TaskCompletionSource<(bool Status, PurchaseModel Product)>? subscriptionTcs;
    internal static TaskCompletionSource<(bool Status, string ProductID)>? consumePurchaseTcs;
    internal static TaskCompletionSource<(bool Status, List<ItemModel> Products)>? allItemsTcs;



    public static async Task<(bool Status, PurchaseModel Product)> Purchase(string productId, string payload = "")
    {
        if (!Initialized)
            throw new InvalidOperationException(
            "SLVZ.Billing is not initialized. Call Billing.Initialize(context, rsaKey, productIds) once at app start (e.g. in MainActivity.OnCreate) before using any billing method.");


        purchaseTcs = new TaskCompletionSource<(bool Status, PurchaseModel Product)>();
        MyketBridge.Purchase(productId, payload);
        return await purchaseTcs.Task;
    }
    public static async Task<(bool Status, PurchaseModel Product)> Subscription(string productId, string payload = "")
    {
        if (!Initialized)
            throw new InvalidOperationException(
            "SLVZ.Billing is not initialized. Call Billing.Initialize(context, rsaKey, productIds) once at app start (e.g. in MainActivity.OnCreate) before using any billing method.");


        subscriptionTcs = new TaskCompletionSource<(bool Status, PurchaseModel Product)>();
        MyketBridge.Subscribe(productId, payload);
        return await subscriptionTcs.Task;
    }
    public static async Task<(bool Status, string ProductID)> Consume(PurchaseModel purchase)
    {
        if (!Initialized)
            throw new InvalidOperationException(
            "SLVZ.Billing is not initialized. Call Billing.Initialize(context, rsaKey, productIds) once at app start (e.g. in MainActivity.OnCreate) before using any billing method.");


        consumePurchaseTcs = new TaskCompletionSource<(bool Status, string ProductID)>();
        MyketBridge.Consume(purchase.type, purchase.originalJson, purchase.signature);
        return await consumePurchaseTcs.Task;
    }
    public static async Task<(bool Status, List<ItemModel> Products)> AllAvailibleItems()
    {
        if (!Initialized)
            throw new InvalidOperationException(
            "SLVZ.Billing is not initialized. Call Billing.Initialize(context, rsaKey, productIds) once at app start (e.g. in MainActivity.OnCreate) before using any billing method.");


        allItemsTcs = new TaskCompletionSource<(bool Status, List<ItemModel> Products)>();
        MyketBridge.AllAvailibleItems(ProductIDs);
        return await allItemsTcs.Task;
    }




    private static void RegisterReceiver()
    {
        var filter = new IntentFilter("library.slvz.myketbridge.RESULT");
        var receiver = new MyketReceiver();
        Context!.RegisterReceiver(receiver, filter, ReceiverFlags.NotExported);
    }



}

[BroadcastReceiver(Enabled = true, Exported = false)]
internal class MyketReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent == null ) return;
        var type = intent.GetStringExtra("result_type") ?? "";
        var message = intent.GetStringExtra("result_message") ?? "";
        var product = intent.GetStringExtra("result_product") ?? "";

        Console.WriteLine($"Myket payment: {message}");

        if (type.StartsWith("purchase"))
        {
            switch (type)
            {
                case "purchase_success":
                    var model = JsonSerializer.Deserialize<PurchaseModel>(product);
                    InAppBilling.purchaseTcs?.TrySetResult((true, model));
                    break;
                case "purchase_failed":
                    InAppBilling.purchaseTcs?.TrySetResult((false, new PurchaseModel()));
                    break;
                default:
                    InAppBilling.purchaseTcs?.TrySetResult((false, new PurchaseModel()));
                    break;
            }
        }
        else if (type.StartsWith("subscribe"))
        {
            switch (type)
            {
                case "subscribe_success":
                    var model = JsonSerializer.Deserialize<PurchaseModel>(product);
                    InAppBilling.subscriptionTcs?.TrySetResult((true, model));
                    break;
                case "subscribe_failed":
                    InAppBilling.subscriptionTcs?.TrySetResult((false, new PurchaseModel()));
                    break;
                default:
                    InAppBilling.subscriptionTcs?.TrySetResult((false, new PurchaseModel()));
                    break;
            }
        }
        else if (type.StartsWith("init"))
        {
            switch (type)
            {
                case "init_success":
                    InAppBilling.Initialized = true;
                    InAppBilling.InitTcs?.TrySetResult(true);
                    break;
                default:
                    InAppBilling.Initialized = false;
                    InAppBilling.InitTcs?.TrySetResult(false);
                    break;
            }
        }
        else if (type.StartsWith("consume"))
        {
            switch (type)
            {
                case "consume_success":
                    InAppBilling.consumePurchaseTcs?.TrySetResult((true, product));
                    break;
                case "consume_failed":
                    InAppBilling.consumePurchaseTcs?.TrySetResult((false, ""));
                    break;
                default:
                    InAppBilling.consumePurchaseTcs?.TrySetResult((false, ""));
                    break;
            }
        }
        else if (type.StartsWith("query"))
        {
            switch (type)
            {
                case "query_success":
                    var model = JsonSerializer.Deserialize<List<ItemModel>>(product);
                    InAppBilling.allItemsTcs?.TrySetResult((true, model));
                    break;
                case "query_failed":
                    InAppBilling.allItemsTcs?.TrySetResult((false, new List<ItemModel>()));
                    break;
                default:
                    InAppBilling.allItemsTcs?.TrySetResult((false, new List<ItemModel>()));
                    break;
            }
        }
        else
        {
            InAppBilling.allItemsTcs?.TrySetResult((false, new List<ItemModel>()));
            InAppBilling.consumePurchaseTcs?.TrySetResult((false, ""));
            InAppBilling.subscriptionTcs?.TrySetResult((false, new PurchaseModel()));
            InAppBilling.purchaseTcs?.TrySetResult((false, new PurchaseModel()));
        }




    }
}

