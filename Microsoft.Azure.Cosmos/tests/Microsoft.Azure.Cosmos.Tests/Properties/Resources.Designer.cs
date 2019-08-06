﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Properties {
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text;


    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Microsoft.Azure.Cosmos.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Byte[].
        /// </summary>
        internal static byte[] BaselineTest_Documents {
            get {
                object obj = ResourceManager.GetObject("BaselineTest_Documents", resourceCulture);
                return ((byte[])(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Byte[].
        /// </summary>
        internal static byte[] BaselineTest_PartitionRoutingHelper_AddFormattedContinuationToHeader {
            get {
                object obj = ResourceManager.GetObject("BaselineTest_PartitionRoutingHelper_AddFormattedContinuationToHeader", resourceCulture);
                return ((byte[])(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Byte[].
        /// </summary>
        internal static byte[] BaselineTest_PartitionRoutingHelper_ExtractPartitionKeyRangeFromHeaders {
            get {
                string fileContent = File.ReadAllText(@"Routing\resources\BaselineTest.PartitionRoutingHelper.ExtractPartitionKeyRangeFromHeaders.json");
                return Encoding.UTF8.GetBytes((string)fileContent);
            }
        }

        /// <summary>
        ///   Looks up a localized resource of type System.Byte[].
        /// </summary>
        internal static byte[] BaselineTest_PartitionRoutingHelper_GetPartitionRoutingInfo {
            get {
                string fileContent = File.ReadAllText(@"Routing\resources\BaselineTest.PartitionRoutingHelper.GetPartitionRoutingInfo.json");
                return Encoding.UTF8.GetBytes((string)fileContent);
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Byte[].
        /// </summary>
        internal static byte[] BaselineTest_PartitionSchemes {
            get {
                string fileContent = File.ReadAllText(@"Routing\resources\BaselineTest.PartitionSchemes.json");
                return Encoding.UTF8.GetBytes((string)fileContent);
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Byte[].
        /// </summary>
        internal static byte[] BaselineTest_PathParser {
            get {
                string fileContent = File.ReadAllText(@"Routing\resources\BaselineTest.PathParser.json");
                return Encoding.UTF8.GetBytes((string)fileContent);
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Byte[].
        /// </summary>
        internal static byte[] BaselineTest_PathParser_Extra {
            get {
                string fileContent = File.ReadAllText(@"Routing\resources\BaselineTest.PathParser.Extra.json");
                return Encoding.UTF8.GetBytes((string)fileContent);
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Byte[].
        /// </summary>
        internal static byte[] BaselineTest_Queries {
            get {
                object obj = ResourceManager.GetObject("BaselineTest_Queries", resourceCulture);
                return ((byte[])(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Byte[].
        /// </summary>
        internal static byte[] BaselineTest_QueryPartitionProvider {
            get {
                object obj = ResourceManager.GetObject("BaselineTest_QueryPartitionProvider", resourceCulture);
                return ((byte[])(obj));
            }
        }
        
        /// <summary>
        ///   Looks up a localized resource of type System.Byte[].
        /// </summary>
        internal static byte[] BaselineTest_QueryPartitionProvider_Error {
            get {
                object obj = ResourceManager.GetObject("BaselineTest_QueryPartitionProvider_Error", resourceCulture);
                return ((byte[])(obj));
            }
        }
    }
}
