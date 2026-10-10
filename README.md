# Godot Bone Driver

A single-script bone driver for Godot 4.4+ (C#). Drive one bone's rotation, scale, or position from another bone using math expressions, similar to drivers in Blender. Works in the editor and at runtime.

<img width="445" height="791" alt="Screenshot_20261010_140251" src="https://github.com/user-attachments/assets/0cae4dc6-c330-4058-961a-1770799d0f4a" />

## Features

- Single script, no dependencies: just drop in `BoneDriverModifier.cs`
- Built on `SkeletonModifier3D`, so it plugs into Godot's skeleton modifier pipeline
- Configure any number of drivers directly in the inspector
- Bone name dropdowns populated from the parent skeleton
- Read from rotation, scale, or position (X/Y/Z) of a source bone
- Write the result to one, two, or all three target channels at once
- Custom expressions using Godot's `Expression` class, with the source value available as `v`
- Selectable Euler rotation order per driver to avoid gimbal issues
- Drivers run top to bottom, so a driver can use the output of an earlier one as its source

## Requirements

- Godot 4.4 or newer (uses `SkeletonModifier3D` and `_ProcessModificationWithDelta`)
- Godot .NET (C#) build

## Installation

1. Copy `BoneDriverModifier.cs` into your Godot project.
2. Build the C# solution (**Build** button in the editor, or `dotnet build`).

## Usage

1. Add a **BoneDriverModifier** node as a child of a **Skeleton3D**.
2. Set **Driver Count** in the inspector.
3. Fill in each driver:

| Field | Description |
| --- | --- |
| **Enabled** | Turn the driver on or off |
| **Source Bone** | Bone to read from |
| **Source Property** | Channel to read, e.g. `rotation.y` |
| **Rotation Order** | Euler order used to read and write rotations |
| **Expression** | Math expression using `v` (the source value) |
| **Target Bone** | Bone to drive |
| **Target Property X / Y / Z** | Channels that receive the result (leave blank to skip) |

Source and target values are measured relative to each bone's rest pose.

### Example

Make a bone's X and Z scale shrink as another bone bends:

- Source Bone: `Forearm`
- Source Property: `rotation.y`
- Expression: `1.0 - abs(v) * 0.3`
- Target Bone: `ForearmTwist`
- Target Property X: `scale.x`
- Target Property Z: `scale.z`

### Expression tips

- `v` is the source value (radians for rotations).
- All of Godot's built-in math functions work, e.g. `abs(v)`, `clamp(v, 0, 1)`, `sin(v)`, `rad_to_deg(v)`.
- Expressions that fail to parse or evaluate print an error to the Output panel, and the driver is skipped.

## Rotation order and gimbal lock

Euler angles have a limitation: the **middle axis** of the chosen order is restricted to +/-90 degrees, and values flip beyond that. Pick an order whose middle axis your bone never rotates 90 degrees about.

| Order | Middle (limited) axis |
| --- | --- |
| XYZ | Y |
| XZY | Z |
| YXZ | X |
| YZX | Z |
| ZXY | X |
| ZYX | Y |

## Notes

- Bone names must match exactly. If a source or target bone isn't found, the driver does nothing.
- Non-finite results (NaN or infinity) are ignored.
- Driver settings are saved in the scene under `drivers/N/...` properties.

## License

See [LICENSE](LICENSE).
