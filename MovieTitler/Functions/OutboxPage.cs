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
                .OrderByDescending(post => post.CreatedAt)
                .Take(20)
                .ToListAsync();

            var galleryPage = Domain.AsGalleryPage(posts.Select(Domain.AsPost), nextid);

            var person = Domain.Actor;

            var acceptableFormats =
                req.Query["format"] == "rss" ? [negotiator.RSS]
                : req.Query["format"] == "atom" ? [negotiator.Atom]
                : negotiator.GetAcceptableFormats(req.Headers);

            foreach (var format in acceptableFormats)
            {
                if (format.Family.IsActivityPub)
                {
                    var outboxPage = translator.AsOutboxPage(req.Url.OriginalString, galleryPage);

                    string json = ActivityPubSerializer.SerializeWithContext(outboxPage);

                    return await req.WriteResponseAsync(format, json);
                }
                else if (format.Family.IsHTML)
                {
                    return await req.WriteResponseAsync(format, markdownTranslator.ToHtml(galleryPage));
                }
                else if (format.Family.IsMarkdown)
                {
                    return await req.WriteResponseAsync(format, markdownTranslator.ToMarkdown(galleryPage));
                }
                else if (format.Family.IsRSS)
                {
                    return await req.WriteResponseAsync(format, feedBuilder.ToRssFeed(person, galleryPage.posts));
                }
                else if (format.Family.IsAtom)
                {
                    return await req.WriteResponseAsync(format, feedBuilder.ToAtomFeed(person, galleryPage.posts));
                }
            }

            return req.CreateResponse(HttpStatusCode.NotAcceptable);
        }
    }
}
