using System;

namespace HighSpeedDAL.Core.Attributes;

/// <summary>
/// Marks a property as an identity/auto-increment column
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class IdentityAttribute : Attribute
{
}
