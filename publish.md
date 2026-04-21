# iOS Publish vs Debug/Run — Signing Configuration Notes

This document captures the iOS code-signing situation in `PotatoVillage/PotatoVillage.csproj` so the
right configuration can be applied quickly for whichever workflow is currently needed.

> **TL;DR**
> - **Publish (App Store / IPA) works** when the signing properties live in the **common iOS**
>   `PropertyGroup` (current state in `master`).
> - **Local Debug/Run on a personal device or simulator works** when the signing properties live
>   in a **Release-only** `PropertyGroup` (or when `EnableCodeSigning=false` is forced for Debug).
> - We have **not** found a single configuration where both work simultaneously, because the
>   "iPhone Distribution" cert + `PotatoVillage` provisioning profile is a distribution profile
>   that the simulator/dev-device build path rejects, but the publish pipeline does not reliably
>   pick the props up when they are gated behind `'$(Configuration)' == 'Release'`.

---

## The two configurations

### Configuration A — "Publish works" (current `master`, default)

Use when: producing an IPA, archiving for App Store / TestFlight, or hitting the **Publish** button
in Visual Studio.

`PotatoVillage/PotatoVillage.csproj` (relevant sections):

```xml
<!-- iOS common settings for all configurations -->
<PropertyGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios'">
  <ApplicationTitle>土豆天天村</ApplicationTitle>
  <ApplicationId>com.biwuenterprise.potatovillage</ApplicationId>
  <ProvisioningType>manual</ProvisioningType>
  <EnableCodeSigning>true</EnableCodeSigning>
  <CodesignKey>iPhone Distribution: Bi Wu (24G79NU8G9)</CodesignKey>
  <CodesignProvision>PotatoVillage</CodesignProvision>
</PropertyGroup>

<!-- Debug-only iOS overrides -->
<PropertyGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios' AND '$(Configuration)' == 'Debug'">
  <MtouchLink>None</MtouchLink>
</PropertyGroup>

<!-- Suppress signing detection during VS design-time builds (project loading/IntelliSense) -->
<PropertyGroup Condition="'$(BuildingInsideVisualStudio)' == 'true' AND '$(BuildingProject)' != 'true'">
  <EnableCodeSigning>false</EnableCodeSigning>
</PropertyGroup>

<!-- Release iOS - additional settings for device/distribution -->
<PropertyGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios' AND '$(Configuration)' == 'Release'">
  <MtouchUseLlvm>true</MtouchUseLlvm>
  <BuildIpa>true</BuildIpa>
</PropertyGroup>
```

What breaks in this configuration:
- Running Debug on the iOS Simulator or a development device fails because MSBuild tries to apply
  the **iPhone Distribution** cert and the `PotatoVillage` distribution provisioning profile to a
  Debug build, which the simulator/dev path does not accept.

### Configuration B — "Debug/Run works"

Use when: actively developing — running on the iOS Simulator or a tethered dev device, hot reload,
debugging.

`PotatoVillage/PotatoVillage.csproj` (relevant sections):

```xml
<!-- iOS common settings for all configurations (no signing here so Debug/Run works without the distribution cert) -->
<PropertyGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios'">
  <ApplicationTitle>土豆天天村</ApplicationTitle>
  <ApplicationId>com.biwuenterprise.potatovillage</ApplicationId>
</PropertyGroup>

<!-- Debug-only iOS overrides -->
<PropertyGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios' AND '$(Configuration)' == 'Debug'">
  <MtouchLink>None</MtouchLink>
  <!-- Do not require the distribution cert when debugging / running locally. -->
  <EnableCodeSigning>false</EnableCodeSigning>
</PropertyGroup>

<!-- Suppress signing detection during VS design-time builds (project loading/IntelliSense) -->
<PropertyGroup Condition="'$(BuildingInsideVisualStudio)' == 'true' AND '$(BuildingProject)' != 'true'">
  <EnableCodeSigning>false</EnableCodeSigning>
</PropertyGroup>

<!-- Release iOS - signing + IPA settings for device/distribution (Publish uses this) -->
<PropertyGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios' AND '$(Configuration)' == 'Release'">
  <ProvisioningType>manual</ProvisioningType>
  <EnableCodeSigning>true</EnableCodeSigning>
  <CodesignKey>iPhone Distribution: Bi Wu (24G79NU8G9)</CodesignKey>
  <CodesignProvision>PotatoVillage</CodesignProvision>
  <MtouchUseLlvm>true</MtouchUseLlvm>
  <BuildIpa>true</BuildIpa>
</PropertyGroup>
```

What breaks in this configuration:
- The Visual Studio **Publish** button stops working — the publish pipeline does not reliably
  evaluate the `'$(Configuration)' == 'Release'` `PropertyGroup` early enough, so the distribution
  cert / profile is not applied and publishing fails or produces an unsigned/incorrectly-signed
  artifact.

---

## How to switch (mechanical recipe)

Both configurations are minor edits to the same four `PropertyGroup`s in
`PotatoVillage/PotatoVillage.csproj`. The four signing properties always travel together:

```
<ProvisioningType>manual</ProvisioningType>
<EnableCodeSigning>true</EnableCodeSigning>
<CodesignKey>iPhone Distribution: Bi Wu (24G79NU8G9)</CodesignKey>
<CodesignProvision>PotatoVillage</CodesignProvision>
```

### Switch from A → B (enable Debug/Run, disable Publish)
1. **Move** the four signing properties **out of** the common iOS `PropertyGroup` and **into** the
   `Release` iOS `PropertyGroup`.
2. **Add** `<EnableCodeSigning>false</EnableCodeSigning>` to the Debug iOS `PropertyGroup`.

### Switch from B → A (enable Publish, disable Debug/Run)
1. **Move** the four signing properties **out of** the `Release` iOS `PropertyGroup` and **into**
   the common iOS `PropertyGroup`.
2. **Remove** the `<EnableCodeSigning>false</EnableCodeSigning>` line from the Debug iOS
   `PropertyGroup`.

The design-time `PropertyGroup`
(`'$(BuildingInsideVisualStudio)' == 'true' AND '$(BuildingProject)' != 'true'`) is not touched
in either direction — leave it as-is.

### Quick git shortcuts
- The original "Publish works" form is the state at commit `0652755` and earlier (and the current
  `master` after the revert).
- The "Debug/Run works" form was introduced in commit `5e6cc71` ("Bug fixes") and later reverted.

So the fastest mechanical switch is:

```powershell
# A -> B (enable Debug/Run): restore the csproj from the 5e6cc71 commit
git checkout 5e6cc71 -- PotatoVillage/PotatoVillage.csproj

# B -> A (enable Publish): restore the csproj from the 0652755 commit
git checkout 0652755 -- PotatoVillage/PotatoVillage.csproj
```

After either checkout, review with `git diff --staged -- PotatoVillage/PotatoVillage.csproj`
and commit (or stash) as appropriate.

---

## Why it's a trade-off (root cause)

- The signing identity is a **distribution** cert + **distribution** provisioning profile. Apple's
  toolchain forbids using a distribution profile to sign a Debug build that targets the simulator
  or a development device.
- Putting signing under `'$(Configuration)' == 'Release'` is the textbook fix, but VS Publish for
  MAUI iOS over the Pair-to-Mac channel does not always evaluate `$(Configuration)` to `Release`
  at the points where signing properties have to be set, so signing silently doesn't get applied.
- A proper long-term fix would be to add a **separate Apple Development cert + a development
  provisioning profile** and key those into Debug, while keeping the distribution cert/profile
  in the common (or Release) group. That's the only way to have both workflows succeed
  simultaneously and is the recommended next step when there's time to set it up.

---

## Default policy

Keep the repo in **Configuration A (Publish works)** on `master`.
Switch to Configuration B only on a local branch / WIP commit while actively debugging on iOS, and
revert before pushing or before tagging a release.
