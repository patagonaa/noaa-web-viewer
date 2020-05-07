using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace NoaaWeb.Data.SatellitePass
{
    public class SatellitePassFileRepository : ISatellitePassRepository
    {
        private readonly FileDbConfiguration _dbConfig;
        private readonly ILogger<SatellitePassFileRepository> _logger;

        public SatellitePassFileRepository(ILogger<SatellitePassFileRepository> logger, IOptions<FileDbConfiguration> dbConfig)
        {
            _dbConfig = dbConfig.Value;
            _logger = logger;
        }

        public IQueryable<SatellitePass> Get()
        {
            using (var dbsr = new StreamReader(OpenDb(FileAccess.Read, FileShare.Read), Encoding.UTF8))
            {
                var dbStr = dbsr.ReadToEnd();
                var db = dbStr.Length == 0 ? new List<SatellitePass>() : JsonConvert.DeserializeObject<IList<SatellitePass>>(dbStr);
                return db.AsQueryable();
            }
        }

        public void Insert(SatellitePass pass)
        {
            using (var dbfile = OpenDb(FileAccess.ReadWrite, FileShare.None))
            {
                IList<SatellitePass> db;
                using (var dbsr = new StreamReader(dbfile, Encoding.UTF8, false, 1024, true))
                {
                    var dbStr = dbsr.ReadToEnd();
                    db = dbStr.Length == 0 ? new List<SatellitePass>() : JsonConvert.DeserializeObject<IList<SatellitePass>>(dbStr);
                }

                db.Add(pass);

                dbfile.Position = 0;
                dbfile.SetLength(0);

                using (var sbsw = new StreamWriter(dbfile, Encoding.UTF8))
                {
                    sbsw.Write(JsonConvert.SerializeObject(db, Formatting.Indented));
                }
            }
        }

        private FileStream OpenDb(FileAccess access, FileShare share)
        {
            while (true)
            {
                try
                {
                    return File.Open(Path.Combine(_dbConfig.DbDirectory, "passes.json"), FileMode.OpenOrCreate, access, share);
                }
                catch (IOException ioex)
                {
                    if (ioex.HResult != 32 && ioex.HResult != 33)
                    {
                        _logger.LogWarning(ioex, "Unhandled IOException with HResult {HResult}. Retrying.", ioex.HResult);
                    }
                    Thread.Sleep(100);
                }
            }
        }
    }
}
