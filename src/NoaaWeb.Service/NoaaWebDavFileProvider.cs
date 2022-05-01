using FileProviders.WebDav;
using Microsoft.Extensions.Options;
using System.Net;

namespace NoaaWeb.Service
{
    internal class NoaaWebDavFileProvider : WebDavFileProvider
    {
        public NoaaWebDavFileProvider(IOptions<WebDavConfiguration> options)
            : base(options)
        {
        }

        public bool DeleteFile(string subpath)
        {
            var uri = CheckAndGetAbsoluteUri(subpath);

            var result = _client.Delete(uri).Result;
            if (result.StatusCode == 404)
            {
                return false;
            }

            if (!result.IsSuccessful)
            {
                throw new WebException("WebDav error " + result.StatusCode + " while deleting file");
            }

            return true;
        }
    }
}
