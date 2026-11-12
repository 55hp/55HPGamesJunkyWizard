using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace JunkyWizard.Prototype
{
    /// <summary>
    /// Maps Input System timestamps (the InputState.currentTime timeline, also used by
    /// InputAction.CallbackContext.time) onto AudioSettings.dspTime.
    ///
    /// Every frame both clocks are read back to back and their difference stored.
    /// dspTime only advances when the audio thread mixes a buffer, so each sample
    /// under-estimates the true offset by 0..1 buffer length (a sawtooth). The maximum
    /// over a short sliding window is the sample taken right after a buffer update,
    /// i.e. the closest to the continuous offset. The window keeps slow drift between
    /// the two clocks from accumulating.
    /// </summary>
    public class DspTimeMapper
    {
        const int WindowSize = 120;

        readonly double[] offsets = new double[WindowSize];
        int count;
        int next;
        double offset;

        public bool HasSamples => count > 0;
        public double Offset => offset;

        public void Sample()
        {
            double dsp = AudioSettings.dspTime;
            double input = InputState.currentTime;

            offsets[next] = dsp - input;
            next = (next + 1) % WindowSize;
            if (count < WindowSize) count++;

            offset = double.MinValue;
            for (int i = 0; i < count; i++)
                if (offsets[i] > offset) offset = offsets[i];
        }

        public double InputTimeToDsp(double inputTime)
        {
            if (!HasSamples) Sample();
            return inputTime + offset;
        }

        /// <summary>Smooth (non buffer-stepped) estimate of the current dspTime, for visuals.</summary>
        public double SmoothDspNow => InputTimeToDsp(InputState.currentTime);
    }
}
