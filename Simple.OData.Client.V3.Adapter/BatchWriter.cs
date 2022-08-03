﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Data.OData;

#pragma warning disable 1591

namespace Simple.OData.Client.V3.Adapter
{
    public class BatchWriter : BatchWriterBase
    {
        private ODataBatchWriter _batchWriter;
        private ODataRequestMessage _requestMessage;
        private ODataMessageWriter _messageWriter;

        public BatchWriter(ISession session, IDictionary<object, IDictionary<string, object>> batchEntries)
            : base(session, batchEntries)
        {
        }

#pragma warning disable 1998
        public override async Task StartBatchAsync()
        {
            _requestMessage = new ODataRequestMessage() { Url = _session.Settings.BaseUri };
            _messageWriter = new ODataMessageWriter(_requestMessage);
            _batchWriter = await _messageWriter.CreateODataBatchWriterAsync();
            await _batchWriter.WriteStartBatchAsync();
            this.HasOperations = true;
        }
#pragma warning restore 1998

#pragma warning disable 1998
        public override async Task<HttpRequestMessage> EndBatchAsync()
        {
            if (_pendingChangeSet)
                await _batchWriter.WriteEndChangesetAsync();
            await _batchWriter.WriteEndBatchAsync();
            var stream = await _requestMessage.GetStreamAsync();
            return CreateMessageFromStream(stream, _requestMessage.Url, _requestMessage.GetHeader);
        }
#pragma warning restore 1998

        protected override async Task StartChangesetAsync()
        {
            if (_batchWriter == null)
                await StartBatchAsync();

            await _batchWriter.WriteStartChangesetAsync();
        }

        protected override Task EndChangesetAsync()
        {
            return _batchWriter.WriteEndChangesetAsync();
        }

        protected override async Task<object> CreateOperationMessageAsync(Uri uri, string method, string collection, string contentId, bool resultRequired)
        {
            if (_batchWriter == null)
                await StartBatchAsync();

            return await CreateBatchOperationMessageAsync(uri, method, collection, contentId, resultRequired);
        }

#pragma warning disable 1998
        private async Task<ODataBatchOperationRequestMessage> CreateBatchOperationMessageAsync(
            Uri uri, string method, string collection, string contentId, bool resultRequired)
        {
            var message = await _batchWriter.CreateOperationRequestMessageAsync(method, uri);

            if (method != RestVerbs.Get && method != RestVerbs.Delete)
                message.SetHeader(HttpLiteral.ContentId, contentId);

            if (method == RestVerbs.Post || method == RestVerbs.Put || method == RestVerbs.Patch)
                message.SetHeader(HttpLiteral.Prefer, resultRequired ? HttpLiteral.ReturnContent : HttpLiteral.ReturnNoContent);

            if (collection != null && _session.Metadata.EntityCollectionRequiresOptimisticConcurrencyCheck(collection) &&
                (method == RestVerbs.Put || method == RestVerbs.Patch || method == RestVerbs.Delete))
            {
                message.SetHeader(HttpLiteral.IfMatch, EntityTagHeaderValue.Any.Tag);
            }

            return message;
        }
#pragma warning restore 1998
    }
}
