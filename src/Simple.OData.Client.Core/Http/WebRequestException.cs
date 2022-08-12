using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Simple.OData.Client
{
	/// <summary>
	/// The exception that is thrown when the service failed to process the Web request
	/// </summary>
	[Serializable]
	public class WebRequestException : Exception
	{
		public async static Task<WebRequestException> CreateFromResponseMessageAsync(HttpResponseMessage response, ISession session, Exception innerException = null)
		{
			var requestUri = response.RequestMessage != null ? response.RequestMessage.RequestUri : null;
			var responseContent = response.Content != null ? await response.Content.ReadAsStringAsync().ConfigureAwait(false) : null;
			var exception = new WebRequestException(response.ReasonPhrase, response.StatusCode, requestUri, responseContent, innerException) { HttpResponse = response };

			try
			{
				var responseReader = session.Adapter.GetResponseReader();
				exception.ODataResponse = await responseReader.GetResponseAsync(response).ConfigureAwait(false);
			}
			catch (Exception)
			{
				// ignored
			}

			return exception;
		}

		public static WebRequestException CreateFromWebException(WebException ex, ISession session)
		{
			var response = ex.Response as HttpWebResponse;
			if (response == null)
				return new WebRequestException(ex);

			return CreateFromResponseMessageAsync(new HttpResponseMessage(response.StatusCode)
			{
				ReasonPhrase = ex.Message,
				Content = new StringContent(Utils.StreamToString(response.GetResponseStream(), true)),
				RequestMessage = new HttpRequestMessage(new HttpMethod(response.Method), response.ResponseUri)
			}, session, ex).Result;
		}

		public static WebRequestException CreateFromResponse(ODataResponse response)
		{
			return new WebRequestException(response.StatusCode.ToString(), (HttpStatusCode)response.StatusCode, null,
				null, null)
			{
				ODataResponse = response
			};
		}

		public static WebRequestException CreateFromBatchResponse(HttpStatusCode statusCode, Stream responseStream)
		{
			var responseContent = Utils.StreamToString(responseStream, true);
			return new WebRequestException(statusCode.ToString(), statusCode, null, responseContent, null);
		}

		private WebRequestException(string message, HttpStatusCode statusCode, Uri requestUri, string responseContent,
			Exception inner)
			: base(message, inner)
		{
			StatusCode = statusCode;
			RawResponse = responseContent;
			RequestUri = requestUri;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="WebRequestException"/> class.
		/// </summary>
		/// <param name="inner">The inner exception.</param>
		private WebRequestException(WebException inner)
			: base("Unexpected WebException encountered", inner)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="WebRequestException"/> class.
		/// </summary>
		/// <param name="info">The exception serialization information.</param>
		/// <param name="context">The exception serialization context.</param>
		protected WebRequestException(
			SerializationInfo info,
			StreamingContext context) : base(info, context)
		{
		}

		/// <summary>
		/// Gets the HTTP Uri
		/// </summary>
		/// <value>
		/// The original request URI, or the resulting URI if a redirect took place.
		/// </value>
		public Uri RequestUri { get; private set; }
		public HttpStatusCode StatusCode { get; private set; }
		public string RawResponse { get; private set; }

		public HttpResponseMessage HttpResponse { get; protected set; }
		public ODataResponse ODataResponse { get; protected set; }
	}
}