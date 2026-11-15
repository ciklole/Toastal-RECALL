# Toastal Recall — Setup Guide

## Quick start

1. Open `Assets/Scenes/MainMenu.unity` and press Play.
2. Assign your nine level scenes:
   - `Tools > Toastal Recall > Select Level Catalog`
   - Pick a `SceneAsset` for each level slot.
   - Click **Add All Assigned Levels to Build Settings**.
3. Create/import your levels under `Assets/Scenes` (or anywhere). Each level scene needs at least one Camera tagged `MainCamera`.

## Menus and UI (editable Canvas)

All menus are real, normal Canvas objects that live in their scenes. Select them in the Hierarchy and edit anything you like: text, fonts, colours, spacing, images, anchors, extra buttons.

| UI | Where |
| --- | --- |
| Main menu | `Assets/Scenes/MainMenu.unity` → `Main Menu Canvas` |
| Level select | `Assets/Scenes/LevelSelect.unity` → `Level Select Canvas` |
| Settings | `Assets/Scenes/Settings.unity` → `Settings Canvas` |
| Gameplay HUD | `Assets/ToastalRecall/Resources/UI/GameplayHUDCanvas.prefab` |
| Respawn screen | `Assets/ToastalRecall/Resources/UI/RespawnCanvas.prefab` |
| Level completed | `Assets/ToastalRecall/Resources/UI/LevelCompleteCanvas.prefab` |

`SampleScene` already contains **Gameplay HUD Canvas** (visible), **Respawn Canvas**
(hidden), and **Level Completed Canvas** (hidden) as editable prefab instances. The
hidden ones stay inactive so they never overlap the HUD until the bread dies or the
level ends.

For any other level, run `Tools > Toastal Recall > UI > Add Gameplay UI To Active Scene`
while that scene is open. It adds the same three Canvases with the correct initial
visibility. If several levels are already assigned in the `LevelCatalog`, use
`Tools > Toastal Recall > UI > Add Gameplay UI To All Catalog Levels` once.

### Adding your own sprites

1. Import the art and set its **Texture Type** to `Sprite (2D and UI)` (select the image
   → Inspector → Texture Type).
2. Open the Canvas you want to decorate:
   - `Tools > Toastal Recall > UI > Open Respawn Canvas Prefab`
   - `Tools > Toastal Recall > UI > Open Gameplay HUD Prefab`
   - `Tools > Toastal Recall > UI > Open Level Complete Prefab`
   - or open `MainMenu` / `LevelSelect` / `Settings` and select the Canvas in the Hierarchy.
3. Select an object with an `Image` component (the panel, the dim `Overlay`, or a button)
   and drag your sprite into **Image > Source Image**. The object turns into your art.
   Buttons keep working; assign the same sprite to each button's `Image`.
4. To add a completely new picture, right-click the Canvas → `UI > Image`, then assign
   the sprite and drag the RectTransform where you want it.

- Each scene also has an `EventSystem` with the Input System UI module.
- The controllers find their controls by name, so you can move or restyle them freely. If you rename a control, drag it into the matching slot on the controller (`MainMenuController`, `LevelSelectionController`, `SettingsMenuController`).
- The dynamic text (level names, volume percentages, status messages) is filled in at runtime; everything else is authored in the Canvas.
- `GameplayHUD` uses the HUD prefab automatically. You can also drag the prefab into a level scene to place a fully editable copy; the script will bind that instance instead of instantiating another.
- `LevelCompletionManager` uses the level-complete prefab automatically.
- Rebuild the generated layout at any time with `Tools > Toastal Recall > UI > Rebuild All UI (Canvases + Prefabs)`. This replaces only Canvases carrying the `EditableUICanvas` marker, so hand-made UI is safe.
- If a scene has no Canvas (for example, a level scene you build from scratch), the controllers still generate a working runtime fallback via `UIFactory`.

## Bread setup

On the bread root (the object with the bread's visual model):

1. Add a `Rigidbody` (mass ~0.3–1.0 works well; interpolation and continuous collision are set by the launcher).
2. Add a `BoxCollider`/`MeshCollider` that matches the bread.
   - Imported slice meshes often use a **concave** `MeshCollider`. A dynamic `Rigidbody` ignores those and Unity logs a warning, so `BreadLaunchController` makes bread mesh colliders **convex** automatically (`Make Mesh Colliders Convex`, on by default). Leave it on unless you authored your own colliders.
3. Add `BreadLaunchController`.
4. Add `BreadRespawnController`.
5. Assign the scene Camera to `BreadLaunchController > Gameplay Camera`.
6. Optional: assign a `Spawn Point` transform on `BreadRespawnController`. If left empty, the bread's starting position/rotation is the respawn point.
7. Put `BreadCameraFollow` on the Main Camera and leave its `Target` empty (the respawn controller finds it and assigns the bread).

The bread starts kinematic and stationary. Click and drag it backward (away from the desired launch direction), then release. The farther you drag, the stronger the launch.

### Launch tuning (BreadLaunchController)

- `Min Launch Force` / `Max Launch Force`: impulse range in Newton-seconds.
- `Drag Sensitivity`: how much mouse movement translates into pull distance.
- `Max Drag Distance`: hard cap on pull distance.
- `Minimum Drag To Launch`: smaller pulls are cancelled.
- `Launch Cooldown`: delay before another launch after a launch/respawn.
- `Apply Launch Spin` / `Spin Per Impulse` / `Max Launch Spin`: natural in-flight rotation.
- `Trajectory Samples` / `Trajectory Time Step`: ballistic preview resolution.
- `Gravity Multiplier`: per-bread gravity scale (1 = project Physics gravity).
- `Max Speed`: safety clamp against high-speed physics glitches.

## Hazards

Add `HazardObject` **to the same GameObject that has the Collider** that should kill the bread.

- Set `Trigger Mode` to `Collision`, `Trigger`, or `CollisionOrTrigger`.
- If using trigger mode, enable `Is Trigger` on that Collider.
- Normal collisions with walls/floors do nothing unless they have a `HazardObject`.

## Goal / winning condition

Add `LevelGoal` to the finish object (again, same GameObject as its Collider).

- Set `Trigger Mode` as needed.
- **Settle Time** (default `4`, range `0`-`10`) keeps the bread fully rigid and
  physical after it touches the goal. Hazards and manual respawns are locked
  immediately, but the bread keeps tumbling for that many seconds; gameplay then
  freezes and the overlay appears. Set it to `0` for the old instant stop.
- The level-completed overlay comes from the editable prefab `Resources/UI/LevelCompleteCanvas` and is shown on first completion.
- Replay, Main Menu and Next Level are handled through `LevelManager`.
- On level 9 the overlay shows **ALL LEVELS COMPLETE!** and the next button returns to the main menu.
- If the next level scene is not assigned, the next button is disabled with a warning instead of crashing.

## Respawn

A hazard shows the editable **Respawn Canvas** (`Resources/UI/RespawnCanvas`, or the
inactive instance in the level) and pauses the game. Press **RESPAWN** to reset the
bread or **MAIN MENU** to leave the level. Uncheck `Show Respawn Screen` on
`BreadRespawnController` to go back to instant automatic respawns.

The HUD also has a manual `RESPAWN` button and a `MENU` button from the editable HUD
prefab (`Resources/UI/GameplayHUDCanvas`); they appear automatically for levels that
contain a bread.
Death and manual respawn use the same `BreadRespawnController` reset path (position, rotation, velocity, angular velocity, launch state, camera).

To use your own UI, drop a Canvas named `Gameplay HUD Canvas` or `Respawn Canvas` into
the level and name the buttons `RespawnButton` / `MenuButton`, or uncheck
`Create Respawn Button` on the bread's `BreadRespawnController` and call `Respawn()`
from your own button.

## Camera walls

`BreadCameraFollow` casts a sphere from the bread to the wanted camera position and
pulls the camera in front of **walls** instead of letting it clip outside the level.
Props, food and other objects between the bread and the camera are ignored, so the
camera no longer zooms right up to the bread when a plate or cabinet is in the way.

The project creates a dedicated **`Wall`** layer. `SampleScene`'s room shell is marked
as a wall automatically, and the room shells of scenes added via
`Tools > Toastal Recall > UI > Add Gameplay UI To ...` are marked too.

For any other level, select the wall/room objects and run
`Tools > Toastal Recall > Camera > Mark Selected Objects As Walls`. You can also use
`Mark Room Objects In Active Scene` to mark every root object whose name contains
"Room" (excluding "RoomAccessories").

Tune it in the **Wall / Obstruction** header on the Main Camera:

- `Avoid Obstruction`: turn the whole feature off/on.
- `Use Wall Layer Only`: on by default; only the `Wall` layer blocks the camera.
- `Wall Layer Name`: defaults to `Wall`.
- `Obstruction Mask`: used only when `Use Wall Layer Only` is off.
- `Obstruction Radius`: probe thickness (raise it for thin walls).
- `Obstruction Margin`: gap kept between camera and wall.
- `Minimum Distance`: closest the camera may get to the bread when squeezed.

The room's walls need colliders (they normally do). If the camera still clips, check
that the relevant colliders are on the `Wall` layer and included in the camera's
Culling Mask (default `Everything`).

## Audio settings

`AudioManager` is persistent and stores values in `PlayerPrefs`.

- Master volume uses `AudioListener.volume` when no mixer is assigned.
- Without a mixer, AudioSources named `Music`/`BGM` are treated as music; every other source is treated as SFX.
- For production, assign an `AudioMixer` and expose parameters named `MasterVolume`, `MusicVolume`, `SFXVolume` (names can be changed in the AudioManager inspector).

## Level catalog

`Assets/ToastalRecall/Resources/LevelCatalog.asset` is the single source of level configuration.
No scene names are hardcoded in gameplay scripts. Menu scenes are also configured on this asset.

## 3D objects in this 2D project

The project comes from the URP 2D template and originally used only the **2D Renderer**.
A 2D Renderer never draws 3D meshes, which is why imported models were invisible.

This is fixed automatically:

- `Assets/Settings/Renderer3D.asset` is created on editor load (Universal Renderer).
- It is registered on the URP asset and set as the **default** renderer.
- Every camera left on **Renderer: Default** now renders 3D meshes and 2D sprites.
- `Renderer2D` is still in the renderer list. Pick it per-camera for scenes that
  need `Light2D` (2D lighting only works with the 2D Renderer).

Re-run it any time from `Tools > Toastal Recall > Setup 3D Rendering`.

### Bringing in a model

1. Drop the FBX/OBJ into the scene (or a `MeshRenderer` prefab).
2. Keep the camera **orthographic** for the Angry Birds look, or switch it to
   **Perspective** if you want more depth. A slight X rotation (5–15°) reveals
   the 3D shape while staying side-on.
3. Set the camera `Near Clip` / `Far Clip` so the model is inside the frustum
   (defaults of 0.3 / 1000 are fine for models at `z = 0` with the camera at `z = -10`).
4. The camera's `Culling Mask` must include the model's layer (default `Everything`).
5. Add a **Directional Light** if the scene has none. `ThreeDLightingBootstrap`
   creates a temporary one at runtime when 3D meshes exist but no directional
   light does, so models are never pitch black. Add your own light to control it.
6. Materials must use URP shaders. Imported materials using the Built-in
   `Standard` shader are converted automatically on import and on editor start.
   You can also run `Tools > Toastal Recall > Upgrade 3D Materials to URP`
   (selection/open scene) or `Upgrade All 3D Materials in Project`.

### Scale and framing

A 3D model imported at real-world scale (metres) can be far larger than the
2D world. Adjust the model's `Transform > Scale`, or change the camera's
`Orthographic Size` (the default of `5` shows 10 world units of height).
If the bread model has its own collider, the launch/camera scripts work
unchanged because aiming uses a ray from the camera.

### Renderer rules to remember

- 3D meshes + URP Lit materials need the **Universal Renderer** (`Renderer3D`).
- `Light2D` needs the **2D Renderer** (`Renderer2D`).
- URP does not support stacking a 3D camera and a 2D camera in the same view.
  If you need 2D lighting, render that scene with `Renderer2D` and treat 3D
  models as unlit or use unlit URP materials.

## Notes

- Menus and gameplay UI are authored as editable Canvases (see **Menus and UI** above). `UIFactory` remains only as a runtime fallback for scenes that have no Canvas.
- The AI Assistant package in `Library/PackageCache` was patched locally to bypass Unity's `UAC0020` analyzer. That patch can be overwritten if the package is re-imported or updated.
