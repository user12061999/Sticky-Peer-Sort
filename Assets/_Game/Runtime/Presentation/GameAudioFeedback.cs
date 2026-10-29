using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace StickyPeerSort
{
    // Unity audio adapter. Rules and application flow only see IGameFeedback.
    public sealed class GameAudioFeedback : MonoBehaviour, IGameFeedback
    {
        const string MutedKey = "StickyPeerSort.SoundMuted";
        GameAudioConfig config;
        readonly AudioSource[] voices = new AudioSource[6];
        AudioSource musicVoice;
        int nextVoice;
        float nextReject;
        public bool Muted { get; private set; }
        public void Initialize(GameAudioConfig value, GameplayUI ui)
        {
            config = value; Muted = PlayerPrefs.GetInt(MutedKey, 0) != 0;
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>(); voices[i].playOnAwake = false;
                voices[i].spatialBlend = 0; voices[i].priority = i == 0 ? 64 : 128;
            }
            if (config != null && config.bgm != null)
            {
                musicVoice = gameObject.AddComponent<AudioSource>();
                musicVoice.clip = config.bgm;
                musicVoice.loop = true;
                musicVoice.volume = config.musicVolume;
                musicVoice.spatialBlend = 0;
                musicVoice.priority = 0;
                if (!Muted) musicVoice.Play();
            }
            if (ui.sound != null)
            {
                UpdateToggle(ui);
                ui.sound.onClick.AddListener(() => { SetMuted(!Muted); UpdateToggle(ui); });
            }
            foreach (var button in ui.GetComponentsInChildren<Button>(true))
                button.gameObject.AddComponent<UIButtonSound>().Initialize(this);
        }
        void UpdateToggle(GameplayUI ui) { ui.sound.GetComponentInChildren<TextMeshProUGUI>().text = Muted ? "SOUND: OFF" : "SOUND: ON"; }
        public void SetMuted(bool value)
        {
            Muted = value; PlayerPrefs.SetInt(MutedKey, value ? 1 : 0); PlayerPrefs.Save();
            if (value)
            {
                foreach (var voice in voices) if (voice != null) voice.Stop();
                if (musicVoice != null) musicVoice.Pause();
            }
            else
            {
                if (musicVoice != null && !musicVoice.isPlaying) musicVoice.Play();
            }
        }
        public void Play(PuzzleEventKind kind)
        {
            if (config == null) return;
            AudioClip clip = null;
            switch (kind)
            {
                case PuzzleEventKind.Peel: clip = config.peel; break;
                case PuzzleEventKind.Collected: clip = config.land; break;
                case PuzzleEventKind.Buffered: clip = config.buffer; break;
                case PuzzleEventKind.CategoryCompleted: clip = config.collection; break;
                case PuzzleEventKind.CategoryActivated: clip = config.entrance; break;
                case PuzzleEventKind.IceBroken: clip = config.freezeBreak != null ? config.freezeBreak : config.special; break;
                case PuzzleEventKind.Revealed: clip = config.mysteryReveal != null ? config.mysteryReveal : config.special; break;
                case PuzzleEventKind.Unlocked: clip = config.special; break;
                case PuzzleEventKind.Win: clip = config.win; break;
                case PuzzleEventKind.Fail: clip = config.fail; break;
            }
            float gain = 1;
            switch (kind)
            {
                case PuzzleEventKind.Collected: case PuzzleEventKind.Buffered: gain = config.landingVolume; break;
                case PuzzleEventKind.CategoryCompleted: gain = config.collectionVolume; break;
                case PuzzleEventKind.CategoryActivated: gain = config.entranceVolume; break;
                case PuzzleEventKind.Revealed: case PuzzleEventKind.Unlocked: case PuzzleEventKind.IceBroken: gain = config.specialVolume; break;
                case PuzzleEventKind.Win: gain = config.winVolume; break;
            }
            PlayClip(clip, kind == PuzzleEventKind.Win || kind == PuzzleEventKind.Fail, gain);
        }
        public void Reject()
        { if (config != null && Time.unscaledTime >= nextReject) { nextReject = Time.unscaledTime + .1f; PlayClip(config.reject); } }
        public void Click() { if (config != null) PlayClip(config.button); }
        void PlayClip(AudioClip clip, bool result = false, float gain = 1)
        {
            if (Muted || config == null || clip == null || voices[0] == null) return;
            int voice = result ? 0 : 1 + nextVoice++ % (voices.Length - 1);
            if (result) foreach (var source in voices) source.Stop();
            voices[voice].clip = clip; voices[voice].volume = config.volume * gain; voices[voice].Play();
        }
        public void Haptic()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }
    }
}
