using Android.Content;
using Library.Slvz.Poolakeybridge;
using System.Text.Json;


namespace SLVZ.Billing;

public class InAppBilling
{
    internal static bool Initialized;
    internal static string RsaKey = string.Empty;
    private static List<string> ProductIDs = new List<string>();
    private static Context? Context;
    internal static TaskCompletionSource<bool>? InitTcs;

    public static void Initialize(Context context, string rsaKey, List<string> productIds)
    {
        InitTcs = new TaskCompletionSource<bool>();
        if (Initialized) return;

        ProductIDs = productIds;
        Context = context;
        RsaKey = rsaKey;

        RegisterReceiver();

        PoolakeyHelper.Initialize(context, rsaKey);

        //Initialized = true;
    }


    internal static TaskCompletionSource<(bool Status, PurchaseModel Product)>? purchaseTcs;
    internal static TaskCompletionSource<(bool Status, PurchaseModel Product)>? subscriptionTcs;
    internal static TaskCompletionSource<(bool Status, string ProductID)>? consumePurchaseTcs;
    internal static TaskCompletionSource<(bool Status, List<ItemModel> Products)>? queryTcs;
    internal static TaskCompletionSource<(bool Status, List<PurchaseModel> Products)>? checkSubscriptionTcs;
    internal static TaskCompletionSource<(bool Status, List<PurchaseModel> Products)>? checkPurchaseTcs;


    public static async Task<(bool Status, PurchaseModel Product)> Purchase(string productId, string payload = "", bool consumeAfter = false)
    {
        if (!Initialized)
            throw new InvalidOperationException(
            "SLVZ.Billing is not initialized. Call Billing.Initialize(context, rsaKey, productIds) once at app start (e.g. in MainActivity.OnCreate) before using any billing method.");

        purchaseTcs = new TaskCompletionSource<(bool Status, PurchaseModel Product)>();
        PoolakeyHelper.Purchase(productId, payload, consumeAfter);
        return await purchaseTcs.Task;
    }

    public static async Task<(bool Status, PurchaseModel Product)> Subscription(string productId, string payload = "")
    {
        if (!Initialized)
            throw new InvalidOperationException(
            "SLVZ.Billing is not initialized. Call Billing.Initialize(context, rsaKey, productIds) once at app start (e.g. in MainActivity.OnCreate) before using any billing method.");


        subscriptionTcs = new TaskCompletionSource<(bool Status, PurchaseModel Product)>();
        PoolakeyHelper.Subscribe(productId, payload);
        return await subscriptionTcs.Task;
    }

    public static async Task<(bool Status, List<ItemModel> Products)> AllInappItems()
    {
        if (!Initialized)
            throw new InvalidOperationException(
            "SLVZ.Billing is not initialized. Call Billing.Initialize(context, rsaKey, productIds) once at app start (e.g. in MainActivity.OnCreate) before using any billing method.");


        queryTcs = new TaskCompletionSource<(bool Status, List<ItemModel> Products)>();
        PoolakeyHelper.AllAvailibleInappItems(ProductIDs);
        return await queryTcs.Task;
    }

    public static async Task<(bool Status, List<ItemModel> Products)> AllSubItems()
    {
        if (!Initialized)
            throw new InvalidOperationException(
            "SLVZ.Billing is not initialized. Call Billing.Initialize(context, rsaKey, productIds) once at app start (e.g. in MainActivity.OnCreate) before using any billing method.");


        queryTcs = new TaskCompletionSource<(bool Status, List<ItemModel> Products)>();
        PoolakeyHelper.AllAvailibleSubItems(ProductIDs);
        return await queryTcs.Task;
    }

    public static async Task<(bool Status, List<PurchaseModel> Products)> CheckPurchases()
    {
        if (!Initialized)
            throw new InvalidOperationException(
            "SLVZ.Billing is not initialized. Call Billing.Initialize(context, rsaKey, productIds) once at app start (e.g. in MainActivity.OnCreate) before using any billing method.");


        checkPurchaseTcs = new TaskCompletionSource<(bool Status, List<PurchaseModel> Products)>();
        PoolakeyHelper.PurchasesCheck();
        return await checkPurchaseTcs.Task;
    }

    public static async Task<(bool Status, List<PurchaseModel> Products)> CheckSubscriptions()
    {
        if (!Initialized)
            throw new InvalidOperationException(
            "SLVZ.Billing is not initialized. Call Billing.Initialize(context, rsaKey, productIds) once at app start (e.g. in MainActivity.OnCreate) before using any billing method.");


        checkSubscriptionTcs = new TaskCompletionSource<(bool Status, List<PurchaseModel> Products)>();
        PoolakeyHelper.SubscriptionCheck();
        return await checkSubscriptionTcs.Task;
    }

    public static async Task<(bool Status, string ProductID)> Consume(PurchaseModel purchase)
    {
        if (!Initialized)
            throw new InvalidOperationException(
            "SLVZ.Billing is not initialized. Call Billing.Initialize(context, rsaKey, productIds) once at app start (e.g. in MainActivity.OnCreate) before using any billing method.");


        consumePurchaseTcs = new TaskCompletionSource<(bool Status, string ProductID)>();
        PoolakeyHelper.Consume(purchase.purchaseToken, purchase.productId);
        return await consumePurchaseTcs.Task;
    }


    internal static void RegisterReceiver()
    {
        var filter = new IntentFilter("library.slvz.poolakeybridge.RESULT");
        var receiver = new PoolakeyReceiver();
        Context!.RegisterReceiver(receiver, filter, ReceiverFlags.NotExported);
    }


}

[BroadcastReceiver(Enabled = true, Exported = false)]
internal class PoolakeyReceiver : BroadcastReceiver
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
                    InAppBilling.queryTcs?.TrySetResult((true, model));
                    break;
                default:
                    InAppBilling.queryTcs?.TrySetResult((false, new List<ItemModel>()));
                    break;
            }
        }
        else if (type.StartsWith("all_purchase"))
        {
            switch (type)
            {
                case "all_purchase_success":
                    var model = JsonSerializer.Deserialize<List<PurchaseModel>>(product);
                    InAppBilling.checkPurchaseTcs?.TrySetResult((true, model));
                    break;
                case "all_purchase_failed":
                    InAppBilling.checkPurchaseTcs?.TrySetResult((false, new List<PurchaseModel>()));
                    break;
                default:
                    InAppBilling.checkPurchaseTcs?.TrySetResult((false, new List<PurchaseModel>()));
                    break;
            }
        }
        else if (type.StartsWith("all_subscribe"))
        {
            switch (type)
            {
                case "all_subscribe_success":
                    var model = JsonSerializer.Deserialize<List<PurchaseModel>>(product);
                    InAppBilling.checkSubscriptionTcs?.TrySetResult((true, model));
                    break;
                case "all_subscribe_failed":
                    InAppBilling.checkSubscriptionTcs?.TrySetResult((false, new List<PurchaseModel>()));
                    break;
                default:
                    InAppBilling.checkSubscriptionTcs?.TrySetResult((false, new List<PurchaseModel>()));
                    break;
            }
        }
        else
        {
            InAppBilling.checkSubscriptionTcs?.TrySetResult((false, new List<PurchaseModel>()));
            InAppBilling.checkPurchaseTcs?.TrySetResult((false, new List<PurchaseModel>()));
            InAppBilling.queryTcs?.TrySetResult((false, new List<ItemModel>()));
            InAppBilling.consumePurchaseTcs?.TrySetResult((false, ""));
            InAppBilling.subscriptionTcs?.TrySetResult((false, new PurchaseModel()));
            InAppBilling.purchaseTcs?.TrySetResult((false, new PurchaseModel()));
        }




    }
}