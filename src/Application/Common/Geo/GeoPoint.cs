using NetTopologySuite.Geometries;

namespace RemSolution.Application.Common.Geo
{
    /// <summary>
    /// Converts the latitude/longitude pair the API speaks in to the geography
    /// <see cref="Point"/> the database stores. Note the argument order: a Point
    /// is (X, Y) — longitude first — which is the opposite of how coordinates are
    /// written and said, and the easiest thing in this codebase to get backwards.
    /// </summary>
    public static class GeoPoint
    {
        // WGS 84 — the SRID SQL Server geography expects for GPS coordinates.
        public const int Srid = 4326;

        /// <summary>
        /// The point for a coordinate pair, or null when either half is missing
        /// (a location is set as a pair or not at all — see the validators).
        /// </summary>
        public static Point? ToPoint(double? latitude, double? longitude)
        {
            if (latitude is null || longitude is null)
                return null;

            return new Point(longitude.Value, latitude.Value) { SRID = Srid };
        }
    }
}
