using System.Linq;

namespace NoaaWeb.Data.SatellitePass
{
    public interface ISatellitePassRepository
    {
        IQueryable<SatellitePass> Get();
        void Insert(SatellitePass pass);
    }
}