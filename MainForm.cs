using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Management;
using System.Net;
using System.Windows.Forms;

namespace NicConfig
{
    public class Adapter
    {
        public int Index;                 // WMI Index used to bind the configuration object
        public string Name;               // NetConnectionID, e.g. "Ethernet 3"
        public string Description;
        public bool DhcpEnabled;
        public string[] IpAddresses;
        public string[] SubnetMasks;
        public string[] Gateways;
        public string[] DnsServers;
        public override string ToString() =>
            string.IsNullOrEmpty(Name) ? Description : $"{Name}  ({Description})";
    }

    public class MainForm : Form
    {
        private ComboBox cboAdapters;
        private RadioButton rbDhcp, rbStaticIp;
        private RadioButton rbDnsAuto, rbDnsStatic;
        private TextBox txtIp, txtMask, txtGw, txtDns1, txtDns2;
        private Button btnApply, btnRefresh;
        private Label lblStatus;

        public MainForm()
        {
            Text = $"Ethernet Adapter Configuration ({Application.ProductVersion})";
            ClientSize = new Size(430, 440);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BuildUi();
            LoadAdapters();
        }

        private void BuildUi()
        {
            var lblAdapter = new Label { Text = "Adapter:", Left = 15, Top = 18, Width = 70 };
            cboAdapters = new ComboBox
            {
                Left = 90,
                Top = 15,
                Width = 320,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboAdapters.SelectedIndexChanged += (s, e) => ShowSelected();

            btnRefresh = new Button { Text = "Refresh", Left = 90, Top = 45, Width = 90 };
            btnRefresh.Click += (s, e) => LoadAdapters();

            // IP settings group. Its own container so the IP radios form an
            // independent mutual-exclusion group, separate from the DNS radios.
            var ipGroup = new GroupBox
            {
                Text = "IP settings",
                Left = 15, Top = 80, Width = 400, Height = 145
            };
            rbDhcp = new RadioButton
            {
                Text = "Obtain an IP address automatically",
                Left = 10, Top = 20, Width = 360
            };
            rbStaticIp = new RadioButton
            {
                Text = "Use the following IP address:",
                Left = 10, Top = 45, Width = 360
            };
            rbDhcp.CheckedChanged += (s, e) => ToggleFields();

            txtIp = MakeRow(ipGroup, "IP address:", 72);
            txtMask = MakeRow(ipGroup, "Subnet mask:", 100);
            txtGw = MakeRow(ipGroup, "Default gateway:", 128);
            ipGroup.Controls.Add(rbDhcp);
            ipGroup.Controls.Add(rbStaticIp);

            // DNS settings group, in its own container for the same reason.
            var dnsGroup = new GroupBox
            {
                Text = "DNS settings",
                Left = 15, Top = 235, Width = 400, Height = 120
            };
            rbDnsAuto = new RadioButton
            {
                Text = "Obtain DNS server address automatically",
                Left = 10, Top = 20, Width = 380
            };
            rbDnsStatic = new RadioButton
            {
                Text = "Use the following DNS server addresses:",
                Left = 10, Top = 45, Width = 380
            };
            rbDnsAuto.CheckedChanged += (s, e) => ToggleFields();

            txtDns1 = MakeRow(dnsGroup, "Preferred DNS:", 72);
            txtDns2 = MakeRow(dnsGroup, "Alternate DNS:", 100);
            dnsGroup.Controls.Add(rbDnsAuto);
            dnsGroup.Controls.Add(rbDnsStatic);

            btnApply = new Button
            {
                Text = "Apply",
                Left = 90, Top = 365, Width = 120, Height = 30
            };
            btnApply.Click += (s, e) => Apply();

            lblStatus = new Label
            {
                Left = 15, Top = 405, Width = 400,
                ForeColor = Color.DimGray
            };

            Controls.AddRange(new Control[]
            {
                lblAdapter, cboAdapters, btnRefresh,
                ipGroup, dnsGroup,
                btnApply, lblStatus
            });
        }

        private TextBox MakeRow(Control parent, string label, int top)
        {
            var lbl = new Label { Text = label, Left = 25, Top = top + 3, Width = 110 };
            var tb = new TextBox { Left = 140, Top = top, Width = 245 };
            parent.Controls.Add(lbl);
            parent.Controls.Add(tb);
            return tb;
        }

        private void LoadAdapters()
        {
            var selectedIndex = (cboAdapters.SelectedItem as Adapter)?.Index;
            cboAdapters.Items.Clear();
            foreach (var a in QueryAdapters())
                cboAdapters.Items.Add(a);

            if (cboAdapters.Items.Count > 0)
            {
                int restore = 0;
                for (int i = 0; i < cboAdapters.Items.Count; i++)
                {
                    if (((Adapter)cboAdapters.Items[i]).Index == selectedIndex) { restore = i; break; }
                }
                cboAdapters.SelectedIndex = restore;
            }
            SetStatus($"{cboAdapters.Items.Count} Ethernet adapter(s) found.");
        }

        // Physical, IP-enabled Ethernet adapters only (excludes Wi-Fi, virtual, loopback).
        private IEnumerable<Adapter> QueryAdapters()
        {
            var ethernetNames = new Dictionary<int, string>();
            using (var nics = new ManagementObjectSearcher(
                "SELECT Index, NetConnectionID FROM Win32_NetworkAdapter WHERE AdapterTypeId = 0 AND PhysicalAdapter = TRUE"))
            {
                foreach (ManagementObject mo in nics.Get())
                    ethernetNames[Convert.ToInt32(mo["Index"])] = mo["NetConnectionID"] as string;
            }

            var result = new List<Adapter>();
            using (var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = TRUE"))
            {
                foreach (ManagementObject mo in searcher.Get())
                {
                    int index = Convert.ToInt32(mo["Index"]);
                    if (!ethernetNames.TryGetValue(index, out string name))
                        continue;

                    result.Add(new Adapter
                    {
                        Index = index,
                        Name = name,
                        Description = (string)mo["Description"],
                        DhcpEnabled = (bool)mo["DHCPEnabled"],
                        IpAddresses = (string[])mo["IPAddress"] ?? new string[0],
                        SubnetMasks = (string[])mo["IPSubnet"] ?? new string[0],
                        Gateways = (string[])mo["DefaultIPGateway"] ?? new string[0],
                        DnsServers = (string[])mo["DNSServerSearchOrder"] ?? new string[0]
                    });
                }
            }
            return result;
        }

        private void ShowSelected()
        {
            if (!(cboAdapters.SelectedItem is Adapter a)) return;
            rbDhcp.Checked = a.DhcpEnabled;
            rbStaticIp.Checked = !a.DhcpEnabled;
            txtIp.Text = a.IpAddresses.FirstOrDefault(IsV4) ?? "";
            txtMask.Text = a.SubnetMasks.FirstOrDefault() ?? "";
            txtGw.Text = a.Gateways.FirstOrDefault() ?? "";
            rbDnsStatic.Checked = a.DnsServers.Length > 0;
            rbDnsAuto.Checked = a.DnsServers.Length == 0;
            txtDns1.Text = a.DnsServers.ElementAtOrDefault(0) ?? "";
            txtDns2.Text = a.DnsServers.ElementAtOrDefault(1) ?? "";
            ToggleFields();
        }

        private static bool IsV4(string ip) => ip.Contains(".");

        private void ToggleFields()
        {
            bool staticIp = rbStaticIp.Checked;
            txtIp.Enabled = txtMask.Enabled = txtGw.Enabled = staticIp;
            bool staticDns = rbDnsStatic.Checked;
            txtDns1.Enabled = txtDns2.Enabled = staticDns;
        }

        private void Apply()
        {
            if (!(cboAdapters.SelectedItem is Adapter a))
            {
                SetStatus("No adapter selected.");
                return;
            }

            if (!Validate(out string error))
            {
                MessageBox.Show(error, "Invalid input",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                using (var config = new ManagementObject(
                    $"Win32_NetworkAdapterConfiguration.Index={a.Index}"))
                {
                    if (rbStaticIp.Checked)
                    {
                        var ipParams = config.GetMethodParameters("EnableStatic");
                        ipParams["IPAddress"] = new[] { txtIp.Text.Trim() };
                        ipParams["SubnetMask"] = new[] { txtMask.Text.Trim() };
                        Invoke(config, "EnableStatic", ipParams);

                        if (!string.IsNullOrWhiteSpace(txtGw.Text))
                        {
                            var gwParams = config.GetMethodParameters("SetGateways");
                            gwParams["DefaultIPGateway"] = new[] { txtGw.Text.Trim() };
                            gwParams["GatewayCostMetric"] = new[] { 1 };
                            Invoke(config, "SetGateways", gwParams);
                        }
                    }
                    else
                    {
                        Invoke(config, "EnableDHCP", null);
                    }

                    if (rbDnsStatic.Checked)
                    {
                        var dns = new[] { txtDns1.Text.Trim(), txtDns2.Text.Trim() }
                            .Where(x => x.Length > 0).ToArray();
                        var dnsParams = config.GetMethodParameters("SetDNSServerSearchOrder");
                        dnsParams["DNSServerSearchOrder"] = dns;
                        Invoke(config, "SetDNSServerSearchOrder", dnsParams);
                    }
                    else
                    {
                        // Empty array reverts DNS to DHCP-assigned.
                        var dnsParams = config.GetMethodParameters("SetDNSServerSearchOrder");
                        dnsParams["DNSServerSearchOrder"] = new string[0];
                        Invoke(config, "SetDNSServerSearchOrder", dnsParams);
                    }
                }
                SetStatus("Settings applied successfully.");
                LoadAdapters();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetStatus("Failed to apply settings.");
            }
        }

        private bool Validate(out string error)
        {
            error = null;
            if (rbStaticIp.Checked)
            {
                if (!IsValidIpv4(txtIp.Text)) { error = "Enter a valid IPv4 address."; return false; }
                if (!IsValidIpv4(txtMask.Text)) { error = "Enter a valid subnet mask."; return false; }
                if (!string.IsNullOrWhiteSpace(txtGw.Text) && !IsValidIpv4(txtGw.Text))
                { error = "Enter a valid default gateway."; return false; }
            }
            if (rbDnsStatic.Checked)
            {
                if (!string.IsNullOrWhiteSpace(txtDns1.Text) && !IsValidIpv4(txtDns1.Text))
                { error = "Enter a valid preferred DNS server."; return false; }
                if (!string.IsNullOrWhiteSpace(txtDns2.Text) && !IsValidIpv4(txtDns2.Text))
                { error = "Enter a valid alternate DNS server."; return false; }
            }
            return true;
        }

        private static bool IsValidIpv4(string value)
        {
            value = value?.Trim();
            if (string.IsNullOrEmpty(value)) return false;
            return IPAddress.TryParse(value, out var ip)
                && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                && value.Count(c => c == '.') == 3;
        }

        private void Invoke(ManagementObject config, string method, ManagementBaseObject args)
        {
            using (var ret = config.InvokeMethod(method, args, null))
            {
                if (ret == null) return;
                uint code = (uint)ret["ReturnValue"];
                // 0 = success, 1 = success but a reboot is required.
                if (code != 0 && code != 1)
                    throw new Exception($"{method} failed with WMI return code {code}.");
            }
        }

        private void SetStatus(string s) => lblStatus.Text = s;
    }
}
