using System.Collections.Generic;
using UnityEngine;

namespace StickyPeerSort
{
    public sealed class ImportedVfxPlayer : MonoBehaviour
    {
        sealed class Effect
        {
            public GameObject simulation;
            public RectTransform graphic;
            public ParticleSystem[] particles;
            public float expires;
        }
        readonly List<Effect> active = new List<Effect>();
        GameVfxConfig config;
        RectTransform layer;
        Camera bakeCamera;
        public void Initialize(RectTransform parent)
        {
            layer = parent; config = Resources.Load<GameVfxConfig>("StickerVfx");
            if (config == null) return;
            var cameraObject = new GameObject("VFX mesh bake camera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 0, -10);
            bakeCamera = cameraObject.AddComponent<Camera>();
            bakeCamera.enabled = false; bakeCamera.orthographic = true; bakeCamera.cullingMask = 0;
        }
        public bool Land(Vector3 position) => config != null && Play(config.landing, position, config.landingScale);
        public bool Collection(Vector3 position) => config != null && Play(config.collection, position, config.collectionScale);
        public bool Reveal(Vector3 position) => config != null && Play(config.reveal, position, config.revealScale);
        public bool Win(Vector3 position) => config != null && Play(config.win, position, config.winScale);
        bool Play(GameObject prefab, Vector3 position, float scale)
        {
            if (prefab == null || layer == null || bakeCamera == null) return false;
            while (active.Count >= Mathf.Max(1, config.maxConcurrentEffects)) Remove(0);
            var simulation = Instantiate(prefab);
            simulation.name = "VFX simulation " + prefab.name;
            simulation.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            // Imported VFX audio is routed through GameAudioFeedback instead of prefab voices.
            foreach (var audio in simulation.GetComponentsInChildren<AudioSource>(true)) { audio.Stop(); audio.enabled = false; }
            var particles = simulation.GetComponentsInChildren<ParticleSystem>(true);
            var graphic = UIFactory.Rect(prefab.name + " UI VFX", layer, Vector2.zero, Vector2.zero);
            graphic.position = position;
            foreach (var particle in particles)
            {
                var main = particle.main; main.useUnscaledTime = true; main.loop = false; main.stopAction = ParticleSystemStopAction.None;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                var renderer = particle.GetComponent<ParticleSystemRenderer>();
                if (renderer == null) continue;
                var child = UIFactory.Rect(particle.name, graphic, Vector2.zero, Vector2.zero);
                child.gameObject.AddComponent<UIParticleGraphic>().Initialize(renderer, simulation.transform, bakeCamera, scale, config);
            }
            foreach (var particle in particles) particle.Play(false);
            active.Add(new Effect { simulation = simulation, graphic = graphic, particles = particles, expires = Time.unscaledTime + config.maxLifetime });
            return true;
        }
        void Update()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                bool alive = false;
                foreach (var particle in active[i].particles) if (particle != null && particle.IsAlive(false)) { alive = true; break; }
                if (!alive || Time.unscaledTime >= active[i].expires) Remove(i);
            }
        }
        void Remove(int index)
        {
            var effect = active[index];
            if (effect.simulation != null) { effect.simulation.SetActive(false); Destroy(effect.simulation); }
            if (effect.graphic != null) { effect.graphic.gameObject.SetActive(false); Destroy(effect.graphic.gameObject); }
            active.RemoveAt(index);
        }
        public void Clear() { while (active.Count > 0) Remove(active.Count - 1); }
        void OnDisable() { Clear(); }
    }
}
