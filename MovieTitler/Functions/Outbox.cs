using MovieTitler.LowLevel;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using MovieTitler.Data;
using Microsoft.EntityFrameworkCore;

namespace MovieTitler.Functions
{
    public class Outbox(
        ActivityPubTranslator translator,
        BotDbContext context,
        ContentNegotiator negotiator)
    {
        /// <summary>
        /// Returns the size of the user's outbox and a link to the first page.
        /// </summary>
        /// <param name="req"></param>
        /// <returns>An ActivityStreams OrderedCollection or a Markdown or HTML response, depending on the user agent's Accept header.</returns>
        [Function("Outbox")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/actor/outbox")] HttpRequestData req)
        {
            int count = await context.GeneratedPosts.CountAsync();

            var outbox = Domain.AsOutbox(count: count);

            foreach (var format in negotiator.GetAcceptableFormats(req.Headers))
            {
                if (format.Family.IsActivityPub)
                {
                    string json = ActivityPubSerializer.SerializeWithContext(
                        translator.AsOutbox(
                            outbox));

                    return await req.WriteResponseAsync(format, json);
                }
            }

            return req.CreateResponse(HttpStatusCode.NotAcceptable);
        }
    }
}
