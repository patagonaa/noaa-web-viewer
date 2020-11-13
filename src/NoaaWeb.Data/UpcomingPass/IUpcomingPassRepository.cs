using System.Collections.Generic;
using System.Linq;

namespace NoaaWeb.Data.UpcomingPass
{
    public interface IUpcomingPassRepository
    {
        IQueryable<UpcomingSatellitePass> Get();
        void Insert(IList<UpcomingSatellitePass> passes);
        void Clear();
    }
}
