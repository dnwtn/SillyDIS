using System;

namespace SillyDis.Core.Services
{
    /// <summary>
    /// Converts DIS geocentric (Earth-Centered, Earth-Fixed / ECEF) coordinates
    /// to geodetic (Latitude, Longitude, Altitude) using the WGS-84 ellipsoid.
    ///
    /// DIS EntityStatePdu stores entity location as ECEF XYZ in metres.
    /// Mapsui (and most map renderers) work in geodetic lat/lon.
    ///
    /// Algorithm: Bowring iterative method — converges in 4–5 iterations,
    /// accurate to sub-millimeter globally.
    /// </summary>
    public static class CoordinateConverter
    {
        // WGS-84 ellipsoid constants
        private const double A  = 6_378_137.0;          // semi-major axis (m)
        private const double B  = 6_356_752.314_245;     // semi-minor axis (m)
        private const double E2 = 1.0 - (B * B) / (A * A); // eccentricity squared
        private const double Ep2 = (A * A) / (B * B) - 1.0; // second eccentricity squared

        /// <summary>
        /// Converts ECEF (X, Y, Z) in metres to geodetic coordinates.
        /// </summary>
        /// <param name="x">ECEF X (metres)</param>
        /// <param name="y">ECEF Y (metres)</param>
        /// <param name="z">ECEF Z (metres)</param>
        /// <param name="latDeg">Geodetic latitude in degrees (output)</param>
        /// <param name="lonDeg">Geodetic longitude in degrees (output)</param>
        /// <param name="altMetres">Altitude above WGS-84 ellipsoid in metres (output)</param>
        public static void EcefToGeodetic(
            double x, double y, double z,
            out double latDeg, out double lonDeg, out double altMetres)
        {
            // Longitude is trivial
            lonDeg = Math.Atan2(y, x) * (180.0 / Math.PI);

            double p   = Math.Sqrt(x * x + y * y);   // distance from Z-axis
            double lat = Math.Atan2(z, p * (1.0 - E2)); // initial estimate

            // Bowring iteration (5 passes is well within sub-mm accuracy)
            for (int i = 0; i < 5; i++)
            {
                double sinLat = Math.Sin(lat);
                double N      = A / Math.Sqrt(1.0 - E2 * sinLat * sinLat); // prime vertical radius
                lat = Math.Atan2(z + E2 * N * sinLat, p);
            }

            latDeg = lat * (180.0 / Math.PI);

            // Altitude
            double sinLat2 = Math.Sin(lat);
            double N2      = A / Math.Sqrt(1.0 - E2 * sinLat2 * sinLat2);
            altMetres      = (p / Math.Cos(lat)) - N2;
        }

        /// <summary>
        /// Converts geodetic (lat, lon) degrees to Mapsui/Web Mercator (EPSG:3857) in metres.
        /// Mapsui uses spherical Mercator internally.
        /// </summary>
        public static (double mx, double my) GeodeticToMercator(double latDeg, double lonDeg)
        {
            const double R = 6_378_137.0;
            double mx = R * lonDeg * Math.PI / 180.0;
            double my = R * Math.Log(Math.Tan(Math.PI / 4.0 + latDeg * Math.PI / 360.0));
            return (mx, my);
        }
    }
}
