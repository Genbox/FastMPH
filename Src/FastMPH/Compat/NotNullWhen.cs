﻿namespace Genbox.FastMPH.Compat;

[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
#if SYSTEM_PRIVATE_CORELIB
    public
#else
internal
#endif
    sealed class NotNullWhenAttribute : Attribute
{
    /// <summary>Initializes the attribute with the specified return value condition.</summary>
    /// <param name="returnValue">
    /// The return value condition. If the method returns this value, the associated parameter will not be null.
    /// </param>
    public NotNullWhenAttribute(bool returnValue) => ReturnValue = returnValue;

    /// <summary>Gets the return value condition.</summary>
    public bool ReturnValue { get; }
}