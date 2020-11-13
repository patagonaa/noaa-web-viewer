using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace NoaaWeb.Data.UpcomingPass
{
    public class UpcomingPassFileRepository : IUpcomingPassRepository
    {
        private readonly FileDbConfiguration _dbConfig;
        private readonly ILogger<UpcomingPassFileRepository> _logger;

        public UpcomingPassFileRepository(ILogger<UpcomingPassFileRepository> logger, IOptions<FileDbConfiguration> dbConfig)
        {
            _dbConfig = dbConfig.Value;
            _logger = logger;
        }

        public IQueryable<UpcomingSatellitePass> Get()
        {
            using (var dbsr = new StreamReader(OpenDb(FileAccess.Read, FileShare.Read), Encoding.UTF8))
            {
                var dbStr = dbsr.ReadToEnd();
                var db = dbStr.Length == 0 ? new List<UpcomingSatellitePass>() : JsonConvert.DeserializeObject<IList<UpcomingSatellitePass>>(dbStr);
                return db.AsQueryable();
            }
        }

        public void Insert(IList<UpcomingSatellitePass> passes)
        {
            using (var dbfile = OpenDb(FileAccess.ReadWrite, FileShare.None))
            {
                List<UpcomingSatellitePass> db;
                using (var dbsr = new StreamReader(dbfile, Encoding.UTF8, false, 1024, true))
                {
                    var dbStr = dbsr.ReadToEnd();
                    db = dbStr.Length == 0 ? new List<UpcomingSatellitePass>() : JsonConvert.DeserializeObject<List<UpcomingSatellitePass>>(dbStr);
                }

                db.AddRange(passes);

                dbfile.Position = 0;
                dbfile.SetLength(0);

                using (var sbsw = new StreamWriter(dbfile, Encoding.UTF8))
                {
                    sbsw.Write(JsonConvert.SerializeObject(db, Formatting.Indented));
                }
            }
        }

        public void Clear()
        {
            using (var dbfile = OpenDb(FileAccess.ReadWrite, FileShare.None))
            {
                dbfile.Position = 0;
                dbfile.SetLength(0);

                using (var sbsw = new StreamWriter(dbfile, Encoding.UTF8))
                {
                    sbsw.Write(JsonConvert.SerializeObject(new List<UpcomingSatellitePass>(), Formatting.Indented));
                }
            }
        }

        private FileStream OpenDb(FileAccess access, FileShare share)
        {
            while (true)
            {
                try
                {
                    return File.Open(Path.Combine(_dbConfig.DbDirectory, "upcoming_passes.json"), FileMode.OpenOrCreate, access, share);
                }
                catch (IOException ioex)
                {
                    if (ioex.HResult != 32 && ioex.HResult != 33)
                    {
                        _logger.LogWarning(ioex, "Unhandled IOException. Retrying.");
                    }
                    Thread.Sleep(100);
                }
            }
        }
    }
}
