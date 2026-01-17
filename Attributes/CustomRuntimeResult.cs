using Discord.Commands;

namespace GeistDesWaldes.Attributes;

public class CustomRuntimeResult : RuntimeResult
{
    public CustomRuntimeResult(CommandError? error, string reason) : base(error, reason)
    {
    }

    public static CustomRuntimeResult FromError(string reason)
    {
        return new CustomRuntimeResult(CommandError.Unsuccessful, reason);
    }

    public static CustomRuntimeResult FromSuccess(string reason = null)
    {
        return new CustomRuntimeResult(null, reason);
    }
}

public class CustomRuntimeResult<T> : RuntimeResult
{
    public T ResultValue;

    public CustomRuntimeResult(CommandError? error, string reason, T value = default) : base(error, reason)
    {
        ResultValue = value;
    }

    public static implicit operator CustomRuntimeResult(CustomRuntimeResult<T> crr)
    {
        return new CustomRuntimeResult(crr.Error, crr.Reason);
    }

    public static implicit operator CustomRuntimeResult<T>(CustomRuntimeResult crr)
    {
        return new CustomRuntimeResult<T>(crr.Error, crr.Reason);
    }

    public static CustomRuntimeResult<T> FromError(string reason)
    {
        return new CustomRuntimeResult<T>(CommandError.Unsuccessful, reason);
    }

    public static CustomRuntimeResult<T> FromSuccess(string reason = null, T value = default)
    {
        return new CustomRuntimeResult<T>(null, reason, value);
    }
}