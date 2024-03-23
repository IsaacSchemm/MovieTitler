using MovieTitler.Interfaces;
using MovieTitler.LowLevel;
using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;

namespace MovieTitler.HighLevel.Feed
{
    /// <summary>
    /// Builds Atom and RSS feeds for the outbox.
    /// </summary>
    public class FeedBuilder(IdMapper mapper, IApplicationInformation appInfo)
    {
        /// <summary>
        /// Creates a feed item for a post.
        /// </summary>
        /// <param name="post">The submission to render</param>
        /// <returns>A feed item</returns>
        private SyndicationItem ToSyndicationItem(Post post)
        {
            var item = new SyndicationItem
            {
                Id = mapper.GetObjectId(post.id),
                PublishDate = post.created,
                LastUpdatedTime = post.created,
                Content = new TextSyndicationContent(string.Join(" ", post.text), TextSyndicationContentKind.Plaintext),
                Title = new TextSyndicationContent(string.Join(" ", post.text), TextSyndicationContentKind.Plaintext)
            };

            item.Links.Add(SyndicationLink.CreateAlternateLink(new Uri(mapper.GetObjectId(post.id)), "text/html"));

            return item;
        }

        /// <summary>
        /// Creates a feed for a list of posts.
        /// </summary>
        /// <param name="person">The author of the posts</param>
        /// <param name="posts">A sequence of submissions</param>
        /// <returns>A feed object</returns>
        private SyndicationFeed ToSyndicationFeed(Person person, IEnumerable<Post> posts)
        {
            string uri = $"{mapper.ActorId}/feed";
            var feed = new SyndicationFeed
            {
                Id = uri,
                Title = new TextSyndicationContent(appInfo.ApplicationName, TextSyndicationContentKind.Plaintext),
                LastUpdatedTime = posts.Select(x => x.created).Max(),
                Items = posts.Select(ToSyndicationItem)
            };
            feed.Links.Add(SyndicationLink.CreateSelfLink(new Uri(uri), "application/rss+xml"));
            feed.Links.Add(SyndicationLink.CreateAlternateLink(new Uri($"https://{appInfo.ApplicationHostname}"), "text/html"));
            return feed;
        }

        /// <summary>
        /// A StringWriter that tells the XmlWriter to declare the encoding as UTF-8.
        /// </summary>
        private class UTF8StringWriter : StringWriter
        {
            public override Encoding Encoding => Encoding.UTF8;
        }

        /// <summary>
        /// Generates an RSS feed for a list of posts.
        /// </summary>
        /// <param name="person">The author of the posts</param>
        /// <param name="posts">A sequence of submissions</param>
        /// <returns>An RSS feed (should be serialized as UTF-8)</returns>
        public string ToRssFeed(Person person, IEnumerable<Post> posts)
        {
            var feed = ToSyndicationFeed(person, posts);

            using var sw = new UTF8StringWriter();

            using (var xmlWriter = XmlWriter.Create(sw))
            {
                new Rss20FeedFormatter(feed).WriteTo(xmlWriter);
            }

            return sw.ToString();
        }

        /// <summary>
        /// Generates an Atom feed for a list of posts.
        /// </summary>
        /// <param name="person">The author of the posts</param>
        /// <param name="posts">A sequence of submissions</param>
        /// <returns>An Atom feed (should be serialized as UTF-8)</returns>
        public string ToAtomFeed(Person person, IEnumerable<Post> posts)
        {
            var feed = ToSyndicationFeed(person, posts);

            using var sw = new UTF8StringWriter();

            using (var xmlWriter = XmlWriter.Create(sw))
            {
                new Atom10FeedFormatter(feed).WriteTo(xmlWriter);
            }

            return sw.ToString();
        }
    }
}
