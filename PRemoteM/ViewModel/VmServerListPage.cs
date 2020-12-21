﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using Windows.ApplicationModel.Store;
using Microsoft.Win32;
using Newtonsoft.Json;
using PRM.Core;
using PRM.Core.DB;
using PRM.Core.Model;
using PRM.Core.Protocol;
using PRM.Core.Protocol.Putty.SSH;
using PRM.Core.Protocol.Putty.Telnet;
using PRM.Core.Protocol.RDP;
using PRM.Core.Protocol.VNC;
using Shawn.Utils;
using MessageBox = System.Windows.MessageBox;

namespace PRM.ViewModel
{
    public class VmServerListPage : NotifyPropertyChangedBase
    {
        public VmServerListPage()
        {
            var lastSelectedGroup = "";
            if (!string.IsNullOrEmpty(SystemConfig.Instance.Locality.MainWindowTabSelected))
            {
                lastSelectedGroup = SystemConfig.Instance.Locality.MainWindowTabSelected;
            }

            RebuildVmServerCardList();
            GlobalData.Instance.VmItemListDataChanged += RebuildVmServerCardList;

            SystemConfig.Instance.General.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(SystemConfig.General.ServerOrderBy))
                    OrderServerList();
            };

            if (!string.IsNullOrEmpty(lastSelectedGroup) && ServerGroupList.Contains(lastSelectedGroup))
            {
                SelectedGroup = lastSelectedGroup;
            }

            GlobalData.Instance.OnMainWindowServerFilterChanged += new Action<string>(s =>
            {
                CalcVisible();
            });
        }

        private VmServerListItem _selectedServerListItem = null;
        public VmServerListItem SelectedServerListItem
        {
            get => _selectedServerListItem;
            set => SetAndNotifyIfChanged(nameof(SelectedServerListItem), ref _selectedServerListItem, value);
        }

        private ObservableCollection<VmServerListItem> _serverListItems = new ObservableCollection<VmServerListItem>();
        /// <summary>
        /// AllServerList data source for list view
        /// </summary>
        public ObservableCollection<VmServerListItem> ServerListItems
        {
            get => _serverListItems;
            set
            {
                SetAndNotifyIfChanged(nameof(ServerListItems), ref _serverListItems, value);
                OrderServerList();
                ServerListItems.CollectionChanged += (sender, args) => { OrderServerList(); };
            }
        }


        private ObservableCollection<string> _serverGroupList = new ObservableCollection<string>();
        public ObservableCollection<string> ServerGroupList
        {
            get => _serverGroupList;
            set => SetAndNotifyIfChanged(nameof(ServerGroupList), ref _serverGroupList, value);
        }


        private string _moveToGroup = "";
        public string MoveToGroup
        {
            get => _moveToGroup;
            set => SetAndNotifyIfChanged(nameof(MoveToGroup), ref _moveToGroup, value);
        }

        private List<string> _groupSelections;
        public List<string> GroupSelections
        {
            get => _groupSelections;
            set => SetAndNotifyIfChanged(nameof(GroupSelections), ref _groupSelections, value);
        }

        private string _selectedGroup = "";
        public string SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                if (_selectedGroup != value)
                {
                    GlobalData.Instance.MainWindowServerFilter = "";
                    SetAndNotifyIfChanged(nameof(SelectedGroup), ref _selectedGroup, value);
                    SystemConfig.Instance.Locality.MainWindowTabSelected = value;
                    SystemConfig.Instance.Locality.Save();
                    CalcVisible();
                }
            }
        }

        private bool _isSelectedAll;
        public bool IsSelectedAll
        {
            get => _isSelectedAll;
            set
            {
                SetAndNotifyIfChanged(nameof(IsSelectedAll), ref _isSelectedAll, value);
                foreach (var vmServerCard in ServerListItems)
                {
                    if (vmServerCard.Visible == Visibility.Visible)
                        vmServerCard.IsSelected = value;
                }
            }
        }


        private void RebuildVmServerCardList()
        {
            _serverListItems.Clear();
            foreach (var vs in GlobalData.Instance.VmItemList)
            {
                ServerListItems.Add(new VmServerListItem(vs.Server));
            }
            OrderServerList();
            RebuildGroupList();
            GroupSelections = GlobalData.Instance.VmItemList.Select(x => x.Server.GroupName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
        }

        private void RebuildGroupList()
        {
            var selectedGroup = _selectedGroup;

            ServerGroupList.Clear();
            foreach (var serverAbstract in ServerListItems.Select(x => x.Server))
            {
                if (!string.IsNullOrEmpty(serverAbstract.GroupName) &&
                    !ServerGroupList.Contains(serverAbstract.GroupName))
                {
                    ServerGroupList.Add(serverAbstract.GroupName);
                }
            }
            if (ServerGroupList.Contains(selectedGroup))
                SelectedGroup = selectedGroup;
            else
                SelectedGroup = "";
        }

        private void OrderServerList()
        {
            if (ServerListItems?.Count > 0)
            {
                var items = new ObservableCollection<VmServerListItem>();
                switch (ItemsOrderByType)
                {
                    case -1:
                    default:
                        switch (SystemConfig.Instance.General.ServerOrderBy)
                        {
                            case EnumServerOrderBy.Name:
                                items = new ObservableCollection<VmServerListItem>(ServerListItems.OrderBy(s => s.Server.DispName).ThenBy(s => s.Server.Id));
                                break;
                            case EnumServerOrderBy.AddTimeAsc:
                                items = new ObservableCollection<VmServerListItem>(ServerListItems.OrderBy(s => s.Server.Id));
                                break;
                            case EnumServerOrderBy.AddTimeDesc:
                                items = new ObservableCollection<VmServerListItem>(ServerListItems.OrderByDescending(s => s.Server.Id));
                                break;
                            case EnumServerOrderBy.Protocol:
                                items = new ObservableCollection<VmServerListItem>(ServerListItems.OrderByDescending(s => s.Server.Protocol).ThenBy(s => s.Server.DispName));
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                        break;
                    case 0:
                        items = new ObservableCollection<VmServerListItem>(ServerListItems.OrderBy(x => x.Server.Protocol).ThenBy(x => x.Server.Id));
                        break;
                    case 1:
                        items = new ObservableCollection<VmServerListItem>(ServerListItems.OrderByDescending(x => x.Server.Protocol).ThenBy(x => x.Server.Id));
                        break;
                    case 2:
                        items = new ObservableCollection<VmServerListItem>(ServerListItems.OrderBy(x => x.Server.DispName).ThenBy(x => x.Server.Id));
                        break;
                    case 3:
                        items = new ObservableCollection<VmServerListItem>(ServerListItems.OrderByDescending(x => x.Server.DispName).ThenBy(x => x.Server.Id));
                        break;
                    case 4:
                        items = new ObservableCollection<VmServerListItem>(ServerListItems.OrderBy(x => x.Server.GroupName).ThenBy(x => x.Server.Id));
                        break;
                    case 5:
                        items = new ObservableCollection<VmServerListItem>(ServerListItems.OrderByDescending(x => x.Server.GroupName).ThenBy(x => x.Server.Id));
                        break;
                    case 6:
                        items = new ObservableCollection<VmServerListItem>(ServerListItems.OrderBy(x => (x.Server is ProtocolServerWithAddrPortBase p) ? p.Address : "").ThenBy(x => x.Server.Id));
                        break;
                    case 7:
                        items = new ObservableCollection<VmServerListItem>(ServerListItems.OrderByDescending(x => (x.Server is ProtocolServerWithAddrPortBase p) ? p.Address : "").ThenBy(x => x.Server.Id));
                        break;
                }


                // add a 'ProtocolServerNone' so that 'add server' button will be shown
                var addServerCard = new VmServerListItem(new ProtocolServerNone());
                addServerCard.Server.GroupName = SelectedGroup;
                //addServerCard.OnAction += VmServerCardOnAction;
                items.Add(addServerCard);
                _serverListItems = items;

                CalcVisible();

                base.RaisePropertyChanged(nameof(ServerListItems));
            }
        }

        private void CalcVisible()
        {
            foreach (var card in ServerListItems)
            {
                var server = card.Server;
                string keyWord = GlobalData.Instance.MainWindowServerFilter;
                string selectedGroup = SelectedGroup;

                //if (server is ProtocolServerNone)
                //{
                //    if (ServerListPageUI == 0)
                //        card.Visible = Visibility.Visible;
                //    else
                //        card.Visible = Visibility.Collapsed;
                //    continue;
                //}

                if (server.Id <= 0)
                {
                    card.Visible = Visibility.Collapsed;
                    continue;
                }

                bool bGroupMatched = string.IsNullOrEmpty(selectedGroup) || server.GroupName == selectedGroup || server.GetType() == typeof(ProtocolServerNone);
                if (!bGroupMatched)
                {
                    card.Visible = Visibility.Collapsed;
                    continue;
                }

                if (string.IsNullOrEmpty(keyWord))
                {
                    card.Visible = Visibility.Visible;
                    continue;
                }

                var keyWords = keyWord.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                var keyWordIsMatch = new List<bool>(keyWords.Length);
                for (var i = 0; i < keyWords.Length; i++)
                    keyWordIsMatch.Add(false);

                var dispName = server.DispName;
                var subTitle = server.SubTitle;
                for (var i = 0; i < keyWordIsMatch.Count; i++)
                {
                    var f1 = dispName.IsMatchPinyinKeywords(keyWords[i], out var m1);
                    var f2 = subTitle.IsMatchPinyinKeywords(keyWords[i], out var m2);
                    keyWordIsMatch[i] = f1 || f2;
                }

                if (keyWordIsMatch.All(x => x == true))
                    card.Visible = Visibility.Visible;
                else
                    card.Visible = Visibility.Collapsed;
            }

            if (ServerListItems.Where(x => x.Visible == Visibility.Visible).All(x => x.IsSelected))
                _isSelectedAll = true;
            else
                _isSelectedAll = false;
            RaisePropertyChanged(nameof(IsSelectedAll));
        }



        private RelayCommand _cmdAdd;
        public RelayCommand CmdAdd
        {
            get
            {
                return _cmdAdd ??= new RelayCommand((o) =>
                {
                    GlobalEventHelper.OnGoToServerAddPage?.Invoke(SelectedGroup);
                });
            }
        }

        private RelayCommand _cmdExportSelectedToJson;
        public RelayCommand CmdExportSelectedToJson
        {
            get
            {
                if (_cmdExportSelectedToJson == null)
                {
                    _cmdExportSelectedToJson = new RelayCommand((isExportAll) =>
                    {
                        var res = SystemConfig.Instance.DataSecurity.CheckIfDbIsOk();
                        if (!res.Item1)
                        {
                            MessageBox.Show(res.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                            return;
                        }
                        var dlg = new SaveFileDialog
                        {
                            Filter = "PRM json array|*.prma",
                            Title = SystemConfig.Instance.Language.GetText("system_options_data_security_export_dialog_title"),
                            FileName = DateTime.Now.ToString("yyyyMMddhhmmss") + ".prma"
                        };
                        if (dlg.ShowDialog() == true)
                        {
                            var list = new List<ProtocolServerBase>();
                            if (isExportAll != null || ServerListItems.All(x => x.IsSelected == false))
                                foreach (var vs in GlobalData.Instance.VmItemList)
                                {
                                    var serverBase = (ProtocolServerBase)vs.Server.Clone();
                                    SystemConfig.Instance.DataSecurity.DecryptPwd(serverBase);
                                    list.Add(serverBase);
                                }
                            else
                                foreach (var vs in ServerListItems.Where(x => (string.IsNullOrWhiteSpace(SelectedGroup) || x.Server.GroupName == SelectedGroup) && x.IsSelected == true))
                                {
                                    var serverBase = (ProtocolServerBase)vs.Server.Clone();
                                    SystemConfig.Instance.DataSecurity.DecryptPwd(serverBase);
                                    list.Add(serverBase);
                                }
                            File.WriteAllText(dlg.FileName, JsonConvert.SerializeObject(list, Formatting.Indented), Encoding.UTF8);
                        }
                    });
                }
                return _cmdExportSelectedToJson;
            }
        }


        private RelayCommand _cmdImportFromJson;
        public RelayCommand CmdImportFromJson
        {
            get
            {
                if (_cmdImportFromJson == null)
                {
                    _cmdImportFromJson = new RelayCommand((o) =>
                    {
                        var res = SystemConfig.Instance.DataSecurity.CheckIfDbIsOk();
                        if (!res.Item1)
                        {
                            MessageBox.Show(res.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                            return;
                        }
                        var dlg = new OpenFileDialog()
                        {
                            Filter = "PRM json array|*.prma",
                            Title = SystemConfig.Instance.Language.GetText("managementpage_import_dialog_title"),
                            FileName = DateTime.Now.ToString("yyyyMMddhhmmss") + ".prma"
                        };
                        if (dlg.ShowDialog() == true)
                        {
                            try
                            {
                                var list = new List<ProtocolServerBase>();
                                var jobj = JsonConvert.DeserializeObject<List<object>>(File.ReadAllText(dlg.FileName, Encoding.UTF8));
                                foreach (var json in jobj)
                                {
                                    var server = ItemCreateHelper.CreateFromJsonString(json.ToString());
                                    if (server != null)
                                    {
                                        server.Id = 0;
                                        list.Add(server);
                                        SystemConfig.Instance.DataSecurity.EncryptPwd(server);
                                        Server.AddOrUpdate(server, true);
                                    }
                                }
                                GlobalData.Instance.ServerListUpdate();
                                MessageBox.Show(SystemConfig.Instance.Language.GetText("managementpage_import_done").Replace("{0}", list.Count.ToString()), SystemConfig.Instance.Language.GetText("messagebox_title_info"), MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None);
                            }
                            catch (Exception e)
                            {
                                SimpleLogHelper.Debug(e, e.StackTrace);
                                MessageBox.Show(SystemConfig.Instance.Language.GetText("managementpage_import_error"), SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                            }
                        }
                    });
                }
                return _cmdImportFromJson;
            }
        }



        private RelayCommand _cmdImportFromCsv;
        public RelayCommand CmdImportFromCsv
        {
            get
            {
                if (_cmdImportFromCsv == null)
                {
                    _cmdImportFromCsv = new RelayCommand((o) =>
                    {
                        var res = SystemConfig.Instance.DataSecurity.CheckIfDbIsOk();
                        if (!res.Item1)
                        {
                            MessageBox.Show(res.Item2, SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                            return;
                        }
                        var dlg = new OpenFileDialog()
                        {
                            Filter = "csv|*.csv",
                            Title = SystemConfig.Instance.Language.GetText("managementpage_import_dialog_title"),
                        };
                        if (dlg.ShowDialog() == true)
                        {
                            string getValue(List<string> keyList, List<string> valueList, string fieldName)
                            {
                                var i = keyList.IndexOf(fieldName.ToLower());
                                if (i >= 0)
                                {
                                    var val = valueList[i];
                                    return val;
                                }
                                return "";
                            }
                            try
                            {
                                var groupNames = new Dictionary<string, string>(); // id -> name
                                var list = new List<ProtocolServerBase>();
                                using (var sr = new StreamReader(new FileStream(dlg.FileName, FileMode.Open)))
                                {
                                    var groupParents = new Dictionary<string, string>(); // id -> name
                                    var firstLine = sr.ReadLine();
                                    if (string.IsNullOrWhiteSpace(firstLine))
                                        return;

                                    var title = firstLine.ToLower().Split(';').ToList();
                                    string line;
                                    while (!string.IsNullOrEmpty(line = sr.ReadLine()))
                                    {
                                        var arr = line.Split(';').ToList();
                                        if (arr.Count >= 7)
                                        {
                                            var id = getValue(title, arr, "Id");
                                            var name = getValue(title, arr, "name");
                                            var parentId = getValue(title, arr, "Parent").ToLower();
                                            var nodeType = getValue(title, arr, "NodeType").ToLower();
                                            if (string.Equals("Container", nodeType, StringComparison.CurrentCultureIgnoreCase))
                                            {
                                                groupNames.Add(id, name);
                                                groupParents.Add(id, parentId);
                                            }
                                        }
                                    }

                                    foreach (var kv in groupNames.ToArray())
                                    {
                                        var name = kv.Value;
                                        var pid = groupParents[kv.Key];
                                        while (groupNames.ContainsKey(pid))
                                        {
                                            name = $"{groupNames[pid]}-{name}";
                                            pid = groupParents[pid];
                                        }
                                        groupNames[kv.Key] = name;
                                    }
                                }

                                using (var sr = new StreamReader(new FileStream(dlg.FileName, FileMode.Open)))
                                {
                                    var firstLine = sr.ReadLine();
                                    if (string.IsNullOrWhiteSpace(firstLine))
                                        return;

                                    // title
                                    var title = firstLine.ToLower().Split(';').ToList();


                                    var r = new Random();
                                    string line;
                                    while (!string.IsNullOrEmpty(line = sr.ReadLine()))
                                    {
                                        var arr = line.Split(';').ToList();
                                        if (arr.Count >= 7)
                                        {
                                            ProtocolServerBase server = null;
                                            var name = getValue(title, arr, "name");
                                            var parentId = getValue(title, arr, "Parent").ToLower();
                                            var nodeType = getValue(title, arr, "NodeType").ToLower();
                                            if (!string.Equals("Connection", nodeType, StringComparison.CurrentCultureIgnoreCase))
                                                continue;
                                            var group = "";
                                            if (groupNames.ContainsKey(parentId))
                                                group = groupNames[parentId];
                                            var protocol = getValue(title, arr, "protocol").ToLower();
                                            var user = getValue(title, arr, "username");
                                            var pwd = getValue(title, arr, "password");
                                            var address = getValue(title, arr, "hostname");
                                            int port = 22;
                                            if (int.TryParse(getValue(title, arr, "port"), out var new_port))
                                            {
                                                port = new_port;
                                            }

                                            switch (protocol)
                                            {
                                                case "rdp":
                                                    server = new ProtocolServerRDP()
                                                    {
                                                        DispName = name,
                                                        GroupName = group,
                                                        Address = address,
                                                        UserName = user,
                                                        Password = pwd,
                                                        Port = port.ToString(),
                                                        RdpWindowResizeMode = ERdpWindowResizeMode.AutoResize, // string.Equals( getValue(title, arr, "AutomaticResize"), "TRUE", StringComparison.CurrentCultureIgnoreCase) ? ERdpWindowResizeMode.AutoResize : ERdpWindowResizeMode.Fixed,
                                                        IsConnWithFullScreen = string.Equals(getValue(title, arr, "Resolution"), "Fullscreen", StringComparison.CurrentCultureIgnoreCase),
                                                        RdpFullScreenFlag = ERdpFullScreenFlag.EnableFullScreen,
                                                        DisplayPerformance = getValue(title, arr, "Colors")?.IndexOf("32") >= 0 ? EDisplayPerformance.High : EDisplayPerformance.Auto,
                                                        IsAdministrativePurposes = string.Equals(getValue(title, arr, "ConnectToConsole"), "TRUE", StringComparison.CurrentCultureIgnoreCase),
                                                        EnableClipboard = string.Equals(getValue(title, arr, "RedirectClipboard"), "TRUE", StringComparison.CurrentCultureIgnoreCase),
                                                        EnableDiskDrives = string.Equals(getValue(title, arr, "RedirectDiskDrives"), "TRUE", StringComparison.CurrentCultureIgnoreCase),
                                                        EnableKeyCombinations = string.Equals(getValue(title, arr, "RedirectKeys"), "TRUE", StringComparison.CurrentCultureIgnoreCase),
                                                        EnableSounds = string.Equals(getValue(title, arr, "BringToThisComputer"), "TRUE", StringComparison.CurrentCultureIgnoreCase),
                                                        EnableAudioCapture = string.Equals(getValue(title, arr, "RedirectAudioCapture"), "TRUE", StringComparison.CurrentCultureIgnoreCase),
                                                        EnablePorts = string.Equals(getValue(title, arr, "RedirectPorts"), "TRUE", StringComparison.CurrentCultureIgnoreCase),
                                                        EnablePrinters = string.Equals(getValue(title, arr, "RedirectPrinters"), "TRUE", StringComparison.CurrentCultureIgnoreCase),
                                                        EnableSmartCardsAndWinHello = string.Equals(getValue(title, arr, "RedirectSmartCards"), "TRUE", StringComparison.CurrentCultureIgnoreCase),
                                                        GatewayMode = string.Equals(getValue(title, arr, "RDGatewayUsageMethod"), "Never", StringComparison.CurrentCultureIgnoreCase) ? EGatewayMode.DoNotUseGateway :
                                                                            (string.Equals(getValue(title, arr, "RDGatewayUsageMethod"), "Detect", StringComparison.CurrentCultureIgnoreCase) ? EGatewayMode.AutomaticallyDetectGatewayServerSettings : EGatewayMode.UseTheseGatewayServerSettings),
                                                        GatewayHostName = getValue(title, arr, "RDGatewayHostname"),
                                                        GatewayPassword = getValue(title, arr, "RDGatewayPassword"),
                                                    };



                                                    break;
                                                case "ssh1":
                                                    server = new ProtocolServerSSH()
                                                    {
                                                        DispName = name,
                                                        GroupName = group,
                                                        Address = address,
                                                        UserName = user,
                                                        Password = pwd,
                                                        Port = port.ToString(),
                                                        SshVersion = ProtocolServerSSH.ESshVersion.V1
                                                    };
                                                    break;
                                                case "ssh2":
                                                    server = new ProtocolServerSSH()
                                                    {
                                                        DispName = name,
                                                        GroupName = group,
                                                        Address = address,
                                                        UserName = user,
                                                        Password = pwd,
                                                        Port = port.ToString(),
                                                        SshVersion = ProtocolServerSSH.ESshVersion.V2
                                                    };
                                                    break;
                                                case "vnc":
                                                    server = new ProtocolServerVNC()
                                                    {
                                                        DispName = name,
                                                        GroupName = group,
                                                        Address = address,
                                                        Password = pwd,
                                                        Port = port.ToString(),
                                                    };
                                                    break;
                                                case "telnet":
                                                    server = new ProtocolServerTelnet()
                                                    {
                                                        DispName = name,
                                                        GroupName = group,
                                                        Address = address,
                                                        Port = port.ToString(),
                                                    };
                                                    break;
                                            }

                                            if (server != null)
                                            {
                                                server.IconImg = ServerIcons.Instance.Icons[r.Next(0, ServerIcons.Instance.Icons.Count)];
                                                list.Add(server);
                                            }
                                        }
                                    }
                                }
                                if (list?.Count > 0)
                                {
                                    foreach (var serverBase in list)
                                    {
                                        SystemConfig.Instance.DataSecurity.EncryptPwd(serverBase);
                                        Server.AddOrUpdate(serverBase, true);
                                    }
                                    GlobalData.Instance.ServerListUpdate();
                                    MessageBox.Show(SystemConfig.Instance.Language.GetText("managementpage_import_done").Replace("{0}", list.Count.ToString()), SystemConfig.Instance.Language.GetText("messagebox_title_info"), MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None);
                                }
                                else
                                    MessageBox.Show(SystemConfig.Instance.Language.GetText("managementpage_import_error"), SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                            }
                            catch (Exception e)
                            {
                                SimpleLogHelper.Debug(e, e.StackTrace);
                                MessageBox.Show(SystemConfig.Instance.Language.GetText("managementpage_import_error") + $": {e.Message}", SystemConfig.Instance.Language.GetText("messagebox_title_error"), MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None);
                            }
                        }
                    });
                }
                return _cmdImportFromCsv;
            }
        }




        private RelayCommand _cmdDeleteSelected;
        public RelayCommand CmdDeleteSelected
        {
            get
            {
                if (_cmdDeleteSelected == null)
                {
                    _cmdDeleteSelected = new RelayCommand((o) =>
                    {
                        if (MessageBoxResult.Yes == MessageBox.Show(
                            SystemConfig.Instance.Language.GetText("managementpage_delete_selected_confirm"),
                            SystemConfig.Instance.Language.GetText("messagebox_title_warning"), MessageBoxButton.YesNo,
                            MessageBoxImage.Question, MessageBoxResult.None))
                        {
                            var ss = ServerListItems.Where(x => (string.IsNullOrWhiteSpace(SelectedGroup) || x.Server.GroupName == SelectedGroup) && x.IsSelected == true).ToList();
                            if (ss?.Count > 0)
                            {
                                foreach (var vs in ss)
                                {
                                    Server.Delete(vs.Server.Id);
                                }
                                GlobalData.Instance.ServerListUpdate();
                            }
                        }
                    }, o => ServerListItems.Any(x => (string.IsNullOrWhiteSpace(SelectedGroup) || x.Server.GroupName == SelectedGroup) && x.IsSelected == true));
                }
                return _cmdDeleteSelected;
            }
        }



        private RelayCommand _cmdMoveSelected;
        public RelayCommand CmdMoveSelected
        {
            get
            {
                if (_cmdMoveSelected == null)
                {
                    _cmdMoveSelected = new RelayCommand((o) =>
                    {
                        if (MessageBoxResult.Yes == MessageBox.Show(
                            SystemConfig.Instance.Language.GetText("managementpage_move_to_group_confirm") + MoveToGroup + "'?",
                            SystemConfig.Instance.Language.GetText("messagebox_title_warning"), MessageBoxButton.YesNo,
                            MessageBoxImage.Question, MessageBoxResult.None))
                        {
                            var ss = ServerListItems.Where(x => (string.IsNullOrWhiteSpace(SelectedGroup) || x.Server.GroupName == SelectedGroup) && x.IsSelected == true).ToList();
                            if (ss?.Count > 0)
                            {
                                foreach (var vs in ss)
                                {
                                    vs.Server.GroupName = MoveToGroup;
                                    Server.AddOrUpdate(vs.Server);
                                }
                            }
                        }
                    }, o => ServerListItems.Any(x => (string.IsNullOrWhiteSpace(SelectedGroup) || x.Server.GroupName == SelectedGroup) && x.IsSelected == true));
                }
                return _cmdMoveSelected;
            }
        }


        private DateTime _lastCmdReOrder;
        private RelayCommand _cmdReOrder;
        public RelayCommand CmdReOrder
        {
            get
            {
                if (_cmdReOrder == null)
                {
                    _cmdReOrder = new RelayCommand((o) =>
                    {
                        if (int.TryParse(o.ToString(), out int ot))
                        {
                            if ((DateTime.Now - _lastCmdReOrder).TotalMilliseconds > 200)
                            {
                                // cancel order
                                if (ItemsOrderByType == (ot + 1))
                                {
                                    ot = -1;
                                }
                                else if (ItemsOrderByType == ot)
                                {
                                    ++ot;
                                }

                                ItemsOrderByType = ot;
                                _lastCmdReOrder = DateTime.Now;
                            }
                        }
                    });
                }
                return _cmdReOrder;
            }
        }


        private int _itemsOrderByType = -1;

        /// <summary>
        /// -1: by name asc(default)
        /// 0: by name asc
        /// 1: by name desc
        /// 2: by size asc
        /// 3: by size desc
        /// 4: by LastUpdate asc
        /// 5: by LastUpdate desc
        /// 6: by FileType asc
        /// 7: by FileType desc
        /// </summary>
        public int ItemsOrderByType
        {
            get => _itemsOrderByType;
            set
            {
                if (_itemsOrderByType != value)
                {
                    SetAndNotifyIfChanged(nameof(ItemsOrderByType), ref _itemsOrderByType, value);
                    OrderServerList();
                }
            }
        }
    }
}
