# MediaForge Store packaging

MediaForge is distributed through the Microsoft Store. The Store is the customer-facing installation and update channel.

The application remains a native WPF/.NET desktop app. The MSIX package adds Windows package identity and Store eligibility while keeping the app's full-trust desktop process model. The package declares `runFullTrust` and uses `uap10:RuntimeBehavior="packagedClassicApp"` with medium integrity.

## Store identity

The package manifest is templated because the final Store identity must match the app reserved in Partner Center exactly. Microsoft requires the manifest identity values to match the Store account's identity details.

The release workflow reads these repository variables when they are set:

- `MEDIAFORGE_STORE_IDENTITY_NAME`
- `MEDIAFORGE_STORE_PUBLISHER`
- `MEDIAFORGE_STORE_PUBLISHER_DISPLAY_NAME`

Until the Partner Center product is associated, the workflow can still produce a technical MSIX using its safe development defaults.

## Customer update model

There is intentionally no second customer update channel. Once the app is published as an MSIX package in the Microsoft Store, updates are delivered through the Store. Windows checks Store app updates automatically according to the Store update service.

## Package validation

The CI pipeline:

1. Publishes the WPF app self-contained x64.
2. Bundles and verifies yt-dlp and FFmpeg.
3. Builds the MSIX with MakeAppx.
4. Unpacks the generated package again as a structural validation step.
5. Publishes the MSIX as a CI artifact for internal verification.

Before a real Store submission, the package must be associated with the reserved Partner Center identity and passed through the Windows App Certification Kit / Store certification flow.
