using System;

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

	public ModGarageCustomizationData(GarageCustomizationData data)
	{
		if (data == null) return;
		SelectedFloor = data.SelectedFloor;
		SelectedWalls = data.SelectedWalls;
		SelectedCeiling = data.SelectedCeiling;
		SelectedDecoration = data.SelectedDecoration;
	}

	public GarageCustomizationData ToGame()
	{
		var data = new GarageCustomizationData();
		data.SelectedFloor = SelectedFloor;
		data.SelectedWalls = SelectedWalls;
		data.SelectedCeiling = SelectedCeiling;
		data.SelectedDecoration = SelectedDecoration;
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

