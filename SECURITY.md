# Security Policy

## Reporting a vulnerability

Please do **not** report security vulnerabilities through public issues, discussions or pull requests.

Report them privately through GitHub instead:
[Report a vulnerability](https://github.com/FriendsOfCADability/CADability/security/advisories/new)
(the *Security* tab of this repository, then *Report a vulnerability*).

Useful details are:

- the CADability version (NuGet package version) and target framework,
- the kind of problem, e.g. a crash, hang or excessive memory use while reading a file, or code execution,
- a minimal file or code sample that reproduces it, if possible,
- whether you know of the vulnerability being exploited.

We aim to acknowledge a report within five working days and keep you informed until it is resolved.
Once a fix is released, we publish a GitHub security advisory for it and credit you, if you wish.

## Supported versions

Security fixes go into the latest release on [nuget.org](https://www.nuget.org/packages/CADability).
Please update to it before reporting.

| Version | Supported |
| ------- | --------- |
| latest 1.x release | yes |
| older releases | no |

## Scope

CADability reads files from many sources (DXF, DWG, STEP, IGES, STL, its own `.cdb` format, …).
Problems that let a crafted file crash or hang an application that opens it, exhaust its memory, or
execute code are in scope. So are vulnerabilities in dependencies that CADability ships, such as
the bundled DXF and DWG readers.
