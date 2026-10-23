using Android.Content;
using Library.Slvz.Poolakeybridge;

namespace SLVZ.Billing.Core;

public class Billing
{
    internal static bool Initialized;
    internal static string RsaKey = string.Empty;
    internal static List<string> ProductIDs = new List<string>();
    internal static Context? Context;
    internal static TaskCompletionSource<bool>? initTcs;

    public static void Initialize(Context context, string rsaKey, List<string> productIds)
    {
        initTcs = new TaskCompletionSource<bool>();
        if (Initialized) return;

        ProductIDs = productIds;
        Context = context;
        RsaKey = rsaKey;

        Bazaar.Billing.RegisterReceiver();
        Initialized = true;

        PoolakeyHelper.Initialize(context, rsaKey);
    }
}
