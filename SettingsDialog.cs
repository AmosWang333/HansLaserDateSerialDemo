using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace HansLaserDateSerialDemo
{
    internal sealed class SettingsDialog : Form
    {
        private readonly TextBox _dllPathTextBox;
        private readonly TextBox _machinePathTextBox;
        private readonly TextBox _templatePathTextBox;
        private readonly TextBox _variableTextAliasTextBox;
        private readonly CheckBox _useFootPedal;
        private readonly NumericUpDown _footPedalTimeoutSeconds;
        private readonly Label _dllVersionLabel;

        public AppConfiguration Configuration { get; private set; }

        public SettingsDialog(AppConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException("configuration");

            Text = "设置";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(760, 468);
            Font = new Font("Microsoft YaHei UI", 9F);

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(14)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 280));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            GroupBox basicBox = new GroupBox
            {
                Dock = DockStyle.Fill,
                Text = "基础配置"
            };
            root.Controls.Add(basicBox, 0, 0);

            TableLayoutPanel basicGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Padding = new Padding(12)
            };
            basicGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            basicGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 5; i++)
                basicGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            basicBox.Controls.Add(basicGrid);

            _dllPathTextBox = AddSettingTextBox(basicGrid, 0, "接口 DLL");
            _dllVersionLabel = AddDllVersionRow(basicGrid, 1);
            _machinePathTextBox = AddSettingTextBox(basicGrid, 2, "设备配置目录");
            _templatePathTextBox = AddSettingTextBox(basicGrid, 3, "打标模板");
            _variableTextAliasTextBox = AddSettingTextBox(basicGrid, 4, "可变文本别名");

            GroupBox pedalBox = new GroupBox
            {
                Dock = DockStyle.Fill,
                Text = "脚踏触发"
            };
            root.Controls.Add(pedalBox, 0, 1);

            _useFootPedal = new CheckBox
            {
                Left = 14,
                Top = 28,
                Width = 260,
                Text = "启用脚踏触发"
            };
            pedalBox.Controls.Add(_useFootPedal);

            Label timeoutLabel = new Label
            {
                Left = 300,
                Top = 31,
                Width = 150,
                Text = "脚踏等待超时"
            };
            pedalBox.Controls.Add(timeoutLabel);

            _footPedalTimeoutSeconds = new NumericUpDown
            {
                Left = 455,
                Top = 27,
                Width = 120,
                Minimum = 1,
                Maximum = 3600,
                Increment = 5
            };
            pedalBox.Controls.Add(_footPedalTimeoutSeconds);

            Label secondsLabel = new Label
            {
                Left = 585,
                Top = 31,
                Width = 40,
                Text = "秒"
            };
            pedalBox.Controls.Add(secondsLabel);

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 16, 0, 0)
            };
            root.Controls.Add(buttons, 0, 2);

            Button saveButton = new Button
            {
                Width = 120,
                Height = 34,
                Text = "保存并应用"
            };
            saveButton.Click += delegate { SaveAndClose(); };
            buttons.Controls.Add(saveButton);

            Button cancelButton = new Button
            {
                Width = 90,
                Height = 34,
                Text = "取消"
            };
            cancelButton.Click += delegate { DialogResult = DialogResult.Cancel; };
            buttons.Controls.Add(cancelButton);

            AcceptButton = saveButton;
            CancelButton = cancelButton;
            ShowConfiguration(configuration);
        }

        private TextBox AddSettingTextBox(TableLayoutPanel grid, int row, string label)
        {
            Label name = new Label
            {
                Dock = DockStyle.Fill,
                Text = label,
                TextAlign = ContentAlignment.MiddleLeft
            };
            grid.Controls.Add(name, 0, row);

            TextBox textBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 10, 0, 0)
            };
            grid.Controls.Add(textBox, 1, row);
            return textBox;
        }

        private Label AddDllVersionRow(TableLayoutPanel grid, int row)
        {
            Label name = new Label
            {
                Dock = DockStyle.Fill,
                Text = "DLL 版本",
                TextAlign = ContentAlignment.MiddleLeft
            };
            grid.Controls.Add(name, 0, row);

            TableLayoutPanel panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Margin = Padding.Empty
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.Controls.Add(panel, 1, row);

            Button readButton = new Button
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 8, 6),
                Text = "读取版本"
            };
            readButton.Click += delegate { ReadDllVersion(); };
            panel.Controls.Add(readButton, 0, 0);

            Label versionLabel = new Label
            {
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "未读取"
            };
            panel.Controls.Add(versionLabel, 1, 0);
            return versionLabel;
        }

        private void ShowConfiguration(AppConfiguration configuration)
        {
            _dllPathTextBox.Text = configuration.DllPath;
            _machinePathTextBox.Text = configuration.MachinePath;
            _templatePathTextBox.Text = configuration.TemplatePath;
            _variableTextAliasTextBox.Text = configuration.VariableTextAlias;
            _useFootPedal.Checked = configuration.UseFootPedal;
            _dllVersionLabel.Text = "未读取";
            _footPedalTimeoutSeconds.Value = Math.Max(
                _footPedalTimeoutSeconds.Minimum,
                Math.Min(_footPedalTimeoutSeconds.Maximum, configuration.FootPedalTimeoutMs / 1000));
        }

        private void ReadDllVersion()
        {
            string dllPath = _dllPathTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(dllPath))
            {
                _dllVersionLabel.Text = "请先填写接口 DLL 路径";
                return;
            }

            try
            {
                if (!File.Exists(dllPath))
                {
                    _dllVersionLabel.Text = "读取失败：找不到接口 DLL";
                    return;
                }

                if (!string.Equals(Path.GetFileName(dllPath), "HansAdvInterface.dll",
                        StringComparison.OrdinalIgnoreCase))
                {
                    _dllVersionLabel.Text = "读取失败：请选择 HansAdvInterface.dll";
                    return;
                }

                using (HansApi api = new HansApi(dllPath))
                {
                    _dllVersionLabel.Text = api.GetVersionText();
                }
            }
            catch (Exception ex)
            {
                _dllVersionLabel.Text = "读取失败：" + ex.Message;
            }
        }

        private void SaveAndClose()
        {
            try
            {
                AppConfiguration configuration = new AppConfiguration
                {
                    DllPath = _dllPathTextBox.Text.Trim(),
                    MachinePath = _machinePathTextBox.Text.Trim(),
                    TemplatePath = _templatePathTextBox.Text.Trim(),
                    VariableTextAlias = _variableTextAliasTextBox.Text.Trim(),
                    UseFootPedal = _useFootPedal.Checked,
                    FootPedalTimeoutMs = Convert.ToInt32(_footPedalTimeoutSeconds.Value) * 1000
                };

                Configuration = AppConfiguration.LoadFromJson(configuration.ToJson(), "config.json");
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show("设置无效：" + ex.Message, "设置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}