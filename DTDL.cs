using System.Collections.Generic;
using OPCUA2DTDL.Models;

namespace OPCUA2DTDL
{
    class DTDL
    {

        private static Dictionary<string, string> _map = new Dictionary<string, string>();  // Primitive schema map

        /// <summary>
        /// Generates a DTDL Interface from an OPC UA node
        /// </summary>
        /// <param name="node"></param>
        /// <param name="expandedDtdlMode"></param>
        /// <param name="dtmiPrefix"></param>
        /// <returns></returns>
        public static DtdlInterface GenerateDTDL(OpcUaNode node, bool expandedDtdlMode, string dtmiPrefix)
        {

            // Create schema mapping table
            CreateSchemaMap();

            string name;

            // If creating an Interface using an OPC UA Object instead of an ObjectType, 
            // then lookup the TypeDefinition.
            if (node.NodeClass == "Object")
            {

                name = OpcUaClient.GetTypeDefinition(node.TypeDefinition);

            }
            else
            {

                name = node.DisplayName;

            }

            // Create Interface
            DtdlInterface dtdlInterface = new DtdlInterface
            {
                Id = dtmiPrefix + name + ";1",
                Type = "Interface",
                DisplayName = name,
                Contents = new List<DtdlContents>(),
                Comment = $"Derived from {node.NodeId}"
            };

            // Generate collapsed mode DTDL
            if (expandedDtdlMode == false)
            {

                if (node.Children.Count > 0)
                {

                    foreach (var child in node.Children)
                    {

                        // i=68 (PropertyType) maps to DTDL property
                        if (child.TypeDefinition == "i=68")
                        {

                            DtdlContents dtdlProperty = new DtdlContents
                            {
                                Type = "Property",
                                Name = child.DisplayName,
                                Schema = GetDtdlDataType(child.DataType)
                            };

                            dtdlInterface.Contents.Add(dtdlProperty);

                        }

                        // i=63 (BaseDataVariableType) maps to DTDL telemetry
                        if (child.TypeDefinition == "i=63")
                        {

                            DtdlContents dtdlTelemetry = new DtdlContents
                            {
                                Type = "Telemetry",
                                Name = child.DisplayName,
                                Schema = GetDtdlDataType(child.DataType)
                            };

                            dtdlInterface.Contents.Add(dtdlTelemetry);

                        }

                        // NodeClass == Method maps to DTDL command
                        if (child.NodeClass == "Method")
                        {

                            DtdlContents dtdlCommand = new DtdlContents
                            {
                                Type = "Command",
                                Name = child.DisplayName
                            };

                            dtdlInterface.Contents.Add(dtdlCommand);

                        }

                    }

                }

                if (node.Children.Count == 0)
                {

                    // i=68 (PropertyType) maps to DTDL property
                    if (node.TypeDefinition == "i=68")
                    {

                        DtdlContents dtdlProperty = new DtdlContents
                        {
                            Type = "Property",
                            Name = node.DisplayName,
                            Schema = GetDtdlDataType(node.DataType)
                        };

                        dtdlInterface.Contents.Add(dtdlProperty);

                    }

                    // i=63 (BaseDataVariableType) maps to DTDL telemetry
                    if (node.TypeDefinition == "i=63")
                    {
                        DtdlContents dtdlTelemetry = new DtdlContents
                        {
                            Type = "Telemetry",
                            Name = node.DisplayName,
                            Schema = GetDtdlDataType(node.DataType)
                        };

                        dtdlInterface.Contents.Add(dtdlTelemetry);
                    }

                    // NodeClass == Method maps to DTDL command
                    if (node.NodeClass == "Method")
                    {

                        DtdlContents dtdlCommand = new DtdlContents
                        {
                            Type = "Command",
                            Name = node.DisplayName
                        };

                        dtdlInterface.Contents.Add(dtdlCommand);

                    }

                }
            }

            // Generate expanded mode DTDL
            if (expandedDtdlMode == true)
            {

                // TODO Remove hard coded ReferenceTypeId's. Support all of them.
                // Refactor and combine with the above
                if (node.Children.Count > 0)
                {

                    foreach(var child in node.Children)
                    {

                        if (child.ReferenceTypeId == "i=46")
                        {

                            DtdlContents dtdlProperty = new DtdlContents
                            {
                                Type = "Relationship",
                                Name = "HasProperty",
                                Target = dtmiPrefix + child.DisplayName + ";1"
                            };

                            dtdlInterface.Contents.Add(dtdlProperty);

                        }

                        if (child.ReferenceTypeId == "i=47")
                        {

                            DtdlContents dtdlTelemetry = new DtdlContents
                            {
                                Type = "Relationship",
                                Name = "HasComponent",
                                Target = dtmiPrefix + child.DisplayName + ";1"
                            };

                            dtdlInterface.Contents.Add(dtdlTelemetry);

                        }
                    }
                }

                if(node.Children.Count == 0)
                {

                    // i=68 (PropertyType) maps to DTDL property
                    if (node.TypeDefinition == "i=68")
                    {

                        DtdlContents dtdlProperty = new DtdlContents
                        {
                            Type = "Property",
                            Name = node.DisplayName,
                            Schema = GetDtdlDataType(node.DataType)
                        };

                        dtdlInterface.Contents.Add(dtdlProperty);

                    }

                    // i=63 (BaseDataVariableType) maps to DTDL telemetry
                    if (node.TypeDefinition == "i=63")
                    {

                        DtdlContents dtdlTelemetry = new DtdlContents
                        {
                            Type = "Telemetry",
                            Name = node.DisplayName,
                            Schema = GetDtdlDataType(node.DataType)
                        };

                        dtdlInterface.Contents.Add(dtdlTelemetry);

                    }

                    // NodeClass == Method maps to DTDL command
                    if (node.NodeClass == "Method")
                    {

                        DtdlContents dtdlCommand = new DtdlContents
                        {
                            Type = "Command",
                            Name = node.DisplayName
                        };

                        dtdlInterface.Contents.Add(dtdlCommand);

                    }
                }
            }

            _map.Clear();

            return dtdlInterface;

        }

        /// <summary>
        /// Generates a sample DTDL interface
        /// </summary>
        /// <param name="dtmiPrefix">DTMI prefix</param>
        /// <returns>DtdlInterface</returns>
        public static DtdlInterface GenerateSampleDTDL(string dtmiPrefix)
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
            _map.Add("Integer", "integer");
            _map.Add("Int16", "integer");
            _map.Add("UInt16", "integer");
            _map.Add("Int32", "integer");
            _map.Add("UInt32", "integer");
            _map.Add("Int64", "long");
            _map.Add("UInt64", "long");
            _map.Add("String", "string");
            //_map.Add("", "time");

        }
        
        /// <summary>
        /// Gets a DTDL primitive data type from a OPC UA data type
        /// </summary>
        /// <param name="id">OPC UA data type e.g. Int64</param>
        /// <returns>DTDL data type</returns>
        public static string GetDtdlDataType(string id)
        {

            try
            {

                return _map[id];

            }
            catch
            {

                return $"No primitive DTDL datatype for {id}.";

            }

        }
    }
}
