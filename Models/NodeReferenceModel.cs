using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Opc.Ua;

namespace OPCUA2DTDL.Models
{
    public class NodeReferenceData
    {
        public ReferenceTypeNode ReferenceType { get; set; }
        public bool IsInverse { get; set; }
        public Node Target { get; set; }
        public Node TypeDefinition { get; set; }

    }
}
