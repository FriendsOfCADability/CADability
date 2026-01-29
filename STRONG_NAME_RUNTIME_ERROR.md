# Strong-Name Runtime Error - Solution Options

## Problem Statement

You are experiencing a **runtime FileLoadException** when trying to load ACadSharp:

```
System.IO.FileLoadException: 'Die Datei oder Assembly "ACadSharp, Version=3.0.8.0, Culture=neutral, PublicKeyToken=null" oder eine Abhängigkeit davon wurde nicht gefunden. Eine Assembly mit starkem Namen ist erforderlich. (Ausnahme von HRESULT: 0x80131044)'
```

Translation: "The file or assembly ACadSharp or one of its dependencies was not found. A strong-named assembly is required. (Exception from HRESULT: 0x80131044)"

**This is FUSION_E_STRONG_NAME_REQUIRED** - .NET Framework is enforcing strong-name signing at runtime and refusing to load the unsigned ACadSharp assembly.

## Why Warning Suppression Doesn't Work

The previous approach of suppressing the CS8002 compiler warning does NOT solve runtime errors. That only silences the compile-time warning - the runtime still enforces strong-name policy in certain configurations (particularly with .NET Framework 4.x with strict security policies).

## Attempted Solution: Vendoring

I attempted to vendor ACadSharp v3.0.8 source code and sign it with CADability's key, but encountered:

1. **C# Language Version Conflicts**: v3.0.8 uses a mix of C# 8.0, 9.0, 11.0, and even 12.0 features
2. **CSUtilities Submodule**: The dependency submodule also has newer C# requirements
3. **Build Complexity**: 146 compilation errors related to language features

## Recommended Solutions

### Option 1: ILRepack (RECOMMENDED)

Merge the unsigned ACadSharp.dll into CADability.dll after compilation using ILRepack.

**Pros:**
- ✅ Single signed assembly
- ✅ No external unsigned dependencies at runtime
- ✅ No source code modifications needed
- ✅ Works with NuGet packages as-is

**Cons:**
- ⚠️ Slightly larger assembly size
- ⚠️ Post-build step required

**Implementation:**
1. Install ILRepack NuGet package
2. Add post-build event to CADability.csproj:
```xml
<Target Name="ILRepack" AfterTargets="Build">
  <ItemGroup>
    <InputAssemblies Include="$(OutputPath)CADability.dll" />
    <InputAssemblies Include="$(OutputPath)ACadSharp.dll" />
  </ItemGroup>
  <ILRepack
    OutputFile="$(OutputPath)CADability.dll"
    InputAssemblies="@(InputAssemblies)"
    KeyFile="$(MSBuildProjectDirectory)\CADabilityKey.snk"
    Internalize="true"
  />
</Target>
```

### Option 2: Assembly Binding Redirect with Skip Verification

Disable strong-name verification for ACadSharp on the deployment machine.

**Pros:**
- ✅ Quick fix
- ✅ No code changes

**Cons:**
- ❌ Requires admin rights on deployment machine
- ❌ Must be done on every machine
- ❌ Security policy violation in some environments
- ❌ NOT recommended for production

**Implementation:**
Run as administrator on each deployment machine:
```cmd
sn.exe -Vr ACadSharp.dll
```

Or add registry key:
```reg
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\StrongName\Verification\ACadSharp,B6C3E656A01B97A1]
```

### Option 3: Downgrade .NET Framework Target

Target .NET Core or .NET 5+ instead of .NET Framework.

**Pros:**
- ✅ Modern .NET ignores strong-name enforcement
- ✅ No workarounds needed
- ✅ Better performance and features

**Cons:**
- ❌ May not be feasible if you need .NET Framework
- ❌ Requires application changes

**Implementation:**
If CADability can target .NET 6+ or .NET Core, change:
```xml
<TargetFramework>net6.0-windows</TargetFramework>
```

### Option 4: Fork ACadSharp and Publish Signed Version

Fork ACadSharp, add strong-name signing, and publish to a private NuGet feed.

**Pros:**
- ✅ Clean solution
- ✅ Can contribute back to upstream

**Cons:**
- ⚠️ Maintenance burden
- ⚠️ Need to sync with upstream updates
- ⚠️ Requires NuGet feed setup

**Implementation:**
1. Fork https://github.com/DomCR/ACadSharp
2. Add strong-name signing to ACadSharp.csproj
3. Build and publish to private NuGet feed or local folder
4. Reference your signed version

### Option 5: Request Official Signed Package

Contact ACadSharp maintainers to publish a strong-name signed NuGet package.

**Pros:**
- ✅ Official support
- ✅ Benefits all users
- ✅ No maintenance on your end

**Cons:**
- ⏳ May take time
- ❓ May be rejected (strong-names are legacy)

**Implementation:**
- Open an issue at: https://github.com/DomCR/ACadSharp/issues
- Explain the .NET Framework runtime requirement
- Offer to help with PR if needed

## Comparison Matrix

| Solution | Complexity | Maintenance | Production Ready | Recommended |
|----------|------------|-------------|------------------|-------------|
| ILRepack | Medium | Low | ✅ Yes | ⭐ **Best** |
| Skip Verification | Low | High | ❌ No | ❌ Not recommended |
| Target .NET 6+ | Medium | None | ✅ Yes | ✅ If possible |
| Fork & Sign | High | High | ✅ Yes | ⚠️ Last resort |
| Request Signed | Low | None | ⏳ Eventually | ✅ Long-term |

## Immediate Action Plan

**I recommend Option 1 (ILRepack)**:

1. **Add ILRepack to CADability**:
   ```bash
   dotnet add package ILRepack
   dotnet add package ILRepack.Lib.MSBuild.Task
   ```

2. **Add post-build merge step** (see implementation above)

3. **Test the merged assembly**

4. **Deploy single CADability.dll** (now includes ACadSharp internally)

This gives you a production-ready solution TODAY while allowing you to switch to an official signed package if it becomes available later.

## Why This Happened

.NET Framework enforces strong-name policies when:
- Running under specific security policies (CAS)
- The hosting application has strict verification enabled
- GAC deployment scenarios
- Some enterprise environments

Modern .NET (Core/.NET 5+) has largely deprecated strong-name enforcement, which is why this is primarily a .NET Framework issue.

## Next Steps

1. Choose your preferred solution (I recommend ILRepack)
2. Implement and test
3. Update this documentation with results
4. Consider filing an issue with ACadSharp project

---

**Status:** Strong-name runtime error identified. Vendoring approach blocked by C# version incompatibilities. ILRepack recommended as best production solution.
