using MovieTitler.Interfaces;
using MovieTitler.LowLevel;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using MovieTitler.Data;
using Microsoft.EntityFrameworkCore;

namespace MovieTitler.Functions
{
    public class Actor(
        ActivityPubTranslator translator,
        BotDbContext context,
        ContentNegotiator negotiator,
        IActorKeyProvider keyProvider,
        IApplicationInformation appInfo,
        MarkdownTranslator markdownTranslator)
    {
        /// <summary>
        /// Returns information about this bot's ActivityPub actor.
        /// </summary>
        /// <param name="req"></param>
        /// <returns>An ActivityStreams Person object or a Markdown or HTML response, depending on the user agent's Accept header.</returns>
        [Function("Actor")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/actor")] HttpRequestData req)
        {
            var person = Domain.Actor;

            foreach (var format in negotiator.GetAcceptableFormats(req.Headers))
            {
                if (format.Family.IsActivityPub)
                {
                    var key = await keyProvider.GetPublicKeyAsync();
                    string json = ActivityPubSerializer.SerializeWithContext(translator.PersonToObject(person, key, appInfo));

                    return await req.WriteResponseAsync(format, json);
                }
                else if (format.Family.IsHTML)
                {
                    var recent = await context.GeneratedPosts
                        .OrderByDescending(post => post.Id)
                        .Take(1)
                        .ToListAsync();
                    return await req.WriteResponseAsync(
                        format,
                        markdownTranslator.ToHtml(
                            person,
                            recent.Select(Domain.AsPost)));
                }
                else if (format.Family.IsMarkdown)
                {
                    var recent = await context.GeneratedPosts
                        .OrderByDescending(post => post.Id)
                        .Take(1)
                        .ToListAsync();
                    return await req.WriteResponseAsync(
                        format,
                        markdownTranslator.ToMarkdown(
                            person,
                            recent.Select(Domain.AsPost)));
                }
            }

            return req.CreateResponse(HttpStatusCode.NotAcceptable);
        }
    }
}
