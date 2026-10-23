using Android.Content;
using Library.Slvz.Myketbridge;
using SLVZ.Billing.Core;
using System.Text.Json;

namespace SLVZ.Billing.Myket;

public class Billing
{
    
    private static TaskCompletionSource<(bool Status, PurchaseModel Product)>? purchaseTcs;
    private static TaskCompletionSource<(bool Status, PurchaseModel Product)>? subscriptionTcs;

    private static TaskCompletionSource<(bool Status, string ProductID)>? consumePurchaseTcs;

    private static TaskCompletionSource<(bool Status, List<ItemModel> Products)>? allItemsTcs;


    
    private static async Task<bool> Initialize()
    {
        Core.Billing.InitTcs = new TaskCompletionSource<bool>();
        MyketBridge.Initialize(Core.Billing.Context, Core.Billing.RsaKey);
        return await Core.Billing.InitTcs.Task;
    }


    public static async Task<(bool Status, PurchaseModel Product)> Purchase(string productId, string payload = "")
    {
        if (!Core.Billing.Initialized)
        {
            var result = await Initialize();
            if (!result)
                return (false, new PurchaseModel());
        }

        purchaseTcs = new TaskCompletionSource<(bool Status, PurchaseModel Product)>();
        MyketBridge.Purchase(productId, payload);
        return await purchaseTcs.Task;
    }

    public static async Task<(bool Status, PurchaseModel Product)> Subscription(string productId, string payload = "")
    {
        if (!Core.Billing.Initialized)
        {
            var result = await Initialize();
            if (!result)
                return (false, new PurchaseModel());
        }

        subscriptionTcs = new TaskCompletionSource<(bool Status, PurchaseModel Product)>();
        MyketBridge.Subscribe(productId, payload);
        return await subscriptionTcs.Task;
    }

    public static async Task<(bool Status, string ProductID)> Consume(PurchaseModel purchase)
    {
        if (!Core.Billing.Initialized)
        {
            var result = await Initialize();
            if (!result)
                return (false, "");
        }

        consumePurchaseTcs = new TaskCompletionSource<(bool Status, string ProductID)>();
        MyketBridge.Consume(purchase.type, purchase.originalJson, purchase.signature);
        return await consumePurchaseTcs.Task;
    }

    public static async Task<(bool Status, List<ItemModel> Products)> AllAvailibleItems()
    {
        if (!Core.Billing.Initialized)
        {
            var result = await Initialize();
            if (!result)
                return (false, new List<ItemModel>());
        }

        allItemsTcs = new TaskCompletionSource<(bool Status, List<ItemModel> Products)>();
        MyketBridge.AllAvailibleItems(Core.Billing.ProductIDs);
        return await allItemsTcs.Task;
    }




    internal static void RegisterReceiver()
    {
        var filter = new IntentFilter("library.slvz.myketbridge.RESULT");
        var receiver = new PoolakeyReceiver();
        Core.Billing.Context!.RegisterReceiver(receiver, filter, ReceiverFlags.NotExported);
    }


    [BroadcastReceiver(Enabled = true, Exported = false)]
    private class PoolakeyReceiver : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent == null) return;
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
                        purchaseTcs?.TrySetResult((true, model));
                        break;
                    case "purchase_failed":
                        purchaseTcs?.TrySetResult((false, new PurchaseModel()));
                        break;
                    default:
                        purchaseTcs?.TrySetResult((false, new PurchaseModel()));
                        break;
                }
            }
            else if (type.StartsWith("subscribe"))
            {
                switch (type)
                {
                    case "subscribe_success":
                        var model = JsonSerializer.Deserialize<PurchaseModel>(product);
                        subscriptionTcs?.TrySetResult((true, model));
                        break;
                    case "subscribe_failed":
                        subscriptionTcs?.TrySetResult((false, new PurchaseModel()));
                        break;
                    default:
                        subscriptionTcs?.TrySetResult((false, new PurchaseModel()));
                        break;
                }
            }
            else if (type.StartsWith("init"))
            {
                switch (type)
                {
                    case "init_success":
                        Core.Billing.Initialized = true;
                        Core.Billing.InitTcs?.TrySetResult(true);
                        break;
                    default:
                        Core.Billing.Initialized = false;
                        Core.Billing.InitTcs?.TrySetResult(false);
                        break;
                }
            }
            else if (type.StartsWith("consume"))
            {
                switch (type)
                {
                    case "consume_success":
                        consumePurchaseTcs?.TrySetResult((true, product));
                        break;
                    case "consume_failed":
                        consumePurchaseTcs?.TrySetResult((false, ""));
                        break;
                    default:
                        consumePurchaseTcs?.TrySetResult((false, ""));
                        break;
                }
            }
            else if (type.StartsWith("query"))
            {
                switch (type)
                {
                    case "query_success":
                        var model = JsonSerializer.Deserialize<List<ItemModel>>(product);
                        allItemsTcs?.TrySetResult((true, model));
                        break;
                    case "query_failed":
                        allItemsTcs?.TrySetResult((false, new List<ItemModel>()));
                        break;
                    default:
                        allItemsTcs?.TrySetResult((false, new List<ItemModel>()));
                        break;
                }
            }
            else
            {
                allItemsTcs?.TrySetResult((false, new List<ItemModel>()));
                consumePurchaseTcs?.TrySetResult((false, ""));
                subscriptionTcs?.TrySetResult((false, new PurchaseModel()));
                purchaseTcs?.TrySetResult((false, new PurchaseModel()));
            }




        }
    }

}

