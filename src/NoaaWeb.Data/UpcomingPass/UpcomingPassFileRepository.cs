using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            using var dbfile = OpenDb(FileAccess.Read, FileShare.Read);
            var db = (dbfile.Length == 0 ? null : JsonSerializer.Deserialize<List<UpcomingSatellitePass>>(dbfile, _serializerOptions)) ?? [];
            return db.AsQueryable();
        }

        public void Insert(IList<UpcomingSatellitePass> passes)
        {
            using var dbfile = OpenDb(FileAccess.ReadWrite, FileShare.None);
            var db = (dbfile.Length == 0 ? null : JsonSerializer.Deserialize<List<UpcomingSatellitePass>>(dbfile, _serializerOptions)) ?? [];

            db.AddRange(passes);

            dbfile.Position = 0;
            dbfile.SetLength(0);

            JsonSerializer.Serialize(dbfile, db, _serializerOptions);
        }

        public void Clear()
        {
            using var dbfile = OpenDb(FileAccess.ReadWrite, FileShare.None);
            dbfile.Position = 0;
            dbfile.SetLength(0);

            JsonSerializer.Serialize(dbfile, new List<UpcomingSatellitePass>(), _serializerOptions);
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
