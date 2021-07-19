﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos.CosmosElements.Telemetry;
    using Newtonsoft.Json;

    [Serializable]
    internal class SystemInfo
    {
        [JsonProperty(PropertyName = "metricInfo")]
        internal MetricInfo MetricInfo { get; set; }

        internal SystemInfo(string metricsName, string unitName)
        {
            this.MetricInfo = new MetricInfo(metricsName, unitName);
        }

        public SystemInfo(MetricInfo metricInfo)
        {
            this.MetricInfo = metricInfo;
        }

        internal void SetAggregators(LongConcurrentHistogram histogram, long adjustment = 1)
        {
            this.MetricInfo.SetAggregators(histogram, adjustment);
        }

    }
}