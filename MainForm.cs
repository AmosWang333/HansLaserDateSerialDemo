using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HansLaserDateSerialDemo
{
    internal sealed class MainForm : Form
    {
        private const string ConfigFile = "config.json";
        private const string StateFile = @".\sequence.state";
        private const string AuditFile = @".\mark-audit.csv";

        private readonly ToolStripButton _settingsButton;
        private readonly ToolStripButton _startButton;
        private readonly Label _status;
        private Label _codeValue;
        private Label _dateValue;
        private Label _serialValue;
        private Label _pendingWarning;
        private TextBox _log;
        private Button _previewButton;
        private Button _markButton;
        private Button _skipButton;
        private Button _exitButton;

        private AppConfiguration _configuration;
        private SequenceStore _store;
        private HansApi _api;
        private Reservation _reservation;
        private bool _busy;

        public MainForm()
        {
            Text = "大族激光日期流水号";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(980, 680);
            Size = new Size(1080, 720);
            Font = new Font("Microsoft YaHei UI", 9F);

            TableLayoutPanel shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Controls.Add(shell);

            ToolStrip toolStrip = new ToolStrip
            {
                Dock = DockStyle.Fill,
                GripStyle = ToolStripGripStyle.Hidden,
                AutoSize = false,
                Height = 44,
                Margin = Padding.Empty,
                Padding = new Padding(8, 6, 8, 6),
                BackColor = Color.FromArgb(245, 247, 250)
            };
            _settingsButton = new ToolStripButton("设置...")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                AutoSize = false,
                Width = 96,
                Height = 32,
                Margin = new Padding(0, 0, 8, 0),
                Padding = new Padding(12, 0, 12, 0),
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                ToolTipText = "打开设置并应用配置"
            };
            _settingsButton.Click += async delegate { await OpenSettingsAsync(); };
            toolStrip.Items.Add(_settingsButton);

            _startButton = new ToolStripButton("启动")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                AutoSize = false,
                Width = 96,
                Height = 32,
                Margin = new Padding(0, 0, 8, 0),
                Padding = new Padding(12, 0, 12, 0),
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                ToolTipText = "按已保存配置初始化设备并加载模板"
            };
            _startButton.Click += async delegate { await StartWithSavedConfigurationAsync(); };
            toolStrip.Items.Add(_startButton);

            shell.Controls.Add(toolStrip, 0, 0);

            _status = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Padding = new Padding(12, 9, 12, 0),
                Text = "未应用配置",
                BackColor = Color.FromArgb(245, 247, 250)
            };
            shell.Controls.Add(_status, 0, 1);

            Panel content = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Padding = new Padding(12)
            };
            shell.Controls.Add(content, 0, 2);
            content.Controls.Add(BuildOperationPanel());

            Load += delegate { LoadConfiguration(); };
            FormClosing += delegate { DisposeApi(); };
        }

        private Control BuildOperationPanel()
        {
            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                AutoSize = false
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            GroupBox currentBox = new GroupBox
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 8),
                Text = "当前编号"
            };
            root.Controls.Add(currentBox, 0, 0);

            FlowLayoutPanel currentFlow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = false,
                Padding = new Padding(14, 12, 14, 10)
            };
            currentFlow.Resize += delegate
            {
                int rowWidth = currentFlow.ClientSize.Width - currentFlow.Padding.Left - currentFlow.Padding.Right;
                foreach (Control child in currentFlow.Controls)
                    child.Width = Math.Max(100, rowWidth);
            };
            currentBox.Controls.Add(currentFlow);

            _codeValue = AddValueRow(currentFlow, "编号", "--", 22F, true);
            _dateValue = AddValueRow(currentFlow, "日期", "--", 10F, false);
            _serialValue = AddValueRow(currentFlow, "流水号", "--", 10F, false);
            _pendingWarning = AddValueRow(currentFlow, "状态", "通过工具栏设置应用配置后显示待确认编号", 9F, false);
            _pendingWarning.ForeColor = Color.FromArgb(180, 96, 0);

            GroupBox flowBox = new GroupBox
            {
                Dock = DockStyle.Fill,
                Height = 140,
                Margin = new Padding(0, 0, 0, 8),
                Text = "操作流程"
            };
            root.Controls.Add(flowBox, 0, 1);

            TableLayoutPanel flow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(10)
            };
            for (int i = 0; i < 4; i++)
                flow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            flowBox.Controls.Add(flow);
            AddFlowStep(flow, 0, "1", "应用配置", "工具栏设置保存 config.json，初始化设备并加载模板。");
            AddFlowStep(flow, 1, "2", "占用编号", "写入 sequence.state，断电后仍回到同一待确认编号。");
            AddFlowStep(flow, 2, "3", "预览/打标", "P 红光预览不提交；M 正常结束才提交。");
            AddFlowStep(flow, 3, "4", "确认异常", "S 确认已用或跳过；Q 退出保留待确认编号。");

            TableLayoutPanel actions = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Height = 100,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(0, 12, 0, 10)
            };
            for (int i = 0; i < 4; i++)
                actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            root.Controls.Add(actions, 0, 2);

            _previewButton = AddActionButton(actions, 0, "P 红光预览");
            _markButton = AddActionButton(actions, 1, "M 激光打标");
            _skipButton = AddActionButton(actions, 2, "S 已用/跳过");
            _exitButton = AddActionButton(actions, 3, "Q 退出");
            _previewButton.Click += async delegate { await PreviewAsync(); };
            _markButton.Click += async delegate { await MarkAsync(); };
            _skipButton.Click += delegate { SkipOrConfirm(); };
            _exitButton.Click += delegate { Close(); };

            GroupBox logBox = new GroupBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                Text = "运行日志"
            };
            root.Controls.Add(logBox, 0, 3);
            _log = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Font = new Font("Consolas", 9F),
                BackColor = Color.White
            };
            logBox.Controls.Add(_log);

            UpdateActionButtons();
            return root;
        }

        private Label AddValueRow(FlowLayoutPanel container, string label, string value, float fontSize, bool bold)
        {
            Font valueFont = new Font(Font.FontFamily, fontSize, bold ? FontStyle.Bold : FontStyle.Regular);
            int rowHeight = Math.Max(28, TextRenderer.MeasureText(value, valueFont).Height + 8);

            TableLayoutPanel row = new TableLayoutPanel
            {
                AutoSize = false,
                Width = Math.Max(100, container.ClientSize.Width - container.Padding.Left - container.Padding.Right),
                Height = rowHeight,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 4),
                Padding = Padding.Empty
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            container.Controls.Add(row);

            Label name = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Text = label,
                TextAlign = ContentAlignment.MiddleLeft
            };
            row.Controls.Add(name, 0, 0);

            Label valueLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Text = value,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = valueFont
            };
            row.Controls.Add(valueLabel, 1, 0);
            return valueLabel;
        }

        private static void AddFlowStep(TableLayoutPanel flow, int column, string number, string title, string text)
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            Label numberLabel = new Label
            {
                AutoSize = false,
                Width = 28,
                Height = 28,
                Text = number,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(229, 236, 246)
            };
            panel.Controls.Add(numberLabel);

            Label titleLabel = new Label
            {
                Left = 38,
                Top = 8,
                Width = 150,
                Height = 24,
                Text = title,
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold)
            };
            panel.Controls.Add(titleLabel);

            Label textLabel = new Label
            {
                Left = 0,
                Top = 42,
                Width = 220,
                Height = 56,
                Text = text
            };
            panel.Controls.Add(textLabel);
            flow.Controls.Add(panel, column, 0);
        }

        private Button AddActionButton(TableLayoutPanel panel, int column, string text)
        {
            Button button = new Button
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 0, 6, 0),
                Text = text
            };
            panel.Controls.Add(button, column, 0);
            return button;
        }

        private void LoadConfiguration()
        {
            try
            {
                _configuration = LoadOrCreateConfiguration();
                Log("已载入 config.json。通过工具栏“设置”修改并应用配置。");
            }
            catch (Exception ex)
            {
                Log("载入配置失败：" + ex.Message);
                SetStatus("配置载入失败", true);
            }
        }

        private async Task OpenSettingsAsync()
        {
            if (_busy)
                return;

            AppConfiguration configuration;
            try
            {
                configuration = _configuration ?? LoadOrCreateConfiguration();
            }
            catch (Exception ex)
            {
                MessageBox.Show("载入配置失败：" + ex.Message, "设置", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using (SettingsDialog dialog = new SettingsDialog(configuration))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                await ApplyConfigurationAsync(dialog.Configuration, true);
            }
        }

        private async Task StartWithSavedConfigurationAsync()
        {
            if (_busy)
                return;

            AppConfiguration configuration;
            try
            {
                configuration = AppConfiguration.Load(ConfigFile);
            }
            catch (Exception ex)
            {
                MessageBox.Show("载入已保存配置失败：" + ex.Message, "启动", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            await ApplyConfigurationAsync(configuration, false);
        }

        private async Task ApplyConfigurationAsync(AppConfiguration configuration, bool saveConfiguration)
        {
            await RunBusyAsync("正在应用配置……", delegate
            {
                if (saveConfiguration)
                    AppConfiguration.Save(ConfigFile, configuration);
                configuration.ValidateFiles();

                HansApi newApi = new HansApi(configuration.DllPath);
                try
                {
                    newApi.Initialize(configuration.MachinePath);
                    newApi.LoadTemplate(configuration.TemplatePath);
                    string version = newApi.GetVersionText();

                    BeginInvoke(new Action(delegate
                    {
                        DisposeApi();
                        _api = newApi;
                        _configuration = configuration;
                        _store = new SequenceStore(StateFile);
                        Log((saveConfiguration ? "配置已保存并应用：" : "已按保存配置启动：") + version);
                        ReserveAndDisplayCurrent();
                        SetStatus("配置已应用，模板已加载", false);
                    }));
                }
                catch
                {
                    newApi.Dispose();
                    throw;
                }
            });
        }

        private AppConfiguration LoadOrCreateConfiguration()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFile);
            if (File.Exists(path))
                return AppConfiguration.Load(ConfigFile);

            AppConfiguration configuration = new AppConfiguration
            {
                DllPath = @"C:\HansLaser\Marking\HansAdvInterface.dll",
                MachinePath = @"C:\HansLaser\Marking",
                TemplatePath = @"C:\HansMark\Templates\DateSerial.HS",
                VariableTextAlias = "CODE",
                UseFootPedal = AppConfiguration.DefaultUseFootPedal,
                FootPedalTimeoutMs = AppConfiguration.DefaultFootPedalTimeoutMs
            };
            AppConfiguration.Save(ConfigFile, configuration);
            return configuration;
        }

        private async Task PreviewAsync()
        {
            Reservation reservation = _reservation;
            if (reservation == null || _api == null)
                return;

            await RunBusyAsync("红光预览中……", delegate
            {
                try
                {
                    MarkEndStatus status = _api.MarkAndWait(true, false, 0, 30 * 1000);
                    AuditLog.Append(AuditFile, "PREVIEW", reservation.Code, status.ToString());
                    BeginInvoke(new Action(delegate { Log("红光预览结束：" + status); }));
                }
                catch (Exception ex)
                {
                    AuditLog.Append(AuditFile, "PREVIEW_ERROR", reservation.Code, ex.Message);
                    BeginInvoke(new Action(delegate { Log("红光预览失败：" + ex.Message); }));
                }
            });
        }

        private async Task MarkAsync()
        {
            Reservation reservation = _reservation;
            AppConfiguration configuration = _configuration;
            if (reservation == null || configuration == null || _api == null)
                return;

            string prompt = configuration.UseFootPedal
                ? "已进入激光打标等待，请踩脚踏/给触发信号。"
                : "将立即激光打标。";

            await RunBusyAsync(prompt, delegate
            {
                try
                {
                    int overallTimeoutMs = configuration.UseFootPedal
                        ? configuration.FootPedalTimeoutMs + 60 * 1000
                        : 2 * 60 * 1000;

                    MarkEndStatus status = _api.MarkAndWait(
                        false,
                        configuration.UseFootPedal,
                        configuration.UseFootPedal ? configuration.FootPedalTimeoutMs : 0,
                        overallTimeoutMs);

                    uint? markTime = _api.TryGetLastMarkTimeMs();
                    string detail = status + (markTime.HasValue ? "; " + markTime.Value + " ms" : string.Empty);

                    if (status == MarkEndStatus.Normal)
                    {
                        _store.Complete(reservation.Code);
                        AuditLog.Append(AuditFile, "MARK_SUCCESS", reservation.Code, detail);
                        BeginInvoke(new Action(delegate
                        {
                            Log("打标正常结束，编号已提交：" + reservation.Code);
                            ReserveAndDisplayCurrent();
                        }));
                        return;
                    }

                    AuditLog.Append(AuditFile, "MARK_NOT_NORMAL", reservation.Code, detail);
                    BeginInvoke(new Action(delegate
                    {
                        Log("打标未正常完成：" + status + "。编号仍处于待确认状态。");
                        SetStatus("打标未正常完成，编号未提交", true);
                    }));
                }
                catch (Exception ex)
                {
                    AuditLog.Append(AuditFile, "MARK_ERROR", reservation.Code, ex.Message);
                    BeginInvoke(new Action(delegate
                    {
                        Log("打标调用异常：" + ex.Message);
                        SetStatus("打标异常，编号仍处于待确认状态", true);
                    }));
                }
            });
        }

        private void SkipOrConfirm()
        {
            if (_reservation == null || _store == null)
                return;

            DialogResult result = MessageBox.Show(
                "确认编号 " + _reservation.Code + " 已经使用或必须跳过？",
                "确认已用/跳过",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (result != DialogResult.Yes)
                return;

            _store.SkipOrConfirmAlreadyMarked(_reservation.Code);
            AuditLog.Append(AuditFile, "SKIP_OR_CONFIRMED", _reservation.Code, "操作员确认该编号已使用或应跳过");
            Log("已确认编号已用/跳过：" + _reservation.Code);
            ReserveAndDisplayCurrent();
        }

        private void ReserveAndDisplayCurrent()
        {
            try
            {
                _reservation = _store.GetOrReserve(DateTime.Now);
                _api.SetVariableText(_configuration.VariableTextAlias, _reservation.Code);
                AuditLog.Append(
                    AuditFile,
                    _reservation.WasAlreadyPending ? "RESUME_PENDING" : "RESERVE",
                    _reservation.Code,
                    _reservation.Date.ToString("yyyy-MM-dd") + "/" + _reservation.Serial);

                _codeValue.Text = _reservation.Code;
                _dateValue.Text = _reservation.Date.ToString("yyyy-MM-dd");
                _serialValue.Text = _reservation.Serial.ToString("0000");
                _pendingWarning.Text = _reservation.WasAlreadyPending
                    ? "上次未确认完成；请检查工件/MES 后再重打或跳过。"
                    : "新编号已占用，等待预览、打标或确认跳过。";
                SetStatus("当前编号：" + _reservation.Code, false);
            }
            catch (Exception ex)
            {
                Log("准备当前编号失败：" + ex.Message);
                SetStatus("准备当前编号失败", true);
            }
            finally
            {
                UpdateActionButtons();
            }
        }

        private async Task RunBusyAsync(string status, Action action)
        {
            _busy = true;
            SetStatus(status, false);
            UpdateActionButtons();

            try
            {
                await Task.Run(action);
            }
            catch (Exception ex)
            {
                Log("操作失败：" + ex.Message);
                SetStatus("操作失败", true);
            }
            finally
            {
                _busy = false;
                UpdateActionButtons();
            }
        }

        private void UpdateActionButtons()
        {
            bool ready = !_busy && _api != null && _reservation != null;
            _settingsButton.Enabled = !_busy;
            _startButton.Enabled = !_busy;
            _previewButton.Enabled = ready;
            _markButton.Enabled = ready;
            _skipButton.Enabled = ready;
            _exitButton.Enabled = !_busy;
        }

        private void SetStatus(string text, bool warning)
        {
            _status.Text = text;
            _status.ForeColor = warning ? Color.FromArgb(150, 50, 0) : Color.FromArgb(30, 70, 110);
        }

        private void Log(string message)
        {
            _log.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + message + Environment.NewLine);
        }

        private void DisposeApi()
        {
            if (_api != null)
            {
                _api.Dispose();
                _api = null;
            }
        }
    }
}