﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;

    internal class TextJsonUniqueKeyPolicyConverter : JsonConverter<UniqueKeyPolicy>
    {
        public override UniqueKeyPolicy Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(string.Format(CultureInfo.CurrentCulture, RMResources.JsonUnexpectedToken));
            }

            using JsonDocument json = JsonDocument.ParseValue(ref reader);
            JsonElement root = json.RootElement;
            return TextJsonUniqueKeyPolicyConverter.ReadProperty(root);
        }

        public override void Write(
            Utf8JsonWriter writer,
            UniqueKeyPolicy key,
            JsonSerializerOptions options)
        {
            TextJsonUniqueKeyPolicyConverter.WritePropertyValues(writer, key, options);
        }

        public static void WritePropertyValues(
            Utf8JsonWriter writer,
            UniqueKeyPolicy policy,
            JsonSerializerOptions options)
        {
            if (policy == null)
            {
                return;
            }

            writer.WriteStartObject();

            if (policy.UniqueKeys != null)
            {
                writer.WritePropertyName(Constants.Properties.UniqueKeys);
                writer.WriteStartArray();
                foreach (UniqueKey uniqueKey in policy.UniqueKeys)
                {
                    TextJsonUniqueKeyConverter.WritePropertyValues(writer, uniqueKey, options);
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        public static UniqueKeyPolicy ReadProperty(JsonElement root)
        {
            UniqueKeyPolicy policy = new UniqueKeyPolicy();
            foreach (JsonProperty property in root.EnumerateObject())
            {
                TextJsonUniqueKeyPolicyConverter.ReadPropertyValue(policy, property);
            }

            return policy;
        }

        private static void ReadPropertyValue(
            UniqueKeyPolicy policy,
            JsonProperty property)
        {
            if (property.NameEquals(Constants.Properties.UniqueKeys))
            {
                policy.UniqueKeys = new Collection<UniqueKey>();
                foreach (JsonElement item in property.Value.EnumerateArray())
                {
                    policy.UniqueKeys.Add(TextJsonUniqueKeyConverter.ReadProperty(item));
                }
            }
        }
    }
}
