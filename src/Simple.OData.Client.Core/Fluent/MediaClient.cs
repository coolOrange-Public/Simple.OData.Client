using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Simple.OData.Client.Extensions;

namespace Simple.OData.Client
{
	public class MediaClient<T> : FluentClientBase<T, MediaClient<T>>, IMediaClient<T> where T : class
	{
		internal MediaClient(ODataClient client, Session session, FluentCommand command = null,
			bool dynamicResults = false)
			: base(client, session, null, command, dynamicResults)
		{
		}

		public Task<Stream> GetStreamAsync()
		{
			return GetStreamAsync(CancellationToken.None);
		}

		public Task<Stream> GetStreamAsync(CancellationToken cancellationToken)
		{
			return _client.GetMediaStreamAsync(_command, cancellationToken);
		}

		public Task<byte[]> GetStreamAsArrayAsync()
		{
			return GetStreamAsArrayAsync(CancellationToken.None);
		}

		public async Task<byte[]> GetStreamAsArrayAsync(CancellationToken cancellationToken)
		{
			using (var stream = await _client.GetMediaStreamAsync(_command, cancellationToken))
			{
				return Utils.StreamToByteArray(stream);
			}
		}

		public Task<string> GetStreamAsStringAsync()
		{
			return GetStreamAsStringAsync(CancellationToken.None);
		}

		public async Task<string> GetStreamAsStringAsync(CancellationToken cancellationToken)
		{
			using (var stream = await _client.GetMediaStreamAsync(_command, cancellationToken))
			{
				return Utils.StreamToString(stream);
			}
		}

		public Task<T> InsertStreamAsync(Stream stream, string contentType)
		{
			return InsertStreamAsync(stream, contentType, CancellationToken.None);
		}

		public async Task<T> InsertStreamAsync(Stream stream, string contentType, CancellationToken cancellationToken)
		{
			var result = await _client.InsertMediaStreamAsync(_command, true, stream, contentType, cancellationToken)
				.ConfigureAwait(false);
			return result.ToObject<T>(TypeCache, _dynamicResults);
		}

		public Task<T> InsertStreamAsync(byte[] streamContent, string contentType)
		{
			return InsertStreamAsync(streamContent, contentType, CancellationToken.None);
		}

		public async Task<T> InsertStreamAsync(byte[] streamContent, string contentType,
			CancellationToken cancellationToken)
		{
			var result = await _client.InsertMediaStreamAsync(_command, true, Utils.ByteArrayToStream(streamContent),
				contentType, cancellationToken).ConfigureAwait(false);
			return result.ToObject<T>(TypeCache, _dynamicResults);
		}

		public Task<T> InsertStreamAsync(string streamContent)
		{
			return InsertStreamAsync(streamContent, CancellationToken.None);
		}

		public async Task<T> InsertStreamAsync(string streamContent, CancellationToken cancellationToken)
		{
			var result = await _client
				.InsertMediaStreamAsync(_command, true, Utils.StringToStream(streamContent), "text/plain",
					cancellationToken).ConfigureAwait(false);
			return result.ToObject<T>(TypeCache, _dynamicResults);
		}

		public Task SetStreamAsync(Stream stream, string contentType, bool optimisticConcurrency)
		{
			return SetStreamAsync(stream, contentType, optimisticConcurrency, CancellationToken.None);
		}

		public Task SetStreamAsync(Stream stream, string contentType, bool optimisticConcurrency,
			CancellationToken cancellationToken)
		{
			return _client.SetMediaStreamAsync(_command, stream, contentType, optimisticConcurrency, cancellationToken);
		}

		public Task SetStreamAsync(byte[] streamContent, string contentType, bool optimisticConcurrency)
		{
			return SetStreamAsync(streamContent, contentType, optimisticConcurrency, CancellationToken.None);
		}

		public Task SetStreamAsync(byte[] streamContent, string contentType, bool optimisticConcurrency,
			CancellationToken cancellationToken)
		{
			return _client.SetMediaStreamAsync(_command, Utils.ByteArrayToStream(streamContent), contentType,
				optimisticConcurrency, cancellationToken);
		}

		public Task SetStreamAsync(string streamContent, bool optimisticConcurrency)
		{
			return SetStreamAsync(streamContent, optimisticConcurrency, CancellationToken.None);
		}

		public Task SetStreamAsync(string streamContent, bool optimisticConcurrency,
			CancellationToken cancellationToken)
		{
			return _client.SetMediaStreamAsync(_command, Utils.StringToStream(streamContent), "text/plain",
				optimisticConcurrency, cancellationToken);
		}
	}
}