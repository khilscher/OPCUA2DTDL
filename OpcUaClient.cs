using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using System.Threading;
using System.Diagnostics;
using System.Collections.ObjectModel;
using OPCUA2DTDL.Models;

namespace OPCUA2DTDL
{

    public enum ExitCode : int
    {
        Ok = 0,
        ErrorCreateApplication = 0x11,
        ErrorDiscoverEndpoints = 0x12,
        ErrorCreateSession = 0x13,
        ErrorBrowseNamespace = 0x14,
        ErrorCreateSubscription = 0x15,
        ErrorMonitoredItem = 0x16,
        ErrorAddSubscription = 0x17,
        ErrorRunning = 0x18,
        ErrorNoKeepAlive = 0x30,
        ErrorInvalidCommandLine = 0x100
    };

    class OpcUaClient
    {
        const int ReconnectPeriod = 10;
        Session session;
        SessionReconnectHandler reconnectHandler;
        string endpointURL;
        int clientRunTime = Timeout.Infinite;
        static bool autoAccept = false;
        static ExitCode exitCode;
        OpcUaNodeList list = new OpcUaNodeList();

        public OpcUaClient(string _endpointURL, bool _autoAccept, int _stopTimeout)
        {
            endpointURL = _endpointURL;
            autoAccept = _autoAccept;
            clientRunTime = _stopTimeout <= 0 ? Timeout.Infinite : _stopTimeout * 1000;
        }

        public static ExitCode ExitCode { get => exitCode; }

        public async Task<Session> Connect()
        {

            Debug.WriteLine("1 - Create an Application Configuration.");

            exitCode = ExitCode.ErrorCreateApplication;

            ApplicationInstance application = new ApplicationInstance
            {
                ApplicationName = "OPCUA2DTDL",
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = Utils.IsRunningOnMono() ? "Opc.Ua.MonoSampleClient" : "Opc.Ua.SampleClient"
            };

            // load the application configuration.
            ApplicationConfiguration config = await application.LoadApplicationConfiguration(false);

            // check the application certificate.
            bool haveAppCertificate = await application.CheckApplicationInstanceCertificate(false, 0);
            if (!haveAppCertificate)
            {
                throw new Exception("Application instance certificate invalid!");
            }

            if (haveAppCertificate)
            {
                config.ApplicationUri = Utils.GetApplicationUriFromCertificate(config.SecurityConfiguration.ApplicationCertificate.Certificate);
                if (config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
                {
                    autoAccept = true;
                }
                config.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(CertificateValidator_CertificateValidation);
            }
            else
            {
                Debug.WriteLine("    WARN: missing application certificate, using unsecure connection.");
            }

            Debug.WriteLine("2 - Discover endpoints of {0}.", endpointURL);
            exitCode = ExitCode.ErrorDiscoverEndpoints;
            var selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointURL, haveAppCertificate, 15000);
            Debug.WriteLine("    Selected endpoint uses: {0}",
                selectedEndpoint.SecurityPolicyUri.Substring(selectedEndpoint.SecurityPolicyUri.LastIndexOf('#') + 1));

            Debug.WriteLine("3 - Create a session with OPC UA server.");
            exitCode = ExitCode.ErrorCreateSession;
            var endpointConfiguration = EndpointConfiguration.Create(config);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);

            return session = await Session.Create(config, endpoint, false, "OPC UA Console Client", 60000, new UserIdentity(new AnonymousIdentityToken()), null);

        }

        public async Task<OpcUaNodeList> Browse()
        {
            exitCode = ExitCode.ErrorBrowseNamespace;
            ReferenceDescriptionCollection references;
            Byte[] continuationPoint;

            references = session.FetchReferences(ObjectIds.ObjectsFolder);

            //Browse root nodes
            session.Browse(
                null,
                null,
                ObjectIds.ObjectsFolder,
                0u,
                BrowseDirection.Forward,
                ReferenceTypeIds.HierarchicalReferences,
                true,
                (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method | (uint)NodeClass.DataType, 
                out continuationPoint,
                out references);

            // Obtain details of each node in the root
            foreach (var rd in references)
            {

                if (!rd.DisplayName.ToString().StartsWith("_"))
                {


                    //Debug.WriteLine(" ** {0}, {1}, {2}, {3}", rd.DisplayName, rd.BrowseName, rd.NodeClass, rd.NodeId);

                    OpcUaNode item = new OpcUaNode();
                    item.DisplayName = rd.DisplayName.ToString();
                    item.BrowseName = rd.BrowseName.ToString();
                    item.NodeClass = rd.NodeClass.ToString();
                    item.NodeId = rd.NodeId.ToString();
                    item.DataType = GetDataType(session, ExpandedNodeId.ToNodeId(rd.NodeId, session.NamespaceUris));

                    item.Children = new ObservableCollection<OpcUaNode>();
                    list.Add(item);

                    // Browse next level down
                    BrowseNext(ExpandedNodeId.ToNodeId(rd.NodeId, session.NamespaceUris), item);
                }

            }

            return list;
        }

        public void BrowseNext(NodeId nodeid, OpcUaNode item)
        {
            exitCode = ExitCode.ErrorBrowseNamespace;
            ReferenceDescriptionCollection references;
            Byte[] continuationPoint;

            session.Browse(
                null,
                null,
                nodeid,
                0u,
                BrowseDirection.Forward,
                ReferenceTypeIds.HierarchicalReferences,
                true,
                (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method,
                out continuationPoint,
                out references);

            foreach (var rd in references)
            {
                try
                {
                    if (!rd.DisplayName.ToString().StartsWith("_"))
                    {

                        /*
                        if (rd.DisplayName.ToString() == "PowerOnOff")
                        {
                            Debug.WriteLine(rd.DisplayName.ToString());
                        }
                        */

                        //Debug.WriteLine("   --> {0}, {1}, {2}, {3}", rd.DisplayName, rd.BrowseName, rd.NodeClass, rd.NodeId);
                        OpcUaNode childItem = new OpcUaNode();
                        childItem.DisplayName = rd.DisplayName.ToString();
                        childItem.BrowseName = rd.BrowseName.ToString();
                        childItem.NodeClass = rd.NodeClass.ToString();
                        childItem.NodeId = rd.NodeId.ToString();
                        childItem.DataType = GetDataType(session, ExpandedNodeId.ToNodeId(rd.NodeId, session.NamespaceUris));
                        childItem.Children = new ObservableCollection<OpcUaNode>();

                        // Add it to the parent
                        item.Children.Add(childItem);

                        // Recursion
                        BrowseNext(ExpandedNodeId.ToNodeId(rd.NodeId, session.NamespaceUris), childItem);

                    }

                }
                catch (Exception e)
                {
                    //Debug.WriteLine(e.Message);
                }
            }
        }

        //private TypeInfo GetExpectedType(Session session, NodeId nodeId)
        private string GetDataType(Session session, NodeId nodeId)
        {
            // build list of attributes to read.
            ReadValueIdCollection nodesToRead = new ReadValueIdCollection();

            foreach (uint attributeId in new uint[] { Attributes.DataType, Attributes.ValueRank })
            {
                ReadValueId nodeToRead = new ReadValueId();
                nodeToRead.NodeId = nodeId;
                nodeToRead.AttributeId = attributeId;
                nodesToRead.Add(nodeToRead);
            }

            // read the attributes.
            DataValueCollection results = null;
            DiagnosticInfoCollection diagnosticInfos = null;

            session.Read(
                null,
                0,
                TimestampsToReturn.Neither,
                nodesToRead,
                out results,
                out diagnosticInfos);

            ClientBase.ValidateResponse(results, nodesToRead);
            ClientBase.ValidateDiagnosticInfos(diagnosticInfos, nodesToRead);

            // this call checks for error and checks the data type of the value.
            // if an error or mismatch occurs the default value is returned.
            NodeId dataTypeId = results[0].GetValue<NodeId>(null);
            //int valueRank = results[1].GetValue<int>(ValueRanks.Scalar);

            // use the local type cache to look up the base type for the data type.
            BuiltInType builtInType = DataTypes.GetBuiltInType(dataTypeId, session.NodeCache.TypeTree);

            // the type info object is used in cast and compare functions.
            return builtInType.ToString();
            //return new TypeInfo(builtInType, valueRank);
        }

        private static void CertificateValidator_CertificateValidation(CertificateValidator validator, CertificateValidationEventArgs e)
        {
            if (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted)
            {
                e.Accept = autoAccept;
                if (autoAccept)
                {
                    //Console.WriteLine("Accepted Certificate: {0}", e.Certificate.Subject);
                    Debug.WriteLine("Accepted Certificate: {0}", e.Certificate.Subject);
                }
                else
                {
                    //Console.WriteLine("Rejected Certificate: {0}", e.Certificate.Subject);
                    Debug.WriteLine("Rejected Certificate: {0}", e.Certificate.Subject);
                }
            }
        }

    }
}
