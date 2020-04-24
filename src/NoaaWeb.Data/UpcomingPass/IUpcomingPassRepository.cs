using System.Linq;

namespace NoaaWeb.Data.UpcomingPass
{
    public interface IUpcomingPassRepository
    {
        IQueryable<UpcomingSatellitePass> Get();
        void Insert(UpcomingSatellitePass pass);
        void Clear();
    }
}
