using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace HansLaserDateSerialDemo
{
    internal sealed class SettingsDialog : Form
    {
        private readonly TextBox _machinePathTextBox;
        private readonly TextBox _variableTextAliasTextBox;
        private readonly ComboBox _productComboBox;
        private readonly CheckBox _useFootPedal;
        private readonly NumericUpDown _footPedalTimeoutSeconds;
        private readonly Label _dllVersionLabel;
        private readonly DataGridView _productsGrid;
        private readonly TextBox _productNameTextBox;
        private readonly TextBox _customerPartNumberTextBox;
        private readonly NumericUpDown _shipcodeBox;
        private readonly NumericUpDown _serialStartValueBox;
        private readonly ComboBox _productCodeGeneratorComboBox;
        private readonly TextBox _productTemplatePathTextBox;
        private readonly TextBox _productPatternTextBox;

        private List<Product> _products = new List<Product>();
        private Product _editingProduct;
        private bool _refreshingProductsGrid;
        private readonly SvgPathIcon _addIcon = new SvgPathIcon("M5 12h14m-7 7V5");

        private readonly SvgPathIcon _deleteIcon =
            new SvgPathIcon(
                "m5 6l.876 13.133A2 2 0 0 0 7.87 21h8.258a2 2 0 0 0 1.995-1.867L19 6M8 6l.772-2.316A1 1 0 0 1 9.721 3h4.558a1 1 0 0 1 .949.684L16 6m-6 5v5m4-5v5M4 6h16");

        public AppConfiguration Configuration { get; private set; }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            SaveAndClose();
        }

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

            GroupBox generatorBox = AddGroup(settingsRoot, "产品", 82);
            TableLayoutPanel generatorGrid = CreateFormGrid(1);
            generatorBox.Controls.Add(generatorGrid);

            _productComboBox = AddComboBox(generatorGrid, 0, "选择产品");

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
            productsRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 380));
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
            _productsGrid.Columns.Add(new DataGridViewButtonColumn
                { HeaderText = "新增", UseColumnTextForButtonValue = false, Width = 36 });
            _productsGrid.Columns.Add(new DataGridViewButtonColumn
                { HeaderText = "删除", UseColumnTextForButtonValue = false, Width = 36 });
            _productsGrid.Columns.Add(new DataGridViewTextBoxColumn
                { HeaderText = "名称", DataPropertyName = "Name", Width = 150 });
            _productsGrid.Columns.Add(new DataGridViewTextBoxColumn
                { HeaderText = "客户料号", DataPropertyName = "CustomerPartNumber", Width = 150 });
            _productsGrid.Columns.Add(new DataGridViewTextBoxColumn
                { HeaderText = "Shipcode", DataPropertyName = "Shipcode", Width = 90 });
            _productsGrid.Columns.Add(new DataGridViewTextBoxColumn
                { HeaderText = "起始流水", DataPropertyName = "SerialStartValue", Width = 90 });
            _productsGrid.Columns.Add(new DataGridViewTextBoxColumn
                { HeaderText = "生成器", DataPropertyName = "CodeGeneratorType", Width = 100 });
            _productsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "模板", DataPropertyName = "TemplatePath",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 150
            });
            _productsGrid.Columns.Add(new DataGridViewTextBoxColumn
                { HeaderText = "Pattern", DataPropertyName = "Pattern", Width = 100 });
            _productsGrid.CellContentClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == 0)
                    AddProductRow();
                else if (e.RowIndex >= 0 && e.ColumnIndex == 1)
                    BeginInvoke(new Action(delegate { DeleteProductAtRow(e.RowIndex); }));
            };
            _productsGrid.CellPainting += PaintProductActionIcon;
            _productsGrid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e)
            {
                e.ThrowException = false;
            };
            _productsGrid.SelectionChanged += delegate
            {
                if (!_refreshingProductsGrid)
                    LoadSelectedProductForEdit();
            };
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

            TableLayoutPanel editorGrid = CreateFormGrid(7);
            editorGrid.ColumnStyles[0].Width = 110;
            editorRoot.Controls.Add(editorGrid, 0, 0);

            _productNameTextBox = AddSettingTextBox(editorGrid, 0, "名称");
            _customerPartNumberTextBox = AddSettingTextBox(editorGrid, 1, "客户料号");
            _shipcodeBox = AddNumericSetting(editorGrid, 2, "Shipcode");
            _serialStartValueBox = AddNumericSetting(editorGrid, 3, "起始流水");
            _serialStartValueBox.Minimum = 1;
            _serialStartValueBox.Maximum = 9999;
            _productCodeGeneratorComboBox = AddComboBox(editorGrid, 4, "生成器");
            _productCodeGeneratorComboBox.Items.Add(CodeGeneratorTypes.EcoFlow);
            _productCodeGeneratorComboBox.Items.Add(CodeGeneratorTypes.Normal);
            _productTemplatePathTextBox = AddPathSettingTextBox(
                editorGrid,
                5,
                "打标模板",
                delegate(TextBox textBox) { BrowseFile(textBox, "选择打标模板", "打标模板 (*.HS)|*.HS|所有文件 (*.*)|*.*"); });
            _productPatternTextBox = AddSettingTextBox(editorGrid, 6, "Pattern");

            FlowLayoutPanel productButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 6, 12, 0)
            };
            editorRoot.Controls.Add(productButtons, 0, 1);

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

            // Button saveButton = new Button { Width = 120, Height = 34, Text = "保存并应用" };
            // saveButton.Click += delegate { SaveAndClose(); };
            // buttons.Controls.Add(saveButton);
            //
            // Button cancelButton = new Button { Width = 90, Height = 34, Text = "取消" };
            // cancelButton.Click += delegate { DialogResult = DialogResult.Cancel; };
            // buttons.Controls.Add(cancelButton);
            //
            // AcceptButton = saveButton;
            // CancelButton = cancelButton;

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
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
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

        private TextBox AddPathSettingTextBox(TableLayoutPanel grid, int row, string label,
            Action<TextBox> browseAction)
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

            RefreshProductComboBox(selectedProductId);
            RefreshProductsGrid();
        }

        private void RefreshProductComboBox(int selectedProductId)
        {
            _productComboBox.BeginUpdate();
            _productComboBox.Items.Clear();
            foreach (Product product in _products)
            {
                if (product.Id > 0)
                    _productComboBox.Items.Add(new Selection<Product>(BuildProductLabel(product), product));
            }

            _productComboBox.EndUpdate();
            SelectProduct(selectedProductId);
        }

        private void RefreshProductsGrid()
        {
            _refreshingProductsGrid = true;
            try
            {
                _productsGrid.DataSource = null;
                _productsGrid.DataSource = _products;
                if (_products.Count == 0)
                    _productsGrid.ClearSelection();
            }
            finally
            {
                _refreshingProductsGrid = false;
            }
        }

        private void PaintProductActionIcon(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || (e.ColumnIndex != 0 && e.ColumnIndex != 1))
                return;

            e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);

            Rectangle iconBounds = new Rectangle(
                e.CellBounds.Left + (e.CellBounds.Width - 20) / 2,
                e.CellBounds.Top + (e.CellBounds.Height - 20) / 2,
                20,
                20);

            Color color = e.ColumnIndex == 0
                ? Color.FromArgb(35, 120, 70)
                : Color.FromArgb(180, 60, 55);
            SvgPathIcon icon = e.ColumnIndex == 0 ? _addIcon : _deleteIcon;
            icon.Draw(e.Graphics, iconBounds, color);
            e.Handled = true;
        }

        private void AddProductRow()
        {
            Product product = new Product();
            _products.Add(product);
            RefreshProductsGrid();

            int rowIndex = _products.Count - 1;
            BeginInvoke(new Action(delegate { FocusProductRow(rowIndex); }));
        }

        private void FocusProductRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _products.Count || rowIndex >= _productsGrid.Rows.Count)
                return;

            if (_productsGrid.DataSource != null && _productsGrid.BindingContext != null)
            {
                CurrencyManager currencyManager =
                    _productsGrid.BindingContext[_productsGrid.DataSource] as CurrencyManager;
                if (currencyManager == null || currencyManager.Count <= rowIndex)
                    return;

                currencyManager.Position = rowIndex;
            }

            _productsGrid.ClearSelection();
            _productsGrid.CurrentCell = _productsGrid.Rows[rowIndex].Cells[2];
            _productsGrid.Rows[rowIndex].Selected = true;
            _productsGrid.Focus();
            LoadSelectedProductForEdit();
        }

        private void DeleteProductAtRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _products.Count)
                return;

            Product product = _products[rowIndex];
            if (product == null)
                return;

            if (product.Id <= 0)
            {
                RemoveProductFromGrid(product, 0);
                return;
            }

            if (product.Id > 0)
            {
                DialogResult result = MessageBox.Show(
                    $"确认删除产品 {product.Name}？",
                    "产品配置",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                if (result != DialogResult.Yes)
                    return;

                using (AppDbContext dbContext = new AppDbContext())
                {
                    ProductService productService = new ProductService(dbContext);
                    productService.DeleteProduct(product.Id);
                }
            }

            RemoveProductFromGrid(product, 0);
        }

        private void RemoveProductFromGrid(Product product, int selectedProductId)
        {
            bool wasEditing = ReferenceEquals(_editingProduct, product);
            _products.Remove(product);
            RefreshProductComboBox(selectedProductId);
            RefreshProductsGrid();
            if (wasEditing)
                ClearProductEditor();
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
            if (_refreshingProductsGrid || _productsGrid.CurrentRow == null)
                return;

            int rowIndex = _productsGrid.CurrentRow.Index;
            if (rowIndex < 0 || rowIndex >= _products.Count)
                return;

            _editingProduct = _products[rowIndex];

            _productNameTextBox.Text = _editingProduct.Name;
            _customerPartNumberTextBox.Text = _editingProduct.CustomerPartNumber;
            _shipcodeBox.Value =
                Math.Max(_shipcodeBox.Minimum, Math.Min(_shipcodeBox.Maximum, _editingProduct.Shipcode));
            _serialStartValueBox.Value = Math.Max(_serialStartValueBox.Minimum,
                Math.Min(_serialStartValueBox.Maximum,
                    _editingProduct.SerialStartValue <= 0 ? 1 : _editingProduct.SerialStartValue));
            SelectProductCodeGenerator(_editingProduct.CodeGeneratorType);
            _productTemplatePathTextBox.Text = _editingProduct.TemplatePath;
            _productPatternTextBox.Text = _editingProduct.Pattern;
        }

        private void ClearProductEditor()
        {
            _editingProduct = null;
            _productNameTextBox.Clear();
            _customerPartNumberTextBox.Clear();
            _shipcodeBox.Value = 0;
            _serialStartValueBox.Value = 1;
            SelectProductCodeGenerator(CodeGeneratorTypes.EcoFlow);
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
                product.SerialStartValue = Convert.ToInt32(_serialStartValueBox.Value);
                product.CodeGeneratorType = _productCodeGeneratorComboBox.SelectedItem == null
                    ? CodeGeneratorTypes.EcoFlow
                    : _productCodeGeneratorComboBox.SelectedItem.ToString();
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

        private static void ValidateProduct(Product product)
        {
            if (string.IsNullOrWhiteSpace(product.Name))
                throw new InvalidDataException("产品名称不能为空。");
            if (string.IsNullOrWhiteSpace(product.TemplatePath))
                throw new InvalidDataException("打标模板不能为空。");
            if (string.IsNullOrWhiteSpace(product.Pattern))
                throw new InvalidDataException("Pattern 不能为空。");
            if (product.SerialStartValue < 1 || product.SerialStartValue > 9999)
                throw new InvalidDataException("起始流水必须在 1-9999 之间。");
            if (!IsCodeGeneratorTypeValid(product.CodeGeneratorType))
                throw new InvalidDataException("生成器无效。");
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

        private void SelectProductCodeGenerator(string generatorType)
        {
            string value = string.IsNullOrWhiteSpace(generatorType)
                ? CodeGeneratorTypes.EcoFlow
                : generatorType;

            for (int i = 0; i < _productCodeGeneratorComboBox.Items.Count; i++)
            {
                if (string.Equals(_productCodeGeneratorComboBox.Items[i].ToString(), value,
                        StringComparison.OrdinalIgnoreCase))
                {
                    _productCodeGeneratorComboBox.SelectedIndex = i;
                    return;
                }
            }

            _productCodeGeneratorComboBox.SelectedIndex = 0;
        }

        private static bool IsCodeGeneratorTypeValid(string generatorType)
        {
            return string.Equals(generatorType, CodeGeneratorTypes.Normal, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(generatorType, CodeGeneratorTypes.EcoFlow, StringComparison.OrdinalIgnoreCase);
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

        private sealed class SvgPathIcon
        {
            private static readonly Regex TokenRegex =
                new Regex(@"[A-Za-z]|[-+]?(?:\d*\.\d+|\d+)(?:[eE][-+]?\d+)?", RegexOptions.Compiled);

            private readonly string _pathData;

            public SvgPathIcon(string pathData)
            {
                _pathData = pathData;
            }

            public void Draw(Graphics graphics, Rectangle bounds, Color color)
            {
                using (GraphicsPath path = BuildPath())
                using (Matrix matrix = new Matrix())
                using (Pen pen = new Pen(color, 2F)
                       {
                           StartCap = LineCap.Round,
                           EndCap = LineCap.Round,
                           LineJoin = LineJoin.Round
                       })
                {
                    matrix.Translate(bounds.Left, bounds.Top);
                    matrix.Scale(bounds.Width / 24F, bounds.Height / 24F);

                    GraphicsState state = graphics.Save();
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.Transform = matrix;
                    graphics.DrawPath(pen, path);
                    graphics.Restore(state);
                }
            }

            private GraphicsPath BuildPath()
            {
                List<string> tokens = TokenRegex.Matches(_pathData)
                    .Cast<Match>()
                    .Select(match => match.Value)
                    .ToList();

                GraphicsPath path = new GraphicsPath();
                PointF current = PointF.Empty;
                PointF figureStart = PointF.Empty;
                char command = '\0';
                int index = 0;

                while (index < tokens.Count)
                {
                    if (IsCommand(tokens[index]))
                    {
                        command = tokens[index][0];
                        index++;
                    }

                    switch (command)
                    {
                        case 'M':
                        case 'm':
                            current = ReadPoint(tokens, ref index, current, command == 'm');
                            figureStart = current;
                            path.StartFigure();
                            command = command == 'm' ? 'l' : 'L';
                            break;
                        case 'L':
                        case 'l':
                            AddLine(path, ref current, ReadPoint(tokens, ref index, current, command == 'l'));
                            break;
                        case 'H':
                        case 'h':
                            AddLine(path, ref current, new PointF(
                                command == 'h'
                                    ? current.X + ReadNumber(tokens, ref index)
                                    : ReadNumber(tokens, ref index),
                                current.Y));
                            break;
                        case 'V':
                        case 'v':
                            AddLine(path, ref current, new PointF(
                                current.X,
                                command == 'v'
                                    ? current.Y + ReadNumber(tokens, ref index)
                                    : ReadNumber(tokens, ref index)));
                            break;
                        case 'A':
                        case 'a':
                            SkipArcArgumentsAndLineToEnd(path, tokens, ref index, ref current, command == 'a');
                            break;
                        case 'Z':
                        case 'z':
                            AddLine(path, ref current, figureStart);
                            path.CloseFigure();
                            break;
                        default:
                            index++;
                            break;
                    }
                }

                return path;
            }

            private static void SkipArcArgumentsAndLineToEnd(
                GraphicsPath path,
                List<string> tokens,
                ref int index,
                ref PointF current,
                bool relative)
            {
                ReadNumber(tokens, ref index);
                ReadNumber(tokens, ref index);
                ReadNumber(tokens, ref index);
                ReadNumber(tokens, ref index);
                ReadNumber(tokens, ref index);
                PointF end = ReadPoint(tokens, ref index, current, relative);
                AddLine(path, ref current, end);
            }

            private static PointF ReadPoint(List<string> tokens, ref int index, PointF current, bool relative)
            {
                float x = ReadNumber(tokens, ref index);
                float y = ReadNumber(tokens, ref index);
                return relative ? new PointF(current.X + x, current.Y + y) : new PointF(x, y);
            }

            private static float ReadNumber(List<string> tokens, ref int index)
            {
                return float.Parse(tokens[index++], System.Globalization.CultureInfo.InvariantCulture);
            }

            private static void AddLine(GraphicsPath path, ref PointF current, PointF next)
            {
                path.AddLine(current, next);
                current = next;
            }

            private static bool IsCommand(string token)
            {
                return token.Length == 1 && char.IsLetter(token[0]);
            }
        }
    }
}