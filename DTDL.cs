using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using OPCUA2DTDL.Models;
using Org.BouncyCastle.Asn1.Mozilla;

namespace OPCUA2DTDL
{
    class DTDL
    {
        private static string dtmiPrefix = "dtmi:com:example:";
        private static Dictionary<string, string> _map = new Dictionary<string, string>();  // Primitive schema map

        public static DtdlInterface GenerateDTDL(OpcUaNode node, OpcUaNodeList variables)
        {
            // Create schema mapping table
            CreateSchemaMap();

            // Create Interface
            DtdlInterface dtdlInterface = new DtdlInterface
            {
                Id = dtmiPrefix + node.DisplayName + ";1",
                Type = "Interface",
                DisplayName = node.DisplayName,
                Contents = new List<DtdlContents>(),
                Comment = $"Derived from {node.NodeId}"
            };

            if (variables.Count > 0)
            {

                foreach (var variable in variables)
                {

                    DtdlContents dtdlProperty = new DtdlContents
                    {
                        Type = "Property",
                        Name = variable.DisplayName,
                        Schema = _map[variable.DataType]
                    };

                    dtdlInterface.Contents.Add(dtdlProperty);

                }
            }
            else
            {

                DtdlContents dtdlRelationship = new DtdlContents
                {
                    Type = "Relationship",
                    Name = "Organizes"
                };

                dtdlInterface.Contents.Add(dtdlRelationship);

            }

            _map.Clear();

            return dtdlInterface;

        }

        public static DtdlInterface GenerateDTDL()
        {

            // Create Interface
            DtdlInterface dtdlInterface = new DtdlInterface
            {
                Id = dtmiPrefix + "sample_interface;1",
                Type = "Interface",
                DisplayName = "",
                Contents = new List<DtdlContents>(),
                Comment = ""
            };

            DtdlContents relationship = new DtdlContents
            {
                Type = "Relationship",
                Name = "Controls"
            };

            dtdlInterface.Contents.Add(relationship);

            DtdlContents telemetry = new DtdlContents
            {
                Type = "Telemetry",
                Name = "temp",
                Schema = "double"
            };

            dtdlInterface.Contents.Add(telemetry);

            DtdlContents property = new DtdlContents
            {
                Type = "Property",
                Name = "firmware_version",
                Schema = "integer"
            };

            dtdlInterface.Contents.Add(property);

            return dtdlInterface;

        }

        /// <summary>
        /// Create a dictionary to map OPC UA datatypes to DTDL primitives
        /// </summary>
        private static void CreateSchemaMap()
        {

            // Customize the schema mappings as needed
            // https://reference.opcfoundation.org/v104/Core/docs/Part6/5.1.2/
            // -> https://github.com/Azure/opendigitaltwins-dtdl/blob/master/DTDL/v2/dtdlv2.md#primitive-schemas

            _map.Add("Boolean", "boolean");
            //_map.Add("", "date");
            _map.Add("DateTime", "dateTime");
            _map.Add("Double", "double");
            //_map.Add("", "duration");
            _map.Add("Float", "float");
            _map.Add("SByte", "integer");
            _map.Add("Byte", "integer");
            _map.Add("Int16", "integer");
            _map.Add("UInt16", "integer");
            _map.Add("Int32", "integer");
            _map.Add("UInt32", "integer");
            _map.Add("Int64", "long");
            _map.Add("UInt64", "long");
            _map.Add("String", "string");
            //_map.Add("", "time");

        }
    }
}
