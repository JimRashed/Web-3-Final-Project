# #145 evidence — profile picture in the React client

Captured on 2026-11-23 against the stack started by `scripts/start-milestone3.ps1` on the milestone-4 branch
(`46f847b`), signed in as the demo player `player-b`.

| File | State |
|---|---|
| `145-display.png` | a saved picture rendered from the private bytes, the "Replace picture" control, and the "Picture saved." confirmation |
| `145-error.png` | a refused file (`not-an-image.txt`): the local validation message appears and the previous picture is still shown, because a rejected upload must not destroy state |
| `145-signed-out.png` | after Sign out: the profile panel is gone and the page is back to the anonymous state, so no picture or session survives locally |

The `missing` state (no picture yet) is covered by the component test in `ProfilePage.test.tsx` and by the API
journey, which asserts a 404 for a player without a picture; it is not reproducible on this database any more
because both demo players now own one.

The four live steps, the component suite and the cross-device API journey together are the evidence for #145.
