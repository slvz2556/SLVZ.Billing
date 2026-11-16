using Android.App;
using Android.Content;
using IR.Myket.Billingclient;
using IR.Myket.Billingclient.Util;
using SLVZ.Billing.Core;
using System.Text.Json;
using MyketPurchase = IR.Myket.Billingclient.Util.Purchase;

namespace SLVZ.Billing;

public class InAppBilling
{
    internal static bool Initialized;
    internal static IabHelper? Helper;
    internal static Activity? CurrentActivity;

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
        CurrentActivity = context as Activity;   // refreshed on every call
        AppContext = context.ApplicationContext ?? context;
        RsaKey = rsaKey;
        ProductIDs = productIds;
        return EnsureInitialized();
    }

    internal static Task<bool> EnsureInitialized()
    {
        lock (_lock)
        {
            if (Initialized && Helper != null) return Task.FromResult(true);
            if (_initTask is { IsCompleted: false }) return _initTask;
            return _initTask = SetupAsync();
        }
    }

    private static Task<bool> SetupAsync()
    {
        var tcs = new TaskCompletionSource<bool>();

        if (AppContext == null || string.IsNullOrEmpty(RsaKey))
        {
            tcs.TrySetResult(false);
            return tcs.Task;
        }

        try
        {
            try { Helper?.Dispose(); } catch { }
            var helper = new IabHelper(AppContext, RsaKey);
            Helper = helper;

            helper.StartSetup(new SetupListener(result =>
            {
                if (result?.IsSuccess == true)
                {
                    Initialized = true;
                    tcs.TrySetResult(true);
                }
                else
                {
                    Initialized = false;
                    try { helper.Dispose(); } catch { }
                    if (ReferenceEquals(Helper, helper)) Helper = null;
                    tcs.TrySetResult(false);
                }
            }));
        }
        catch
        {
            Initialized = false;
            Helper = null;
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
    // Public API (same signatures as SLVZ.Billing.Bazaar)
    // ------------------------------------------------------------------

    public static Task<(bool Status, PurchaseModel Product)> Purchase(string productId, string payload = "", bool consumeAfter = false)
        => WithInit(() => StartFlow(false, productId, payload, consumeAfter), (false, new PurchaseModel()));

    public static Task<(bool Status, PurchaseModel Product)> Subscription(string productId, string payload = "")
        => WithInit(() => StartFlow(true, productId, payload, false), (false, new PurchaseModel()));

    public static Task<(bool Status, string ProductID)> Consume(PurchaseModel purchase)
        => WithInit(() => ConsumeCore(purchase), (false, ""));

    public static Task<(bool Status, List<ItemModel> Products)> AllInappItems()
        => WithInit(() => QueryItems(false), (false, new List<ItemModel>()));

    public static Task<(bool Status, List<ItemModel> Products)> AllSubItems()
        => WithInit(() => QueryItems(true), (false, new List<ItemModel>()));

    public static Task<(bool Status, List<PurchaseModel> Products)> CheckPurchases()
        => WithInit(() => QueryPurchases(false), (false, new List<PurchaseModel>()));

    public static Task<(bool Status, List<PurchaseModel> Products)> CheckSubscriptions()
        => WithInit(() => QueryPurchases(true), (false, new List<PurchaseModel>()));

    /// <summary>Kept for backward compatibility: in-app items + subscriptions in one list.</summary>
    public static async Task<(bool Status, List<ItemModel> Products)> AllAvailibleItems()
    {
        var inapp = await AllInappItems();
        var subs = await AllSubItems();
        var all = new List<ItemModel>(inapp.Products);
        all.AddRange(subs.Products);
        return (inapp.Status || subs.Status, all);
    }

    // ------------------------------------------------------------------
    // Implementation
    // ------------------------------------------------------------------

    private static Task<(bool Status, PurchaseModel Product)> StartFlow(bool subscribe, string productId, string payload, bool consumeAfter)
    {
        var tcs = new TaskCompletionSource<(bool Status, PurchaseModel Product)>();
        var fail = (false, new PurchaseModel());
        var helper = Helper;
        var activity = CurrentActivity;

        if (helper == null || activity == null)
        {
            tcs.TrySetResult(fail);
            return tcs.Task;
        }

        var type = subscribe ? "subs" : "inapp";

        var listener = new PurchaseListener((result, purchase) =>
        {
            if (result?.IsSuccess == true && purchase != null)
            {
                var model = ToModel(purchase, type);
                var ok = purchase.PurchaseState == 0;   // 0 = purchased
                tcs.TrySetResult((ok, model));

                if (ok && consumeAfter && !subscribe)
                    _ = Consume(model);
            }
            else
            {
                tcs.TrySetResult(fail);
            }
        });

        activity.RunOnUiThread(() =>
        {
            try
            {
                if (subscribe) helper.LaunchSubscriptionPurchaseFlow(activity, productId, listener, payload);
                else helper.LaunchPurchaseFlow(activity, productId, listener, payload);
            }
            catch
            {
                tcs.TrySetResult(fail);
            }
        });

        return tcs.Task;
    }

    private static Task<(bool Status, string ProductID)> ConsumeCore(PurchaseModel purchase)
    {
        var tcs = new TaskCompletionSource<(bool Status, string ProductID)>();
        var helper = Helper;

        if (helper == null
            || string.IsNullOrEmpty(purchase.originalJson)
            || string.IsNullOrEmpty(purchase.signature))
        {
            tcs.TrySetResult((false, ""));
            return tcs.Task;
        }

        try
        {
            var p = new MyketPurchase(purchase.type ?? IabHelper.ItemTypeInapp, purchase.originalJson, purchase.signature);

            helper.ConsumeAsync(p, new ConsumeListener((_, result) =>
            {
                var ok = result?.IsSuccess == true;
                tcs.TrySetResult((ok, ok ? purchase.productId ?? "" : ""));
            }));
        }
        catch
        {
            tcs.TrySetResult((false, ""));
        }

        return tcs.Task;
    }

    private static async Task<(bool Status, List<ItemModel> Products)> QueryItems(bool subs)
    {
        var helper = Helper;
        var items = new List<ItemModel>();
        if (helper == null) return (false, items);

        var wantedType = subs ? IabHelper.ItemTypeSubs : IabHelper.ItemTypeInapp;

        // QueryInventory is a blocking call, so it must not run on the UI thread
        var inventory = await Task.Run(() =>
        {
            try
            {
                return subs
                    ? helper.QueryInventory(true, null, ProductIDs)
                    : helper.QueryInventory(true, ProductIDs, null);
            }
            catch
            {
                return null;
            }
        });

        if (inventory == null) return (false, items);

        foreach (var s in inventory.AllProducts ?? new List<SkuDetails>())
        {
            if (!string.Equals(s.Type, wantedType, StringComparison.OrdinalIgnoreCase)) continue;

            items.Add(new ItemModel
            {
                productId = s.Sku,
                description = s.Description,
                price = s.Price,
                title = s.Title,
                type = s.Type
            });
        }

        return (true, items);
    }

    private static async Task<(bool Status, List<PurchaseModel> Products)> QueryPurchases(bool subs)
    {
        var helper = Helper;
        var result = new List<PurchaseModel>();
        if (helper == null) return (false, result);

        var wantedType = subs ? IabHelper.ItemTypeSubs : IabHelper.ItemTypeInapp;

        var inventory = await Task.Run(() =>
        {
            try { return helper.QueryInventory(false, null, null); }
            catch { return null; }
        });

        if (inventory == null) return (false, result);

        foreach (var p in inventory.AllPurchases ?? new List<MyketPurchase>())
        {
            if (!string.Equals(p.ItemType, wantedType, StringComparison.OrdinalIgnoreCase)) continue;
            result.Add(ToModel(p, wantedType));
        }

        return (true, result);
    }

    internal static PurchaseModel ToModel(MyketPurchase p, string fallbackType) => new()
    {
        productId = p.Sku,
        purchaseToken = p.Token,
        state = JsonSerializer.SerializeToElement(StateName(p.PurchaseState)),
        payload = p.DeveloperPayload,
        signature = p.Signature,
        orderId = p.OrderId,
        originalJson = p.OriginalJson,
        purchaseTime = p.PurchaseTime,
        type = p.ItemType ?? fallbackType
    };

    private static string StateName(int state) => state switch
    {
        0 => "PURCHASED",
        1 => "CANCELED",
        2 => "REFUNDED",
        _ => state.ToString()
    };
}

// ----------------------------------------------------------------------
// Listener adapters (Myket's IabHelper uses Java callback interfaces)
// ----------------------------------------------------------------------

internal sealed class SetupListener : Java.Lang.Object, IabHelper.IOnIabSetupFinishedListener
{
    private readonly Action<IabResult?> _action;
    public SetupListener(Action<IabResult?> action) => _action = action;

    public void OnIabSetupFinished(IabResult? result) => _action(result);
}

internal sealed class PurchaseListener : Java.Lang.Object, IabHelper.IOnIabPurchaseFinishedListener
{
    private readonly Action<IabResult?, MyketPurchase?> _action;
    public PurchaseListener(Action<IabResult?, MyketPurchase?> action) => _action = action;

    public void OnIabPurchaseFinished(IabResult? result, MyketPurchase? purchase) => _action(result, purchase);
}

internal sealed class ConsumeListener : Java.Lang.Object, IabHelper.IOnConsumeFinishedListener
{
    private readonly Action<MyketPurchase?, IabResult?> _action;
    public ConsumeListener(Action<MyketPurchase?, IabResult?> action) => _action = action;

    public void OnConsumeFinished(MyketPurchase? purchase, IabResult? result) => _action(purchase, result);
}