using System;
using UnityEngine;

namespace JunkyWizard.Prototype
{
    public enum Judgement { Perfect, Good, Miss }

    /// <summary>
    /// Music-driven beat clock. Rhythm is derived only from AudioSettings.dspTime:
    /// songTime = dspTime - dspStart - firstBeatOffset.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class BeatClock : MonoBehaviour
    {
        [SerializeField] PrototypeConfig config;

        /// <summary>Raised once per beat index (frame-quantized). Beats before the first beat are negative.</summary>
        public event Action<int> OnBeat;
        public event Action OnTrackEnded;

        readonly DspTimeMapper mapper = new DspTimeMapper();
        AudioSource source;
        double dspStart;
        double trackLength;
        int lastBeat;
        int generation;
        bool running;

        public double SecondsPerBeat => 60.0 / config.bpm;
        public bool IsRunning => running;

        /// <summary>Beat position (fractional) at the current dspTime.</summary>
        public double BeatPosition => BeatAt(AudioSettings.dspTime);
        public int CurrentBeat => (int)Math.Floor(BeatPosition);

        /// <summary>Beat position from the smoothed dsp estimate. Use for visuals only.</summary>
        public double VisualBeatPosition => BeatAt(mapper.SmoothDspNow);

        public void Init(PrototypeConfig cfg, AudioClip clip)
        {
            config = cfg;
            source = GetComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            trackLength = (double)clip.samples / clip.frequency;
            mapper.Sample();
        }

        public void StartTrack()
        {
            source.Stop();
            dspStart = AudioSettings.dspTime + config.scheduleLeadTime;
            source.PlayScheduled(dspStart);
            lastBeat = (int)Math.Floor(BeatAt(AudioSettings.dspTime));
            generation++;
            running = true;
        }

        public double SongTimeAt(double dspTime) => dspTime - dspStart - config.firstBeatOffset;
        public double BeatAt(double dspTime) => SongTimeAt(dspTime) / SecondsPerBeat;
        public double SongTimeOfBeat(double beat) => beat * SecondsPerBeat;

        /// <summary>Converts an Input System timestamp (InputAction.CallbackContext.time) to dspTime.</summary>
        public double InputTimeToDsp(double inputTime) => mapper.InputTimeToDsp(inputTime);

        public Judgement Judge(double dspTimeInput) => Judge(dspTimeInput, out _);

        /// <param name="errorSeconds">Signed distance from the nearest beat (negative = early).</param>
        public Judgement Judge(double dspTimeInput, out double errorSeconds)
        {
            double beat = BeatAt(dspTimeInput);
            errorSeconds = (beat - Math.Round(beat)) * SecondsPerBeat;
            double abs = Math.Abs(errorSeconds);
            if (abs <= config.perfectWindow) return Judgement.Perfect;
            if (abs <= config.goodWindow) return Judgement.Good;
            return Judgement.Miss;
        }

        void Update()
        {
            mapper.Sample();
            if (!running) return;

            double dsp = AudioSettings.dspTime;
            int beat = (int)Math.Floor(BeatAt(dsp));
            int gen = generation;
            while (lastBeat < beat)
            {
                lastBeat++;
                OnBeat?.Invoke(lastBeat);
                if (gen != generation) return; // a handler restarted the track
            }

            if (dsp >= dspStart + trackLength)
            {
                running = false;
                OnTrackEnded?.Invoke();
            }
        }
    }
}
