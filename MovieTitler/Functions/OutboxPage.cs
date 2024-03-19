using MovieTitler.HighLevel;
using MovieTitler.HighLevel.Feed;
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
    public class OutboxPage(
        ActivityPubTranslator translator,
        BotDbContext context,
        ContentNegotiator negotiator,
        FeedBuilder feedBuilder,
        MarkdownTranslator markdownTranslator)
    {
        /// <summary>
        /// Returns up to 20 of the user's posts mirrored from Weasyl and
        /// cached in the bot's database. Posts are rendered in reverse
        /// chronological order (newest first).
        /// </summary>
        /// <param name="req"></param>
        /// <returns>An ActivityStreams OrderedCollectionPage or a Markdown or HTML response, depending on the user agent's Accept header.</returns>
        [Function("OutboxPage")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/actor/outbox/page")] HttpRequestData req)
        {
            int nextid = int.TryParse(req.Query["nextid"], out int n)
                ? n
                : int.MaxValue;

            var posts = await context.GeneratedPosts
                .Where(post => post.Id < nextid)
                .OrderByDescending(post => post.Id)
                .Take(20)
                .ToListAsync();

            var outboxPage = Domain.AsOutboxPage(posts.Select(Domain.AsPost), nextid);

            var person = Domain.Actor;

            var acceptableFormats =
                req.Query["format"] == "rss" ? [negotiator.RSS]
                : req.Query["format"] == "atom" ? [negotiator.Atom]
                : negotiator.GetAcceptableFormats(req.Headers);

            foreach (var format in acceptableFormats)
            {
                if (format.Family.IsActivityPub)
                {
                    string json = ActivityPubSerializer.SerializeWithContext(
                        translator.AsOutboxPage(
                            req.Url.OriginalString,
                            outboxPage));

                    return await req.WriteResponseAsync(format, json);
                }
                else if (format.Family.IsHTML)
                {
                    return await req.WriteResponseAsync(format, markdownTranslator.ToHtml(outboxPage));
                }
                else if (format.Family.IsMarkdown)
                {
                    return await req.WriteResponseAsync(format, markdownTranslator.ToMarkdown(outboxPage));
                }
                else if (format.Family.IsRSS)
                {
                    return await req.WriteResponseAsync(format, feedBuilder.ToRssFeed(person, outboxPage.posts));
                }
                else if (format.Family.IsAtom)
                {
                    return await req.WriteResponseAsync(format, feedBuilder.ToAtomFeed(person, outboxPage.posts));
                }
            }

            return req.CreateResponse(HttpStatusCode.NotAcceptable);
        }
    }
}
