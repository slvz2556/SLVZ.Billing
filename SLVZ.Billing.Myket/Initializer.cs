using Android.Content;
using Library.Slvz.Myketbridge;

namespace SLVZ.Billing.Core;

public static class Billing
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

        Myket.Billing.RegisterReceiver();
        Initialized = true;
        MyketBridge.Initialize(context, Key);

    }
}
