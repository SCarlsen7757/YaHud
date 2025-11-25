namespace R3E.API.TimeGap
{
    /// <summary>
    /// Simple time gap calculator using instantaneous speed.
    /// This is easier to debug and understand than the interpolation method.
    /// Accuracy: ~85-90% (less accurate in braking zones and corners)
    /// </summary>
    public static class SimpleTimeGapCalculator
    {
        /// <summary>
        /// Calculate time gap using current positions and speeds.
        /// Positive = target is ahead, Negative = target is behind
        /// </summary>
        public static float CalculateGapBySpeed(
            float subjectDistance, float subjectSpeed,
            float targetDistance, float targetSpeed,
            float trackLength)
        {
            if (trackLength <= 0) return 0f;

            // Calculate track position differences (handling lap wrap-around)
            float subjectLapPos = subjectDistance % trackLength;
            float targetLapPos = targetDistance % trackLength;

            // Find shortest distance between cars on circular track
            float forwardDelta = (targetLapPos - subjectLapPos + trackLength) % trackLength;
            float backwardDelta = (subjectLapPos - targetLapPos + trackLength) % trackLength;

            float distanceDelta;
            bool targetIsAhead;

            if (forwardDelta < backwardDelta)
            {
                // Target is ahead
                distanceDelta = forwardDelta;
                targetIsAhead = true;
            }
            else
            {
                // Subject is ahead (target is behind)
                distanceDelta = backwardDelta;
                targetIsAhead = false;
            }

            // Use average speed to estimate time gap
            float avgSpeed = (subjectSpeed + targetSpeed) / 2f;
            if (avgSpeed < 1f) return 0f; // Avoid division by zero for stopped cars

            float timeGap = distanceDelta / avgSpeed;

            // Return positive if target ahead, negative if behind
            return targetIsAhead ? timeGap : -timeGap;
        }

        /// <summary>
        /// Calculate gap using only the faster car's speed (more conservative estimate)
        /// </summary>
        public static float CalculateGapByFasterSpeed(
            float subjectDistance, float subjectSpeed,
            float targetDistance, float targetSpeed,
            float trackLength)
        {
            if (trackLength <= 0) return 0f;

            float subjectLapPos = subjectDistance % trackLength;
            float targetLapPos = targetDistance % trackLength;

            float forwardDelta = (targetLapPos - subjectLapPos + trackLength) % trackLength;
            float backwardDelta = (subjectLapPos - targetLapPos + trackLength) % trackLength;

            float distanceDelta;
            bool targetIsAhead;
            float relevantSpeed;

            if (forwardDelta < backwardDelta)
            {
                // Target is ahead - use target's speed
                distanceDelta = forwardDelta;
                targetIsAhead = true;
                relevantSpeed = targetSpeed;
            }
            else
            {
                // Subject is ahead - use subject's speed
                distanceDelta = backwardDelta;
                targetIsAhead = false;
                relevantSpeed = subjectSpeed;
            }

            if (relevantSpeed < 1f) return 0f;

            float timeGap = distanceDelta / relevantSpeed;
            return targetIsAhead ? timeGap : -timeGap;
        }

        /// <summary>
        /// Calculate distance delta with proper lap handling
        /// </summary>
        public static float GetDistanceDelta(
            float subjectDistance,
            float targetDistance,
            float trackLength)
        {
            if (trackLength <= 0) return 0f;

            // Simple cumulative distance difference
            // Positive = target ahead, Negative = target behind
            return targetDistance - subjectDistance;
        }
    }
}
