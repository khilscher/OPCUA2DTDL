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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using System.Collections.Specialized;
using Microsoft.IdentityModel.Clients.ActiveDirectory;  // ADAL
using System.Text;
using Newtonsoft.Json.Linq;
using System.Linq;
using Windows.UI.ViewManagement;
using Windows.Foundation;


// https://docs.microsoft.com/en-us/windows/uwp/design/layout/

namespace OPCUA2DTDL
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        // Do not edit
        private static OpcUaClient _opcUaClient;
        private OpcUaNodeList _dataSource = new OpcUaNodeList();
        private static List<DtdlInterface> _interfaceList = new List<DtdlInterface>();
        private static string _opcUaEndpointURL;
        private OpcUaNodeList _children = new OpcUaNodeList();
        private bool _isExpandedDtdlMode = false;
        private bool _autoAccept = true;
        private OpcUaNode _selectedNode;
        private string _authority = "https://login.microsoftonline.com";
        private string _resource = "0b07f429-9f4b-4714-9392-cc5e8e80c8b0"; // ADT resource id. Do not change.
        private static HttpClient _httpClient = new HttpClient();

        // Edit these as needed
        private const string _appName = "OPCUA2DTDL";
        private static string _dtmiPrefix = "dtmi:com:example:";

        // Fill in your ADT instance URL  
        private string _adtInstanceUrl = "https://<yourinstance>.digitaltwins.azure.net";

        // Fill in with your AAD app registration.
        //   See https://docs.microsoft.com/en-us/azure/digital-twins/how-to-create-app-registration
        // Ensure your app has "Azure Digital Twins Data Owner" permissions to your ADT instance
        private string _tenantId = "";
        private string _clientId = "";
        private string _secret = "";


        public MainPage()
        {

            this.InitializeComponent();

            // Set preferred window size
            ApplicationView.PreferredLaunchViewSize = new Size(1400, 800);
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.PreferredLaunchViewSize;

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

            _opcUaClient = new OpcUaClient(_opcUaEndpointURL, _autoAccept, stopTimeout, _appName);

            NotifyUser("Connecting...");

            Session opcsession = await _opcUaClient.Connect();

            if (opcsession.Connected)
            {

                NotifyUser("Connected");

                NotifyUser("Browsing nodes...");

                // https://docs.microsoft.com/en-us/windows/winui/api/microsoft.ui.xaml.controls.treeview?view=winui-2.5
                _dataSource = await _opcUaClient.Browse();

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

            if (node.NodeClass == "Variable" || node.NodeClass == "Method")
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

                if (IsVariableOrMethod(_selectedNode) && _isExpandedDtdlMode == false)
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

        private async void btnUploadToAdt_Click(object sender, RoutedEventArgs e)
        {

            try
            {

                // ADAL - TODO Replace with MSAL PublicClientApplication
                var credentials = new ClientCredential(_clientId, _secret);
                var authContext = new AuthenticationContext($"{_authority}/{_tenantId}");
                var result = await authContext.AcquireTokenAsync(_resource, credentials);

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);

                string content = txtBoxDTDL.Text.ToString();
                var buffer = Encoding.UTF8.GetBytes(content);
                var byteContent = new ByteArrayContent(buffer);
                byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                var response = await _httpClient.PostAsync($"{_adtInstanceUrl}/models?api-version=2020-10-31", byteContent);

                if (response.IsSuccessStatusCode)
                {
                    NotifyUser($"Model upload successful.");
                }
                else
                {
                    NotifyUser($"Model upload error: {response.StatusCode}");
                }
            }
            catch(Exception ex)
            {

                NotifyUser($"{ex.Message}");

            }

        }

        private async void btnDownloadFromAdt_Click(object sender, RoutedEventArgs e)
        {

            try
            {
                // ADAL - TODO Replace with MSAL PublicClientApplication
                var credentials = new ClientCredential(_clientId, _secret);
                var authContext = new AuthenticationContext($"{_authority}/{_tenantId}");
                var result = await authContext.AcquireTokenAsync(_resource, credentials);

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);

                string json = await _httpClient.GetStringAsync($"{_adtInstanceUrl}/models?includeModelDefinition=true&api-version=2020-10-31");

                if(!String.IsNullOrEmpty(json))
                {

                    JObject jObject = JObject.Parse(json);

                    // Get JSON result objects into a list
                    IList<JToken> results = jObject["value"].Children().ToList();

                    IList<DtdlInterface> resultsList = new List<DtdlInterface>();

                    foreach (JToken r in results)
                    {
                        JObject inner = r["model"].Value<JObject>();
                        _interfaceList.Add(inner.ToObject<DtdlInterface>());
                    }

                    txtBoxDTDL.Text = JsonConvert.SerializeObject(_interfaceList, Formatting.Indented);

                    // Clear list
                    _interfaceList.Clear();

                    NotifyUser($"Model download complete.");

                }

            }
            catch (Exception ex)
            {

                NotifyUser($"{ex.Message}");

            }

        }

    }

}
