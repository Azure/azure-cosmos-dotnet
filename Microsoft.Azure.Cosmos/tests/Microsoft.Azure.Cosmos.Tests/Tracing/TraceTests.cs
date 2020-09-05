﻿namespace Microsoft.Azure.Cosmos.Tests.Tracing
{
    using System;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TraceTests
    {
        [TestMethod]
        public void TestRootTrace()
        {
            Trace rootTrace;
            using (rootTrace = Trace.GetRootTrace(name: "RootTrace"))
            {
                Assert.IsNotNull(rootTrace);
                Assert.IsNotNull(rootTrace.Children);
                Assert.AreEqual(rootTrace.Children.Count, 0);
                Assert.AreEqual(rootTrace.Component, TraceComponent.Unknown);
                Assert.AreNotEqual(rootTrace.Id, Guid.Empty);
                Assert.IsNull(rootTrace.Info);
                Assert.AreEqual(rootTrace.Level, System.Diagnostics.TraceLevel.Verbose);
                Assert.AreEqual(rootTrace.Name, "RootTrace");
                Assert.IsNull(rootTrace.Parent);
                Assert.IsNotNull(rootTrace.StackFrame);
            }

            Assert.IsTrue(rootTrace.Duration > TimeSpan.Zero);
        }

        [TestMethod]
        public void TestTraceChildren()
        {
            using (Trace rootTrace = Trace.GetRootTrace(name: "RootTrace", component: TraceComponent.Query))
            {
                using (ITrace childTrace1 = rootTrace.StartChild("Child1" /*inherits parent's component*/))
                {
                }

                using (ITrace childTrace2 = rootTrace.StartChild("Child2", component: TraceComponent.Transport))
                {
                }

                Assert.AreEqual(rootTrace.Children.Count, 2);
                Assert.AreEqual(rootTrace.Children[0].Component, TraceComponent.Query);
                Assert.AreEqual(rootTrace.Children[1].Component, TraceComponent.Transport);
            }
        }

        [TestMethod]
        public void TestNoOpTrace()
        {
            using (NoOpTrace rootTrace = NoOpTrace.Singleton)
            {
                using (ITrace childTrace1 = rootTrace.StartChild("Child1"))
                {
                }

                using (ITrace childTrace2 = rootTrace.StartChild("Child2"))
                {
                }

                Assert.AreEqual(rootTrace.Children.Count, 0);
            }
        }
    }
}
