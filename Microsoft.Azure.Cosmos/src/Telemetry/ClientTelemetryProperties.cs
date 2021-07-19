﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Text;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos.CosmosElements.Telemetry;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Newtonsoft.Json;

    [Serializable]
    internal sealed class ClientTelemetryProperties
    {
        [JsonProperty(PropertyName = "timeStamp")]
        internal string DateTimeUtc { get; set; }

        [JsonProperty(PropertyName = "clientId")]
        private string ClientId { get; }

        [JsonProperty(PropertyName = "processId")]
        private string ProcessId { get; }

        [JsonProperty(PropertyName = "userAgent")]
        private string UserAgent { get; }

        [JsonProperty(PropertyName = "connectionMode")]
        private string ConnectionMode { get; }

        [JsonProperty(PropertyName = "globalDatabaseAccountName")]
        internal string GlobalDatabaseAccountName { get; set; }

        [JsonProperty(PropertyName = "applicationRegion")]
        internal string ApplicationRegion { get; set; }

        [JsonProperty(PropertyName = "hostEnvInfo")]
        internal string HostEnvInfo { get; set; }

        [JsonProperty(PropertyName = "acceleratedNetworking")]
        private bool? AcceleratedNetworking { get; set; }

        [JsonProperty(PropertyName = "systemInfo")]
        internal List<SystemInfo> SystemInfo { get; set; }

        [JsonProperty(PropertyName = "cacheRefreshInfo")]
        private List<OperationInfo> CacheRefreshInfo { get; set; }

        [JsonProperty(PropertyName = "operationInfo")]
        internal List<OperationInfo> OperationInfo { get; set; }

        [JsonIgnore]
        private readonly ConnectionMode ConnectionModeEnum;

        internal ClientTelemetryProperties(string clientId,
                                   string processId,
                                   string userAgent,
                                   ConnectionMode connectionMode)
        {
            this.ClientId = clientId;
            this.ProcessId = processId;
            this.UserAgent = userAgent;
            this.ConnectionModeEnum = connectionMode;
            this.ConnectionMode = connectionMode.ToString();
            this.SystemInfo = new List<SystemInfo>();
        }

        public ClientTelemetryProperties(string dateTimeUtc,
            string clientId,
            string processId,
            string userAgent,
            string connectionMode,
            string globalDatabaseAccountName,
            string applicationRegion,
            string hostEnvInfo,
            bool? acceleratedNetworking,
            List<SystemInfo> systemInfo,
            List<OperationInfo> cacheRefreshInfo,
            List<OperationInfo> operationInfo)
        {
            this.DateTimeUtc = dateTimeUtc;
            this.ClientId = clientId;
            this.ProcessId = processId;
            this.UserAgent = userAgent;
            this.ConnectionMode = connectionMode;
            this.GlobalDatabaseAccountName = globalDatabaseAccountName;
            this.ApplicationRegion = applicationRegion;
            this.HostEnvInfo = hostEnvInfo;
            this.AcceleratedNetworking = acceleratedNetworking;
            this.SystemInfo = systemInfo;
            this.CacheRefreshInfo = cacheRefreshInfo;
            this.OperationInfo = operationInfo;
        }
    }
}