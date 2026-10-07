# Build handoff rule

Status: **CANONICAL OPERATIONAL RULE**

This rule applies every time a Windows test build is handed to the user for manual validation.

## Mandatory response order

When a build is ready for the user to test, the assistant must provide, in this order:

1. **Direct download in the chat**
   - Attach or expose the downloadable build artifact directly in the conversation whenever the available tools allow it.
   - Do not make the user navigate to the GitHub repository, Actions page, run page or artifact list just to obtain a build that the assistant has asked them to test.
   - The build number, artifact ID, commit and checksum may also be shown for traceability, but they do not replace the direct download.
   - The user-facing download link/file name must follow the canonical convention:
     `SolarEnergyMonitor-Build-XXX-win-x64.zip`
     where `XXX` is the Windows Build number.
   - This naming convention remains active until the owner explicitly changes it or there is a documented technical reason to adopt a new convention.
   - If the underlying GitHub Actions artifact has a different internal name, the handoff must still expose/download it to the user using the canonical filename whenever tooling permits.

2. **Short identification**
   - Build number.
   - Code commit used for that build.
   - CI result.
   - Artifact/checksum when relevant.

3. **Exact manual test steps**
   - State what existing Data folder or files must be copied/reused.
   - State only the checks required for that build.
   - State exactly what evidence/artifact the user should return.
   - Explicitly state what does **not** need to be repeated when relevant.

## If direct attachment is technically unavailable

If the current interface/tooling cannot attach the artifact directly:
- say so explicitly;
- provide the closest direct download mechanism available;
- do not merely tell the user to “go to GitHub” without a concrete download target.

## Rationale

The user is the target-PC tester, not the release operator. Build handoff should minimize repository navigation and make every validation cycle self-contained in the chat.

## Current example

For Build 310:
- Windows Build: 310;
- code commit: `1f942ad2435a2835ab78eea471b3b8f8f40aacda`;
- artifact ID: `10939387805`;
- artifact name: `SolarEnergyMonitor-win-x64-dev`;
- SHA-256: `0975d5ccdcae0e65a1f85c4fd88ed69043f3a5c69b20a9f1750ab5ddeb49661c`.

The chat handoff must include the downloadable ZIP first, followed by the test steps.
