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
    using Azure.Cosmos.Spatial;
    using Microsoft.Azure.Documents;

    internal class TextJsonSpatialPathConverter : JsonConverter<SpatialPath>
    {
        public override SpatialPath Read(
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
            return TextJsonSpatialPathConverter.ReadProperty(root);
        }

        public override void Write(
            Utf8JsonWriter writer,
            SpatialPath path,
            JsonSerializerOptions options)
        {
            TextJsonSpatialPathConverter.WritePropertyValues(writer, path, options);
        }

        public static void WritePropertyValues(
            Utf8JsonWriter writer,
            SpatialPath path,
            JsonSerializerOptions options)
        {
            if (path == null)
            {
                return;
            }

            writer.WriteStartObject();
            writer.WriteString(Constants.Properties.Path, path.Path);

            if (path.SpatialTypes != null)
            {
                writer.WritePropertyName(Constants.Properties.Types);
                writer.WriteStartArray();
                foreach (Spatial.SpatialType type in path.SpatialTypes)
                {
                    writer.WriteStringValue(type.ToString());
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        public static SpatialPath ReadProperty(JsonElement root)
        {
            SpatialPath path = new SpatialPath();
            foreach (JsonProperty property in root.EnumerateObject())
            {
                TextJsonSpatialPathConverter.ReadPropertyValue(path, property);
            }

            return path;
        }

        private static void ReadPropertyValue(
            SpatialPath path,
            JsonProperty property)
        {
            if (property.NameEquals(Constants.Properties.Path))
            {
                path.Path = property.Value.GetString();
            }
            else if (property.NameEquals(Constants.Properties.Types))
            {
                path.SpatialTypes = new Collection<Spatial.SpatialType>();
                foreach (JsonElement item in property.Value.EnumerateArray())
                {
                    if (Enum.TryParse(item.GetString(), out Spatial.SpatialType type))
                    {
                        path.SpatialTypes.Add(type);
                    }
                }
            }
        }
    }
}
