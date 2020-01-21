﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Text;
    using Newtonsoft.Json;

    /// <summary>
    /// This represents the core diagnostics object used in the SDK.
    /// This object gets created on the initial request and passed down
    /// through the pipeline appending information as it goes into a list
    /// where it is lazily converted to a JSON string.
    /// </summary>
    internal sealed class CosmosDiagnosticsContext : CosmosDiagnostics
    {
        // Summary contains the high level overview of operations
        // like start time, retries, and other aggregated information
        internal CosmosDiagnosticSummary Summary { get; }

        // Context list is detailed view of all the operations
        internal CosmosDiagnosticsContextList ContextList { get; }

        private bool isOverallScopeSet = false;

        internal CosmosDiagnosticsContext()
        {
            this.Summary = new CosmosDiagnosticSummary(DateTime.UtcNow);
            this.ContextList = new CosmosDiagnosticsContextList();
        }

        internal CosmosDiagnosticScope CreateOverallScope(string name)
        {
            CosmosDiagnosticScope scope;
            // If overall is already set then let the original set the elapsed time.
            if (this.isOverallScopeSet)
            {
                scope = new CosmosDiagnosticScope(name);
            }
            else
            {
                scope = new CosmosDiagnosticScope(name, this.Summary.SetElapsedTime);
                this.isOverallScopeSet = true;
            }

            this.ContextList.AddWriter(scope);
            return scope;
        }

        internal CosmosDiagnosticScope CreateScope(string name)
        {
            CosmosDiagnosticScope scope = new CosmosDiagnosticScope(name);

            this.ContextList.AddWriter(scope);
            return scope;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);

            using (JsonWriter jsonWriter = new JsonTextWriter(sw))
            {
                jsonWriter.WriteStartObject();
                this.Summary.WriteJsonProperty(jsonWriter);
                jsonWriter.WritePropertyName("Context");
                jsonWriter.WriteStartArray();
                this.ContextList.WriteJsonObject(jsonWriter);
                jsonWriter.WriteEndArray();
                jsonWriter.WriteEndObject();
            }

            return sb.ToString();
        }

        internal void AddContextWriter(CosmosDiagnosticWriter diagnosticWriter)
        {
            this.ContextList.AddWriter(diagnosticWriter);
        }

        internal void Append(CosmosDiagnosticsContext newContext)
        {
            if (Object.ReferenceEquals(this, newContext))
            {
                return;
            }

            this.Summary.Append(newContext.Summary);

            this.ContextList.Append(newContext.ContextList);
        }
    }
}