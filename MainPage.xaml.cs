using System;
using System.Collections.Generic;
using System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Opc.Ua.Client;
using Windows.UI.Core;
using muxc = Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;
using OPCUA2DTDL.Models;
using Newtonsoft.Json;
using Microsoft.Azure.DigitalTwins.Parser;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Azure;
using System.Net.Http;


// https://docs.microsoft.com/en-us/windows/uwp/design/layout/

namespace OPCUA2DTDL
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const string _appName = "OPCUA2DTDL";
        private static string _dtmiPrefix = "dtmi:com:example:";
        private OpcUaClient _client;
        private OpcUaNodeList _dataSource = new OpcUaNodeList();
        private static List<DtdlInterface> _interfaceList = new List<DtdlInterface>();
        private static string _opcUaEndpointURL;
        private OpcUaNodeList _children = new OpcUaNodeList();
        private bool _isExpandedDtdlMode = false;
        private bool _autoAccept = true;
        private OpcUaNode _selectedNode;

        public MainPage()
        {

            this.InitializeComponent();

        }

        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            btnConnect.IsEnabled = false;
            ProgressRing.IsActive = true;
            this.OpcUaNodeTree.RootNodes.Clear();
            _opcUaEndpointURL = txtBoxOpcServer.Text;
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

            _client = new OpcUaClient(_opcUaEndpointURL, _autoAccept, stopTimeout, _appName);

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

            _selectedNode = (OpcUaNode)args.InvokedItem;

            // Display the node's properties in the console window when user clicks on the node
            NotifyUser("**********************************************************************************************************************");
            NotifyUser($"BrowseName: {_selectedNode.BrowseName}");
            NotifyUser($"DisplayName: {_selectedNode.DisplayName}");
            NotifyUser($"NodeId: {_selectedNode.NodeId}");
            NotifyUser($"NodeClass: {_selectedNode.NodeClass}");
            NotifyUser($"DataType: {_selectedNode.DataType}");
            NotifyUser($"Child Count: {_selectedNode.Children.Count}");
            NotifyUser($"ReferenceTypeId: {_selectedNode.ReferenceTypeId} Name: {OpcUaClient.GetTypeDefinition(_selectedNode.ReferenceTypeId)}");
            NotifyUser($"TypeDefinitionId: {_selectedNode.TypeDefinition} Name: {OpcUaClient.GetTypeDefinition(_selectedNode.TypeDefinition)}");
            NotifyUser("**********************************************************************************************************************");

        }
       
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

        private void btnAddSampleInterface_Click(object sender, RoutedEventArgs e)
        {

            // If text box contains json, load json from text box into list
            if (txtBoxDTDL.Text.Length != 0)
            {

                _interfaceList.AddRange(JsonConvert.DeserializeObject<List<DtdlInterface>>(txtBoxDTDL.Text));

            }

            // Generate DTDL for selected tree node and add to list
            var dtdl = DTDL.GenerateSampleDTDL(_dtmiPrefix);
            _interfaceList.Add(dtdl);

            // Display list in text box
            txtBoxDTDL.Text = JsonConvert.SerializeObject(_interfaceList, Formatting.Indented);

            // Clear list
            _interfaceList.Clear();

        }

        private void btnConvertNodeToDtdl_Click(object sender, RoutedEventArgs e)
        {

            if (_selectedNode != null)
            {

                if(IsVariableOrMethod(_selectedNode) && _isExpandedDtdlMode == false)
                {

                    NotifyUser("Select this node's parent or enable expanded mode to convert individual Properties, Variables, or Methods to DTDL Interfaces.");

                    return;

                }

                // If text box contains json, load json from text box into list
                if (txtBoxDTDL.Text.Length != 0)
                {

                    _interfaceList.AddRange(JsonConvert.DeserializeObject<List<DtdlInterface>>(txtBoxDTDL.Text));

                }

                // Generate the DTDL for selected node
                DtdlInterface dtdl = DTDL.GenerateDTDL(_selectedNode, _isExpandedDtdlMode, _dtmiPrefix);

                // Add DTDL to the list of Interfaces
                _interfaceList.Add(dtdl);

                // Display list in text box
                txtBoxDTDL.Text = JsonConvert.SerializeObject(_interfaceList, Formatting.Indented);

                // Clear list
                _interfaceList.Clear();

            }

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

        private void btnUploadToAdt_Click(object sender, RoutedEventArgs e)
        {

            NotifyUser("Not yet implemented");

        }

    }

}
