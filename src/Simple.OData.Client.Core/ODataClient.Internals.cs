﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Simple.OData.Client.Extensions;

namespace Simple.OData.Client
{
	public partial class ODataClient
	{
		private async Task<IDictionary<string, object>> GetUpdatedResult(ResolvedCommand command,
			CancellationToken cancellationToken)
		{
			var entryKey = command.Details.HasKey ? command.KeyValues : command.FilterAsKey;
			var entryData = command.CommandData;

			var updatedKey = entryKey.Where(x => !entryData.ContainsKey(x.Key)).ToIDictionary();
			foreach (var item in entryData.Where(x => entryKey.ContainsKey(x.Key)))
			{
				updatedKey.Add(item);
			}

			var updatedCommand = new FluentCommand(command).Key(updatedKey);
			return await FindEntryAsync(updatedCommand.Resolve(_session).Format(), cancellationToken);
		}

		private async Task<IEnumerable<IDictionary<string, object>>> ExecuteFunctionAsync(ResolvedCommand command,
			ODataFeedAnnotations annotations, CancellationToken cancellationToken)
		{
			var request = await _session.Adapter.GetRequestWriter(BatchWriter)
				.CreateFunctionRequestAsync(command.Format(), command.Details.FunctionName, command.Details.Headers);

			return await ExecuteRequestWithResultAsync(request, cancellationToken,
				x =>
				{
					var result = x.AsEntries(_session.Settings.IncludeAnnotationsInResults);
					if (annotations != null && x.Feed != null)
						annotations.CopyFrom(x.Feed.Annotations);
					return result;
				},
				x => Enumerable.Empty<IDictionary<string, object>>());
		}

		private async Task<IEnumerable<IDictionary<string, object>>> ExecuteFunctionAsync(ResolvedCommand command,
			CancellationToken cancellationToken)
		{
			var request = await _session.Adapter.GetRequestWriter(BatchWriter)
				.CreateFunctionRequestAsync(command.Format(), command.Details.FunctionName, command.Details.Headers);

			return await ExecuteRequestWithResultAsync(request, cancellationToken,
				x => x.AsEntries(_session.Settings.IncludeAnnotationsInResults),
				x => Enumerable.Empty<IDictionary<string, object>>());
		}

		private async Task<IEnumerable<IDictionary<string, object>>> ExecuteActionAsync(ResolvedCommand command,
			ODataFeedAnnotations annotations, CancellationToken cancellationToken)
		{
			var entityTypeName = command.EntityCollection != null
				? _session.Metadata.GetQualifiedTypeName(command.EntityCollection.Name)
				: null;
			var request = await _session.Adapter.GetRequestWriter(BatchWriter)
				.CreateActionRequestAsync(command.Format(), command.Details.ActionName, entityTypeName,
					command.CommandData, true, command.Details.Headers);

			return await ExecuteRequestWithResultAsync(request, cancellationToken,
				x =>
				{
					var result = x.AsEntries(_session.Settings.IncludeAnnotationsInResults);
					if (annotations != null && x.Feed != null)
						annotations.CopyFrom(x.Feed.Annotations);
					return result;
				},
				x => Enumerable.Empty<IDictionary<string, object>>());
		}

		private async Task<IEnumerable<IDictionary<string, object>>> ExecuteActionAsync(ResolvedCommand command,
			CancellationToken cancellationToken)
		{
			var entityTypeName = command.EntityCollection != null
				? _session.Metadata.GetQualifiedTypeName(command.EntityCollection.Name)
				: null;
			var request = await _session.Adapter.GetRequestWriter(BatchWriter)
				.CreateActionRequestAsync(command.Format(), command.Details.ActionName, entityTypeName,
					command.CommandData, true, command.Details.Headers);

			return await ExecuteRequestWithResultAsync(request, cancellationToken,
				x => x.AsEntries(_session.Settings.IncludeAnnotationsInResults),
				x => Enumerable.Empty<IDictionary<string, object>>());
		}

		private async Task ExecuteBatchActionsAsync(IList<Func<IODataClient, Task>> actions,
			IDictionary<string, string> headers, CancellationToken cancellationToken)
		{
			if (!actions.Any())
				return;

			var responseIndexes = new List<int>();
			var request = await BatchWriter.Value.CreateBatchRequestAsync(this, actions, responseIndexes, headers);
			if (request != null)
			{
				// Execute batch and get response
				using (var response = await _requestRunner.ExecuteRequestAsync(request, cancellationToken)
					       )
				{
					var responseReader = _session.Adapter.GetResponseReader();
					var batchResponse = await responseReader.GetResponseAsync(response);

					// Replay batch operations to assign results
					await responseReader.AssignBatchActionResultsAsync(this, batchResponse, actions, responseIndexes);
				}
			}
		}

		private async Task ExecuteBatchActionsAsync(IList<Func<IODataClient, Task>> actions,
			CancellationToken cancellationToken)
		{
			if (!actions.Any())
				return;

			var responseIndexes = new List<int>();
			var request = await BatchWriter.Value.CreateBatchRequestAsync(this, actions, responseIndexes);
			if (request != null)
			{
				// Execute batch and get response
				using (var response = await _requestRunner.ExecuteRequestAsync(request, cancellationToken))
				{
					var responseReader = _session.Adapter.GetResponseReader();
					var batchResponse = await responseReader.GetResponseAsync(response);

					// Replay batch operations to assign results
					await responseReader.AssignBatchActionResultsAsync(this, batchResponse, actions, responseIndexes);
				}
			}
		}

		private async Task ExecuteRequestAsync(ODataRequest request, CancellationToken cancellationToken)
		{
			if (IsBatchRequest)
				return;

			try
			{
				using (await _requestRunner.ExecuteRequestAsync(request, cancellationToken))
				{
				}
			}
			catch (WebRequestException ex)
			{
				if (_settings.IgnoreResourceNotFoundException && ex.StatusCode == HttpStatusCode.NotFound)
					return;
				else
					throw;
			}
		}

		private async Task<T> ExecuteRequestWithResultAsync<T>(ODataRequest request,
			CancellationToken cancellationToken,
			Func<ODataResponse, T> createResult, Func<ODataResponse, T> createEmptyResult,
			Func<T> createBatchResult = null)
		{
			if (IsBatchRequest)
				return createBatchResult != null
					? createBatchResult()
					: createEmptyResult != null
						? createEmptyResult(null)
						: default(T);

			try
			{
				using (var response = await _requestRunner.ExecuteRequestAsync(request, cancellationToken))
				{
					var responseReader = _session.Adapter.GetResponseReader();
					var odataResponse = await responseReader.GetResponseAsync(response);
					if (response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NoContent &&
					    (request.Method == RestVerbs.Get || request.ResultRequired))
						return createResult(odataResponse);
					return createEmptyResult != null ? createEmptyResult(odataResponse) : default(T);
				}
			}
			catch (WebRequestException ex)
			{
				if (_settings.IgnoreResourceNotFoundException && ex.StatusCode == HttpStatusCode.NotFound)
					return createEmptyResult != null ? createEmptyResult(null) : default(T);
				else
					throw;
			}
		}

		private async Task<Stream> ExecuteGetStreamRequestAsync(ODataRequest request,
			CancellationToken cancellationToken)
		{
			if (IsBatchRequest)
				return Stream.Null;

			try
			{
				using (var response = await _requestRunner.ExecuteRequestAsync(request, cancellationToken))
				{
					if (response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NoContent &&
					    (request.Method == RestVerbs.Get || request.ResultRequired))
					{
						var stream = new MemoryStream();
						await response.Content.CopyToAsync(stream);
						return stream;
					}
					else
					{
						return Stream.Null;
					}
				}
			}
			catch (WebRequestException ex)
			{
				if (_settings.IgnoreResourceNotFoundException && ex.StatusCode == HttpStatusCode.NotFound)
					return Stream.Null;
				else
					throw;
			}
		}

		private async Task<IEnumerable<IDictionary<string, object>>> IterateEntriesAsync(
			ResolvedCommand command, bool resultRequired,
			Func<string, IDictionary<string, object>, IDictionary<string, object>, bool,
				Task<IDictionary<string, object>>> funcAsync, CancellationToken cancellationToken)
		{
			var collectionName = command.QualifiedEntityCollectionName;
			var entryData = command.CommandData;

			IEnumerable<IDictionary<string, object>> result = null;
			var client = new ODataClient(this);
			var entries = await client.FindEntriesAsync(command.Format(), cancellationToken);
			if (entries != null)
			{
				var entryList = entries.ToList();
				var resultList = new List<IDictionary<string, object>>();
				foreach (var entry in entryList)
				{
					resultList.Add(await funcAsync(collectionName, entry, entryData, resultRequired)
						);
					if (cancellationToken.IsCancellationRequested) cancellationToken.ThrowIfCancellationRequested();
				}

				result = resultList;
			}

			return result;
		}

		private async Task<int> IterateEntriesAsync(ResolvedCommand command,
			Func<string, IDictionary<string, object>, Task> funcAsync, CancellationToken cancellationToken)
		{
			var collectionName = command.QualifiedEntityCollectionName;

			var result = 0;
			var client = new ODataClient(this);
			var entries = await client.FindEntriesAsync(command.Format(), cancellationToken);
			if (entries != null)
			{
				var entryList = entries.ToList();
				foreach (var entry in entryList)
				{
					await funcAsync(collectionName, entry);
					if (cancellationToken.IsCancellationRequested) cancellationToken.ThrowIfCancellationRequested();
					++result;
				}
			}

			return result;
		}

		private void RemoveAnnotationProperties(IDictionary<string, object> entryData, IList<Action> actions = null)
		{
			var runActionsOnExist = false;
			if (actions == null)
			{
				actions = new List<Action>();
				runActionsOnExist = true;
			}

			if (!_settings.IncludeAnnotationsInResults)
			{
				foreach (var entry in entryData)
				{
					var key = entry.Key;
					if (key == FluentCommand.AnnotationsLiteral ||
					    key.StartsWith(FluentCommand.AnnotationsLiteral + "_"))
						actions.Add(() => entryData.Remove(key));
				}

				var nestedEntries = entryData.Where(x => x.Value is IDictionary<string, object>);
				foreach (var nestedEntry in nestedEntries)
				{
					RemoveAnnotationProperties(nestedEntry.Value as IDictionary<string, object>, actions);
				}

				nestedEntries = entryData.Where(x => x.Value is IList<IDictionary<string, object>>);
				foreach (var nestedEntry in nestedEntries)
				{
					foreach (var element in nestedEntry.Value as IList<IDictionary<string, object>>)
					{
						RemoveAnnotationProperties(element, actions);
					}
				}
			}

			if (runActionsOnExist)
			{
				foreach (var action in actions)
				{
					action();
				}
			}
		}

		private void AssertHasKey(ResolvedCommand command)
		{
			if (!command.Details.HasKey && command.Details.FilterAsKey == null)
				throw new InvalidOperationException("No entry key specified.");
		}

		private async Task<string> FormatEntryKeyAsync(string collection, IDictionary<string, object> entryKey,
			CancellationToken cancellationToken)
		{
			var entryIdent = await GetBoundClient()
				.For(collection)
				.Key(entryKey)
				.GetCommandTextAsync(cancellationToken);

			return entryIdent;
		}

		private string FormatEntryKey(ResolvedCommand command)
		{
			var entryIdent = command.Details.HasKey
				? command.Format()
				: new FluentCommand(command.Details).Key(command.Details.FilterAsKey).Resolve(_session).Format();

			return entryIdent;
		}

		private async Task EnrichWithMediaPropertiesAsync(IEnumerable<AnnotatedEntry> entries, ResolvedCommand command,
			CancellationToken cancellationToken)
		{
			if (entries != null)
			{
				foreach (var entry in entries)
				{
					await EnrichWithMediaPropertiesAsync(entry, command.Details.MediaProperties, cancellationToken);
					if (cancellationToken.IsCancellationRequested) cancellationToken.ThrowIfCancellationRequested();
				}
			}
		}

		private async Task EnrichWithMediaPropertiesAsync(AnnotatedEntry entry, IEnumerable<string> mediaProperties,
			CancellationToken cancellationToken)
		{
			if (entry != null && mediaProperties != null)
			{
				var entityMediaPropertyName = mediaProperties.FirstOrDefault(x => !entry.Data.ContainsKey(x));
				entityMediaPropertyName = entityMediaPropertyName ?? FluentCommand.AnnotationsLiteral;
				if (entry.Annotations != null)
				{
					await GetMediaStreamValueAsync(entry.Data, entityMediaPropertyName, entry.Annotations.MediaResource,
						cancellationToken);
				}

				foreach (var propertyName in mediaProperties)
				{
					object value;
					if (entry.Data.TryGetValue(propertyName, out value))
					{
						await GetMediaStreamValueAsync(entry.Data, propertyName, value as ODataMediaAnnotations,
							cancellationToken);
					}
				}
			}
		}

		private async Task GetMediaStreamValueAsync(IDictionary<string, object> entry, string propertyName,
			ODataMediaAnnotations annotations, CancellationToken cancellationToken)
		{
			var mediaLink = annotations == null ? null : annotations.ReadLink ?? annotations.EditLink;
			if (mediaLink != null)
			{
				var stream = await GetMediaStreamAsync(mediaLink.AbsoluteUri, cancellationToken);
				if (cancellationToken.IsCancellationRequested) cancellationToken.ThrowIfCancellationRequested();

				object _;
				if (entry.TryGetValue(propertyName, out _))
					entry[propertyName] = stream;
				else
					entry.Add(propertyName, stream);
			}
		}
	}
}