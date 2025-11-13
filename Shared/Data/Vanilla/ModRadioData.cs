using System;

namespace CMS21Together.Shared.Data.Vanilla;

[Serializable]
public class ModRadioData
{
	public int currentTrackIndex;
	public bool isPlaying;
	public float volume;
	public bool isEnabled;

	public ModRadioData()
	{
		currentTrackIndex = 0;
		isPlaying = false;
		volume = 1.0f;
		isEnabled = false;
	}

	public ModRadioData(int trackIndex, bool playing, float vol, bool enabled)
	{
		currentTrackIndex = trackIndex;
		isPlaying = playing;
		volume = vol;
		isEnabled = enabled;
	}

	public bool Equals(ModRadioData other)
	{
		if (other == null) return false;
		return currentTrackIndex == other.currentTrackIndex &&
		       isPlaying == other.isPlaying &&
		       Math.Abs(volume - other.volume) < 0.01f &&
		       isEnabled == other.isEnabled;
	}
}

