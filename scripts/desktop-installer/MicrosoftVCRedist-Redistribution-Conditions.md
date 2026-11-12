# Microsoft Visual C++ Redistributable: redistribution gate

The installer packages the official x64 `vc_redist.x64.exe` file without
modifying it. Pin for this package: **14.52.37029.0**,
SHA-256 **9C02C9ED120F671B900E6B9471D890E3B2414117A7E53F4AE2E855466FA2B7C9**.

## Conditions before compiling or distributing

1. The responsible developer must be a licensed Visual Studio user and must
   confirm that the applicable Visual Studio 2026 Community license terms
   cover this development activity. Microsoft's Community usage page says an
   individual developer may create their own free or paid apps. That statement
   does not grant the same usage rights to an organization or another developer.
   The recorded toolchain is Visual Studio Community 2026 **Insiders**. Do not
   assume the GA Community terms apply to that channel: the responsible person
   must identify the terms accepted by the exact Insiders build and confirm
   they authorize this use and redistribution. If the accepted Insiders terms
   cannot be established, stop or use a toolchain with confirmed GA licensing.
2. The selected Visual C++ v14 Redistributable package must be listed as
   redistributable by the applicable Visual Studio license and Microsoft's
   redistribution list. Copy it unmodified; do not substitute individual DLLs,
   debug/non-redistributable files, or a preview package.
3. Keep the original Microsoft package intact. Authenticode and SHA-256 checks
   establish publisher/integrity for the selected file; they do not themselves
   establish redistribution rights.
4. If any condition is uncertain, false, or not confirmed by the responsible
   person, stop. Do not compile or distribute either installer with the runtime.

Both Inno Setup scripts require the explicit compile-time value
`MicrosoftRedistributionRightsConfirmed=1`, and the production build script also
requires `-MicrosoftRedistributionRightsConfirmed`. These are deliberate
attestations, not automated legal determinations. The person invoking the build
must first review the current terms and confirm every condition above.

## Official Microsoft references

- [Visual Studio Community usage rights](https://visualstudio.microsoft.com/vs/community/)
- [Visual Studio Community 2026 license terms](https://visualstudio.microsoft.com/license-terms/vs2026-ga-community/)
- [Visual Studio 2026 Pre-release license terms](https://visualstudio.microsoft.com/license-terms/vs2026-prerelease/)
- [Visual Studio 2026 redistribution list](https://learn.microsoft.com/en-us/visualstudio/releases/2026/redistribution)
- [Microsoft Visual C++ v14 Redistributable and Runtime 2026 license terms](https://visualstudio.microsoft.com/license-terms/)
- [Latest supported Visual C++ Redistributable downloads](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist)

The links above are references, not legal advice. Recheck the current versions
and terms immediately before a commercial release. The redistribution page
states that files/components marked pre-release or preview in the software are
not licensed for redistribution; a validly licensed copy and unmodified
redistributable files are required.
