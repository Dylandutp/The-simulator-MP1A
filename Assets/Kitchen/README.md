# Kitchen — CS417 MP2A

The Kitchen contribution contains three dishes: oven-roasted chicken, pan-fried steak, and washed fruit/vegetable salad. All raw ingredients come from the refrigerator. The scene implements Quest 2 / Quest 3 controller controls and a complete mouse/keyboard mode for cooking without a headset. The mappings below have been checked against the source and imported input assets; on-device interaction verification remains pending.

## Open and play

Use Unity **6000.5.6f1**, matching `ProjectSettings/ProjectVersion.txt`. Open **`Assets/Kitchen/Scenes/KitchenEntry.unity`** and enter Play mode. The entry scene loads the team's `MasterScene` and the `Kitchen` room additively. The master owns the XR Origin; the room contains its geometry, lights, appliances, ingredients, and kitchen session.

For room editing, open `MasterScene`, add `Assets/Kitchen/Scenes/Kitchen.unity`, then make **Kitchen** the active scene before adding objects. Keep room geometry out of `MasterScene`. Keep all new Kitchen assets and their `.meta` files together under `Assets/Kitchen/`.

The working branch is **`Kitchen`**. Push Kitchen commits only to that branch. Request a teammate's review before merging through a pull request.

## Controls

| Action | Quest 2 / Quest 3 | PC keyboard and mouse |
| --- | --- | --- |
| Move | On either controller, push the thumbstick forward and aim at clear floor; release the stick to teleport | WASD |
| Turn / look | Flick either thumbstick left/right for a 45-degree snap turn; look by moving your head | Hold right mouse and move the mouse |
| Cancel teleport | Press grip while aiming, before releasing the thumbstick | Not needed for continuous WASD movement |
| Pick up / release food or tools | Grip and release grip | Aim crosshair, left click to pick up; left click again to release |
| Refrigerator / oven door | Grip/select the handle and pull; alternatively keep grip held and press the same controller's trigger to open/close automatically | Aim at the door or handle and press E |
| Oven timer / induction heat | Grip/select the knob and rotate the hand; alternatively keep grip held and tap the same controller's trigger to advance a step (maximum wraps to 0) | Aim at knob, scroll up/down; E increases one step, scroll down returns toward 0 |
| Tap, mix, or build button | Aim at the labeled physical button and squeeze/release grip; selection activates it | Aim at the button and press E |
| Eat a prepared dish | Keep grip held, bring food within 30 cm of the headset, and press the same controller's trigger | Hold food and press F |
| Cut produce | Put produce on worktop and touch it with a held knife | Hold produce and press C; replacement pieces drop in the same place |
| Change reach while holding | Move the hand | Scroll while not aiming at a knob |
| Recover a tipped pan or dish | Grip and rotate it upright before setting it down | Hold it and press R; it stays level while you aim and place it |

On Windows, PC mode activates when no live headset is available; the Android Quest build keeps its XR camera/controller path. PC help and a crosshair are screen overlays; the VR interface uses signs and world-space text. PC movement uses a CharacterController, and held objects use collision checks to avoid moving through worktops. The PC cut and eat keys provide complete keyboard equivalents; VR uses the knife and bringing food to the mouth. Mouse interactions use the center crosshair, not the free mouse cursor.

Either **one left controller or one right controller** has the complete movement, grab, door, knob, button, and eating mapping. The kitchen sets both controllers to teleport plus snap turning at runtime, leaving the shared Starter Assets prefab unchanged. No step requires squeezing controls on two different controllers simultaneously. Complete left-only and right-only hardware runs remain acceptance checks, not claimed test results.

For one-controller cooking, open the door before taking the chicken, put the chicken on the oven shelf, then use that same hand to close the door and set its timer. Leave the pan resting on the hob, add steak, then turn the heat knob. Turn the faucet on first, rinse produce, set it on the cutting board, release it, and pick up the knife with the same hand. Set down the knife, transfer the chopped ingredients, and select Mix. The second hand makes these transfers faster. When a distant object is selected, the Starter Assets may use the thumbstick to manipulate it and temporarily suppress locomotion; release it onto a worktop before teleporting or turning.

For PC oven placement, open the door first, hold the chicken, aim into the chamber, adjust held reach with the wheel, and release it onto the shelf before closing the door. Press R while holding a tipped pan or dish to level it before placing it. Meat still held in a hand does not cook.

## Recipes and energy rules

Energy starts at **70 / 100** when the shared inventory first gains this resource, stays between 0 and 100, and preserves its existing value when the Kitchen reloads. These thresholds are accelerated game timings, not real cooking instructions. The food definition assets contain the cooking thresholds.

| Dish / state | Cooking condition | Appearance | Energy when eaten |
| --- | --- | --- | ---: |
| Chicken, undercooked | Less than 20 effective seconds | Pale | -15 |
| Chicken, cooked | At least 20 and less than 40 effective seconds | Golden brown | +30 |
| Chicken, burnt | 40 effective seconds or more | Blackened | -20 |
| Steak, lightly cooked | Less than 8 effective seconds | Red / pale | +5 |
| Steak, cooked | At least 8 and less than 18 effective seconds | Brown | +25 |
| Steak, burnt | 18 effective seconds or more | Blackened | -15 |
| Clean salad | All included produce washed fully | Fresh produce colors | +20 |
| Dirty salad | Any included produce not washed fully | Dull, dirty appearance | -15 |

**Chicken:** take raw chicken from the fridge, release it inside the oven, set its timer, and close the door. The timer offers 0–60 seconds in 5-second steps. Opening the door pauses both heat and countdown. The oven turns off and rings when time expires. Existing cooking progress remains if the door is opened or another timer is set, so reheating can burn an already cooked chicken. Taking chicken out of the chamber stops its heating.

**Steak:** release steak into the flat pan, place the pan upright on the induction ring, and set the heat knob. Level 0 is off. Levels 1 / 2 / 3 advance cooking by 0.5 / 1 / 2 effective seconds per real second. At those levels, cooked steak begins after approximately 16 / 8 / 4 seconds; burning begins after 36 / 18 / 9 seconds. The ring becomes brighter red at higher power. Lifting the pan off the burner or taking the steak out stops heating. A steak directly on the bare hob does not cook.

**Salad:** take tomato, lettuce, and/or apple from the fridge. Turn on the faucet and hold each ingredient in the water for a cumulative **3 seconds**, then cut it. Chopped produce may also be washed. Release chopped produce of **at least two different kinds** inside the salad bowl and select Mix. The bowl accepts one portion of each kind. One unwashed ingredient makes the finished salad dirty; washing the finished dish does not undo this. Whole produce cannot be added directly or eaten as a completed recipe.

## Refrigerator, disposal, and building

At startup, the fridge randomly assigns its configured ingredients to safe interior slots while preserving their quantities. Opening or closing the fridge door replenishes vacant ingredient slots. It also attempts one automatic delivery after each randomly sampled **12–20 second** interval, selecting an eligible empty slot; a full refrigerator causes no new spawn. Ingredients already taken remain in the world; existing slot contents are not duplicated or replaced under the player's hand. The stock system separates displayed stock from the player's inventory.

Release unwanted food into the trash beside the preparation area. Discarding removes a claimed item from inventory without giving energy. Food has one remaining serving; successful eating, bowl transfer, or disposal reduces it to zero and destroys the physical instance. Cutting transfers that same serving into the chopped replacement. The trash does not delete the pan, knife, or salad bowl. Dropped tools with a return component recover to their worktop rather than leaving the cooking loop without a utensil.

The optional expansion grid leaves the furnished kitchen usable. Select **Foundation**, then a numbered tile; select an appliance and use the same tile. Equipment requires an existing foundation, and an occupied tile refuses another module. Green/red tile indicators show whether placement is valid. Building is free and creates working prefab instances; it does not move the original appliances.

## Team integration and attribution

The shared HouseSim foundation and XR Origin were inherited from the team's **`bathroom`** work, preserving the original Unity asset GUIDs. The shared `PlayerInventory`, `ResourceData`, `StatData`, and `HouseEvents` files are team infrastructure; their original creation is not claimed as Kitchen authorship. Kitchen-specific inventory additions should be described separately in the contribution statement. Unity's XR Interaction Toolkit Starter Assets and TextMesh Pro assets retain their upstream attribution.

Kitchen uses `HouseSim.Kitchen`, shared `ResourceData` assets, and atomic inventory exchanges. A consumed, discarded, or transferred item is settled once. Other rooms should use the same resource assets to exchange ingredients or energy; they should not create duplicate resource assets with matching display names and expect them to share inventory identity. The kitchen should have one active KitchenSession and share the team's player inventory.

## Verification and submission

For a Windows build, use **Kitchen > Build Windows Preview**. For an Android build from a path containing Chinese characters, close the editor and run `Tools/Build-Quest.ps1` in PowerShell. It copies only Assets, Packages, and ProjectSettings to a fresh ASCII temporary directory, builds there, then returns the APK to `Builds/Quest/Kitchen.apk`. The source checkout stays in place. The temporary build copy is retained for diagnostics. On an ASCII project path, **Kitchen > Build Quest APK** also works.

See [快速测试指南.md](Documentation/快速测试指南.md) for the Chinese play guide. Rule checks are available from **Kitchen > Validate Food Rules**; the full Play Mode regression entry is `HouseSim.Kitchen.Editor.KitchenPlayChecks.Run` (batch mode without `-quit`, because the harness exits after editor teardown).

Consult [Contribution-Checklist.md](Documentation/Contribution-Checklist.md) for evidence requirements and pending checks. Source implementation, editor compilation, automated checks, APK build, and on-headset testing are separate verification stages. **Quest 2 / Quest 3 hardware interaction, comfort, and performance remain pending until tested on those devices.** A build alone does not establish those results.

The original proposed 47-point budget is not a claim that 47 points have been implemented or awarded. The changed recipes and supply design require a fresh rubric audit and a recorded demonstration. Approved PRs, reviewer screenshots, video, APK, itch.io page, and the final contribution statement require their own evidence.
