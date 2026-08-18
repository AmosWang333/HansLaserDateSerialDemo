using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace HansLaserDateSerialDemo
{
    internal sealed class SettingsDialog : Form
    {
        private readonly TextBox _machinePathTextBox;
        private readonly TextBox _variableTextAliasTextBox;
        private readonly ComboBox _codeGeneratorComboBox;
        private readonly ComboBox _productComboBox;
        private readonly CheckBox _useFootPedal;
        private readonly NumericUpDown _footPedalTimeoutSeconds;
        private readonly Label _dllVersionLabel;
        private readonly DataGridView _productsGrid;
        private readonly TextBox _productNameTextBox;
        private readonly TextBox _customerPartNumberTextBox;
        private readonly NumericUpDown _shipcodeBox;
        private readonly TextBox _productTemplatePathTextBox;
        private readonly TextBox _productPatternTextBox;

        private List<Product> _products = new List<Product>();
        private Product _editingProduct;

        public AppConfiguration Configuration { get; private set; }

        public SettingsDialog(AppConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            Text = "设置";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(760, 620);
            Size = new Size(860, 700);
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

            TabControl tabs = new TabControl
            {
                Dock = DockStyle.Fill
            };
            shell.Controls.Add(tabs, 0, 0);

            TabPage settingsPage = new TabPage("运行设置");
            tabs.TabPages.Add(settingsPage);

            FlowLayoutPanel settingsRoot = CreateVerticalFlow();
            settingsPage.Controls.Add(settingsRoot);

            GroupBox basicBox = AddGroup(settingsRoot, "基础配置", 192);
            TableLayoutPanel basicGrid = CreateFormGrid(3);
            basicBox.Controls.Add(basicGrid);

            _machinePathTextBox = AddPathSettingTextBox(
                basicGrid,
                0,
                "设备配置目录",
                delegate(TextBox textBox) { BrowseFolder(textBox, "选择设备配置目录"); });
            _dllVersionLabel = AddDllVersionRow(basicGrid, 1);
            _variableTextAliasTextBox = AddSettingTextBox(basicGrid, 2, "可变文本别名");

            GroupBox generatorBox = AddGroup(settingsRoot, "编号与产品", 128);
            TableLayoutPanel generatorGrid = CreateFormGrid(2);
            generatorBox.Controls.Add(generatorGrid);

            _codeGeneratorComboBox = AddComboBox(generatorGrid, 0, "生成器");
            _codeGeneratorComboBox.Items.Add(CodeGeneratorTypes.EcoFlow);
            _codeGeneratorComboBox.Items.Add(CodeGeneratorTypes.Normal);

            _productComboBox = AddComboBox(generatorGrid, 1, "产品");

            GroupBox pedalBox = AddGroup(settingsRoot, "脚踏触发", 104);
            TableLayoutPanel pedalGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1,
                Padding = new Padding(12)
            };
            pedalGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            pedalGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            pedalGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            pedalGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pedalBox.Controls.Add(pedalGrid);

            _useFootPedal = new CheckBox
            {
                Dock = DockStyle.Fill,
                Text = "启用脚踏触发"
            };
            pedalGrid.Controls.Add(_useFootPedal, 0, 0);

            Label timeoutLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "等待超时",
                TextAlign = ContentAlignment.MiddleLeft
            };
            pedalGrid.Controls.Add(timeoutLabel, 1, 0);

            _footPedalTimeoutSeconds = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                Minimum = 1,
                Maximum = 3600,
                Increment = 5,
                Margin = new Padding(0, 12, 8, 0)
            };
            pedalGrid.Controls.Add(_footPedalTimeoutSeconds, 2, 0);

            Label secondsLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "秒",
                TextAlign = ContentAlignment.MiddleLeft
            };
            pedalGrid.Controls.Add(secondsLabel, 3, 0);

            TabPage productsPage = new TabPage("产品配置");
            tabs.TabPages.Add(productsPage);

            TableLayoutPanel productsRoot = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(10)
            };
            productsRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            productsRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 292));
            productsPage.Controls.Add(productsRoot);

            _productsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                MultiSelect = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false
            };
            _productsGrid.Columns.Add(new DataGridViewButtonColumn { HeaderText = "+", Text = "+", UseColumnTextForButtonValue = true, Width = 36 });
            _productsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "名称", DataPropertyName = "Name", Width = 150 });
            _productsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "客户料号", DataPropertyName = "CustomerPartNumber", Width = 150 });
            _productsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Shipcode", DataPropertyName = "Shipcode", Width = 90 });
            _productsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "模板", DataPropertyName = "TemplatePath", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _productsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Pattern", DataPropertyName = "Pattern", Width = 150 });
            _productsGrid.CellContentClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == 0)
                    AddProductRow();
            };
            _productsGrid.ColumnHeaderMouseClick += delegate(object sender, DataGridViewCellMouseEventArgs e)
            {
                if (e.ColumnIndex == 0)
                    AddProductRow();
            };
            _productsGrid.SelectionChanged += delegate { LoadSelectedProductForEdit(); };
            productsRoot.Controls.Add(_productsGrid, 0, 0);

            GroupBox editorBox = new GroupBox
            {
                Dock = DockStyle.Fill,
                Text = "产品信息",
                Margin = new Padding(0, 10, 0, 0)
            };
            productsRoot.Controls.Add(editorBox, 0, 1);

            TableLayoutPanel editorRoot = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            editorRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            editorRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            editorBox.Controls.Add(editorRoot);

            TableLayoutPanel editorGrid = CreateFormGrid(5);
            editorGrid.ColumnStyles[0].Width = 110;
            editorRoot.Controls.Add(editorGrid, 0, 0);

            _productNameTextBox = AddSettingTextBox(editorGrid, 0, "名称");
            _customerPartNumberTextBox = AddSettingTextBox(editorGrid, 1, "客户料号");
            _shipcodeBox = AddNumericSetting(editorGrid, 2, "Shipcode");
            _productTemplatePathTextBox = AddPathSettingTextBox(
                editorGrid,
                3,
                "打标模板",
                delegate(TextBox textBox) { BrowseFile(textBox, "选择打标模板", "打标模板 (*.HS)|*.HS|所有文件 (*.*)|*.*"); });
            _productPatternTextBox = AddSettingTextBox(editorGrid, 4, "Pattern");

            FlowLayoutPanel productButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 6, 12, 0)
            };
            editorRoot.Controls.Add(productButtons, 0, 1);

            Button deleteProductButton = new Button { Width = 90, Height = 30, Text = "删除" };
            deleteProductButton.Click += delegate { DeleteSelectedProduct(); };
            productButtons.Controls.Add(deleteProductButton);

            Button saveProductButton = new Button { Width = 90, Height = 30, Text = "保存" };
            saveProductButton.Click += delegate { SaveProduct(); };
            productButtons.Controls.Add(saveProductButton);

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Margin = Padding.Empty,
                Padding = new Padding(0, 10, 0, 0),
                WrapContents = false
            };
            shell.Controls.Add(buttons, 0, 1);

            Button saveButton = new Button { Width = 120, Height = 34, Text = "保存并应用" };
            saveButton.Click += delegate { SaveAndClose(); };
            buttons.Controls.Add(saveButton);

            Button cancelButton = new Button { Width = 90, Height = 34, Text = "取消" };
            cancelButton.Click += delegate { DialogResult = DialogResult.Cancel; };
            buttons.Controls.Add(cancelButton);

            AcceptButton = saveButton;
            CancelButton = cancelButton;

            LoadProducts(configuration.ProductId);
            ShowConfiguration(configuration);
        }

        private static FlowLayoutPanel CreateVerticalFlow()
        {
            FlowLayoutPanel root = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = new Padding(10)
            };
            root.Resize += delegate
            {
                int width = root.ClientSize.Width - root.Padding.Left - root.Padding.Right;
                foreach (Control child in root.Controls)
                    child.Width = Math.Max(300, width - SystemInformation.VerticalScrollBarWidth);
            };
            return root;
        }

        private static GroupBox AddGroup(FlowLayoutPanel root, string text, int height)
        {
            GroupBox box = new GroupBox
            {
                Width = 700,
                Height = height,
                Margin = new Padding(0, 0, 0, 10),
                Text = text
            };
            root.Controls.Add(box);
            return box;
        }

        private static TableLayoutPanel CreateFormGrid(int rows)
        {
            TableLayoutPanel grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = rows,
                Padding = new Padding(12)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < rows; i++)
                grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / rows));
            return grid;
        }

        private TextBox AddSettingTextBox(TableLayoutPanel grid, int row, string label)
        {
            AddLabel(grid, row, label);
            TextBox textBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 0, 0)
            };
            grid.Controls.Add(textBox, 1, row);
            return textBox;
        }

        private TextBox AddReadOnlyTextBox(TableLayoutPanel grid, int row, string label)
        {
            TextBox textBox = AddSettingTextBox(grid, row, label);
            textBox.ReadOnly = true;
            textBox.BackColor = Color.White;
            return textBox;
        }

        private ComboBox AddComboBox(TableLayoutPanel grid, int row, string label)
        {
            AddLabel(grid, row, label);
            ComboBox comboBox = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 8, 0, 0)
            };
            grid.Controls.Add(comboBox, 1, row);
            return comboBox;
        }

        private NumericUpDown AddNumericSetting(TableLayoutPanel grid, int row, string label)
        {
            AddLabel(grid, row, label);
            NumericUpDown numeric = new NumericUpDown
            {
                Dock = DockStyle.Left,
                Minimum = 0,
                Maximum = 999999,
                Width = 140,
                Margin = new Padding(0, 8, 0, 0)
            };
            grid.Controls.Add(numeric, 1, row);
            return numeric;
        }

        private void AddLabel(TableLayoutPanel grid, int row, string label)
        {
            Label name = new Label
            {
                Dock = DockStyle.Fill,
                Text = label,
                TextAlign = ContentAlignment.MiddleLeft
            };
            grid.Controls.Add(name, 0, row);
        }

        private TextBox AddPathSettingTextBox(TableLayoutPanel grid, int row, string label, Action<TextBox> browseAction)
        {
            AddLabel(grid, row, label);

            TableLayoutPanel panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Margin = Padding.Empty
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
            grid.Controls.Add(panel, 1, row);

            TextBox textBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 8, 8, 0)
            };
            panel.Controls.Add(textBox, 0, 0);

            Button browseButton = new Button
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 6, 0, 4),
                Text = "打开"
            };
            browseButton.Click += delegate { browseAction(textBox); };
            panel.Controls.Add(browseButton, 1, 0);
            return textBox;
        }

        private Label AddDllVersionRow(TableLayoutPanel grid, int row)
        {
            AddLabel(grid, row, "DLL 版本");

            TableLayoutPanel panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Margin = Padding.Empty
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.Controls.Add(panel, 1, row);

            Button readButton = new Button
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 6, 8, 4),
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

        private void LoadProducts(int selectedProductId)
        {
            using (AppDbContext dbContext = new AppDbContext())
            {
                ProductService productService = new ProductService(dbContext);
                _products = productService.GetProducts();
            }

            _productComboBox.BeginUpdate();
            _productComboBox.Items.Clear();
            foreach (Product product in _products)
                _productComboBox.Items.Add(new Selection<Product>(BuildProductLabel(product), product));
            _productComboBox.EndUpdate();

            RefreshProductsGrid();

            SelectProduct(selectedProductId);
        }

        private void RefreshProductsGrid()
        {
            _productsGrid.DataSource = null;
            _productsGrid.DataSource = _products;
        }

        private void AddProductRow()
        {
            Product product = new Product();
            _products.Add(product);
            RefreshProductsGrid();

            int rowIndex = _products.Count - 1;
            if (rowIndex >= 0 && rowIndex < _productsGrid.Rows.Count)
            {
                _productsGrid.ClearSelection();
                _productsGrid.Rows[rowIndex].Selected = true;
                _productsGrid.CurrentCell = _productsGrid.Rows[rowIndex].Cells[1];
            }
        }

        private static string BuildProductLabel(Product product)
        {
            string part = string.IsNullOrWhiteSpace(product.CustomerPartNumber) ? "-" : product.CustomerPartNumber;
            return $"{product.Name} [{part}]";
        }

        private void SelectProduct(int productId)
        {
            if (productId <= 0)
            {
                _productComboBox.SelectedIndex = _productComboBox.Items.Count > 0 ? 0 : -1;
                return;
            }

            for (int i = 0; i < _productComboBox.Items.Count; i++)
            {
                Selection<Product> selection = (Selection<Product>)_productComboBox.Items[i];
                if (selection.Value.Id == productId)
                {
                    _productComboBox.SelectedIndex = i;
                    return;
                }
            }

            _productComboBox.SelectedIndex = _productComboBox.Items.Count > 0 ? 0 : -1;
        }

        private void ShowConfiguration(AppConfiguration configuration)
        {
            _machinePathTextBox.Text = configuration.MachinePath;
            _variableTextAliasTextBox.Text = configuration.VariableTextAlias;
            SelectCodeGenerator(configuration.CodeGeneratorType);
            _useFootPedal.Checked = configuration.UseFootPedal;
            _dllVersionLabel.Text = "未读取";
            _footPedalTimeoutSeconds.Value = Math.Max(
                _footPedalTimeoutSeconds.Minimum,
                Math.Min(_footPedalTimeoutSeconds.Maximum, configuration.FootPedalTimeoutMs / (decimal)1000));
            SelectProduct(configuration.ProductId);
        }

        private Product GetSelectedProduct()
        {
            return _productComboBox.SelectedItem is Selection<Product> selection ? selection.Value : null;
        }

        private void LoadSelectedProductForEdit()
        {
            if (_productsGrid.CurrentRow == null || _productsGrid.CurrentRow.DataBoundItem == null)
                return;

            _editingProduct = (Product)_productsGrid.CurrentRow.DataBoundItem;

            _productNameTextBox.Text = _editingProduct.Name;
            _customerPartNumberTextBox.Text = _editingProduct.CustomerPartNumber;
            _shipcodeBox.Value = Math.Max(_shipcodeBox.Minimum, Math.Min(_shipcodeBox.Maximum, _editingProduct.Shipcode));
            _productTemplatePathTextBox.Text = _editingProduct.TemplatePath;
            _productPatternTextBox.Text = _editingProduct.Pattern;
        }

        private void ClearProductEditor()
        {
            _editingProduct = null;
            _productNameTextBox.Clear();
            _customerPartNumberTextBox.Clear();
            _shipcodeBox.Value = 0;
            _productTemplatePathTextBox.Clear();
            _productPatternTextBox.Clear();
            _productsGrid.ClearSelection();
        }

        private void SaveProduct()
        {
            try
            {
                Product product = _editingProduct ?? new Product();
                product.Name = _productNameTextBox.Text.Trim();
                product.CustomerPartNumber = _customerPartNumberTextBox.Text.Trim();
                product.Shipcode = Convert.ToInt32(_shipcodeBox.Value);
                product.TemplatePath = _productTemplatePathTextBox.Text.Trim();
                product.Pattern = _productPatternTextBox.Text.Trim();

                ValidateProduct(product);

                using (AppDbContext dbContext = new AppDbContext())
                {
                    ProductService productService = new ProductService(dbContext);
                    if (product.Id == 0)
                        productService.AddProduct(product);
                    else
                        productService.UpdateProduct(product);
                }

                LoadProducts(product.Id);
                SelectProduct(product.Id);
                MessageBox.Show("产品已保存。", "产品配置", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"产品保存失败：{ex.Message}", "产品配置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void DeleteSelectedProduct()
        {
            if (_editingProduct == null || _editingProduct.Id == 0)
                return;

            DialogResult result = MessageBox.Show(
                $"确认删除产品 {_editingProduct.Name}？",
                "产品配置",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes)
                return;

            int deletedId = _editingProduct.Id;
            using (AppDbContext dbContext = new AppDbContext())
            {
                ProductService productService = new ProductService(dbContext);
                productService.DeleteProduct(deletedId);
            }

            ClearProductEditor();
            LoadProducts(0);
        }

        private static void ValidateProduct(Product product)
        {
            if (string.IsNullOrWhiteSpace(product.Name))
                throw new InvalidDataException("产品名称不能为空。");
            if (string.IsNullOrWhiteSpace(product.TemplatePath))
                throw new InvalidDataException("打标模板不能为空。");
            if (string.IsNullOrWhiteSpace(product.Pattern))
                throw new InvalidDataException("Pattern 不能为空。");
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
                if (string.Equals(_codeGeneratorComboBox.Items[i].ToString(), value, StringComparison.OrdinalIgnoreCase))
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
                Product product = GetSelectedProduct();
                if (product == null)
                    throw new InvalidDataException("请先选择产品。");

                AppConfiguration configuration = new AppConfiguration
                {
                    MachinePath = _machinePathTextBox.Text.Trim(),
                    TemplatePath = product.TemplatePath,
                    VariableTextAlias = _variableTextAliasTextBox.Text.Trim(),
                    UseFootPedal = _useFootPedal.Checked,
                    FootPedalTimeoutMs = Convert.ToInt32(_footPedalTimeoutSeconds.Value) * 1000,
                    CodeGeneratorType = _codeGeneratorComboBox.SelectedItem == null
                        ? AppConfiguration.DefaultCodeGeneratorType
                        : _codeGeneratorComboBox.SelectedItem.ToString(),
                    ProductId = product.Id
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
