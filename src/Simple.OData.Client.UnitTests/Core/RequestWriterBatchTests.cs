﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Microsoft.Data.OData;
using Microsoft.Data.Edm;
using Xunit;

namespace Simple.OData.Client.Tests.Core
{
	public class RequestWriterBatchV3Tests : RequestWriterBatchTests
	{
		public override string MetadataFile
		{
			get { return "Northwind3.xml"; }
		}

		public override IFormatSettings FormatSettings
		{
			get { return new ODataV3Format(); }
		}

		protected async override Task<IRequestWriter> CreateBatchRequestWriter()
		{
			return new V3.Adapter.RequestWriter(
				_session,
				await _client.GetMetadataAsync<Microsoft.Data.Edm.IEdmModel>(),
				new Lazy<IBatchWriter>(() => _session.Adapter.GetBatchWriter(
					new Dictionary<object, IDictionary<string, object>>())));
		}
	}

	public class RequestWriterBatchV4Tests : RequestWriterBatchTests
	{
		public override string MetadataFile
		{
			get { return "Northwind4.xml"; }
		}

		public override IFormatSettings FormatSettings
		{
			get { return new ODataV4Format(); }
		}

		protected async override Task<IRequestWriter> CreateBatchRequestWriter()
		{
			return new V4.Adapter.RequestWriter(
				_session,
				await _client.GetMetadataAsync<Microsoft.OData.Edm.IEdmModel>(),
				new Lazy<IBatchWriter>(() => base.BatchWriter));
		}
	}

	public abstract class RequestWriterBatchTests : CoreTestBase
	{
		private readonly Dictionary<object, IDictionary<string, object>> _batchContent =
			new Dictionary<object, IDictionary<string, object>>(3);

		protected Dictionary<object, IDictionary<string, object>> BatchContent
		{
			get { return _batchContent; }
		}

		protected abstract Task<IRequestWriter> CreateBatchRequestWriter();

		protected IBatchWriter BatchWriter
		{
			get
			{
				return _session.Adapter.GetBatchWriter(
					_batchContent);
			}
		}

		[Fact]
		public async Task CreateUpdateRequest_NoPreferredVerb_AllProperties_OperationHeaders_Patch()
		{
			var requestWriter = await CreateBatchRequestWriter();

			var result = await requestWriter.CreateUpdateRequestAsync("Products", "",
				new Dictionary<string, object>() { { "ProductID", 1 } },
				new Dictionary<string, object>() {
					{ "ProductID", 1 },
					{ "SupplierID", 2 },
					{ "CategoryID", 3 },
					{ "ProductName", "Chai" },
					{ "EnglishName", "Tea" },
					{ "QuantityPerUnit", "10" },
					{ "UnitPrice", 20m },
					{ "UnitsInStock", 100 },
					{ "UnitsOnOrder", 1000 },
					{ "ReorderLevel", 500 },
					{ "Discontinued", false },
				},
				false,
				new Dictionary<string, string>() {
					{ "Header1", "HeaderValue1" }
				});

			Assert.Equal("PATCH", result.Method);
			string value;
			Assert.True(result.Headers.TryGetValue("Header1", out value) && value == "HeaderValue1");
			IEnumerable<string> values;
			Assert.True(result.RequestMessage.Headers.TryGetValues("Header1", out values) &&
			            values.Contains("HeaderValue1"));
		}
	}
}