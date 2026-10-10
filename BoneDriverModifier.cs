using System.Collections.Generic;
using Godot;

// A single-script bone driver node. Add it as a child of a Skeleton3D, set "Driver Count",
// and fill in each driver in the inspector. Works in the editor and at runtime.
// Requires Godot 4.4+ (uses _ProcessModificationWithDelta).
[Tool]
[GlobalClass]
public partial class BoneDriverModifier : SkeletonModifier3D
{
	private const string PropertyChoices =
		"rotation.x,rotation.y,rotation.z,scale.x,scale.y,scale.z,position.x,position.y,position.z";

	// Same order as Godot's EulerOrder enum (XYZ=0, XZY=1, YXZ=2, YZX=3, ZXY=4, ZYX=5).
	// The *middle* letter is the axis limited to +/-90 deg (gimbal flip). Pick an order whose
	// middle axis your bone never rotates 90 deg about. XZY = middle axis Z, Y is the innermost axis.
	private const string RotationOrders = "XYZ,XZY,YXZ,YZX,ZXY,ZYX";

	private class Driver
	{
		public bool Enabled = true;
		public string SourceBone = "";
		public string SourceProperty = "rotation.y";
		public int RotationOrder = 1;
		public string Expr = "v";
		public string TargetBone = "";
		public string TargetX = "scale.x";
		public string TargetY = "";
		public string TargetZ = "scale.z";

		public readonly Expression Compiled = new();
		public bool Parsed;
		public string ParsedText;
	}

	private readonly List<Driver> _drivers = new();

	[Export(PropertyHint.Range, "0,64,1,or_greater")]
	public int DriverCount
	{
		get => _drivers.Count;
		set
		{
			value = Mathf.Max(0, value);
			while (_drivers.Count < value) _drivers.Add(new Driver());
			if (_drivers.Count > value) _drivers.RemoveRange(value, _drivers.Count - value);
			NotifyPropertyListChanged();
		}
	}

	// ---------------------------------------------------------------- inspector properties

	public override void _EnterTree()
	{
		// Refresh bone-name dropdowns once the skeleton is reachable.
		NotifyPropertyListChanged();
	}

	private static Godot.Collections.Dictionary Prop(string name, Variant.Type type,
		PropertyHint hint = PropertyHint.None, string hintString = "")
	{
		return new Godot.Collections.Dictionary
		{
			{ "name", name },
			{ "type", (int)type },
			{ "hint", (int)hint },
			{ "hint_string", hintString },
			{ "usage", (int)PropertyUsageFlags.Default },
		};
	}

	private string BuildBoneHint()
	{
		Skeleton3D skeleton = GetSkeleton();
		if (skeleton == null) return null;

		var names = new List<string> { "" }; // empty first entry = "none"
		for (int i = 0; i < skeleton.GetBoneCount(); i++)
			names.Add(skeleton.GetBoneName(i));
		return string.Join(",", names);
	}

	public override Godot.Collections.Array<Godot.Collections.Dictionary> _GetPropertyList()
	{
		var list = new Godot.Collections.Array<Godot.Collections.Dictionary>();
		string boneHint = BuildBoneHint();
		PropertyHint boneHintType = boneHint != null ? PropertyHint.Enum : PropertyHint.None;
		string optionalProps = "," + PropertyChoices;

		for (int i = 0; i < _drivers.Count; i++)
		{
			string p = $"drivers/{i}/";
			list.Add(Prop(p + "enabled", Variant.Type.Bool));
			list.Add(Prop(p + "source_bone", Variant.Type.String, boneHintType, boneHint ?? ""));
			list.Add(Prop(p + "source_property", Variant.Type.String, PropertyHint.Enum, PropertyChoices));
			list.Add(Prop(p + "rotation_order", Variant.Type.Int, PropertyHint.Enum, RotationOrders));
			list.Add(Prop(p + "expression", Variant.Type.String, PropertyHint.MultilineText));
			list.Add(Prop(p + "target_bone", Variant.Type.String, boneHintType, boneHint ?? ""));
			list.Add(Prop(p + "target_property_x", Variant.Type.String, PropertyHint.Enum, optionalProps));
			list.Add(Prop(p + "target_property_y", Variant.Type.String, PropertyHint.Enum, optionalProps));
			list.Add(Prop(p + "target_property_z", Variant.Type.String, PropertyHint.Enum, optionalProps));
		}
		return list;
	}

	private static bool TryParseName(string name, out int index, out string field)
	{
		index = 0;
		field = null;
		if (!name.StartsWith("drivers/")) return false;

		string[] parts = name.Split('/');
		if (parts.Length != 3 || !int.TryParse(parts[1], out index) || index < 0) return false;

		field = parts[2];
		return true;
	}

	public override Variant _Get(StringName property)
	{
		if (!TryParseName(property.ToString(), out int i, out string field) || i >= _drivers.Count)
			return default;

		Driver d = _drivers[i];
		switch (field)
		{
			case "enabled": return Variant.From(d.Enabled);
			case "source_bone": return Variant.From(d.SourceBone);
			case "source_property": return Variant.From(d.SourceProperty);
			case "rotation_order": return Variant.From(d.RotationOrder);
			case "expression": return Variant.From(d.Expr);
			case "target_bone": return Variant.From(d.TargetBone);
			case "target_property_x": return Variant.From(d.TargetX);
			case "target_property_y": return Variant.From(d.TargetY);
			case "target_property_z": return Variant.From(d.TargetZ);
			default: return default;
		}
	}

	public override bool _Set(StringName property, Variant value)
	{
		if (!TryParseName(property.ToString(), out int i, out string field) || i >= 1024)
			return false;

		// Scene loading may set driver fields before DriverCount; grow the list as needed.
		while (_drivers.Count <= i) _drivers.Add(new Driver());
		Driver d = _drivers[i];

		switch (field)
		{
			case "enabled": d.Enabled = value.AsBool(); return true;
			case "source_bone": d.SourceBone = value.AsString(); return true;
			case "source_property": d.SourceProperty = value.AsString(); return true;
			case "rotation_order": d.RotationOrder = Mathf.Clamp(value.AsInt32(), 0, 5); return true;
			case "expression": d.Expr = value.AsString(); return true;
			case "target_bone": d.TargetBone = value.AsString(); return true;
			case "target_property_x": d.TargetX = value.AsString(); return true;
			case "target_property_y": d.TargetY = value.AsString(); return true;
			case "target_property_z": d.TargetZ = value.AsString(); return true;
			default: return false;
		}
	}

	// ---------------------------------------------------------------- evaluation

	// Drivers run top to bottom, so a driver can use the output of an earlier one as its source.
	public override void _ProcessModificationWithDelta(double delta)
	{
		Skeleton3D skeleton = GetSkeleton();
		if (skeleton == null) return;

		foreach (Driver d in _drivers)
		{
			if (d.Enabled) Evaluate(d, skeleton);
		}
	}

	private static void Evaluate(Driver d, Skeleton3D skeleton)
	{
		int sourceIdx = skeleton.FindBone(d.SourceBone);
		if (sourceIdx < 0) return;

		int targetIdx = skeleton.FindBone(d.TargetBone);
		if (targetIdx < 0) return;

		EulerOrder order = (EulerOrder)d.RotationOrder;

		if (!TryGetBoneProperty(skeleton, sourceIdx, d.SourceProperty, order, out float sourceValue))
			return;

		// Re-parse if the expression text was edited.
		if (!d.Parsed || d.ParsedText != d.Expr)
		{
			d.ParsedText = d.Expr; // remember it even on failure so errors don't spam every frame
			Error err = d.Compiled.Parse(d.Expr, new string[] { "v" });
			if (err != Error.Ok)
			{
				d.Parsed = false;
				GD.PrintErr($"Driver expression parse error in '{d.Expr}': {d.Compiled.GetErrorText()}");
				return;
			}
			d.Parsed = true;
		}
		else if (!d.Parsed)
		{
			return;
		}

		Variant result = d.Compiled.Execute(new Godot.Collections.Array { sourceValue });
		if (d.Compiled.HasExecuteFailed())
		{
			GD.PrintErr($"Driver expression execute error in '{d.Expr}': {d.Compiled.GetErrorText()}");
			return;
		}

		float targetValue = result.AsSingle();
		if (!float.IsFinite(targetValue)) return;

		ApplyToTarget(d, skeleton, targetIdx, targetValue, order);
	}

	private static Transform3D GetLocalDelta(Skeleton3D skeleton, int boneIdx)
	{
		Transform3D rest = skeleton.GetBoneRest(boneIdx);
		Transform3D pose = skeleton.GetBonePose(boneIdx);
		return rest.AffineInverse() * pose;
	}

	private static void Decompose(Transform3D t, EulerOrder order, out Vector3 euler, out Vector3 scale, out Vector3 pos)
	{
		Vector3 c0 = t.Basis.Column0;
		Vector3 c1 = t.Basis.Column1;
		Vector3 c2 = t.Basis.Column2;

		float sx = c0.Length();
		float sy = c1.Length();
		float sz = c2.Length();

		if (t.Basis.Determinant() < 0f) sx = -sx;

		scale = new Vector3(sx, sy, sz);

		if (Mathf.Abs(sx) > 1e-9f) c0 /= sx; else c0 = Vector3.Right;
		if (Mathf.Abs(sy) > 1e-9f) c1 /= sy; else c1 = Vector3.Up;
		if (Mathf.Abs(sz) > 1e-9f) c2 /= sz; else c2 = Vector3.Back;

		Basis rot = new Basis(c0, c1, c2).Orthonormalized();
		euler = rot.GetEuler(order);
		pos = t.Origin;
	}

	private static Transform3D Compose(Vector3 euler, Vector3 scale, Vector3 pos, EulerOrder order)
	{
		Basis rot = Basis.FromEuler(euler, order);
		Basis basis = new Basis(
			rot.Column0 * scale.X,
			rot.Column1 * scale.Y,
			rot.Column2 * scale.Z
		);
		return new Transform3D(basis, pos);
	}

	private static bool TryGetBoneProperty(Skeleton3D skeleton, int boneIdx, string property, EulerOrder order, out float value)
	{
		value = 0f;
		string[] parts = property.Split('.');
		if (parts.Length != 2) return false;

		string type = parts[0].ToLowerInvariant();
		string axis = parts[1].ToLowerInvariant();

		Decompose(GetLocalDelta(skeleton, boneIdx), order, out Vector3 euler, out Vector3 scale, out Vector3 pos);

		switch (type)
		{
			case "rotation":
				value = axis switch { "x" => euler.X, "y" => euler.Y, "z" => euler.Z, _ => 0f };
				return true;
			case "scale":
				value = axis switch { "x" => scale.X, "y" => scale.Y, "z" => scale.Z, _ => 0f };
				return true;
			case "location":
			case "position":
				value = axis switch { "x" => pos.X, "y" => pos.Y, "z" => pos.Z, _ => 0f };
				return true;
			default:
				return false;
		}
	}

	private static bool SetComponent(string property, float value, ref Vector3 euler, ref Vector3 scale, ref Vector3 pos)
	{
		string[] parts = property.Split('.');
		if (parts.Length != 2) return false;

		string type = parts[0].ToLowerInvariant();
		string axis = parts[1].ToLowerInvariant();

		ref Vector3 v = ref pos;
		switch (type)
		{
			case "rotation": v = ref euler; break;
			case "scale": v = ref scale; break;
			case "location":
			case "position": v = ref pos; break;
			default: return false;
		}

		switch (axis)
		{
			case "x": v.X = value; return true;
			case "y": v.Y = value; return true;
			case "z": v.Z = value; return true;
			default: return false;
		}
	}

	// Decompose the target once, set all driven channels, recompose once.
	private static void ApplyToTarget(Driver d, Skeleton3D skeleton, int boneIdx, float value, EulerOrder order)
	{
		Transform3D rest = skeleton.GetBoneRest(boneIdx);
		Decompose(GetLocalDelta(skeleton, boneIdx), order, out Vector3 euler, out Vector3 scale, out Vector3 pos);

		bool changed = false;
		if (!string.IsNullOrEmpty(d.TargetX)) changed |= SetComponent(d.TargetX, value, ref euler, ref scale, ref pos);
		if (!string.IsNullOrEmpty(d.TargetY)) changed |= SetComponent(d.TargetY, value, ref euler, ref scale, ref pos);
		if (!string.IsNullOrEmpty(d.TargetZ)) changed |= SetComponent(d.TargetZ, value, ref euler, ref scale, ref pos);
		if (!changed) return;

		if (!float.IsFinite(euler.X) || !float.IsFinite(euler.Y) || !float.IsFinite(euler.Z) ||
			!float.IsFinite(scale.X) || !float.IsFinite(scale.Y) || !float.IsFinite(scale.Z) ||
			!float.IsFinite(pos.X) || !float.IsFinite(pos.Y) || !float.IsFinite(pos.Z))
			return;

		skeleton.SetBonePose(boneIdx, rest * Compose(euler, scale, pos, order));
	}
}
