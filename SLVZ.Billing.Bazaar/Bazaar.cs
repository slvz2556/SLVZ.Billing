using Android.Content;
using Library.Slvz.Poolakeybridge;
using SLVZ.Billing.Core;
using System.Text.Json;


namespace SLVZ.Billing.Bazaar;

public class Billing
{


    private static bool _isInitialized;
    private static string _rsaKey = string.Empty;
    private static List<string> ProductIDs = new List<string>();
    private static Context? _context;



    private static TaskCompletionSource<(bool Status, PurchaseModel Product)>? purchaseTcs;
    private static TaskCompletionSource<(bool Status, PurchaseModel Product)>? subscriptionTcs;

    private static TaskCompletionSource<(bool Status, string ProductID)>? consumePurchaseTcs;

    private static TaskCompletionSource<bool>? initTcs;

    private static TaskCompletionSource<(bool Status, List<ItemModel> Products)>? queryTcs;
    private static TaskCompletionSource<(bool Status, List<PurchaseModel> Products)>? checkSubscriptionTcs;
    private static TaskCompletionSource<(bool Status, List<PurchaseModel> Products)>? checkPurchaseTcs;


    public static void Initialize(Context context, string rsaKey, List<string> productIds)
    {
        initTcs = new TaskCompletionSource<bool>();
        if (_isInitialized) return;

        ProductIDs = productIds;
        _context = context;
        _rsaKey = rsaKey;

        RegisterReceiver();
        _isInitialized = true;

        PoolakeyHelper.Initialize(context, rsaKey);
    }

    private static async Task<bool> Initialize()
    {
        initTcs = new TaskCompletionSource<bool>();
        PoolakeyHelper.Initialize(_context, _rsaKey);
        return await initTcs.Task;
    }

    public static async Task<(bool Status, PurchaseModel Product)> Purchase(string productId, string payload = "", bool consumeAfter = false)
    {
        if (!_isInitialized)
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
        if (!_isInitialized)
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
        if (!_isInitialized)
        {
            var result = await Initialize();
            if (!result)
                return (false, new List<ItemModel>());
        }

        queryTcs = new TaskCompletionSource<(bool Status, List<ItemModel> Products)>();
        PoolakeyHelper.AllAvailibleInappItems(ProductIDs);
        return await queryTcs.Task;
    }

    public static async Task<(bool Status, List<ItemModel> Products)> AllSubItems()
    {
        if (!_isInitialized)
        {
            var result = await Initialize();
            if (!result)
                return (false, new List<ItemModel>());
        }

        queryTcs = new TaskCompletionSource<(bool Status, List<ItemModel> Products)>();
        PoolakeyHelper.AllAvailibleSubItems(ProductIDs);
        return await queryTcs.Task;
    }

    public static async Task<(bool Status, List<PurchaseModel> Products)> CheckPurchases()
    {
        if (!_isInitialized)
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
        if (!_isInitialized)
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
        if (!_isInitialized)
        {
            var result = await Initialize();
            if (!result)
                return (false, "");
        }

        consumePurchaseTcs = new TaskCompletionSource<(bool Status, string ProductID)>();
        PoolakeyHelper.Consume(purchase.purchaseToken, purchase.productId);
        return await consumePurchaseTcs.Task;
    }


    private static void RegisterReceiver()
    {
        var filter = new IntentFilter("library.slvz.poolakeybridge.RESULT");
        var receiver = new PoolakeyReceiver();
        _context!.RegisterReceiver(receiver, filter, ReceiverFlags.Exported);
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
                        _isInitialized = true;
                        initTcs?.TrySetResult(true);
                        break;
                    default:
                        _isInitialized = false;
                        initTcs?.TrySetResult(false);
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
