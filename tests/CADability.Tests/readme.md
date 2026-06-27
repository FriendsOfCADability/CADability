# CADability.Tests

Run tests from the command line (requires .NET 8 SDK):

```bash
dotnet test CADability.sln
```

# Coverage in Visual Studio

* Recommended extension: Fine Code Coverage
* Settings:
  * Enabled: True
  * RunMsCodeCoverage: True

This will show coverage markers directly in Visual Studio and a Coverage Report in the Fine Code Coverage Tool window.

# Generating Test Artifacts

Generate cobertura.xml and junit.xml (e.g., for CI pipelines):

```bash
dotnet test CADability.sln --collect:"XPlat Code Coverage" --logger:"junit;LogFilePath=test-results.xml;MethodFormat=Class;FailureBodyFormat=Verbose" --settings coverlet.runsettings
```
