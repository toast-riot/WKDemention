using BepInEx.Configuration;
using UnityEngine;

namespace Demention {
	public static class Settings {
		public static ConfigEntry<float> EpisodeInterval { get; private set; }

		public static ConfigEntry<KeyCode> ForceEpisodeKey { get; private set; }

		internal static void Init(ConfigFile config) {
			string section = "1. General";
			EpisodeInterval = config.Bind(section, "Episode Interval", 60f, "Interval between episodes in seconds");

			section = "2. Debug";
			ForceEpisodeKey = config.Bind(section, "Force Episode Key", KeyCode.F7, "Key to force start a new episode");
		}
	}
}