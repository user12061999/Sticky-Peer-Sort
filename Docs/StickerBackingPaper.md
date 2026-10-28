# Sticker backing paper

A translucent cream release liner follows the sticker's exact sprite alpha and size. It has no rectangular carrier, extra margin or separate imprint. The existing paper-face shader keeps the art silhouette with a soft shadow. It stays below the lifted sticker and above the board; it does not intercept input.

Cancelling or dragging back fades the attached liner away with the peel. Completing a move transfers ownership of that liner to the presentation, which releases it as the sticker starts flying. The loose paper falls independently with sideways drift, oscillating rotation, gentle mesh waves, perspective squash and a fade. The same release effect is used for gallery and waiting destinations. Transfers from waiting to a sheet do not create another liner.

Paper animation is cosmetic and does not keep input locked or change targets, buffer contents, rewards or undo. Restart/level reload clears it with the effect layer. Opening a modal clears leftover paper so it cannot cover the result or pause UI.

Configure **Sticker backing paper** on the main/reference GameBalanceConfig: enable/disable, **Backing Paper Opacity** (default 0.45; 0 is invisible and 1 is opaque), fall duration (default 0.85s), fall distance (230 UI units) and paper tint. No additional art asset is required.

Validation: 29/29 GameplaySmokeTests passed (de6b633e820447918fae1dd0053ad5dc), plus 2/2 ReferenceStylePlayTests (1edd12044e944c6984a500fe44783115). The two dedicated backing-paper lifecycle checks passed again after the final paper shape adjustment (673826d2e2924e419922c291c22e5353). Verified attached/cancelled liner, layered rendering, independent fall with collection/input still available, natural expiry and restart cleanup. Visual checks: actual Level19 partial peel and a captured fluttering fall in the test fixture.

The liner now uses the exact original artwork sprite/UV alpha, with opacity 0.45 by default; the rectangular carrier and faint imprint were removed. The updated scripts compiled without errors. Live-render validation was unavailable because the current scene had no board sticker; the lifecycle test results above predate this silhouette/opacity adjustment.

