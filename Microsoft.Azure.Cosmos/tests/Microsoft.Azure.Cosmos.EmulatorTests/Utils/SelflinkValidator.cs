﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.RegularExpressions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    internal static class SelflinkValidator
    {
        internal static void ValidateDbSelfLink(string selflink)
        {
            Assert.IsTrue(Regex.IsMatch(selflink, "dbs/(.*)/"));
        }

        internal static void ValidateContainerSelfLink(string selflink)
        {
            Assert.IsTrue(Regex.IsMatch(selflink, "dbs/(.*)/colls/(.*)/"));
        }

        public static void ValidateUdfSelfLink(string selfLink)
        {
            Assert.IsTrue(Regex.IsMatch(selfLink, "dbs/(.*)/colls/(.*)/udfs/(.*)/"));
        }

        internal static void ValidateUserSelfLink(string selflink)
        {
            Assert.IsTrue(Regex.IsMatch(selflink, "dbs/(.*)/users/(.*)/"));
        }

        internal static void ValidatePermissionSelfLink(string selflink)
        {
            Assert.IsTrue(Regex.IsMatch(selflink, "dbs/(.*)/users/(.*)/permissions/(.*)/"));
        }

        internal static void ValidateTroughputSelfLink(string selflink)
        {
            Assert.IsTrue(Regex.IsMatch(selflink, "offers/(.*)/"));
        }

        internal static void ValidateAccountSelfLink(string selflink)
        {
            Assert.AreEqual(string.Empty, selflink);
        }
    }
}
