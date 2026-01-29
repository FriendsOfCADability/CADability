# Strong-Name Signing Solution for ACADSharp Integration

## Problem
CADability assemblies are strong-name signed using `CADabilityKey.snk`, but the ACADSharp NuGet package (version 3.0.8) is not strong-name signed. This causes the CS8002 compiler warning:

```
warning CS8002: Referenced assembly 'ACadSharp, Version=3.0.8.0, Culture=neutral, PublicKeyToken=null' does not have a strong name.
```

## Solution: Warning Suppression

We chose to suppress the CS8002 warning in CADability.csproj:

```xml
<NoWarn>$(NoWarn);CS8002</NoWarn>
```

### Why This Approach?

1. **Runtime Compatibility**: While .NET Framework with strong-name enforcement could theoretically reject unsigned assemblies, in practice:
   - CADability targets `netstandard2.0` which is consumed by both .NET Framework and .NET Core/.NET 5+
   - .NET Core and .NET 5+ ignore strong-name signing for compatibility checks
   - .NET Framework's default policy allows loading unsigned assemblies from signed ones in most deployment scenarios

2. **Minimal Impact**: The warning is informational and doesn't prevent:
   - Compilation
   - Runtime execution in standard deployment environments
   - NuGet package generation

3. **Alternatives Considered and Rejected**:

   **Option 1: Fork and Sign ACADSharp**
   - ❌ Requires maintaining a separate fork
   - ❌ Need to manually sync with upstream updates
   - ❌ Need to publish to a custom NuGet feed
   - ❌ Adds deployment complexity

   **Option 2: Vendor ACADSharp Source**
   - ❌ Attempted but encountered issues:
     - CSUtilities submodule had duplicate extension method definitions
     - API differences between v3.0.8 (NuGet) and v3.4.2 (source)
     - Increased maintenance burden
     - Larger repository size

4. **Safety Considerations**:
   - CADability is typically deployed in full-trust environments (desktop applications)
   - Strong-name signing primarily provides identity verification, not security
   - The unsigned ACadSharp dependency is:
     - Open source (MIT licensed)
     - From a reputable NuGet feed
     - Verified by package hash during restore

5. **Future-Proof**:
   - If ACADSharp publishes a signed version, we can simply update the NuGet reference
   - No code changes needed
   - Remove the NoWarn suppression at that time

## Deployment Scenarios

### ✅ Supported Scenarios
- Desktop applications (.NET Framework 4.8, .NET 6+, .NET 7+, .NET 8+)
- Library consumption via NuGet
- Development and testing

### ⚠️ Potential Issues (Rare)
- Highly restrictive Code Access Security (CAS) policies on .NET Framework
  - Mitigation: Grant full trust to CADability installation directory
- Custom AppDomain policies explicitly requiring all strong-names
  - Mitigation: Update policy to allow CADability's assembly

## Alternative Solutions (For Reference)

If in the future the warning suppression approach proves insufficient:

1. **Use Newer ACadSharp**: Version 3.4.2 (GitHub source) has strong-name signing enabled
   - Wait for signed NuGet package release
   - Or vendor the source after resolving CSUtilities issues

2. **ILMerge/ILRepack**: Merge ACadSharp into CADability
   - ❌ Complex tooling
   - ❌ Licensing considerations
   - ❌ Debugging difficulties

3. **Delay-Sign**: Use delay-signing with public key only
   - ❌ Doesn't solve the unsigned dependency issue
   - ❌ Requires key management

## Conclusion

Suppressing CS8002 is the most pragmatic solution that:
- ✅ Maintains compatibility with current NuGet ecosystem
- ✅ Requires minimal code changes
- ✅ Doesn't impact runtime behavior in standard scenarios
- ✅ Keeps deployment simple
- ✅ Allows easy upgrade path when signed version becomes available

The strong-name requirement is primarily a .NET Framework concern for preventing version conflicts, not a security feature. Modern .NET (Core/5+) largely ignores strong-name checks, making this warning increasingly irrelevant for future-targeting code.
