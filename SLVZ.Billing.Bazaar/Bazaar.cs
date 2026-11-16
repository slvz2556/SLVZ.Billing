using Android.Content;
using Android.Runtime;
using AndroidX.AppCompat.App;
using IR.Cafebazaar.Poolakey;
using IR.Cafebazaar.Poolakey.Callback;
using IR.Cafebazaar.Poolakey.Config;
using IR.Cafebazaar.Poolakey.Entity;
using IR.Cafebazaar.Poolakey.Request;
using System.Text.Json;

namespace SLVZ.Billing;

public class InAppBilling
{
    internal static bool Initialized;
    internal static Payment? Pay;
    internal static AppCompatActivity? Activity;

    private static Context? AppContext;
    private static string RsaKey = string.Empty;
    private static List<string> ProductIDs = new();
    private static Task<bool>? _initTask;
    private static readonly object _lock = new();

    // ------------------------------------------------------------------
    // Initialize / auto-reconnect
    // ------------------------------------------------------------------

    public static Task<bool> Initialize(Context context, string rsaKey, List<string> productIds)
    {
        Activity = context as AppCompatActivity;   // refreshed on every call
        AppContext = context.ApplicationContext ?? context;
        RsaKey = rsaKey;
        ProductIDs = productIds;
        return EnsureInitialized();
    }

    internal static Task<bool> EnsureInitialized()
    {
        lock (_lock)
        {
            if (Initialized && Pay != null) return Task.FromResult(true);
            if (_initTask is { IsCompleted: false }) return _initTask;
            return _initTask = ConnectAsync();
        }
    }

    private static Task<bool> ConnectAsync()
    {
        var tcs = new TaskCompletionSource<bool>();

        if (AppContext == null || string.IsNullOrEmpty(RsaKey))
        {
            tcs.TrySetResult(false);
            return tcs.Task;
        }

        try
        {
            var config = new PaymentConfiguration(new SecurityCheck.Enable(RsaKey));
            Pay = new Payment(AppContext, config);

            Pay.Connect(new KFunc1(o =>
            {
                var cb = o!.JavaCast<ConnectionCallback>();
                cb.ConnectionSucceed(new KFunc0(() => { Initialized = true; tcs.TrySetResult(true); }));
                cb.ConnectionFailed(new KFunc1(_ => { Initialized = false; tcs.TrySetResult(false); }));
                cb.Disconnected(new KFunc0(() => Initialized = false));
            }));
        }
        catch
        {
            tcs.TrySetResult(false);
        }

        return tcs.Task;
    }

    private static async Task<T> WithInit<T>(Func<Task<T>> action, T fail)
    {
        if (!await EnsureInitialized()) return fail;
        return await action();
    }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    public static Task<(bool Status, PurchaseModel Product)> Purchase(string productId, string payload = "", bool consumeAfter = false)
        => WithInit(() => StartFlow(false, productId, payload, consumeAfter), (false, new PurchaseModel()));

    public static Task<(bool Status, PurchaseModel Product)> Subscription(string productId, string payload = "")
        => WithInit(() => StartFlow(true, productId, payload, false), (false, new PurchaseModel()));

    public static Task<(bool Status, List<ItemModel> Products)> AllInappItems()
        => WithInit(() => QueryItems(false), (false, new List<ItemModel>()));

    public static Task<(bool Status, List<ItemModel> Products)> AllSubItems()
        => WithInit(() => QueryItems(true), (false, new List<ItemModel>()));

    public static Task<(bool Status, List<PurchaseModel> Products)> CheckPurchases()
        => WithInit(() => QueryPurchases(false), (false, new List<PurchaseModel>()));

    public static Task<(bool Status, List<PurchaseModel> Products)> CheckSubscriptions()
        => WithInit(() => QueryPurchases(true), (false, new List<PurchaseModel>()));

    public static Task<(bool Status, string ProductID)> Consume(PurchaseModel purchase)
        => WithInit(() => ConsumeCore(purchase), (false, ""));

    public static Task<(bool Status, bool IsAvailable, int TrialPeriodDays)> CheckTrialSubscription()
    => WithInit<(bool Status, bool IsAvailable, int TrialPeriodDays)>(
        () => CheckTrialCore(), (false, false, 0));



    // ------------------------------------------------------------------
    // Implementation
    // ------------------------------------------------------------------

    private static Task<(bool Status, PurchaseModel Product)> StartFlow(bool subscribe, string productId, string payload, bool consumeAfter)
    {
        var tcs = new TaskCompletionSource<(bool Status, PurchaseModel Product)>();
        var fail = (false, new PurchaseModel());

        if (Pay == null || Activity == null)
        {
            tcs.TrySetResult(fail);
            return tcs.Task;
        }

        try
        {
            var req = new PurchaseRequest(productId, payload, null);
            var type = subscribe ? "subs" : "inapp";

            var callback = new KFunc1(o =>
            {
                var cb = o!.JavaCast<PurchaseCallback>();
                cb.FailedToBeginFlow(new KFunc1(_ => tcs.TrySetResult(fail)));
                cb.PurchaseCanceled(new KFunc0(() => tcs.TrySetResult(fail)));
                cb.PurchaseFailed(new KFunc1(_ => tcs.TrySetResult(fail)));
                cb.PurchaseSucceed(new KFunc1(p =>
                {
                    var info = p!.JavaCast<PurchaseInfo>();
                    var model = ToModel(info, type);
                    var ok = subscribe || info.PurchaseState.Equals(PurchaseState.Purchased);
                    tcs.TrySetResult((ok, model));

                    if (ok && consumeAfter && !subscribe)
                        _ = Consume(model);
                }));
            });

            if (subscribe) Pay.SubscribeProduct(Activity.ActivityResultRegistry, req, callback);
            else Pay.PurchaseProduct(Activity.ActivityResultRegistry, req, callback);
        }
        catch
        {
            tcs.TrySetResult(fail);
        }

        return tcs.Task;
    }

    private static Task<(bool Status, string ProductID)> ConsumeCore(PurchaseModel purchase)
    {
        var tcs = new TaskCompletionSource<(bool Status, string ProductID)>();

        if (Pay == null || string.IsNullOrEmpty(purchase.purchaseToken))
        {
            tcs.TrySetResult((false, ""));
            return tcs.Task;
        }

        try
        {
            Pay.ConsumeProduct(purchase.purchaseToken, new KFunc1(o =>
            {
                var cb = o!.JavaCast<ConsumeCallback>();
                cb.ConsumeSucceed(new KFunc0(() => tcs.TrySetResult((true, purchase.productId ?? ""))));
                cb.ConsumeFailed(new KFunc1(_ => tcs.TrySetResult((false, ""))));
            }));
        }
        catch
        {
            tcs.TrySetResult((false, ""));
        }

        return tcs.Task;
    }

    private static Task<(bool Status, List<ItemModel> Products)> QueryItems(bool subs)
    {
        var tcs = new TaskCompletionSource<(bool Status, List<ItemModel> Products)>();

        if (Pay == null)
        {
            tcs.TrySetResult((false, new List<ItemModel>()));
            return tcs.Task;
        }

        try
        {
            var callback = new KFunc1(o =>
            {
                var cb = o!.JavaCast<GetSkuDetailsCallback>();
                cb.GetSkuDetailsSucceed(new KFunc1(list =>
                {
                    var items = new List<ItemModel>();
                    if (list != null)
                    {
                        var jl = new JavaList<SkuDetails>(list.Handle, JniHandleOwnership.DoNotTransfer);
                        foreach (var s in jl)
                            items.Add(new ItemModel
                            {
                                productId = s.Sku,
                                description = s.Description,
                                price = s.Price,
                                title = s.Title,
                                type = s.Type
                            });
                    }
                    tcs.TrySetResult((true, items));
                }));
                cb.GetSkuDetailsFailed(new KFunc1(_ => tcs.TrySetResult((false, new List<ItemModel>()))));
            });

            if (subs) Pay.GetSubscriptionSkuDetails(ProductIDs, callback);
            else Pay.GetInAppSkuDetails(ProductIDs, callback);
        }
        catch
        {
            tcs.TrySetResult((false, new List<ItemModel>()));
        }

        return tcs.Task;
    }

    private static Task<(bool Status, bool IsAvailable, int TrialPeriodDays)> CheckTrialCore()
    {
        var tcs = new TaskCompletionSource<(bool Status, bool IsAvailable, int TrialPeriodDays)>();

        if (Pay == null)
        {
            tcs.TrySetResult((false, false, 0));
            return tcs.Task;
        }

        try
        {
            Pay.CheckTrialSubscription(new KFunc1(o =>
            {
                var cb = o!.JavaCast<CheckTrialSubscriptionCallback>();
                cb.CheckTrialSubscriptionSucceed(new KFunc1(p =>
                {
                    var info = p!.JavaCast<TrialSubscriptionInfo>();
                    tcs.TrySetResult((true, info.IsAvailable, info.TrialPeriodDays));
                }));
                cb.CheckTrialSubscriptionFailed(new KFunc1(_ => tcs.TrySetResult((false, false, 0))));
            }));
        }
        catch
        {
            tcs.TrySetResult((false, false, 0));
        }

        return tcs.Task;
    }

    private static Task<(bool Status, List<PurchaseModel> Products)> QueryPurchases(bool subs)
    {
        var tcs = new TaskCompletionSource<(bool Status, List<PurchaseModel> Products)>();

        if (Pay == null)
        {
            tcs.TrySetResult((false, new List<PurchaseModel>()));
            return tcs.Task;
        }

        try
        {
            var type = subs ? "subs" : "inapp";
            var callback = new KFunc1(o =>
            {
                var cb = o!.JavaCast<PurchaseQueryCallback>();
                cb.QuerySucceed(new KFunc1(list =>
                {
                    var result = new List<PurchaseModel>();
                    if (list != null)
                    {
                        var jl = new JavaList<PurchaseInfo>(list.Handle, JniHandleOwnership.DoNotTransfer);
                        foreach (var info in jl)
                            result.Add(ToModel(info, type));
                    }
                    tcs.TrySetResult((true, result));
                }));
                cb.QueryFailed(new KFunc1(_ => tcs.TrySetResult((false, new List<PurchaseModel>()))));
            });

            if (subs) Pay.GetSubscribedProducts(callback);
            else Pay.GetPurchasedProducts(callback);
        }
        catch
        {
            tcs.TrySetResult((false, new List<PurchaseModel>()));
        }

        return tcs.Task;
    }

    internal static PurchaseModel ToModel(PurchaseInfo info, string type) => new()
    {
        productId = info.ProductId,
        purchaseToken = info.PurchaseToken,
        state = JsonSerializer.SerializeToElement(info.PurchaseState.Name()),
        payload = info.Payload,
        signature = info.DataSignature,
        orderId = info.OrderId,
        originalJson = info.OriginalJson,
        purchaseTime = info.PurchaseTime,
        type = type
    };
}