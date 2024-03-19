using MovieTitler.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MovieTitler.LowLevel;
using MovieTitler.Data;
using Microsoft.EntityFrameworkCore;

namespace MovieTitler.Functions
{
    public class NodeInfo(BotDbContext context, IApplicationInformation appInfo)
    {
        /// <summary>
        /// Returns a NodeInfo 2.2 response with information about the bot.
        /// </summary>
        /// <param name="req"></param>
        /// <returns>A NodeInfo response</returns>
        [Function("NodeInfo")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/nodeinfo")] HttpRequestData req)
        {
            var user = Domain.Actor;

            int postCount = await context.GeneratedPosts.CountAsync();

            var resp = req.CreateResponse(HttpStatusCode.OK);
            resp.Headers.Add("Content-Type", $"application/json; charset=utf-8");
            await resp.WriteStringAsync(JsonSerializer.Serialize(new
            {
                version = "2.2",
                instance = new
                {
                    name = $"@{user.username}@{appInfo.ApplicationHostname}",
                    description = $"An ActivityPub bot written with Azure Functions"
                },
                software = new
                {
                    name = "movietitler",
                    version = appInfo.VersionNumber,
                    repository = "https://github.com/IsaacSchemm/MovieTitler",
                    homepage = "https://github.com/IsaacSchemm/MovieTitler"
                },
                protocols = new[]
                {
                    "activitypub"
                },
                services = new
                {
                    inbound = Array.Empty<object>(),
                    outbound = new[]
                    {
                        "atom1.0",
                        "rss2.0"
                    }
                },
                openRegistrations = false,
                usage = new
                {
                    users = new
                    {
                        total = 1
                    },
                    localPosts = postCount
                },
                metadata = new { }
            }), Encoding.UTF8);
            return resp;
        }
    }
}
