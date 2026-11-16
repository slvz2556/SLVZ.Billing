using Kotlin;
using Kotlin.Jvm.Functions;

namespace SLVZ.Billing;




internal sealed class KFunc0 : Java.Lang.Object, IFunction0
{
    private readonly Action _action;
    public KFunc0(Action action) => _action = action;

    public Java.Lang.Object? Invoke()
    {
        _action();
        return Unit.Instance;
    }
}

internal sealed class KFunc1 : Java.Lang.Object, IFunction1
{
    private readonly Action<Java.Lang.Object?> _action;
    public KFunc1(Action<Java.Lang.Object?> action) => _action = action;

    public Java.Lang.Object? Invoke(Java.Lang.Object? p0)
    {
        _action(p0);
        return Unit.Instance;
    }
}