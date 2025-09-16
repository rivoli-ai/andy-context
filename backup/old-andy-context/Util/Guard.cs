using System.Text.RegularExpressions;

namespace Andy.Context.Util;

// Guard.cs
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;

/// <summary>Static, dependency-free guard helpers.</summary>
public static class Guard
{
    // ---- Null / Empty / Whitespace ----

    public static T Null<T>(T input, string paramName)
    {
        if (input is null) throw new ArgumentNullException(paramName);
        return input;
    }

    public static string NullOrEmpty(string input, string paramName)
    {
        if (string.IsNullOrEmpty(input))
            throw new ArgumentException("Value cannot be null or empty.", paramName);
        return input;
    }

    public static Guid NullOrEmpty(Guid input, string paramName)
    {
        if (input == Guid.Empty)
            throw new ArgumentException("Guid cannot be empty.", paramName);
        return input;
    }

    public static T[] NullOrEmpty<T>(T[] input, string paramName)
    {
        if (input is null || input.Length == 0)
            throw new ArgumentException("Array cannot be null or empty.", paramName);
        return input;
    }

    public static IEnumerable<T> NullOrEmpty<T>(IEnumerable<T> input, string paramName)
    {
        if (input is null || !input.Any())
            throw new ArgumentException("Sequence cannot be null or empty.", paramName);
        return input;
    }

    public static string NullOrWhiteSpace(string input, string paramName)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Value cannot be null, empty, or whitespace.", paramName);
        return input;
    }

    // ---- Ranges ----

    /// <summary>Inclusive range check for IComparable types (int, DateTime, decimal, etc.).</summary>
    public static T OutOfRange<T>(T input, string paramName, T rangeFrom, T rangeTo)
        where T : IComparable<T>
    {
        if (rangeFrom.CompareTo(rangeTo) > 0)
            throw new ArgumentException("rangeFrom must be <= rangeTo.", nameof(rangeFrom));

        if (input.CompareTo(rangeFrom) < 0 || input.CompareTo(rangeTo) > 0)
            throw new ArgumentOutOfRangeException(paramName,
                $"Value {input} must be in range [{rangeFrom}, {rangeTo}].");
        return input;
    }

    /// <summary>Enum range by min/max underlying value (inclusive).</summary>
    public static TEnum EnumOutOfRange<TEnum>(TEnum value, string paramName, TEnum min, TEnum max)
        where TEnum : struct, Enum
    {
        long v = Convert.ToInt64(value);
        long lo = Convert.ToInt64(min);
        long hi = Convert.ToInt64(max);

        if (lo > hi) throw new ArgumentException("min must be <= max.");

        if (v < lo || v > hi)
            throw new ArgumentOutOfRangeException(paramName,
                $"Enum value '{value}' must be between '{min}' and '{max}'.");
        return value;
    }

    /// <summary>Enum allowed set check.</summary>
    public static TEnum EnumOutOfRange<TEnum>(TEnum value, string paramName, params TEnum[] allowed)
        where TEnum : struct, Enum
    {
        if (allowed is null || allowed.Length == 0)
            throw new ArgumentException("Allowed enum set cannot be null or empty.", nameof(allowed));

        // Fast path for defined values
        if (!allowed.Contains(value))
            throw new ArgumentOutOfRangeException(paramName,
                $"Enum value '{value}' must be one of: {string.Join(", ", allowed)}.");
        return value;
    }

    /// <summary>SQL Server DATETIME valid range (1753-01-01 to 9999-12-31).</summary>
    public static DateTime OutOfSQLDateRange(DateTime value, string paramName)
    {
        var min = SqlDateTime.MinValue.Value;
        var max = SqlDateTime.MaxValue.Value;
        if (value < min || value > max)
            throw new ArgumentOutOfRangeException(paramName,
                $"DateTime must be within SQL DATETIME range [{min}, {max}].");
        return value;
    }

    // ---- Zero (numeric) ----
    // If you're on .NET 7+, you can replace these with a single generic INumber<T> method.
    public static int Zero(int value, string paramName)
    {
        if (value == 0) throw new ArgumentException("Value cannot be zero.", paramName);
        return value;
    }

    public static long Zero(long value, string paramName)
    {
        if (value == 0L) throw new ArgumentException("Value cannot be zero.", paramName);
        return value;
    }

    public static float Zero(float value, string paramName)
    {
        if (value == 0f) throw new ArgumentException("Value cannot be zero.", paramName);
        return value;
    }

    public static double Zero(double value, string paramName)
    {
        if (value == 0d) throw new ArgumentException("Value cannot be zero.", paramName);
        return value;
    }

    public static decimal Zero(decimal value, string paramName)
    {
        if (value == 0m) throw new ArgumentException("Value cannot be zero.", paramName);
        return value;
    }

    // ---- Expression / Format ----

    /// <summary>Throws if the provided predicate identifies the input as invalid.</summary>
    public static T Expression<T>(T input, string paramName, Func<T, bool> isInvalid,
        string message = "Value failed validation.")
    {
        if (isInvalid is null) throw new ArgumentNullException(nameof(isInvalid));
        if (isInvalid(input)) throw new ArgumentException(message, paramName);
        return input;
    }

    /// <summary>Regex format guard (string).</summary>
    public static string InvalidFormat(string input, string paramName, Regex allowedPattern,
        string message = "Value has an invalid format.")
    {
        if (allowedPattern is null) throw new ArgumentNullException(nameof(allowedPattern));
        if (input is null) throw new ArgumentNullException(paramName);
        if (!allowedPattern.IsMatch(input))
            throw new FormatException($"{message} Parameter: {paramName}.");
        return input;
    }

    /// <summary>Custom format guard using a validator func returning true when valid.</summary>
    public static T InvalidFormat<T>(T input, string paramName, Func<T, bool> isValid,
        string message = "Value has an invalid format.")
    {
        if (isValid is null) throw new ArgumentNullException(nameof(isValid));
        if (!isValid(input))
            throw new FormatException($"{message} Parameter: {paramName}.");
        return input;
    }

    // ---- NotFound ----

    /// <summary>Use after lookups: throws NotFoundException when the result is null.</summary>
    public static T NotFound<T>(T input, string name, object key)
    {
        if (input is null) throw new NotFoundException(name, key);
        return input;
    }
}