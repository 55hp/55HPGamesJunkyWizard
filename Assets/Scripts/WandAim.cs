using UnityEngine;

namespace JunkyWizard.Prototype
{
    /// <summary>
    /// Wand yaw driven by held arrows: speed = min(max, start * exp(accel * holdTime)).
    /// Releasing stops immediately; reversing restarts holdTime.
    /// </summary>
    public class WandAim
    {
        readonly PrototypeConfig config;
        float holdTime;
        int heldDirection;

        public WandAim(PrototypeConfig config)
        {
            this.config = config;
        }

        /// <summary>Degrees from forward, positive = right.</summary>
        public float Angle { get; private set; }

        public void Reset()
        {
            Angle = 0f;
            holdTime = 0f;
            heldDirection = 0;
        }

        /// <summary>Direct control (mouse mode): no acceleration, no smoothing, clamped to ±wandMaxAngle.</summary>
        public void SetAngle(float angle)
        {
            Angle = Mathf.Clamp(angle, -config.wandMaxAngle, config.wandMaxAngle);
        }

        /// <param name="direction">-1 left, +1 right, 0 none (or both held).</param>
        public void Update(int direction, float deltaTime)
        {
            if (direction == 0)
            {
                heldDirection = 0;
                holdTime = 0f;
                return;
            }
            if (direction != heldDirection)
            {
                heldDirection = direction;
                holdTime = 0f;
            }

            float speed = Mathf.Min(config.wandMaxSpeed, config.wandStartSpeed * Mathf.Exp(config.WandAccel * holdTime));
            Angle = Mathf.Clamp(Angle + direction * speed * deltaTime, -config.wandMaxAngle, config.wandMaxAngle);
            holdTime += deltaTime;
        }
    }
}
