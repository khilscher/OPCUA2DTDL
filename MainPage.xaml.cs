using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using Windows.UI.Core;
using muxc = Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using OPCUA2DTDL.Models;
using Newtonsoft.Json;
using Microsoft.Azure.DigitalTwins.Parser;
using Newtonsoft.Json.Linq;
using Azure.DigitalTwins.Core;
using Windows.UI.Composition;

// https://docs.microsoft.com/en-us/windows/uwp/design/layout/

namespace OPCUA2DTDL
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const string _appName = "OPCUA2DTDL";
        private OpcUaClient _client;
        private OpcUaNodeList _dataSource = new OpcUaNodeList();
        private static List<DtdlInterface> _interfaceList = new List<DtdlInterface>();      // List of DTDL Interfaces
        private static string _endpointURL;
        private OpcUaNodeList _children = new OpcUaNodeList();
        private bool _isExpandedDtdlMode = false;
        private bool _autoAccept = true;

        public MainPage()
        {

            this.InitializeComponent();

        }

        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            btnConnect.IsEnabled = false;
            ProgressRing.IsActive = true;
            this.OpcUaNodeTree.RootNodes.Clear();
            _endpointURL = txtBoxOpcServer.Text;
            _dataSource.Clear();
            _children.Clear();

            try
            {
                await Task.Run(() => ConnectAndBrowse());

                // Hack to get the treeview to refresh. 
                // ObservableCollection with built-in INotifyCollectionChanged doesn't appear to work.
                this.OpcUaNodeTree.ItemsSource = _dataSource;

                NotifyUser("Finished browsing nodes");
            }
            catch (Exception ex)
            {
                NotifyUser(ex.Message);
            }

            btnConnect.IsEnabled = true;
            ProgressRing.IsActive = false;

        }

        private async Task ConnectAndBrowse()
        {

            int stopTimeout = Timeout.Infinite;

            _client = new OpcUaClient(_endpointURL, _autoAccept, stopTimeout, _appName);

            NotifyUser("Connecting...");

            Session opcsession = await _client.Connect();

            if (opcsession.Connected)
            {

                NotifyUser("Connected");

                NotifyUser("Browsing nodes...");

                // https://docs.microsoft.com/en-us/windows/winui/api/microsoft.ui.xaml.controls.treeview?view=winui-2.5
                _dataSource = await _client.Browse();

            }

        }

        private void OpcUaNodeTree_ItemInvoked(muxc.TreeView sender, muxc.TreeViewItemInvokedEventArgs args)
        {

            OpcUaNode selectedNode = (OpcUaNode)args.InvokedItem;

            //List<NodeReferenceData> nodeRefs = client.BrowseReferences(selectedNode.NodeId);

            // Display the node's properties in the console window when user clicks on the node
            NotifyUser("**********************************************************************************************************************");
            NotifyUser($"BrowseName: {selectedNode.BrowseName}");
            NotifyUser($"DisplayName: {selectedNode.DisplayName}");
            NotifyUser($"NodeId: {selectedNode.NodeId}");
            NotifyUser($"NodeClass: {selectedNode.NodeClass}");
            NotifyUser($"DataType: {selectedNode.DataType}");
            NotifyUser($"Child Count: {selectedNode.Children.Count}");
            NotifyUser($"ReferenceTypeId: {selectedNode.ReferenceTypeId} {OpcUaClient.GetNameFromNodeId(selectedNode.ReferenceTypeId)}");
            NotifyUser($"TypeDefinition: {selectedNode.TypeDefinition} {OpcUaClient.GetNameFromNodeId(selectedNode.TypeDefinition)}");
            NotifyUser("**********************************************************************************************************************");

            // If text box contains json, load json from text box into list
            if (txtBoxDTDL.Text.Length != 0)
            {
                _interfaceList.AddRange(JsonConvert.DeserializeObject<List<DtdlInterface>>(txtBoxDTDL.Text));
            }

            // Generate the DTDL for selected node
            DtdlInterface dtdl = DTDL.GenerateDTDL(selectedNode, _isExpandedDtdlMode);

            // Add DTDL to the list of Interfaces
            _interfaceList.Add(dtdl);

            // Display list in text box
            txtBoxDTDL.Text = JsonConvert.SerializeObject(_interfaceList, Formatting.Indented);

            // Clear list
            _interfaceList.Clear();

        }

        /*
        private void GetChildNodes(OpcUaNode currentNode, OpcUaNode targetNode)
        {

            if (currentNode.NodeId == targetNode.NodeId)
            {

                int numOfChildren = currentNode.Children.Count;

                for (int i=0; i < numOfChildren; i++)
                {

                    if(IsVariableOrMethod(currentNode.Children[i]))
                    {

                        children.Add(currentNode.Children[i]);

                    }

                }

            }
            else
            {

                int numOfChildren = currentNode.Children.Count;

                for (int i = 0; i < numOfChildren; i++)
                {

                    GetChildNodes(currentNode.Children[i], targetNode);

                }

            }

        }
        */

        /*
        private bool IsVariableOrMethod(OpcUaNode node)
        {
            if(node.NodeClass == "Variable" || node.NodeClass == "Method")
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        */

        // Display message in console textbox
        public void NotifyUser(string strMessage)
        {
            // If called from the UI thread, then update immediately.
            // Otherwise, schedule a task on the UI thread to perform the update.
            if (Dispatcher.HasThreadAccess)
            {

                UpdateStatus(strMessage);

            }
            else
            {

                var task = Dispatcher.RunAsync(CoreDispatcherPriority.Low, () => UpdateStatus(strMessage));

            }
        }

        private void UpdateStatus(string strMessage)
        {

            txtBoxConsole.Text += strMessage + "\n";

        }

        // Hack to allow status textbox to autoscroll to bottom
        private void txtBoxStatus_TextChanged(object sender, TextChangedEventArgs e)
        {

            var grid = (Grid)VisualTreeHelper.GetChild(txtBoxConsole, 0);

            for (var i = 0; i <= VisualTreeHelper.GetChildrenCount(grid) - 1; i++)
            {

                object obj = VisualTreeHelper.GetChild(grid, i);

                if (!(obj is ScrollViewer)) continue;

                ((ScrollViewer)obj).ChangeView(0.0f, ((ScrollViewer)obj).ExtentHeight, 1.0f);

                break;

            }

        }

        private void btnValidate_Click(object sender, RoutedEventArgs e)
        {

            NotifyUser("Validating DTDL...");

            try
            {

                string json = txtBoxDTDL.Text.ToString();

                ModelParser modelParser = new ModelParser();

                List<string> model = new List<string>();

                model.Add(json);
                
                IReadOnlyDictionary<Dtmi, DTEntityInfo> parseTask = modelParser.ParseAsync(model).GetAwaiter().GetResult();

                NotifyUser($"Validation passed");

            }
            catch (ParsingException pe)
            {

                NotifyUser($"Validation error(s)");

                int errCount = 1;

                foreach (ParsingError err in pe.Errors)
                {

                    NotifyUser($"Error {errCount}: {err.Message}");
                    NotifyUser($"Primary ID: {err.PrimaryID}");
                    NotifyUser($"Secondary ID: {err.SecondaryID}");
                    NotifyUser($"Property: {err.Property}");
                    errCount++;

                }

            }
            catch (Exception ex)
            {

                NotifyUser($"{ex.Message}");

            }

        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {

            txtBoxDTDL.Text = "";
            _interfaceList.Clear();

        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {

            // If text box contains json, load json from text box into list
            if (txtBoxDTDL.Text.Length != 0)
            {

                _interfaceList.AddRange(JsonConvert.DeserializeObject<List<DtdlInterface>>(txtBoxDTDL.Text));

            }

            // Generate DTDL for selected tree node and add to list
            var dtdl = DTDL.GenerateSampleDTDL();
            _interfaceList.Add(dtdl);

            // Display list in text box
            txtBoxDTDL.Text = JsonConvert.SerializeObject(_interfaceList, Formatting.Indented);

            // Clear list
            _interfaceList.Clear();

        }

        private void btnCollapsedExpandedToggle_Click(object sender, RoutedEventArgs e)
        {
            if (btnCollapsedExpandedToggle.IsChecked == true)
            {

                btnCollapsedExpandedToggle.Label = "Expanded DTDL";
                _isExpandedDtdlMode = true;

            }
            else
            {

                btnCollapsedExpandedToggle.Label = "Collapsed DTDL";
                _isExpandedDtdlMode = false;

            }

        }
    }
}
