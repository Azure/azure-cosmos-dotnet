﻿//-----------------------------------------------------------------------
// <copyright file="JsonPerfMeasurment.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.NetFramework.Tests.Json
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.Json;

    internal static class JsonPerfMeasurement
    {
        private const string SerializationFormatHeader = "Serialization Format";
        private const string ReadTimeHeader = "Read Time (ms)";
        private const string WriteTimeHeader = "Write Time (ms)";
        private const string NavigatorTimeHeader = "Navigator Time (ms)";
        private const string DocumentSize = "Document Size";

        private static readonly TextTable.Column[] Columns = new TextTable.Column[]
        {
                new TextTable.Column(SerializationFormatHeader, SerializationFormatHeader.Length),
                new TextTable.Column(ReadTimeHeader, ReadTimeHeader.Length),
                new TextTable.Column(WriteTimeHeader, WriteTimeHeader.Length),
                new TextTable.Column(NavigatorTimeHeader, NavigatorTimeHeader.Length),
                new TextTable.Column(DocumentSize, DocumentSize.Length),
        };

        public static readonly TextTable TextTable = new TextTable(Columns);

        public static void MeasurePerf(string json, string filename, int numberOfIterations = 1)
        {
            byte[] utf8ByteArray = Encoding.UTF8.GetBytes(json);

            // Text
            TimeSpan textReaderTime = JsonPerfMeasurement.MeasureReadPerformance(JsonReader.Create(utf8ByteArray), numberOfIterations);
            TimeSpan textWriterTime = JsonPerfMeasurement.MeasureWritePerformance(JsonWriter.Create(JsonSerializationFormat.Text), json, numberOfIterations);
            TimeSpan textNavigatorTime = JsonPerfMeasurement.MeasureNavigationPerformance(JsonNavigator.Create(utf8ByteArray), numberOfIterations);
            JsonExecutionTimes textExecutionTimes = new JsonExecutionTimes(textReaderTime, textWriterTime, textNavigatorTime, utf8ByteArray.Length, "Text");

            // Newtonsoft
            TimeSpan newtonsoftReaderTime = JsonPerfMeasurement.MeasureReadPerformance(new JsonNewtonsoftNewtonsoftTextReader(json), numberOfIterations);
            TimeSpan newtonsoftWriterTime = JsonPerfMeasurement.MeasureWritePerformance(new JsonNewtonsoftNewtonsoftTextWriter(), json, numberOfIterations);
            TimeSpan newtonsoftNavigatorTime = JsonPerfMeasurement.MeasureNavigationPerformance(new JsonNewtonsoftNavigator(json), numberOfIterations);
            JsonExecutionTimes newtonsoftExecutionTimes = new JsonExecutionTimes(newtonsoftReaderTime, newtonsoftWriterTime, newtonsoftNavigatorTime, json.Length, "Newtonsoft");

            // Binary
            byte[] binaryPayload = JsonTestUtils.ConvertTextToBinary(json);
            TimeSpan binaryReaderTime = JsonPerfMeasurement.MeasureReadPerformance(JsonReader.Create(binaryPayload), numberOfIterations);
            TimeSpan binarytWriterTime = JsonPerfMeasurement.MeasureWritePerformance(JsonWriter.Create(JsonSerializationFormat.Binary), json, numberOfIterations);
            TimeSpan binaryNavigatorTime = JsonPerfMeasurement.MeasureNavigationPerformance(JsonNavigator.Create(binaryPayload), numberOfIterations);
            JsonExecutionTimes binaryExecutionTimes = new JsonExecutionTimes(binaryReaderTime, binarytWriterTime, binaryNavigatorTime, binaryPayload.Length, "Binary");

            JsonPerfMeasurement.PrintStatisticsTable(filename, textExecutionTimes, newtonsoftExecutionTimes, binaryExecutionTimes);
        }

        public static TimeSpan MeasureReadPerformance(IJsonReader jsonReader, int numberOfIterations = 1)
        {
            Stopwatch stopwatch = new Stopwatch();
            for (int i = 0; i < numberOfIterations; i++)
            {
                stopwatch.Start();
                while (jsonReader.Read())
                {
                    /* all the work is done in the loop */
                }

                stopwatch.Stop();
            }

            return stopwatch.Elapsed;
        }

        public static TimeSpan MeasureNavigationPerformance(IJsonNavigator jsonNavigator, int numberOfIterations = 1)
        {
            JsonTokenInfo[] tokensFromNode;
            Stopwatch stopwatch = new Stopwatch();
            for (int i = 0; i < numberOfIterations; i++)
            {
                stopwatch.Start();
                tokensFromNode = JsonNavigatorTests.GetTokensFromNode(jsonNavigator.GetRootNode(), jsonNavigator, false);
                stopwatch.Stop();

                if (tokensFromNode.Length == 0)
                {
                    throw new InvalidOperationException("got back zero tokens");
                }
            }

            return stopwatch.Elapsed;
        }

        public static TimeSpan MeasureWritePerformance(IJsonWriter jsonWriter, string json, int numberOfIterations = 1)
        {
            JsonTokenInfo[] tokens = JsonPerfMeasurement.Tokenize(json);
            return JsonPerfMeasurement.MeasureWritePerformance(tokens, jsonWriter, numberOfIterations);
        }

        public static TimeSpan MeasureWritePerformance(JsonTokenInfo[] tokensToWrite, IJsonWriter jsonWriter, int numberOfIterations = 1)
        {
            Stopwatch stopwatch = new Stopwatch();
            foreach (JsonTokenInfo token in tokensToWrite)
            {
                switch (token.JsonTokenType)
                {
                    case JsonTokenType.BeginArray:
                        stopwatch.Start();
                        jsonWriter.WriteArrayStart();
                        stopwatch.Stop();
                        break;
                    case JsonTokenType.EndArray:
                        stopwatch.Start();
                        jsonWriter.WriteArrayEnd();
                        stopwatch.Stop();
                        break;
                    case JsonTokenType.BeginObject:
                        stopwatch.Start();
                        jsonWriter.WriteObjectStart();
                        stopwatch.Stop();
                        break;
                    case JsonTokenType.EndObject:
                        stopwatch.Start();
                        jsonWriter.WriteObjectEnd();
                        stopwatch.Stop();
                        break;
                    case JsonTokenType.String:
                        string stringWithQuotes = Encoding.Unicode.GetString(token.BufferedToken.ToArray());
                        string value = stringWithQuotes.Substring(1, stringWithQuotes.Length - 2);
                        stopwatch.Start();
                        jsonWriter.WriteStringValue(value);
                        stopwatch.Stop();
                        break;
                    case JsonTokenType.Number:
                        stopwatch.Start();
                        jsonWriter.WriteNumberValue(token.Value);
                        stopwatch.Stop();
                        break;
                    case JsonTokenType.True:
                        stopwatch.Start();
                        jsonWriter.WriteBoolValue(true);
                        stopwatch.Stop();
                        break;
                    case JsonTokenType.False:
                        stopwatch.Start();
                        jsonWriter.WriteBoolValue(false);
                        stopwatch.Stop();
                        break;
                    case JsonTokenType.Null:
                        stopwatch.Start();
                        jsonWriter.WriteNullValue();
                        stopwatch.Stop();
                        break;
                    case JsonTokenType.FieldName:
                        string fieldNameWithQuotes = Encoding.Unicode.GetString(token.BufferedToken.ToArray());
                        string fieldName = fieldNameWithQuotes.Substring(1, fieldNameWithQuotes.Length - 2);
                        stopwatch.Start();
                        jsonWriter.WriteFieldName(fieldName);
                        stopwatch.Stop();
                        break;
                    case JsonTokenType.NotStarted:
                    default:
                        throw new ArgumentException("invalid jsontoken");
                }
            }

            return stopwatch.Elapsed;
        }

        public static JsonTokenInfo[] Tokenize(string json)
        {
            IJsonReader jsonReader = JsonReader.Create(Encoding.UTF8.GetBytes(json));
            return JsonPerfMeasurement.Tokenize(jsonReader, json);
        }

        public static JsonTokenInfo[] Tokenize(IJsonReader jsonReader, string json)
        {
            List<JsonTokenInfo> tokensFromReader = new List<JsonTokenInfo>();
            while (jsonReader.Read())
            {
                switch (jsonReader.CurrentTokenType)
                {
                    case JsonTokenType.NotStarted:
                        throw new ArgumentException(string.Format("Got an unexpected JsonTokenType: {0} as an expected token type", jsonReader.CurrentTokenType));
                    case JsonTokenType.BeginArray:
                        tokensFromReader.Add(JsonTokenInfo.ArrayStart());
                        break;
                    case JsonTokenType.EndArray:
                        tokensFromReader.Add(JsonTokenInfo.ArrayEnd());
                        break;
                    case JsonTokenType.BeginObject:
                        tokensFromReader.Add(JsonTokenInfo.ObjectStart());
                        break;
                    case JsonTokenType.EndObject:
                        tokensFromReader.Add(JsonTokenInfo.ObjectEnd());
                        break;
                    case JsonTokenType.String:
                        tokensFromReader.Add(JsonTokenInfo.String(jsonReader.GetStringValue()));
                        break;
                    case JsonTokenType.Number:
                        tokensFromReader.Add(JsonTokenInfo.Number(jsonReader.GetNumberValue()));
                        break;
                    case JsonTokenType.True:
                        tokensFromReader.Add(JsonTokenInfo.Boolean(true));
                        break;
                    case JsonTokenType.False:
                        tokensFromReader.Add(JsonTokenInfo.Boolean(false));
                        break;
                    case JsonTokenType.Null:
                        tokensFromReader.Add(JsonTokenInfo.Null());
                        break;
                    case JsonTokenType.FieldName:
                        tokensFromReader.Add(JsonTokenInfo.FieldName(jsonReader.GetStringValue()));
                        break;
                    default:
                        break;
                }
            }

            return tokensFromReader.ToArray();
        }

        public static byte[] ConvertTextToBinary(string text)
        {
            IJsonWriter binaryWriter = JsonWriter.Create(JsonSerializationFormat.Binary);
            IJsonReader textReader = JsonReader.Create(Encoding.UTF8.GetBytes(text));
            binaryWriter.WriteAll(textReader);
            return binaryWriter.GetResult();
        }

        public static string ConvertBinaryToText(byte[] binary)
        {
            IJsonReader binaryReader = JsonReader.Create(binary);
            IJsonWriter textWriter = JsonWriter.Create(JsonSerializationFormat.Text);
            textWriter.WriteAll(binaryReader);
            return Encoding.UTF8.GetString(textWriter.GetResult());
        }

        public static void PrintStatisticsTable(string inputFileName, JsonExecutionTimes text, JsonExecutionTimes newtonsoft, JsonExecutionTimes binary)
        {
            // Until we merge the text table just do something hacky
            Console.WriteLine("Input File: " + inputFileName);

            Console.WriteLine(TextTable.TopLine);
            Console.WriteLine(TextTable.Header);
            Console.WriteLine(TextTable.MiddleLine);
            Console.WriteLine(TextTable.GetRow(
                "Text",
                text.ReadTime.TotalMilliseconds.ToString("0.00"),
                text.WriteTime.TotalMilliseconds.ToString("0.00"),
                text.NavigationTime.TotalMilliseconds.ToString("0.00"),
                text.DocumentSize));
            Console.WriteLine(TextTable.GetRow(
                "Newtonsoft",
                newtonsoft.ReadTime.TotalMilliseconds.ToString("0.00"),
                newtonsoft.WriteTime.TotalMilliseconds.ToString("0.00"),
                newtonsoft.NavigationTime.TotalMilliseconds.ToString("0.00"),
                newtonsoft.DocumentSize));
            Console.WriteLine(TextTable.GetRow(
                "Binary",
                binary.ReadTime.TotalMilliseconds.ToString("0.00"),
                binary.WriteTime.TotalMilliseconds.ToString("0.00"),
                binary.NavigationTime.TotalMilliseconds.ToString("0.00"),
                binary.DocumentSize));
            Console.WriteLine(TextTable.BottomLine);
        }
    }
}
