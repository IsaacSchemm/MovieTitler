using MovieTitler.Interfaces;
using MovieTitler.LowLevel;
using Microsoft.Azure.Functions.Worker.Http;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace MovieTitler
{
    /// <summary>
    /// Provides methods that make it easier to return ActivityStreams,
    /// Markdown, or HTML responses from Azure Functions endpoints.
    /// </summary>
    public static class HttpRequestDataExtensions
    {
        private class Wrapper(HttpRequestData Request) : IRequest
        {
            HttpMethod IRequest.Method => new(Request.Method);
            Uri IRequest.RequestUri => Request.Url;
            HttpHeaders IRequest.Headers => Request.Headers;
        }

        /// <summary>
        /// Returns a view of the HttpRequestData object using the IRequest
        /// interface that the bot uses for HTTP signature validation.
        /// </summary>
        /// <param name="req">The HTTP request from Azure Functions</param>
        /// <returns>A wrapper of type IRequest</returns>
        public static IRequest AsIRequest(this HttpRequestData req)
        {
            return new Wrapper(req);
        }

        /// <summary>
        /// Writes the given string to the HTTP response, with a Content-Type
        /// header derived from the given OutputFormat.
        /// </summary>
        /// <param name="req">The HTTP request</param>
        /// <param name="format">The format to use (Markdown, HTML, ActivityStreams, RSS, or Atom)</param>
        /// <param name="content">The string content (after any serialization)</param>
        /// <returns>The HTTP response object</returns>
        public static async Task<HttpResponseData> WriteResponseAsync(
            this HttpRequestData req,
            OutputFormat format,
            string content)
        {
            var resp = req.CreateResponse(HttpStatusCode.OK);
            resp.Headers.Add("Content-Type", $"{format.MediaType}; charset=utf-8");
            await resp.WriteStringAsync(content, Encoding.UTF8);
            return resp;
        }

        /// <summary>
        /// Writes an HTTP 307 redirect response.
        /// </summary>
        /// <param name="req">The HTTP request</param>
        /// <param name="url">The URL to redirect to</param>
        /// <returns>The HTTP response object</returns>
        public static HttpResponseData Redirect(
            this HttpRequestData req,
            string url)
        {
            var resp = req.CreateResponse(HttpStatusCode.TemporaryRedirect);
            resp.Headers.Add("Location", url);
            return resp;
        }
    }
}
