using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace ToastalRecall
{
    /// <summary>
    /// Persistent audio settings (master/music/SFX). Uses an optional AudioMixer
    /// with exposed parameters, otherwise falls back to per-source volume control.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public class AudioManager : MonoBehaviour
    {
        private const string MasterKey = "toastal.audio.master";
        private const string MusicKey = "toastal.audio.music";
        private const string SfxKey = "toastal.audio.sfx";
        private const string MixerMasterParam = "MasterVolume";
        private const string MixerMusicParam = "MusicVolume";
        private const string MixerSfxParam = "SFXVolume";

        public static AudioManager Instance { get; private set; }

        [Header("Optional Mixer")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private string masterMixerParameter = MixerMasterParam;
        [SerializeField] private string musicMixerParameter = MixerMusicParam;
        [SerializeField] private string sfxMixerParameter = MixerSfxParam;

        [Header("Fallback Sources (used when no mixer is assigned)")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;

        private readonly List<AudioSource> registeredMusic = new List<AudioSource>();
        private readonly List<AudioSource> registeredSfx = new List<AudioSource>();
        private readonly Dictionary<AudioSource, float> baseVolumes = new Dictionary<AudioSource, float>();
        private MusicCatalog musicCatalog;
        private AudioClip[] activeMenuTracks = System.Array.Empty<AudioClip>();
        private int menuTrackIndex;
        private int nextMenuTrackIndex;
        private bool playingMenuPlaylist;

        public float MasterVolume { get; private set; } = 1f;
        public float MusicVolume { get; private set; } = 1f;
        public float SfxVolume { get; private set; } = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        public static AudioManager EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            var existing = FindAnyObjectByType<AudioManager>();
            if (existing != null)
                return existing;

            var go = new GameObject("AudioManager");
            return go.AddComponent<AudioManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            musicCatalog = Resources.Load<MusicCatalog>("MusicCatalog");
            if (musicSource == null)
            {
                musicSource = GetComponent<AudioSource>();
                if (musicSource == null)
                    musicSource = gameObject.AddComponent<AudioSource>();
            }
            musicSource.playOnAwake = false;
            musicSource.loop = true;

            MasterVolume = PlayerPrefs.GetFloat(MasterKey, MasterVolume);
            MusicVolume = PlayerPrefs.GetFloat(MusicKey, MusicVolume);
            SfxVolume = PlayerPrefs.GetFloat(SfxKey, SfxVolume);

            if (musicSource != null)
                RegisterMusicSource(musicSource);
            if (sfxSource != null)
                RegisterSfxSource(sfxSource);

            ApplyAll();
            RegisterMusicSource(musicSource);
            if (SceneManager.GetActiveScene().isLoaded)
                PlaySceneMusic(SceneManager.GetActiveScene().name);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                Instance = null;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RegisterSceneSources();
            PlaySceneMusic(scene.name);
            ApplyAll();
            PlayerPrefs.Save();
        }

        private void Update()
        {
            if (!playingMenuPlaylist || musicSource == null || musicSource.isPlaying ||
                activeMenuTracks.Length < 2)
                return;

            menuTrackIndex = (menuTrackIndex + 1) % activeMenuTracks.Length;
            PlayMenuTrack(menuTrackIndex);
        }

        private void PlaySceneMusic(string sceneName)
        {
            if (musicSource == null)
                return;

            if (musicCatalog == null)
                musicCatalog = Resources.Load<MusicCatalog>("MusicCatalog");

            musicSource.Stop();
            musicSource.clip = null;
            playingMenuPlaylist = false;
            activeMenuTracks = System.Array.Empty<AudioClip>();

            if (musicCatalog == null)
            {
                Debug.LogWarning("[AudioManager] MusicCatalog is missing. Run Tools > Toastal Recall > Audio > Rebuild Music Catalog.");
                return;
            }

            if (musicCatalog.UsesMainMenuMusic(sceneName))
            {
                activeMenuTracks = musicCatalog.mainMenuTracks ?? System.Array.Empty<AudioClip>();
                if (activeMenuTracks.Length == 0)
                    return;

                if (activeMenuTracks.Length == 1)
                {
                    PlayLoopingTrack(activeMenuTracks[0]);
                    return;
                }

                playingMenuPlaylist = true;
                menuTrackIndex = nextMenuTrackIndex % activeMenuTracks.Length;
                PlayMenuTrack(menuTrackIndex);
                return;
            }

            AudioClip[] tracks = musicCatalog.GetTracks(sceneName);
            if (tracks.Length > 0)
                PlayLoopingTrack(tracks[0]);
        }

        private void PlayMenuTrack(int index)
        {
            if (activeMenuTracks.Length == 0 || musicSource == null)
                return;

            menuTrackIndex = index % activeMenuTracks.Length;
            nextMenuTrackIndex = (menuTrackIndex + 1) % activeMenuTracks.Length;
            musicSource.loop = false;
            musicSource.clip = activeMenuTracks[menuTrackIndex];
            if (musicSource.clip != null)
                musicSource.Play();
        }

        private void PlayLoopingTrack(AudioClip clip)
        {
            if (musicSource == null || clip == null)
                return;

            musicSource.loop = true;
            musicSource.clip = clip;
            musicSource.Play();
        }

        /// <summary>
        /// Picks up AudioSources in each loaded scene so the music/SFX sliders have
        /// an effect even when no AudioMixer is configured. Sources named "Music"
        /// or "BGM" are treated as music; everything else is treated as SFX.
        /// </summary>
        private void RegisterSceneSources()
        {
            PruneDestroyedSources();

            var sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude);
            foreach (var source in sources)
            {
                if (source == null ||
                    registeredMusic.Contains(source) ||
                    registeredSfx.Contains(source))
                    continue;

                string sourceName = source.gameObject.name.ToLowerInvariant();
                bool isMusic = sourceName.Contains("music") || sourceName.Contains("bgm");

                if (!isMusic && source.outputAudioMixerGroup != null)
                    isMusic = source.outputAudioMixerGroup.name.ToLowerInvariant().Contains("music");

                if (isMusic)
                    RegisterMusicSource(source);
                else
                    RegisterSfxSource(source);
            }
        }

        private void PruneDestroyedSources()
        {
            for (int i = registeredMusic.Count - 1; i >= 0; i--)
            {
                if (registeredMusic[i] == null)
                {
                    registeredMusic.RemoveAt(i);
                }
            }

            for (int i = registeredSfx.Count - 1; i >= 0; i--)
            {
                if (registeredSfx[i] == null)
                {
                    registeredSfx.RemoveAt(i);
                }
            }
        }

        public void SetMasterVolume(float value)
        {
            MasterVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MasterKey, MasterVolume);
            ApplyMaster();
        }

        public void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicKey, MusicVolume);
            ApplyMusic();
        }

        public void SetSfxVolume(float value)
        {
            SfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxKey, SfxVolume);
            ApplySfx();
        }

        public void RegisterMusicSource(AudioSource source)
        {
            if (source == null || registeredMusic.Contains(source))
                return;

            registeredMusic.Add(source);
            RememberBaseVolume(source);
            ApplyMusic();
        }

        public void RegisterSfxSource(AudioSource source)
        {
            if (source == null || registeredSfx.Contains(source))
                return;

            registeredSfx.Add(source);
            RememberBaseVolume(source);
            ApplySfx();
        }

        private void RememberBaseVolume(AudioSource source)
        {
            if (!baseVolumes.ContainsKey(source))
                baseVolumes[source] = source.volume;
        }

        private void ApplyAll()
        {
            ApplyMaster();
            ApplyMusic();
            ApplySfx();
        }

        private void ApplyMaster()
        {
            if (audioMixer != null && TrySetMixer(masterMixerParameter, MasterVolume))
                return;

            AudioListener.volume = MasterVolume;
        }

        private void ApplyMusic()
        {
            if (audioMixer != null && TrySetMixer(musicMixerParameter, MusicVolume))
                return;

            foreach (var source in registeredMusic)
            {
                if (source == null) continue;
                source.volume = GetBaseVolume(source) * MusicVolume;
            }
        }

        private void ApplySfx()
        {
            if (audioMixer != null && TrySetMixer(sfxMixerParameter, SfxVolume))
                return;

            foreach (var source in registeredSfx)
            {
                if (source == null) continue;
                source.volume = GetBaseVolume(source) * SfxVolume;
            }
        }

        private float GetBaseVolume(AudioSource source)
        {
            return baseVolumes.TryGetValue(source, out float volume) ? volume : source.volume;
        }

        private bool TrySetMixer(string parameter, float linearValue)
        {
            if (string.IsNullOrWhiteSpace(parameter))
                return false;

            float db = linearValue <= 0.0001f ? -80f : Mathf.Log10(linearValue) * 20f;
            return audioMixer.SetFloat(parameter, db);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                PlayerPrefs.Save();
        }

        private void OnApplicationQuit()
        {
            PlayerPrefs.Save();
        }
    }
}
