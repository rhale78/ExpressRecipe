using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Data.SqlClient;
using Microsoft.Win32;
using System.Runtime.Remoting.Messaging;
using System;
using System.Text;
using System.Transactions;
using Microsoft.Pex.Framework.Suppression;

// <copyright file="PexAssemblyInfo.cs">Copyright ©  2016</copyright>
using Microsoft.Pex.Framework.Coverage;
using Microsoft.Pex.Framework.Creatable;
using Microsoft.Pex.Framework.Instrumentation;
using Microsoft.Pex.Framework.Settings;
using Microsoft.Pex.Framework.Validation;

// Microsoft.Pex.Framework.Settings
[assembly: PexAssemblySettings(TestFramework = "MSTestv2")]

// Microsoft.Pex.Framework.Instrumentation
[assembly: PexAssemblyUnderTest("CodeGenerator.Core")]
[assembly: PexInstrumentAssembly("System.Core")]
[assembly: PexInstrumentAssembly("System.Data")]

// Microsoft.Pex.Framework.Creatable
[assembly: PexCreatableFactoryForDelegates]

// Microsoft.Pex.Framework.Validation
[assembly: PexAllowedContractRequiresFailureAtTypeUnderTestSurface]
[assembly: PexAllowedXmlDocumentedException]

// Microsoft.Pex.Framework.Coverage
[assembly: PexCoverageFilterAssembly(PexCoverageDomain.UserOrTestCode, "System.Core")]
[assembly: PexCoverageFilterAssembly(PexCoverageDomain.UserOrTestCode, "System.Data")]
[assembly: PexSuppressUninstrumentedMethodFromType(typeof(Transaction))]
[assembly: PexSuppressUninstrumentedMethodFromType(typeof(UnicodeEncoding))]
[assembly: PexSuppressUninstrumentedMethodFromType(typeof(BitConverter))]
[assembly: PexSuppressUninstrumentedMethodFromType(typeof(EncodingProvider))]
[assembly: PexSuppressUninstrumentedMethodFromType("System.Text.InternalEncoderBestFitFallback")]
[assembly: PexSuppressUninstrumentedMethodFromType("System.Text.InternalDecoderBestFitFallback")]
[assembly: PexSuppressUninstrumentedMethodFromType(typeof(CallContext))]
[assembly: PexSuppressUninstrumentedMethodFromType("System.Text.EncodingNLS")]
[assembly: PexSuppressUninstrumentedMethodFromType(typeof(Environment))]
[assembly: PexSuppressUninstrumentedMethodFromType(typeof(RegistryKey))]
[assembly: PexSuppressUninstrumentedMethodFromType(typeof(Random))]
[assembly: PexSuppressUninstrumentedMethodFromType(typeof(AppDomain))]
[assembly: PexSuppressStaticFieldStore(typeof(SqlConnection), "_objectTypeCount")]
[assembly: PexSuppressStaticFieldStore("System.Data.ProviderBase.DbConnectionPoolGroup", "_objectTypeCount")]
[assembly: PexSuppressStaticFieldStore("System.Data.ProviderBase.DbConnectionPool", "_objectTypeCount")]
[assembly: PexSuppressStaticFieldStore("System.Data.ProviderBase.DbConnectionPool+TransactedConnectionPool", "_objectTypeCount")]
[assembly: PexSuppressStaticFieldStore("System.Data.ProviderBase.DbConnectionInternal", "_objectTypeCount")]
[assembly: PexSuppressStaticFieldStore("System.Data.SqlClient.TdsParser", "_objectTypeCount")]
[assembly: PexSuppressStaticFieldStore("System.Data.SqlClient.TdsParserStateObject", "_objectTypeCount")]
[assembly: PexSuppressStaticFieldStore("System.Data.SqlClient.TdsParser", "s_maxSSPILength")]
[assembly: PexSuppressStaticFieldStore("System.Data.SqlClient.TdsParser", "s_fSSPILoaded")]
[assembly: PexSuppressStaticFieldStore("System.Data.Common.ADP", "_systemDataVersion")]
[assembly: PexSuppressStaticFieldStore("System.Data.Common.ActivityCorrelator", "tlsActivity")]
[assembly: PexSuppressStaticFieldStore("System.Data.SqlClient.TdsParser", "s_nicAddress")]
[assembly: PexSuppressStaticFieldStore(typeof(SqlCommand), "_objectTypeCount")]
[assembly: PexSuppressStaticFieldStore(typeof(SqlDataReader), "_objectTypeCount")]
[assembly: PexSuppressStaticFieldStore("System.Data.SqlClient.SqlReferenceCollection+<>c", "<>9__5_0")]
[assembly: PexSuppressUninstrumentedMethodFromType("System.Data.ProviderBase.DbConnectionPoolIdentity")]
[assembly: PexSuppressUninstrumentedMethodFromType(typeof(SafeHandle))]
[assembly: PexSuppressUninstrumentedMethodFromType("System.Data.Common.SafeNativeMethods")]
[assembly: PexSuppressUninstrumentedMethodFromType("Microsoft.Win32.SafeNativeMethods")]
[assembly: PexSuppressUninstrumentedMethodFromType(typeof(SafeHandleZeroOrMinusOneIsInvalid))]
[assembly: PexSuppressUninstrumentedMethodFromType(typeof(Marshal))]
[assembly: PexSuppressUninstrumentedMethodFromType(typeof(GCHandle))]
[assembly: PexSuppressUninstrumentedMethodFromType(typeof(Buffer))]
[assembly: PexSuppressUninstrumentedMethodFromType("Microsoft.Win32.Win32Native")]

