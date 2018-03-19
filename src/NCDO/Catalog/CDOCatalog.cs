using System;
using System.Collections.Generic;
using System.IO;
using System.Json;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Threading.Tasks;
using NCDO.Interfaces;
using NCDO.Extensions;

namespace NCDO.Catalog
{
    public class CDOCatalog : ICDOCatalog
    {
        private Uri _catalogUri;
        private CDOSession _cDOSession;

        private CDOCatalog(Uri catalogUri, CDOSession cDOSession)
        {
            this._catalogUri = catalogUri;
            this._cDOSession = cDOSession;
        }

        private async Task<ICDOCatalog> Load()
        {
            using (var request = new HttpRequestMessage())
            {
                request.Method = new HttpMethod("GET");
                _cDOSession.OnOpenRequest(_cDOSession.HttpClient, request);
                request.RequestUri = _catalogUri;

                var response = await _cDOSession.HttpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead);
                await ProcessResponse(_cDOSession.HttpClient, response);
            }

            return this;
        }

        public virtual async Task ProcessResponse(HttpClient client, HttpResponseMessage response)
        {
            if ((response.Headers.TransferEncodingChunked.HasValue && response.Headers.TransferEncodingChunked.Value) ||
                response.Content.Headers.ContentLength > 0)
            {
                using (var dataStream = await response.Content.ReadAsStreamAsync())
                {
                    if (dataStream != null)
                    {
                        var jsonResponse = JsonObject.Load(dataStream);
                        if (jsonResponse == null) throw new InvalidDataException("Could not parse catalog data.");
                        jsonResponse.ThrowOnErrorResponse();
                        //init catalog from response
                        Version = jsonResponse.Get("version");
                        LastModified = jsonResponse.Get("lastModified");
                        foreach (JsonValue serviceDefinition in jsonResponse.Get("services"))
                        {
                            Services.Add(new Service(serviceDefinition));
                        }

                    }
                }
            }
        }

        public static async Task<ICDOCatalog> Load(Uri catalogUri, CDOSession cDOSession)
        {
            return await new CDOCatalog(catalogUri, cDOSession).Load();
        }

        public string Version { get; private set; }
        public string LastModified { get; private set; }
        public IList<Service> Services { get; } = new List<Service>();
    }
}
