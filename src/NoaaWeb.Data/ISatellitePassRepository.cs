using System.Linq;

namespace NoaaWeb.Data
{
    public interface ISatellitePassRepository
    {
        IQueryable<SatellitePass> Get();
        void Insert(SatellitePass pass);
    }
}