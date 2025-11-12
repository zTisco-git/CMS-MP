using System;
using System.Reflection;
using MelonLoader;

namespace CMS21Together.Shared.Data.Vanilla;

[Serializable]
public class ModGarageCustomizationData
{
	public int SelectedFloor;
	public int SelectedWalls;
	public int SelectedCeiling;
	public int SelectedDecoration;

	public ModGarageCustomizationData()
	{
	}

	private static int GetIntValue(object obj, string[] names)
	{
		if (obj == null) return 0;
		var type = obj.GetType();
		foreach (var name in names)
		{
			var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
			if (prop != null)
			{
				try
				{
					var value = prop.GetValue(obj);
					if (value is int intVal) return intVal;
				}
				catch { }
			}
			
			var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
			if (field != null)
			{
				try
				{
					var value = field.GetValue(obj);
					if (value is int intVal) return intVal;
				}
				catch { }
			}
		}
		return 0;
	}

	private static void SetIntValue(object obj, string[] names, int value)
	{
		if (obj == null) return;
		var type = obj.GetType();
		foreach (var name in names)
		{
			var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
			if (prop != null && prop.CanWrite)
			{
				try
				{
					prop.SetValue(obj, value);
					return;
				}
				catch { }
			}
			
			var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
			if (field != null)
			{
				try
				{
					field.SetValue(obj, value);
					return;
				}
				catch { }
			}
		}
	}

	public ModGarageCustomizationData(GarageCustomizationData data)
	{
		if (data == null) return;
		
		try
		{
			SelectedFloor = GetIntValue(data, new[] { "SelectedFloor", "selectedFloor", "floor", "Floor" });
			SelectedWalls = GetIntValue(data, new[] { "SelectedWalls", "selectedWalls", "walls", "Walls" });
			SelectedCeiling = GetIntValue(data, new[] { "SelectedCeiling", "selectedCeiling", "ceiling", "Ceiling" });
			SelectedDecoration = GetIntValue(data, new[] { "SelectedDecoration", "selectedDecoration", "decoration", "Decoration" });
		}
		catch (Exception ex)
		{
			MelonLogger.Error($"Error reading GarageCustomizationData properties: {ex.Message}");
		}
	}

	public GarageCustomizationData ToGame()
	{
		var data = new GarageCustomizationData();
		try
		{
			SetIntValue(data, new[] { "SelectedFloor", "selectedFloor", "floor", "Floor" }, SelectedFloor);
			SetIntValue(data, new[] { "SelectedWalls", "selectedWalls", "walls", "Walls" }, SelectedWalls);
			SetIntValue(data, new[] { "SelectedCeiling", "selectedCeiling", "ceiling", "Ceiling" }, SelectedCeiling);
			SetIntValue(data, new[] { "SelectedDecoration", "selectedDecoration", "decoration", "Decoration" }, SelectedDecoration);
		}
		catch (Exception ex)
		{
			MelonLogger.Error($"Error setting GarageCustomizationData properties: {ex.Message}");
		}
		return data;
	}

	public bool Equals(ModGarageCustomizationData other)
	{
		if (other == null) return false;
		return SelectedFloor == other.SelectedFloor &&
		       SelectedWalls == other.SelectedWalls &&
		       SelectedCeiling == other.SelectedCeiling &&
		       SelectedDecoration == other.SelectedDecoration;
	}

	public override bool Equals(object obj)
	{
		return obj is ModGarageCustomizationData other && Equals(other);
	}

	public override int GetHashCode()
	{
		unchecked
		{
			var hashCode = SelectedFloor;
			hashCode = (hashCode * 397) ^ SelectedWalls;
			hashCode = (hashCode * 397) ^ SelectedCeiling;
			hashCode = (hashCode * 397) ^ SelectedDecoration;
			return hashCode;
		}
	}
}

