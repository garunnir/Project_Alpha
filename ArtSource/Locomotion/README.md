# Brake turn authoring

`BrakeTurn.blend` contains the preserved source action/rig and the authored rig.
Source: user's `X Bot@Running Turn 180.fbx` from Downloads.
The original scene is preserved; authoring is in `Locomotion_BrakeTurn_Work`.

`build_brake_turn.py` documents the bake. It expects the imported source rig/action
and refuses to replace an existing authored rig or export. Use a fresh working
copy for another bake. The script's ROOT must match the project location.

- Duration: 0.9 seconds, frames 1–28 at 30 fps.
- Source frames 1–7 receive more braking time.
- Pelvis lowers up to 3.5 cm during the brace.
- Two-bone leg solve preserves ankle targets and the original knee bend side.
- Left ankle is anchored during the main plant; original foot orientation remains.
- Blender measured foot drift on frames 13–20: 0.000124 m.

Unity outputs (and their `.meta` files) are in
`Assets/Dist/Visual/Anim/Authored/`: the FBX and Right/Left `.anim` variants.
FBX import uses Humanoid/CreateFromThisModel with original root orientation and
position retained. Clips do not loop. Left is Unity Humanoid mirror.
Runtime root motion stays disabled; the motor owns translation and Facing uses
the extracted normalized root yaw curve. After rebaking, refresh both `.anim`
copies, the MoveSet yaw curve, duration, and `LocomotionPoseCalibration.Bake()`.

Validation: valid humanoid import, 33 transition checks and 8 momentum checks.
Blender foot accuracy does not guarantee world-space planting on the game's
different avatar or across arbitrary movement speeds. Test continuous play when
tuning the motor braking and animation timing together.

Changed/created for this revision: this authoring directory, Authored animation
directory and metadata, CharacterLocomotionMoveSet.asset, and
docs/locomotion/LOCOMOTION.md. Original source clips and runtime scripts preserved.
