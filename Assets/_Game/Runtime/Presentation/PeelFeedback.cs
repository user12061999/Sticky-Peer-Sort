using System;
using UnityEngine;

namespace StickyPeerSort
{
    // Gesture notifications shared by the peel input, the recurring drag guide and the level-1 tips.
    public static class PeelSignals
    {
        public static event Action<int> StandaloneTap;   // tap (no peel progress) on an available sticker
        public static event Action<int> DragStarted;
        public static event Action<int> PeelCompleted;
        public static event Action<int> Hinted;          // Hint booster chose this sticker
        public static event Action<string> Message;      // short player-facing notice (booster failed, etc.)
        public static void Tap(int index) { StandaloneTap?.Invoke(index); }
        public static void Started(int index) { DragStarted?.Invoke(index); }
        public static void Completed(int index) { PeelCompleted?.Invoke(index); }
        public static void Hint(int index) { Hinted?.Invoke(index); }
        public static void Say(string message) { Message?.Invoke(message); }
    }

    // Spec "Peeling and feedback" 3–5: fine peel ticks + small haptic pulses that follow the finger's progress and speed,
    // silence while the finger is still, a distinct reattach sound without haptics, and one short pulse at the threshold
    // (the final peel sound itself is the existing Peel event clip). Clips are synthesised so no audio file is required.
    public sealed class PeelFeedback : MonoBehaviour
    {
        const int Rate = 44100;
        GameplayUI ui;
        GameBalanceConfig balance;
        AudioSource[] voices;
        AudioClip[] ticks;
        AudioClip reattach;
        int next;
        float forward, backward, lastTime;

        public void Initialize(GameplayUI owner, GameBalanceConfig config)
        {
            ui = owner; balance = config;
            voices = new AudioSource[4];
            for (int i = 0; i < voices.Length; i++) { voices[i] = gameObject.AddComponent<AudioSource>(); voices[i].playOnAwake = false; voices[i].spatialBlend = 0; }
            var random = new System.Random(7);
            ticks = new AudioClip[4];
            for (int i = 0; i < ticks.Length; i++) ticks[i] = Synth("peel tick " + i, .028f + i * .004f, (t, len) =>
                (float)(random.NextDouble() * 2 - 1) * Mathf.Exp(-t * 170) * (1 - t / len) + .25f * Mathf.Sin(2 * Mathf.PI * (2600 + 400 * i) * t) * Mathf.Exp(-t * 260));
            reattach = Synth("sticker reattach", .09f, (t, len) => Mathf.Sin(2 * Mathf.PI * (420 - 1900 * t) * t) * Mathf.Exp(-t * 38) * .7f);
        }

        static AudioClip Synth(string name, float seconds, Func<float, float, float> wave)
        {
            int n = Mathf.CeilToInt(seconds * Rate); var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(wave(i / (float)Rate, seconds), -1, 1);
            var clip = AudioClip.Create(name, n, 1, Rate, false); clip.SetData(data, 0); return clip;
        }

        bool Muted => ui != null && ui.AudioFeedback != null && ui.AudioFeedback.Muted;
        void Play(AudioClip clip, float volume, float pitch)
        {
            if (Muted || clip == null) return;
            var v = voices[next++ % voices.Length]; v.clip = clip; v.volume = volume; v.pitch = pitch; v.Play();
        }

        public void Begin() { forward = backward = 0; lastTime = Time.unscaledTime; }

        // Called for every drag sample. Only movement produces feedback, so a resting finger is silent.
        public void Progress(float previous, float current)
        {
            float dt = Mathf.Max(.008f, Time.unscaledTime - lastTime); lastTime = Time.unscaledTime;
            float delta = current - previous;
            if (delta > 0)
            {
                backward = 0; forward += delta;
                float speed = Mathf.Clamp01(delta / dt / balance.peelFeedbackFastSpeed);   // 0 slow … 1 fast drag
                float step = Mathf.Lerp(balance.peelTickSpacingSlow, balance.peelTickSpacingFast, speed);
                while (forward >= step)
                {
                    forward -= step;
                    Play(ticks[UnityEngine.Random.Range(0, ticks.Length)], Mathf.Lerp(.18f, .42f, speed), Mathf.Lerp(.85f, 1.35f, speed) * UnityEngine.Random.Range(.94f, 1.06f));
                    if (balance.haptics) GameHaptics.Pulse(Mathf.RoundToInt(Mathf.Lerp(6, 11, speed)), Mathf.RoundToInt(Mathf.Lerp(35, 110, speed)));
                }
            }
            else if (delta < 0)
            {
                forward = 0; backward -= delta;
                if (backward >= balance.reattachTickSpacing) { backward = 0; Play(reattach, .28f, UnityEngine.Random.Range(.95f, 1.08f)); }   // reversing: sound only, no haptics
            }
        }

        public void Reattached(float amount) { if (amount > .03f) Play(reattach, .5f, 1); }
        public void Completed() { if (balance.haptics) GameHaptics.Pulse(22, 170, true); }
    }
}
