﻿// Copyright (c) 2015 Abel Cheng <abelcys@gmail.com> and other contributors.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Text;
using System.Web.Http;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;

namespace DataBooster.DbWebApi.Csv
{
	public class CsvFormatPlug : IFormatPlug
	{
		private readonly MediaTypeHeaderValue _DefaultMediaType;
		private readonly MediaTypeHeaderValue[] _SupportedMediaTypes;

		public CsvFormatPlug()
		{
			_DefaultMediaType = new MediaTypeHeaderValue("text/csv");
			_SupportedMediaTypes = new MediaTypeHeaderValue[] { _DefaultMediaType };
		}

		public MediaTypeHeaderValue DefaultMediaType
		{
			get { return _DefaultMediaType; }
		}

		public IEnumerable<MediaTypeHeaderValue> SupportedMediaTypes
		{
			get { return _SupportedMediaTypes; }
		}

		public string FormatShortName
		{
			get { return "csv"; }
		}

		private int GetQueryResultSetIndex(Dictionary<string, string> queryStrings, string queryName)
		{
			if (queryStrings == null || queryStrings.Count == 0)
				return 0;

			string queryResultSet;

			if (queryStrings.TryGetValue(queryName, out queryResultSet))
			{
				int resultSetIndex;

				if (int.TryParse(queryResultSet, out resultSetIndex))
					if (resultSetIndex > 0 && resultSetIndex < 1024)
						return resultSetIndex;
			}

			return 0;
		}

		public HttpResponseMessage Respond(ApiController apiController, string sp, IDictionary<string, object> parameters,
			MediaTypeHeaderValue negotiatedMediaType, Encoding negotiatedEncoding)
		{
			HttpResponseMessage csvResponse = apiController.Request.CreateResponse();
			Dictionary<string, string> queryStrings = apiController.Request.GetQueryStringDictionary();
			int[] resultSetChoices = new int[] { GetQueryResultSetIndex(queryStrings, "ResultSet") };

			csvResponse.Content = new PushStreamContent((stream, httpContent, transportContext) =>
			{
				StreamWriter textWriter = (negotiatedEncoding == null) ? new StreamWriter(stream) : new StreamWriter(stream, negotiatedEncoding);

				using (DbContext dbContext = new DbContext())
				{
					CsvExporter csvExporter = new CsvExporter(textWriter);

					dbContext.ExecuteDbApi(sp, parameters, null,
						readHeader =>
						{
							string[] headers = new string[readHeader.VisibleFieldCount];

							for (int i = 0; i < headers.Length; i++)
								headers[i] = readHeader.GetName(i);

							csvExporter.WriteHeader(headers);
						},
						readRow =>
						{
							object[] values = new object[readRow.VisibleFieldCount];

							readRow.GetValues(values);

							csvExporter.WriteRow(values);
						},
						null, null, resultSetChoices);

					textWriter.Flush();
				}

				stream.Close();
			}, negotiatedMediaType ?? _DefaultMediaType);

			csvResponse.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = queryStrings.GetQueryFileName("FileName", FormatShortName) };

			return csvResponse;
		}
	}
}