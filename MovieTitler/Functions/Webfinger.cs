using MovieTitler.Interfaces;
using MovieTitler.LowLevel;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Threading.Tasks;

namespace MovieTitler.Functions
{
    public class WebFinger(IdMapper mapper, IApplicationInformation appInfo)
    {
        /// <summary>
        /// Points the user agent to the bot's actor ID, or redirects to the equivalent endpoint on the admin actor's server.
        /// </summary>
        /// <param name="req"></param>
        /// <returns>A WebFinger response</returns>
        [Function("WebFinger")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ".well-known/webfinger")] HttpRequestData req)
        {
            if (req.Query["resource"] is not string resource)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            var user = Domain.Actor;

            string handle = $"acct:{user.username}@{appInfo.ApplicationHostname}";

            if (resource == handle || resource == mapper.ActorId)
            {
                var resp = req.CreateResponse(HttpStatusCode.OK);
                await resp.WriteAsJsonAsync(new
                {
                    subject = handle,
                    aliases = new[] { mapper.ActorId },
                    links = new[]
                    {
                        new
                        {
                            rel = "http://webfinger.net/rel/profile-page",
                            type = "text/html",
                            href = mapper.ActorId
                        },
                        new
                        {
                            rel = "self",
                            type = "application/activity+json",
                            href = mapper.ActorId
                        }
                    }
                });
                return resp;
            }
            else
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }
        }
    }
}
