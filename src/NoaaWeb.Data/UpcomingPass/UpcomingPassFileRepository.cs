using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace NoaaWeb.Data.UpcomingPass
{
    public class UpcomingPassFileRepository : IUpcomingPassRepository
    {
        private readonly FileDbConfiguration _dbConfig;
        private readonly ILogger<UpcomingPassFileRepository> _logger;
        private readonly JsonSerializerOptions _serializerOptions;

        public UpcomingPassFileRepository(ILogger<UpcomingPassFileRepository> logger, IOptions<FileDbConfiguration> dbConfig)
        {
            _dbConfig = dbConfig.Value;
            _logger = logger;
            _serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.General)
            {
                WriteIndented = true
            };
        }

        public IQueryable<UpcomingSatellitePass> Get()
        {
            using (var dbsr = new StreamReader(OpenDb(FileAccess.Read, FileShare.Read), Encoding.UTF8))
            {
                var dbStr = dbsr.ReadToEnd();
                var db = (dbStr.Length == 0 ? null : JsonSerializer.Deserialize<List<UpcomingSatellitePass>>(dbStr, _serializerOptions)) ?? [];
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
                    db = (dbStr.Length == 0 ? null : JsonSerializer.Deserialize<List<UpcomingSatellitePass>>(dbStr, _serializerOptions)) ?? [];
                }

                db.AddRange(passes);

                dbfile.Position = 0;
                dbfile.SetLength(0);

                using (var sbsw = new StreamWriter(dbfile, Encoding.UTF8))
                {
                    sbsw.Write(JsonSerializer.Serialize(db, _serializerOptions));
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
                    sbsw.Write(JsonSerializer.Serialize(new List<UpcomingSatellitePass>(), _serializerOptions));
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
