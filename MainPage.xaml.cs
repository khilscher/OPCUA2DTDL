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

// https://docs.microsoft.com/en-us/windows/uwp/design/layout/

namespace OPCUA2DTDL
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private OpcUaClient client;
        private OpcUaNodeList DataSource = new OpcUaNodeList();
        private static List<DtdlInterface> InterfaceList = new List<DtdlInterface>();      // List of DTDL Interfaces
        private static string endpointURL;
        private OpcUaNodeList variables = new OpcUaNodeList();

        public MainPage()
        {

            this.InitializeComponent();

        }

        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            btnConnect.IsEnabled = false;
            ProgressRing.IsActive = true;
            this.OpcUaNodeTree.RootNodes.Clear();
            endpointURL = txtBoxOpcServer.Text;

            try
            {
                await Task.Run(() => ConnectAndBrowse());

                // Hack to get the treeview to refresh. 
                // ObservableCollection with built-in INotifyCollectionChanged doesn't appear to work.
                this.OpcUaNodeTree.ItemsSource = DataSource;

                NotifyUser("Finished browsing nodes");
            }
            catch (Exception ex)
            {
                NotifyUser(ex.Message);
            }



            /*
            int stopTimeout = Timeout.Infinite;
            bool autoAccept = true;
            string endpointURL = txtBoxOpcServer.Text;
            this.OpcUaNodeTree.RootNodes.Clear();

            OpcUaClient client = new OpcUaClient(endpointURL, autoAccept, stopTimeout);

            try
            {


                NotifyUser("Connecting...");

                Session opcsession = await client.Connect();

                if (opcsession.Connected)
                {
                    NotifyUser("Connected");

                    // https://docs.microsoft.com/en-us/windows/winui/api/microsoft.ui.xaml.controls.treeview?view=winui-2.5
                    DataSource = await client.Browse();

                    // Hack to get the treeview to refresh. 
                    // ObservableCollection with built-in INotifyCollectionChanged doesn't appear to work.
                    this.OpcUaNodeTree.ItemsSource = DataSource;

                }

  
            }
            catch (Exception ex)
            {
                NotifyUser(ex.Message);
            }
            */

            btnConnect.IsEnabled = true;
            ProgressRing.IsActive = false;

        }

        private async Task ConnectAndBrowse()
        {

            int stopTimeout = Timeout.Infinite;
            bool autoAccept = true;

            //OpcUaClient client = new OpcUaClient(endpointURL, autoAccept, stopTimeout);
            client = new OpcUaClient(endpointURL, autoAccept, stopTimeout);

            NotifyUser("Connecting...");

            Session opcsession = await client.Connect();

            if (opcsession.Connected)
            {
                NotifyUser("Connected");

                NotifyUser("Browsing nodes...");

                // https://docs.microsoft.com/en-us/windows/winui/api/microsoft.ui.xaml.controls.treeview?view=winui-2.5
                DataSource = await client.Browse();

            }


        }


        private void OpcUaNodeTree_ItemInvoked(muxc.TreeView sender, muxc.TreeViewItemInvokedEventArgs args)
        {

            OpcUaNode selectedNode = (OpcUaNode)args.InvokedItem;

            // Variables must be converted to property and telemetry on the parent
            if (selectedNode.NodeClass != "Variable")
            {

                // If text box contains json, load json from text box into list
                if (txtBoxDTDL.Text.Length != 0)
                {
                    InterfaceList.AddRange(JsonConvert.DeserializeObject<List<DtdlInterface>>(txtBoxDTDL.Text));
                }


                variables.Clear();

                foreach (var opcNode in DataSource)
                {
                    // Check if Object has Variables underneath it.
                    GetVariablesForObjectNode(opcNode, selectedNode);
                }

                // If node has variables underneath it, they will be converted to Telemetry or Property types.
                DtdlInterface dtdl = DTDL.GenerateDTDL(selectedNode, variables);

                InterfaceList.Add(dtdl);

                // Display list in text box
                txtBoxDTDL.Text = JsonConvert.SerializeObject(InterfaceList, Formatting.Indented);

                // Clear list
                InterfaceList.Clear();

            }
            else
            {

                NotifyUser($"{selectedNode.DisplayName} is a {selectedNode.NodeClass}. Select this node's parent.");

            }

        }

        private void GetVariablesForObjectNode(OpcUaNode currentNode, OpcUaNode targetNode)
        {

            if (currentNode.NodeId == targetNode.NodeId)
            {

                int numOfChildren = currentNode.Children.Count;

                for (int i=0; i < numOfChildren; i++)
                {

                    if(IsVariable(currentNode.Children[i]))
                    {

                        variables.Add(currentNode.Children[i]);

                    }

                }

            }
            else
            {

                int numOfChildren = currentNode.Children.Count;

                for (int i = 0; i < numOfChildren; i++)
                {

                    GetVariablesForObjectNode(currentNode.Children[i], targetNode);

                }

            }

        }

        private bool IsVariable(OpcUaNode node)
        {
            if(node.NodeClass == "Variable")
            {
                return true;
            }
            else
            {
                return false;
            }
        }

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

                // Serialize to string
                //string json = JsonConvert.SerializeObject(InterfaceList);

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
            InterfaceList.Clear();
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {

            // If text box contains json, load json from text box into list
            if (txtBoxDTDL.Text.Length != 0)
            {
                InterfaceList.AddRange(JsonConvert.DeserializeObject<List<DtdlInterface>>(txtBoxDTDL.Text));
            }

            // Generate DTDL for selected tree node and add to list
            var dtdl = DTDL.GenerateDTDL();
            InterfaceList.Add(dtdl);

            // Display list in text box
            txtBoxDTDL.Text = JsonConvert.SerializeObject(InterfaceList, Formatting.Indented);

            // Clear list
            InterfaceList.Clear();

        }
    }
}
