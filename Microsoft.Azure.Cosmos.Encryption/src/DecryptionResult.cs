﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;

    /// <summary>
    /// Represents the details of failure encountered while decrypting response content.
    /// </summary>
    public class DecryptionResult
    {
        protected DecryptionResult()
        {
        }

        /// <summary>
        /// The encrypted document returned as is (without decryption) in case of failure
        /// </summary>
        public virtual ReadOnlyMemory<byte> EncryptedStream { get; }

        /// <summary>
        /// Represents the exception encountered.
        /// </summary>
        public virtual Exception Exception { get; }

        internal DecryptionResult(
            ReadOnlyMemory<byte> encryptedStream,
            Exception exception)
        {
            this.EncryptedStream = encryptedStream;
            this.Exception = exception;
        }
    }
}
