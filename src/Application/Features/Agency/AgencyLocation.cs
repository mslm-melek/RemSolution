using NetTopologySuite.Geometries;

namespace RemSolution.Application.Features.Agency
{
    public static class AgencyLocation
    {
        // WGS 84 — the SRID SQL Server geography expects for GPS coordinates.
        public const int Srid = 4326;

        public static Point? ToPoint(double? latitude, double? longitude)
        {
            if (latitude is null || longitude is null)
                return null;

            return new Point(longitude.Value, latitude.Value) { SRID = Srid };
        }
    }
}
