using MovieTitler.LowLevel;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Threading.Tasks;
using MovieTitler.Data;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace MovieTitler.Functions
{
    public class Posts(
        ActivityPubTranslator translator,
        BotDbContext context,
        ContentNegotiator negotiator,
        MarkdownTranslator markdownTranslator)
    {
        /// <summary>
        /// Returns a post.
        /// </summary>
        /// <param name="req"></param>
        /// <param name="id">The internal numeric ID</param>
        /// <returns>An ActivityStreams Note or Collection or a Markdown or HTML response, depending on the query string and the user agent's Accept header.</returns>
        [Function("Posts")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/posts/{id}")] HttpRequestData req,
            int id)
        {
            var post = await context.GeneratedPosts
                .Where(post => post.Id == id)
                .SingleOrDefaultAsync();

            if (post == null)
                return req.CreateResponse(HttpStatusCode.NotFound);

            var domainPost = Domain.AsPost(post);

            foreach (var format in negotiator.GetAcceptableFormats(req.Headers))
            {
                if (format.Family.IsActivityPub)
                {
                    return await req.WriteResponseAsync(format, ActivityPubSerializer.SerializeWithContext(translator.AsObject(domainPost)));
                }
                else if (format.Family.IsMarkdown)
                {
                    return await req.WriteResponseAsync(format, markdownTranslator.ToMarkdown(domainPost));
                }
                else if (format.Family.IsHTML)
                {
                    return await req.WriteResponseAsync(format, markdownTranslator.ToHtml(domainPost));
                }
            }

            return req.CreateResponse(HttpStatusCode.NotAcceptable);
        }
    }
}
