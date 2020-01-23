﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    internal sealed class CosmosDiagnosticsContextList : CosmosDiagnosticsInternal, IEnumerable<CosmosDiagnosticsInternal>
    {
        private List<CosmosDiagnosticsInternal> contextList { get; }

        public CosmosDiagnosticsContextList(List<CosmosDiagnosticsInternal> contextList)
        {
            this.contextList = contextList ?? throw new ArgumentNullException(nameof(contextList));
        }

        public CosmosDiagnosticsContextList()
            : this(new List<CosmosDiagnosticsInternal>())
        {
        }

        public void AddWriter(CosmosDiagnosticsInternal diagnosticWriter)
        {
            this.contextList.Add(diagnosticWriter);
        }

        public void Append(CosmosDiagnosticsContextList newContext)
        {
            this.contextList.Add(newContext);
        }

        public override void Accept(CosmosDiagnosticsInternalVisitor cosmosDiagnosticsInternalVisitor)
        {
            cosmosDiagnosticsInternalVisitor.Visit(this);
        }

        public override TResult Accept<TResult>(CosmosDiagnosticsInternalVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }

        public IEnumerator<CosmosDiagnosticsInternal> GetEnumerator()
        {
            return this.contextList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.contextList.GetEnumerator();
        }
    }
}
