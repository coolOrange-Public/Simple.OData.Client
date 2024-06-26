﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Simple.OData.Client
{
	class RequestRunner
	{
		private readonly ISession _session;

		public RequestRunner(ISession session)
		{
			_session = session;
		}

		public async Task<HttpResponseMessage> ExecuteRequestAsync(ODataRequest request,
			CancellationToken cancellationToken)
		{
			HttpConnection httpConnection = null;
			try
			{
				await PreExecuteAsync(request);

				_session.Trace("{0} request: {1}", request.Method, request.RequestMessage.RequestUri.AbsoluteUri);
				if (request.RequestMessage.Content != null &&
				    (_session.Settings.TraceFilter & ODataTrace.RequestContent) != 0)
				{
					var content = await request.RequestMessage.Content.ReadAsStringAsync();
					_session.Trace("Request content:{0}{1}", Environment.NewLine, content);
				}

				HttpResponseMessage response;
				if (_session.Settings.RequestExecutor != null)
				{
					response = await _session.Settings.RequestExecutor(request.RequestMessage);
				}
				else
				{
					httpConnection = _session.Settings.RenewHttpConnection
						? new HttpConnection(_session.Settings)
						: _session.GetHttpConnection();

					response = await httpConnection.HttpClient.SendAsync(request.RequestMessage, cancellationToken);
					if (cancellationToken.IsCancellationRequested) cancellationToken.ThrowIfCancellationRequested();
				}

				_session.Trace("Request completed: {0}", response.StatusCode);
				if (response.Content != null && (_session.Settings.TraceFilter & ODataTrace.ResponseContent) != 0)
				{
					var content = await response.Content.ReadAsStringAsync();
					_session.Trace("Response content:{0}{1}", Environment.NewLine, content);
				}

				await PostExecuteAsync(response);
				return response;
			}
			catch (WebException ex)
			{
				throw WebRequestException.CreateFromWebException(ex, _session, request.RequestMessage);
			}
			catch (HttpRequestException ex)
			{
				if (ex.InnerException is WebException)
					throw WebRequestException.CreateFromWebException(ex.InnerException as WebException, _session, request.RequestMessage);
				throw;
			}
			catch (AggregateException ex)
			{
				if (ex.InnerException is WebException)
					throw WebRequestException.CreateFromWebException(ex.InnerException as WebException, _session, request.RequestMessage);
				else if (ex.InnerException is HttpRequestException && ex.InnerException.InnerException is WebException)
					throw WebRequestException.CreateFromWebException(ex.InnerException.InnerException as WebException, _session, request.RequestMessage);
				else
					throw;
			}
			finally
			{
				if (httpConnection != null && _session.Settings.RenewHttpConnection)
				{
					httpConnection.Dispose();
				}
			}
		}

		private async Task PreExecuteAsync(ODataRequest request)
		{
			if (request.Accept != null)
			{
				foreach (var accept in request.Accept)
				{
					request.RequestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
				}
			}

			if (request.CheckOptimisticConcurrency &&
			    (request.Method == RestVerbs.Put ||
			     request.Method == RestVerbs.Patch ||
			     request.Method == RestVerbs.Merge ||
			     request.Method == RestVerbs.Delete))
			{
				request.RequestMessage.Headers.IfMatch.Add(EntityTagHeaderValue.Any);
			}

			foreach (var header in request.Headers)
			{
				IEnumerable<string> values;
				if (request.RequestMessage.Headers.TryGetValues(header.Key, out values) &&
				    !values.Contains(header.Value))
					request.RequestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
			}

			if (_session.Settings.BeforeRequest != null)
				_session.Settings.BeforeRequest(request.RequestMessage);
		}

		private async Task PostExecuteAsync(HttpResponseMessage responseMessage)
		{
			if (_session.Settings.AfterResponse != null)
				_session.Settings.AfterResponse(responseMessage);

			if (!responseMessage.IsSuccessStatusCode)
				throw await WebRequestException.CreateFromResponseMessageAsync(responseMessage, _session);
		}
	}
}