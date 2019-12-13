﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;

    internal class TextJsonReplicationPolicyConverter : JsonConverter<ReplicationPolicy>
    {
        public override ReplicationPolicy Read(
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
            return TextJsonReplicationPolicyConverter.ReadProperty(root);
        }

        public override void Write(
            Utf8JsonWriter writer,
            ReplicationPolicy policy,
            JsonSerializerOptions options)
        {
            TextJsonReplicationPolicyConverter.WritePropertyValue(writer, policy);
        }

        public static void WritePropertyValue(
            Utf8JsonWriter writer,
            ReplicationPolicy policy)
        {
            if (policy == null)
            {
                return;
            }

            writer.WriteStartObject();

            writer.WriteNumber(Constants.Properties.MaxReplicaSetSize, policy.MaxReplicaSetSize);

            writer.WriteNumber(Constants.Properties.MinReplicaSetSize, policy.MinReplicaSetSize);

            writer.WriteBoolean(Constants.Properties.AsyncReplication, policy.AsyncReplication);

            writer.WriteEndObject();
        }

        public static ReplicationPolicy ReadProperty(JsonElement root)
        {
            ReplicationPolicy policy = new ReplicationPolicy();
            foreach (JsonProperty property in root.EnumerateObject())
            {
                TextJsonReplicationPolicyConverter.ReadPropertyValue(policy, property);
            }

            return policy;
        }

        private static void ReadPropertyValue(
            ReplicationPolicy policy,
            JsonProperty property)
        {
            if (property.NameEquals(Constants.Properties.MaxReplicaSetSize))
            {
                policy.MaxReplicaSetSize = property.Value.GetInt32();
            }
            else if (property.NameEquals(Constants.Properties.MinReplicaSetSize))
            {
                policy.MinReplicaSetSize = property.Value.GetInt32();
            }
            else if (property.NameEquals(Constants.Properties.AsyncReplication))
            {
                policy.AsyncReplication = property.Value.GetBoolean();
            }
        }
    }
}
