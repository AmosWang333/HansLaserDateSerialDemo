using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace HansLaserDateSerialDemo
{
    internal sealed class SettingsDialog : Form
    {
        private readonly TextBox _machinePathTextBox;
        private readonly TextBox _templatePathTextBox;
        private readonly TextBox _variableTextAliasTextBox;
        private readonly ComboBox _codeGeneratorComboBox;
        private readonly TextBox _codePatternTextBox;
        private readonly CheckBox _useFootPedal;
        private readonly NumericUpDown _footPedalTimeoutSeconds;
        private readonly Label _dllVersionLabel;

        public AppConfiguration Configuration { get; private set; }

        public SettingsDialog(AppConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            Text = "设置";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(760, 620);
            Font = new Font("Microsoft YaHei UI", 9F);

            TableLayoutPanel shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(14)
            };
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            Controls.Add(shell);

            FlowLayoutPanel root = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            root.Resize += delegate
            {
                int width = root.ClientSize.Width - root.Padding.Left - root.Padding.Right;
                foreach (Control child in root.Controls)
                    child.Width = Math.Max(200, width - SystemInformation.VerticalScrollBarWidth);
            };
            shell.Controls.Add(root, 0, 0);

            GroupBox basicBox = new GroupBox
            {
                Width = 712,
                Height = 232,
                Margin = new Padding(0, 0, 0, 10),
                Text = "基础配置"
            };
            root.Controls.Add(basicBox);

            TableLayoutPanel basicGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(12)
            };
            basicGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            basicGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 4; i++)
                basicGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            basicBox.Controls.Add(basicGrid);

            _machinePathTextBox = AddPathSettingTextBox(
                basicGrid,
                0,
                "设备配置目录",
                delegate(TextBox textBox) { BrowseFolder(textBox, "选择设备配置目录"); });
            _dllVersionLabel = AddDllVersionRow(basicGrid, 1);
            _templatePathTextBox = AddPathSettingTextBox(
                basicGrid,
                2,
                "打标模板",
                delegate(TextBox textBox) { BrowseFile(textBox, "选择打标模板", "打标模板 (*.HS)|*.HS|所有文件 (*.*)|*.*"); });
            _variableTextAliasTextBox = AddSettingTextBox(basicGrid, 3, "可变文本别名");

            GroupBox generatorBox = new GroupBox
            {
                Width = 712,
                Height = 92,
                Margin = new Padding(0, 0, 0, 10),
                Text = "编号生成"
            };
            root.Controls.Add(generatorBox);

            TableLayoutPanel generatorGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(12)
            };
            generatorGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            generatorGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            generatorGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            generatorGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            generatorBox.Controls.Add(generatorGrid);

            Label generatorLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "生成器",
                TextAlign = ContentAlignment.MiddleLeft
            };
            generatorGrid.Controls.Add(generatorLabel, 0, 0);

            _codeGeneratorComboBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 10, 16, 0)
            };
            _codeGeneratorComboBox.Items.Add(CodeGeneratorTypes.EcoFlow);
            _codeGeneratorComboBox.Items.Add(CodeGeneratorTypes.Normal);
            generatorGrid.Controls.Add(_codeGeneratorComboBox, 1, 0);

            Label patternLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "固定部分/Pattern",
                TextAlign = ContentAlignment.MiddleLeft
            };
            generatorGrid.Controls.Add(patternLabel, 2, 0);

            _codePatternTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 10, 0, 0)
            };
            generatorGrid.Controls.Add(_codePatternTextBox, 3, 0);

            GroupBox pedalBox = new GroupBox
            {
                Width = 712,
                Height = 100,
                Margin = new Padding(0, 0, 0, 10),
                Text = "脚踏触发"
            };
            root.Controls.Add(pedalBox);

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
                Margin = Padding.Empty,
                Padding = new Padding(0, 10, 0, 0),
                WrapContents = false
            };
            shell.Controls.Add(buttons, 0, 1);

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

        private TextBox AddPathSettingTextBox(TableLayoutPanel grid, int row, string label,
            Action<TextBox> browseAction)
        {
            Label name = new Label
            {
                Dock = DockStyle.Fill,
                Text = label,
                TextAlign = ContentAlignment.MiddleLeft
            };
            grid.Controls.Add(name, 0, row);

            TableLayoutPanel panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Margin = Padding.Empty
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            grid.Controls.Add(panel, 1, row);

            TextBox textBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 10, 8, 0)
            };
            panel.Controls.Add(textBox, 0, 0);

            Button browseButton = new Button
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 0, 6),
                Text = "打开"
            };
            browseButton.Click += delegate { browseAction(textBox); };
            panel.Controls.Add(browseButton, 1, 0);

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
            _machinePathTextBox.Text = configuration.MachinePath;
            _templatePathTextBox.Text = configuration.TemplatePath;
            _variableTextAliasTextBox.Text = configuration.VariableTextAlias;
            SelectCodeGenerator(configuration.CodeGeneratorType);
            _codePatternTextBox.Text = configuration.CodePattern;
            _useFootPedal.Checked = configuration.UseFootPedal;
            _dllVersionLabel.Text = "未读取";
            _footPedalTimeoutSeconds.Value = Math.Max(
                _footPedalTimeoutSeconds.Minimum,
                Math.Min(_footPedalTimeoutSeconds.Maximum, configuration.FootPedalTimeoutMs / (decimal)1000));
        }

        private void ReadDllVersion()
        {
            string machinePath = _machinePathTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(machinePath))
            {
                _dllVersionLabel.Text = "请先填写设备配置目录";
                return;
            }

            string dllPath = Path.Combine(machinePath, "HansAdvInterface.dll");
            try
            {
                if (!File.Exists(dllPath))
                {
                    _dllVersionLabel.Text = "读取失败：MachinePath 下没有 HansAdvInterface.dll";
                    return;
                }

                using (HansApi api = new HansApi(dllPath))
                {
                    _dllVersionLabel.Text = api.GetVersionText();
                }
            }
            catch (Exception ex)
            {
                _dllVersionLabel.Text = $"读取失败：{ex.Message}";
            }
        }

        private void BrowseFile(TextBox target, string title, string filter)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = title;
                dialog.Filter = filter;
                dialog.CheckFileExists = true;
                dialog.CheckPathExists = true;

                string currentPath = target.Text.Trim();
                if (File.Exists(currentPath))
                {
                    dialog.FileName = currentPath;
                    dialog.InitialDirectory = Path.GetDirectoryName(Path.GetFullPath(currentPath));
                }
                else if (Directory.Exists(currentPath))
                {
                    dialog.InitialDirectory = currentPath;
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                    target.Text = dialog.FileName;
            }
        }

        private void BrowseFolder(TextBox target, string description)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = description;
                dialog.ShowNewFolderButton = false;

                string currentPath = target.Text.Trim();
                if (Directory.Exists(currentPath))
                    dialog.SelectedPath = currentPath;
                else if (File.Exists(currentPath))
                    dialog.SelectedPath = Path.GetDirectoryName(Path.GetFullPath(currentPath));

                if (dialog.ShowDialog(this) == DialogResult.OK)
                    target.Text = dialog.SelectedPath;
            }
        }

        private void SelectCodeGenerator(string generatorType)
        {
            string value = string.IsNullOrWhiteSpace(generatorType)
                ? AppConfiguration.DefaultCodeGeneratorType
                : generatorType;

            for (int i = 0; i < _codeGeneratorComboBox.Items.Count; i++)
            {
                if (string.Equals(_codeGeneratorComboBox.Items[i].ToString(), value,
                        StringComparison.OrdinalIgnoreCase))
                {
                    _codeGeneratorComboBox.SelectedIndex = i;
                    return;
                }
            }

            _codeGeneratorComboBox.SelectedIndex = 0;
        }

        private void SaveAndClose()
        {
            try
            {
                AppConfiguration configuration = new AppConfiguration
                {
                    MachinePath = _machinePathTextBox.Text.Trim(),
                    TemplatePath = _templatePathTextBox.Text.Trim(),
                    VariableTextAlias = _variableTextAliasTextBox.Text.Trim(),
                    UseFootPedal = _useFootPedal.Checked,
                    FootPedalTimeoutMs = Convert.ToInt32(_footPedalTimeoutSeconds.Value) * 1000,
                    CodeGeneratorType = _codeGeneratorComboBox.SelectedItem == null
                        ? AppConfiguration.DefaultCodeGeneratorType
                        : _codeGeneratorComboBox.SelectedItem.ToString(),
                    CodePattern = _codePatternTextBox.Text.Trim()
                };

                Configuration = AppConfiguration.LoadFromJson(configuration.ToJson(), "config.json");
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"设置无效：{ex.Message}", "设置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
