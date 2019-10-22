﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json.Interop
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    internal sealed class NewtonsoftToCosmosDBWriter : Microsoft.Azure.Cosmos.Json.JsonWriter
    {
        private readonly Newtonsoft.Json.JsonWriter writer;
        private readonly Func<byte[]> getResultCallback;

        private NewtonsoftToCosmosDBWriter(
            Newtonsoft.Json.JsonWriter writer,
            Func<byte[]> getResultCallback)
            : base(true)
        {
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
            this.getResultCallback = getResultCallback ?? throw new ArgumentNullException(nameof(getResultCallback));
        }

        public override long CurrentLength
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override JsonSerializationFormat SerializationFormat => JsonSerializationFormat.Text;

        public override ReadOnlyMemory<byte> GetResult()
        {
            return this.getResultCallback();
        }

        public override void WriteArrayEnd()
        {
            this.writer.WriteEndArray();
        }

        public override void WriteArrayStart()
        {
            this.writer.WriteStartArray();
        }

        public override void WriteBinaryValue(ReadOnlySpan<byte> value)
        {
            throw new NotImplementedException();
        }

        public override void WriteBoolValue(bool value)
        {
            this.writer.WriteValue(value);
        }

        public override void WriteFieldName(string fieldName)
        {
            this.writer.WritePropertyName(fieldName);
        }

        public override void WriteFloat32Value(float value)
        {
            throw new NotImplementedException();
        }

        public override void WriteFloat64Value(double value)
        {
            throw new NotImplementedException();
        }

        public override void WriteGuidValue(Guid value)
        {
            throw new NotImplementedException();
        }

        public override void WriteInt16Value(short value)
        {
            throw new NotImplementedException();
        }

        public override void WriteInt32Value(int value)
        {
            throw new NotImplementedException();
        }

        public override void WriteInt64Value(long value)
        {
            throw new NotImplementedException();
        }

        public override void WriteInt8Value(sbyte value)
        {
            throw new NotImplementedException();
        }

        public override void WriteNullValue()
        {
            this.writer.WriteNull();
        }

        public override void WriteNumberValue(Number64 value)
        {
            if (value.IsInteger)
            {
                this.writer.WriteValue(Number64.ToLong(value));
            }
            else
            {
                this.writer.WriteValue(Number64.ToDouble(value));
            }
        }

        public override void WriteObjectEnd()
        {
            this.writer.WriteEndObject();
        }

        public override void WriteObjectStart()
        {
            this.writer.WriteStartObject();
        }

        public override void WriteStringValue(string value)
        {
            this.writer.WriteValue(value);
        }

        public override void WriteUInt32Value(uint value)
        {
            throw new NotImplementedException();
        }

        protected override void WriteRawJsonToken(
            JsonTokenType jsonTokenType,
            ReadOnlySpan<byte> rawJsonToken)
        {
            throw new NotImplementedException();
        }

        public static NewtonsoftToCosmosDBWriter CreateTextWriter()
        {
            StringWriter stringWriter = new StringWriter();
            Newtonsoft.Json.JsonTextWriter newtonsoftJsonWriter = new Newtonsoft.Json.JsonTextWriter(stringWriter);
            NewtonsoftToCosmosDBWriter newtonsoftToCosmosDBWriter = new NewtonsoftToCosmosDBWriter(
                newtonsoftJsonWriter,
                () => Encoding.UTF8.GetBytes(stringWriter.ToString()));
            return newtonsoftToCosmosDBWriter;
        }

        public static NewtonsoftToCosmosDBWriter CreateFromWriter(Newtonsoft.Json.JsonWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            return new NewtonsoftToCosmosDBWriter(writer, () => throw new NotSupportedException());
        }
    }
}
