using Mediso.PaymentSample.SharedKernel.Domain;
using System.Runtime.CompilerServices;

namespace Mediso.PaymentSample.SharedKernel;

public static class Guards
{
    /// <summary>
    /// Validates that a GUID is not empty
    /// </summary>
    /// <param name="value">GUID value to validate</param>
    /// <param name="paramName">Parameter name for exception</param>
    /// <returns>The validated GUID</returns>
    /// <exception cref="DomainException">Thrown when GUID is empty</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guid NotEmpty(Guid value, string? paramName = null)
    {
        if (value != Guid.Empty) 
            return value;
            
        ThrowHelper.ThrowDomainException_NotEmpty(paramName ?? nameof(value));
        return default; // Never reached
    }

    /// <summary>
    /// Validates that a string is not null or whitespace
    /// </summary>
    /// <param name="value">String value to validate</param>
    /// <param name="paramName">Parameter name for exception</param>
    /// <returns>The validated string</returns>
    /// <exception cref="DomainException">Thrown when string is null, empty or whitespace</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string NotNullOrWhiteSpace(string? value, string? paramName = null)
    {
        if (value != null && !IsWhiteSpaceOrEmpty(value.AsSpan()))
            return value;
            
        ThrowHelper.ThrowDomainException_NotNullOrWhiteSpace(paramName ?? nameof(value));
        return default!; // Never reached
    }
    
    /// <summary>
    /// Efficient whitespace check using Span<char>
    /// </summary>
    /// <param name="value">Character span to check</param>
    /// <returns>True if empty or whitespace, false otherwise</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWhiteSpaceOrEmpty(ReadOnlySpan<char> value)
    {
        return value.IsEmpty || value.IsWhiteSpace();
    }
}

/// <summary>
/// Helper class for efficient exception throwing
/// </summary>
internal static class ThrowHelper
{
    /// <summary>
    /// Throws DomainException for empty GUID
    /// </summary>
    /// <param name="paramName">Parameter name</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowDomainException_NotEmpty(string paramName)
    {
        throw new DomainException($"{paramName} must not be empty");
    }
    
    /// <summary>
    /// Throws DomainException for null or whitespace string
    /// </summary>
    /// <param name="paramName">Parameter name</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ThrowDomainException_NotNullOrWhiteSpace(string paramName)
    {
        throw new DomainException($"{paramName} must not be empty");
    }
}
