﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class QueryRequestOptionsUniTests
    {
        [TestMethod]
        public void StatelessTest()
        {
            QueryRequestOptions requestOption = new QueryRequestOptions();
            requestOption.RequestContinuation = "SomeToken";

            CosmosRequestMessage testMessage = new CosmosRequestMessage();
            requestOption.FillRequestOptions(testMessage);

            Assert.IsNull(testMessage.Headers.Continuation);
        }
    }
}
