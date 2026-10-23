using Android.Content;
using Library.Slvz.Poolakeybridge;
using SLVZ.Billing.Core;
using System.Text.Json;


namespace SLVZ.Billing.Bazaar;

public class Billing
{

    private static TaskCompletionSource<(bool Status, PurchaseModel Product)>? purchaseTcs;
    private static TaskCompletionSource<(bool Status, PurchaseModel Product)>? subscriptionTcs;

    private static TaskCompletionSource<(bool Status, string ProductID)>? consumePurchaseTcs;



    private static TaskCompletionSource<(bool Status, List<ItemModel> Products)>? queryTcs;
    private static TaskCompletionSource<(bool Status, List<PurchaseModel> Products)>? checkSubscriptionTcs;
    private static TaskCompletionSource<(bool Status, List<PurchaseModel> Products)>? checkPurchaseTcs;



    internal static async Task<bool> Initialize()
    {
        Core.Billing.initTcs = new TaskCompletionSource<bool>();
        PoolakeyHelper.Initialize(Core.Billing.Context, Core.Billing.RsaKey);
        return await Core.Billing.initTcs.Task;
    }

    public static async Task<(bool Status, PurchaseModel Product)> Purchase(string productId, string payload = "", bool consumeAfter = false)
    {
        if (!Core.Billing.Initialized)
        {
            var result = await Initialize();
            if (!result)
                return (false, new PurchaseModel());
        }

        purchaseTcs = new TaskCompletionSource<(bool Status, PurchaseModel Product)>();
        PoolakeyHelper.Purchase(productId, payload, consumeAfter);
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
        PoolakeyHelper.Subscribe(productId, payload);
        return await subscriptionTcs.Task;
    }

    public static async Task<(bool Status, List<ItemModel> Products)> AllInappItems()
    {
        if (!Core.Billing.Initialized)
        {
            var result = await Initialize();
            if (!result)
                return (false, new List<ItemModel>());
        }

        queryTcs = new TaskCompletionSource<(bool Status, List<ItemModel> Products)>();
        PoolakeyHelper.AllAvailibleInappItems(Core.Billing.ProductIDs);
        return await queryTcs.Task;
    }

    public static async Task<(bool Status, List<ItemModel> Products)> AllSubItems()
    {
        if (!Core.Billing.Initialized)
        {
            var result = await Initialize();
            if (!result)
                return (false, new List<ItemModel>());
        }

        queryTcs = new TaskCompletionSource<(bool Status, List<ItemModel> Products)>();
        PoolakeyHelper.AllAvailibleSubItems(Core.Billing.ProductIDs);
        return await queryTcs.Task;
    }

    public static async Task<(bool Status, List<PurchaseModel> Products)> CheckPurchases()
    {
        if (!Core.Billing.Initialized)
        {
            var result = await Initialize();
            if (!result)
                return (false, new List<PurchaseModel>());
        }

        checkPurchaseTcs = new TaskCompletionSource<(bool Status, List<PurchaseModel> Products)>();
        PoolakeyHelper.PurchasesCheck();
        return await checkPurchaseTcs.Task;
    }

    public static async Task<(bool Status, List<PurchaseModel> Products)> CheckSubscriptions()
    {
        if (!Core.Billing.Initialized)
        {
            var result = await Initialize();
            if (!result)
                return (false, new List<PurchaseModel>());
        }

        checkSubscriptionTcs = new TaskCompletionSource<(bool Status, List<PurchaseModel> Products)>();
        PoolakeyHelper.SubscriptionCheck();
        return await checkSubscriptionTcs.Task;
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
        PoolakeyHelper.Consume(purchase.purchaseToken, purchase.productId);
        return await consumePurchaseTcs.Task;
    }


    internal static void RegisterReceiver()
    {
        var filter = new IntentFilter("library.slvz.poolakeybridge.RESULT");
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

            Console.WriteLine($"Bazaar payment: {message}");

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
                        Core.Billing.initTcs?.TrySetResult(true);
                        break;
                    default:
                        Core.Billing.Initialized = false;
                        Core.Billing.initTcs?.TrySetResult(false);
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
                        queryTcs?.TrySetResult((true, model));
                        break;
                    default:
                        queryTcs?.TrySetResult((false, new List<ItemModel>()));
                        break;
                }
            }
            else if (type.StartsWith("all_purchase"))
            {
                switch (type)
                {
                    case "all_purchase_success":
                        var model = JsonSerializer.Deserialize<List<PurchaseModel>>(product);
                        checkPurchaseTcs?.TrySetResult((true, model));
                        break;
                    case "all_purchase_failed":
                        checkPurchaseTcs?.TrySetResult((false, new List<PurchaseModel>()));
                        break;
                    default:
                        checkPurchaseTcs?.TrySetResult((false, new List<PurchaseModel>()));
                        break;
                }
            }
            else if (type.StartsWith("all_subscribe"))
            {
                switch (type)
                {
                    case "all_subscribe_success":
                        var model = JsonSerializer.Deserialize<List<PurchaseModel>>(product);
                        checkSubscriptionTcs?.TrySetResult((true, model));
                        break;
                    case "all_subscribe_failed":
                        checkSubscriptionTcs?.TrySetResult((false, new List<PurchaseModel>()));
                        break;
                    default:
                        checkSubscriptionTcs?.TrySetResult((false, new List<PurchaseModel>()));
                        break;
                }
            }
            else
            {
                checkSubscriptionTcs?.TrySetResult((false, new List<PurchaseModel>()));
                checkPurchaseTcs?.TrySetResult((false, new List<PurchaseModel>()));
                queryTcs?.TrySetResult((false, new List<ItemModel>()));
                consumePurchaseTcs?.TrySetResult((false, ""));
                subscriptionTcs?.TrySetResult((false, new PurchaseModel()));
                purchaseTcs?.TrySetResult((false, new PurchaseModel()));
            }




        }
    }



}
