using MovieTitler.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MovieTitler.Functions
{
    public class NodeInfoWellKnown(IApplicationInformation appInfo)
    {
        /// <summary>
        /// Points the user agent to the NodeInfo endpoint.
        /// </summary>
        /// <param name="req"></param>
        /// <returns>A WebFinger response</returns>
        [Function("NodeInfoWellKnown")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ".well-known/nodeinfo")] HttpRequestData req)
        {
            var resp = req.CreateResponse(HttpStatusCode.OK);
            resp.Headers.Add("Content-Type", $"application/json; charset=utf-8");
            await resp.WriteStringAsync(JsonSerializer.Serialize(new
            {
                links = new[] {
                    new
                    {
                        rel = "http://nodeinfo.diaspora.software/ns/schema/2.2",
                        href = $"https://{appInfo.ApplicationHostname}/api/nodeinfo"
                    }
                }
            }), Encoding.UTF8);
            return resp;
        }
    }
}
