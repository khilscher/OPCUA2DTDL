using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace OPCUA2DTDL.Models
{
    public class OpcUaNodeList : ObservableCollection<OpcUaNode> 
    {
        public OpcUaNodeList()
        { 
            // Constructor
        }

    }

    public class OpcUaNode
    {
        public string DisplayName { get; set; }
        public string BrowseName { get; set; }
        public string NodeClass { get; set; }
        public string NodeId { get; set; }
        public string DataType { get; set; }

        public ObservableCollection<OpcUaNode> Children { get; set; } = new ObservableCollection<OpcUaNode>();

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
