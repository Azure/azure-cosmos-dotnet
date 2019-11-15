﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    internal sealed class TestSettings
    {
        public TestSettings(bool simulate429s, bool simulateEmptyPages)
        {
            this.Simulate429s = simulate429s;
            this.SimulateEmptyPages = simulateEmptyPages;
        }

        public bool Simulate429s { get; }

        public bool SimulateEmptyPages { get; }
    }
}
