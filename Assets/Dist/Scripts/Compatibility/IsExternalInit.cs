// Compatibility shim for older runtimes that don't include IsExternalInit
// This file provides the minimal required type so C#9+ init-only properties
// and records can compile on older target frameworks. Remove this file once
// the project targets a runtime that defines this type (net5+/net6+).
namespace System.Runtime.CompilerServices
{
    // internal to avoid public API exposure and to reduce conflict risk
    internal static class IsExternalInit { }
}
