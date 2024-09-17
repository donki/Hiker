namespace Hiker.Helpers
{
    public class KalmanFilter
    {
        private const float MinAccuracy = 1f;

        private float Q_metres_per_second;
        private long TimeStamp_milliseconds;
        private double lat;
        private double lng;
        private float variance; // P matrix. Negative means object uninitialised. NB: units irrelevant, as long as same units used throughout

        public KalmanFilter(float qMetresPerSecond)
        {
            Q_metres_per_second = qMetresPerSecond;
            variance = -1;
        }

        public long TimeStamp => TimeStamp_milliseconds;
        public double Latitude => lat;
        public double Longitude => lng;
        public float Accuracy => (float)Math.Sqrt(variance);

        public void SetState(double latitude, double longitude, float accuracy, long timeStampMilliseconds)
        {
            lat = latitude;
            lng = longitude;
            variance = accuracy * accuracy;
            TimeStamp_milliseconds = timeStampMilliseconds;
        }

        public void Process(double latMeasurement, double lngMeasurement, float accuracy, long timeStampMilliseconds)
        {
            if (accuracy < MinAccuracy) accuracy = MinAccuracy;
            if (variance < 0)
            {
                // If variance < 0, object is uninitialized, so initialize with current values
                TimeStamp_milliseconds = timeStampMilliseconds;
                lat = latMeasurement;
                lng = lngMeasurement;
                variance = accuracy * accuracy;
            }
            else
            {
                // Apply Kalman filter methodology
                long timeIncMilliseconds = timeStampMilliseconds - TimeStamp_milliseconds;
                if (timeIncMilliseconds > 0)
                {
                    // Time has moved on, so the uncertainty in the current position increases
                    variance += timeIncMilliseconds * Q_metres_per_second * Q_metres_per_second / 1000;
                    TimeStamp_milliseconds = timeStampMilliseconds;
                }

                // Kalman gain matrix K = Covariance * Inverse(Covariance + MeasurementVariance)
                float k = variance / (variance + accuracy * accuracy);
                // Apply K
                lat += k * (latMeasurement - lat);
                lng += k * (lngMeasurement - lng);
                // New Covariance matrix is (IdentityMatrix - K) * Covariance
                variance = (1 - k) * variance;
            }
        }
    }

}
