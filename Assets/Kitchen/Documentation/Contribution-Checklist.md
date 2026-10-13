# Kitchen contribution and evidence checklist

This is a working evidence checklist, not a completed contribution submission or a score guarantee. The supplied MP2A rubric caps individual grades at **33 points for 3-credit students** and **42 points for 4-credit students**. Record only features that work in the submitted APK, are shown clearly in the video, and are accurately described in the individual statement.

## Submission identity and provenance

- [ ] Add student name, team number, all team members, Kitchen branch link, and itch.io link.
- [ ] Identify the exact final Kitchen commit and APK used for the video.
- [ ] Credit the shared HouseSim foundation and XR Origin inherited from the `bathroom` branch; preserve original asset GUIDs.
- [ ] Distinguish Kitchen edits to shared inventory code from the original team's `PlayerInventory`, `ResourceData`, `StatData`, and `HouseEvents` contributions.
- [ ] Credit Unity package/sample assets and any additional third-party assets. Do not claim teammates' prefabs or interfaces as individual Kitchen work.

## Candidate rubric evidence

The rows below identify potential submissions. All remain subject to a final scene audit, recording, and instructor evaluation. Counts refer to rubric thresholds, not an automatically earned score.

| Rubric story | Kitchen evidence to capture | Status / counting caution |
| --- | --- | --- |
| Prefab Content | Named chicken, steak, tomato, lettuce, apple, salad bowl, refrigerator, oven, or hob prefabs | Audit each claimed prefab for at least **three actual affordances or spatial dynamics**. A decorative mesh or script alone is insufficient. List exact prefab names. |
| Stat Content | Refrigerator stock; oven remaining time; timer setting; hob power; cooking progress; washing progress; bowl ingredient count, clean count, and different-kind count; trash discarded count | List distinct named stats and their visible changes. Repeated `WashProgress` on multiple ingredients is not multiple distinct stat names. UI evidence must exist for every claimed stat. |
| Passive Stat Gain — 2 | Oven countdown and meat cooking progress advance with delta time | Show the change over time with the appliance running. |
| Resource Content | Visible raw ingredients, chopped ingredients, prepared meats, salad, and energy | Record at least eight **actual visible, changing resource types** for the 3-point tier. Do not count a mesh, a stat, or unused resource asset as a player resource. |
| Wild Instances — 2 | `KitchenFridge.EnsureStockLayout` shuffles the configured ingredient-to-slot assignment before initial spawning; record repeated startups with different ingredient positions inside the refrigerator | **Implemented candidate; recording and evaluation pending.** Safe slot coordinates remain fixed, while the instantiated prefab occupying each slot is randomized and the configured ingredient quantities are preserved. All ingredients still originate inside the refrigerator. |
| Interface Metaphor — up to 4 | Refrigerator handle and stock, oven door/timer, knob and red hob ring, pan, faucet and washing, knife and cutting, salad bowl and mix, trash opening | Explain each affordance, signifier, mapping, and the corresponding kitchen metaphor. Legibility and interaction clarity need device testing. |
| Building Content | Select a module and place a real instance on a numbered regular-grid tile | Show three distinct device placement interactions for the 3-point tier. Verify spawned modules have working interactions, not only appearance. |
| Texture Animation Signifiers — 2 | Water or another working-state strip uses scrolling texture UVs | Show that the flow corresponds to a mechanic being active; a moving Transform or color flash alone is insufficient. |
| Pop-up Tutorials | First-use, world-space hints by fridge, oven, hob, faucet, knife, salad, eating, trash, and grid mechanics | Record/count at least eight **distinct spatial tutorials** for the 3-point tier. Static signs and desktop control overlays do not count as pop-ups. |
| Harvesting Content | Taking several ingredient types changes inventory and reduces available fridge stock | Candidate 3-affordance / 3-point tier; show three prefab types and the observable resource/stat change. Repeated grabs of one item must not award more inventory. |
| Caretaking Content | Wash tomato, lettuce, and apple: each instance's cleanliness increases with visible feedback | Candidate 3-prefab / 3-point tier. Record each type and its cleanliness change. |
| Shatter Content | Cut whole produce into a different harvestable chopped prefab | Candidate 3-prefab / 3-point tier if all three kinds are demonstrated; at minimum one verified instance replacement supports the 2-point tier. An animation or color change alone does not. |
| Crafting Content | Raw chicken becomes cooked chicken; raw steak becomes cooked steak; chopped produce becomes salad | Candidate 3-recipe / 2-point tier. Record corresponding inventory decreases/increases. Food appearance alone is insufficient. |
| Collision Interaction Content | Chopped food entering the bowl is destroyed and increases bowl counts; discarded food increases the trash count | These are two candidate collision scripts, not the 3-script threshold. Claim the 1-point tier unless a third qualifying script exists. Heating meat does not destroy the meat instance. |
| Foundation Buildings — 2 | Placing equipment fails on an empty tile and succeeds after a foundation is built on that same tile | Show both rejection and success. Additional equipment types do not automatically create additional foundation prefab types. |
| Death — 2 | `KitchenFood.ServingsRemaining` starts at 1; a successful eat, bowl transfer, or discard transaction consumes that serving, and `ConsumeServing` destroys the instance only when the stat reaches 0 | **Implemented candidate; demonstrate the actual 1-to-0 rule and disappearance.** Repeat use must not consume another inventory unit or award energy again. Chopping transfers the existing portion to a replacement and is not an extra serving. |
| Intermittent Instances — 2 | `KitchenFridge` samples a fresh 12–20 second delay, then randomly chooses one unobstructed empty stock slot to replenish; record this with the door left open after removing an item | **Implemented candidate; random-time generation still needs video evidence and Wild Instances evidence.** A full refrigerator causes no spawn. Opening/closing the door also refills slots, so door-triggered replenishment alone does not demonstrate this story. |
| Solar Coaster — 1 | Texture UV offset advances along a mesh strip | Confirm the strip geometry, scrolling UVs, and recording. This may also demonstrate the base Texture Animation story. |
| Git Merged — 2 | Your Kitchen PR approved and merged into main | Pending teammate workflow. Screenshot must include the purple **Merged** badge. Pushing a branch or opening a PR is insufficient. |
| Pull Request Reviewed — 2 | The merged Kitchen PR has a named assigned reviewer and approval | Pending. Screenshot must include that reviewer's green checkmark in the Reviewers panel. |
| Pull Request Reviewer | You meaningfully review and approve teammates' PRs | Pending user participation. 1 / 2 / 4 reviews correspond to 2 / 3 / 4 points. Use screenshots showing your name and approval, not merely PR quantity. |

Do not reuse the earlier plan's 47-point sum. Add only the verified tier for each story after filling this checklist. Paths, AI, neighboring-building exchanges, UV distortion, and other unimplemented options are not claimed.

## Functional acceptance

- [ ] Starting KitchenEntry loads one shared XR Origin, one Kitchen room, and the intended shared inventory.
- [ ] PC without a headset: complete every recipe, building, and disposal using WASD, mouse, E, C, and F; open/close both doors; increase/decrease both knobs; enter/leave the oven safely with held reach. Pick up a tipped pan, use R to level it, and resume cooking.
- [ ] Quest 2, **left controller only**: complete every recipe and disposal; push the left thumbstick forward to aim a teleport and release to confirm, use side flicks to snap turn, and use grip to cancel a teleport. Hold grip and physically drag each handle; turn both knobs clockwise and counterclockwise continuously. Trigger near the mouth eats held meals. Selection and trigger must not toggle doors or step knobs.
- [ ] Quest 2, **right controller only**: repeat the entire loop using the right controller with the left controller inactive; include knife contact, placing food down before changing tools, and all build buttons.
- [ ] Quest 3: repeat separate **left-only** and **right-only** runs; inspect world text, the upper-left camera HUD, frame rate, and comfort on hardware. These hardware runs remain pending until actually performed.
- [ ] Both-controller convenience: transfer ingredients between hands without duplicate inventory or energy; no recipe should require simultaneous two-hand input.
- [ ] Near-Far interaction: after grabbing a distant item, bring it close or set it on a worktop before locomotion; releasing the item must restore thumbstick teleport/snap-turn actions.
- [ ] Raw / cooked / burnt chicken changes energy by -15 / +30 / -20; lightly cooked / cooked / burnt steak changes it by +5 / +25 / -15.
- [ ] Chicken boundaries are 20 and 40 effective seconds; steak boundaries are 8 and 18 effective seconds. Verify exactly at each boundary.
- [ ] Opening the oven pauses heating/countdown; an empty oven does not heat remote food; held chicken does not heat; timer expiration stops heating. The physical dial winds back in step with remaining seconds; no timer board is displayed.
- [ ] Hob power is continuous from 0–100%; cooking rate is twice the normalized power (25% = 0.5, 50% = 1, 100% = 2). Ring redness and emission scale linearly. A removed, held, or tilted pan stops cooking. Bare-hob steak does not cook.
- [ ] Clean salad gives +20 energy; salad with any dirty ingredient gives -15. Fewer than two produce kinds refuses mixing without losing bowl contents. Washing whole/chopped produce works for a cumulative 3 seconds.
- [ ] Eating while at energy limits clamps to 0–100. An already consumed object cannot award energy or inventory again.
- [ ] Repeated fridge door transitions do not duplicate stocked objects, delete food already taken, or place new items in the player's hand. All five raw ingredient types remain available.
- [ ] Repeated startups vary ingredient-to-slot assignments; after taking an ingredient and leaving the door open, a 12–20 second delivery cycle refills an eligible empty slot without a door interaction.
- [ ] Trash consumes only released food, adjusts claimed inventory once, and preserves tools. Dropped reusable tools recover as intended. Falling below the room returns the player to the arrival point, ends active grabs, and clears falling velocity.
- [ ] Successful food use changes `ServingsRemaining` from 1 to 0 and destroys that instance; chopping preserves exactly one serving in the replacement. Unloading the Kitchen clears destroyed physical-food claims without depending on Session destruction order, while shared Energy persists.
- [ ] Foundation/occupancy rejection never spawns extra equipment; each successful grid placement produces a working appliance with independent state.
- [ ] First-use tutorials appear once per session next to the correct mechanic and remain readable in the headset.

## Build, video, and delivery

- [ ] Record editor compile and automated test results separately, including the tested commit and limitations.
- [ ] Build the Android APK with the project's Quest-compatible OpenXR configuration; record the output path and build log.
- [ ] Install and launch that exact APK on target devices. **Hardware validation is pending until these checks are completed; editor or batch-mode success is not a substitute.**
- [ ] Record a narrated video **under five minutes** showing the completed interaction loop and each claimed rubric item.
- [ ] Prepare the Individual Contribution Summary with one row per claimed story: exact prefab/stat/resource names, selected point tier, description, and video timestamp. Do not submit this unfinished checklist as that statement.
- [ ] Publish the final APK, cover image, description, authors, and the required video/screenshots on itch.io; verify its download link.
- [ ] Verify repository access and Git LFS tracking; Unity source and all needed `.meta` files must be committed. Check a fresh checkout can recover LFS assets.
- [ ] Obtain and attach real GitHub merge/review screenshots. Keep branch push, PR creation, approval, and merging as separate recorded facts.

Suggested video sequence: brief scene/control introduction; refrigerator and inventory; chicken states and oven timer; steak states and heat speed; clean versus dirty salad; trash and repeatability; grid foundation/placement; remaining tutorial/UV/stat evidence. Use timestamped cuts where needed to fit the time limit while preserving truthful evidence.
