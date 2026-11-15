using System;
using UnityEngine;

namespace ToastalRecall
{
    /// <summary>Scene-to-music mapping populated from the matching Assets/Music folders.</summary>
    [CreateAssetMenu(fileName = "MusicCatalog", menuName = "Toastal Recall/Music Catalog")]
    public sealed class MusicCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class ScenePlaylist
        {
            public string sceneName;
            public AudioClip[] tracks = Array.Empty<AudioClip>();
        }

        public string mainMenuSceneName = "MainMenu";
        public string[] mainMenuMusicSceneNames = { "LevelSelect", "Settings" };
        public AudioClip[] mainMenuTracks = Array.Empty<AudioClip>();
        public ScenePlaylist[] scenePlaylists = Array.Empty<ScenePlaylist>();

        public bool UsesMainMenuMusic(string sceneName)
        {
            if (string.Equals(sceneName, mainMenuSceneName, StringComparison.OrdinalIgnoreCase))
                return true;

            if (mainMenuMusicSceneNames == null)
                return false;

            foreach (string menuSceneName in mainMenuMusicSceneNames)
            {
                if (string.Equals(sceneName, menuSceneName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public AudioClip[] GetTracks(string sceneName)
        {
            if (scenePlaylists == null)
                return Array.Empty<AudioClip>();

            foreach (ScenePlaylist playlist in scenePlaylists)
            {
                if (playlist != null && string.Equals(playlist.sceneName, sceneName,
                        StringComparison.OrdinalIgnoreCase))
                    return playlist.tracks ?? Array.Empty<AudioClip>();
            }

            return Array.Empty<AudioClip>();
        }
    }
}
