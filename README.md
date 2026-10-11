# HomeFocus for Windows

HomeFocus for Windows is a parent-controlled **default-deny internet access system** designed around earned access rather than a simple website blacklist.

It is intended for a dedicated child Windows 10/11 PC or a machine where the parent understands that enabling HomeFocus affects networking for the whole computer. **On new installations, filtering initially starts OFF** until a parent creates a PIN, reviews the whitelist, and enables it. Upgrading preserves existing settings.

## What this build does

### Default-deny network enforcement

When HomeFocus is enabled, its SYSTEM agent:

1. stores the existing Windows Firewall outbound defaults;
2. changes Domain/Private/Public profiles to **Default outbound: Block**;
3. allows the HomeFocus Agent to make HTTP/HTTPS connections;
4. allows DNS, DHCP, and NTP required for normal networking;
5. optionally allows local-subnet traffic if the parent enables that less-strict option;
6. allows specific executable paths only while a reward grants them.

The child-facing browser is configured to use the HomeFocus loopback proxy at `127.0.0.1:48190`.

The proxy does **not** decrypt HTTPS. For HTTPS it evaluates the hostname from the browser's `CONNECT` request, then tunnels the encrypted stream only if that hostname is currently permitted. For ordinary HTTP it evaluates the requested URI host.

This gives HomeFocus a stronger whitelist posture than ordinary DNS blocking:

- Wi-Fi vs Ethernet does not matter.
- Switching networks does not disable the policy.
- Direct browser connections are denied by Windows Firewall.
- QUIC/HTTP3 direct UDP traffic is denied, causing compatible browsers to fall back to proxied HTTPS.
- A normal VPN application cannot establish its tunnel unless its traffic has explicitly been allowed.
- Disabling the browser proxy does not restore internet; direct web traffic remains blocked by the firewall.

No local app can be literally unbypassable by someone with Windows administrator control, firmware/boot access, or the ability to replace the OS. HomeFocus is designed to be highly resistant to ordinary child-account workarounds, not to defeat the machine administrator.

### Always-allowed whitelist

Parent Settings includes an **Always-allowed domains** list. These domains and their subdomains work while HomeFocus is locked.

Use this for school systems, learning sites, or other resources that should not require earned time.

### Reward groups

A reward group defines what becomes available temporarily.

Example `YouTube` group:

- `youtube.com`
- `youtu.be`
- `ytimg.com`
- `googlevideo.com`
- `youtubei.googleapis.com`
- `youtube.googleapis.com`

A reward can also temporarily allow a specific Windows executable path, or (less strictly) allow all web destinations during that reward.

### Tasks

Tasks exchange effort for access. Each task defines:

- Name
- Task type
- Reward group
- Reward duration
- Cooldown
- Daily reward cap
- Task-specific difficulty

Included task types:

#### Chip the Ice

The parent chooses the number of required clicks. Default examples use 100. Completion grants the configured reward for the configured number of minutes.

#### Scripture Passages / typing

The built-in **Scripture Passages** task includes selections from **both the Bible and the Book of Mormon**. Parents can edit or replace the selections and add custom passage collections for scripture mastery, spelling, vocabulary, and other practice.

A typing task specifies how many passages are required. HomeFocus randomly selects distinct passages each attempt. The child does not choose them.

During typing:

- untyped characters are gray;
- correct characters turn black;
- a mistake turns red;
- additional characters are refused until the mistake is backspaced;
- paste is disabled.

The passage library is entirely parent-editable, so scripture text is not hard-coded into the app.

### Daily limits and cooldowns

Each task can have:

- a cooldown between completions; and
- a maximum number of reward minutes it may earn in a day.

This prevents repeatedly completing a tiny task to accumulate unlimited recreational time.

### Parent PIN

The parent PIN is verified by the SYSTEM agent. The app stores only a PBKDF2-HMAC-SHA256 verifier with a random salt; it does not store the plaintext PIN.

Parent Settings requires the PIN. Child task completion never grants administrative settings access.

## Startup and failure behavior

`Install.ps1` registers **HomeFocus Agent** as a Scheduled Task that runs as `SYSTEM` at Windows startup, before a child needs the UI.

The HomeFocus UI is registered to run at interactive logon and configures that Windows user's WinINET proxy to the local HomeFocus proxy.

The enforcement posture is intentionally **fail-closed after filtering has been enabled**. On a new install, filtering starts off. If it was enabled and the UI closes, the firewall remains locked. If the UI stops, direct internet remains blocked. This is why the repository includes `Emergency-Unlock.ps1` for an administrator.

## GitHub updates

HomeFocus can periodically check a manifest stored in your repository's `main` branch, for example:

`https://raw.githubusercontent.com/lasquires/Home-Focus/main/update-manifest.json`

The manifest contains:

- version;
- URL of a prebuilt ZIP package;
- SHA-256 of that package;
- optional release notes.

HomeFocus **does not compile or execute source directly from `main`**. The SYSTEM agent downloads the prebuilt package and verifies its SHA-256 before handing it to the separate updater.

The intended publishing workflow is:

1. make your source changes;
2. run `tools/Build-Release.ps1` on Windows, or let GitHub Actions build it;
3. publish the generated ZIP as a GitHub Release asset;
4. commit the generated `update-manifest.json` to `main`;
5. installed HomeFocus agents periodically read that file and install a newer verified package when Auto Install is enabled.

For a public repository this needs no GitHub token. Private-repository update authentication is intentionally not implemented in 1.0 because storing a reusable GitHub credential on the child PC would create another security problem.

## Easy Windows installer

1. Visit [GitHub Releases](https://github.com/lasquires/Home-Focus/releases) and download the latest **HomeFocus-Setup-VERSION.exe**.
2. Double-click the installer, approve Windows administrator access, and follow the wizard.
3. Launch HomeFocus and choose a parent PIN. Review the Bible and Book of Mormon examples in **Parent Settings > Passages** and change any passages you like.
4. Enable internet filtering in Parent Settings when ready. **Filtering affects the entire PC**, not just YouTube or a single browser.

The installer bundles the .NET application: you do **not** need to install the .NET SDK, build source code, or use PowerShell. The current installer is unsigned, so Windows may show a publisher warning. To uninstall, use Windows **Settings > Apps > HomeFocus**. If necessary, see the emergency unlock instructions below.

## Build (for developers only)

Requirements:

- Windows 10/11 x64 (Windows 11 recommended)
- .NET 8 SDK
- PowerShell 5.1+
- Inno Setup 6 for building the graphical installer
- Administrator access for installation

From PowerShell:

```powershell
./tools/Build-Release.ps1 -Version 1.0.0 -Owner YOUR_GITHUB -Repo YOUR_REPO
```

This publishes self-contained single-file executables into `dist/`:

- `HomeFocus.exe`
- `HomeFocus.Agent.exe`
- `HomeFocus.Updater.exe`

and creates `HomeFocus-Windows-1.0.0.zip` plus `update-manifest.json`.

To create the graphical installer after building, run `./tools/Build-Installer.ps1 -Version 1.0.1`. Manual PowerShell installation (without the graphical installer) remains available:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
./Install.ps1
```

## Recovery

If networking is ever left locked because the agent/update fails, sign into an administrator account and run:

```powershell
./Emergency-Unlock.ps1
```

That removes only firewall rules in the `HomeFocus` rule group, restores the previously saved outbound firewall defaults, and disables the current user's HomeFocus proxy setting.

If the child account itself still has the proxy enabled, run Emergency-Unlock once while signed into that account as well, or disable that account's proxy after the firewall has been restored.

## Important limitations in 1.0

This source build is intentionally conservative:

- Enforcement is currently machine-wide while HomeFocus is enabled. It is best suited to a dedicated child PC.
- Some Windows background services will not have internet while HomeFocus is locked unless you explicitly create an access path for them. Windows Update can run while HomeFocus is disabled/parent-unlocked.
- Domain access is enforced for browser/proxy traffic. A reward that directly allows a third-party executable gives that executable direct networking for the reward period.
- A whitelisted domain can still host content you do not want; whitelist policy is hostname-based, not page-content classification.
- A local Windows administrator can disable HomeFocus. Use a standard child account and keep administrator credentials with the parent.

## Next task types

The task engine is intentionally extensible. A future `MusicPractice` task can plug into the same reward path:

`music exercise verified -> task completed -> reward grant created`

MIDI piano support is relatively straightforward; monophonic cello pitch recognition can be added later without changing the firewall/reward architecture.
