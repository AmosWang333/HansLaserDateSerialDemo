using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace HansLaserDateSerialDemo
{
    internal enum SettingsPage
    {
        RunSettings,
        ProductConfiguration
    }

    internal sealed class SettingsDialog : Form
    {
        private const string ProductActionColumnName = "ProductActions";

        private readonly TextBox _machinePathTextBox;
        private readonly TextBox _variableTextAliasTextBox;
        private readonly ComboBox _productComboBox;
        private readonly CheckBox _useFootPedal;
        private readonly NumericUpDown _footPedalTimeoutSeconds;
        private readonly Label _dllVersionLabel;
        private readonly DataGridView _productsGrid;
        private readonly SettingsPage _initialPage;

        private List<Product> _products = new List<Product>();
        private readonly SvgPathIcon _editIcon = new SvgPathIcon("M4 20l4-1 11-11-3-3L5 16l-1 4M14 5l3 3");

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
            : this(configuration, SettingsPage.RunSettings)
        {
        }

        public SettingsDialog(AppConfiguration configuration, SettingsPage initialPage)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            _initialPage = initialPage;
            Text = initialPage == SettingsPage.ProductConfiguration ? "产品配置" : "运行设置";
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
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute,
                initialPage == SettingsPage.ProductConfiguration ? 0 : 56));
            Controls.Add(shell);

            FlowLayoutPanel settingsRoot = CreateVerticalFlow();

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

            TableLayoutPanel productsRoot = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(4)
            };
            productsRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            productsRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            FlowLayoutPanel productToolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            productsRoot.Controls.Add(productToolbar, 0, 0);

            Button addProductButton = new Button
            {
                Width = 90,
                Height = 26,
                Margin = new Padding(0, 0, 0, 4),
                Text = "新增"
            };
            addProductButton.Click += delegate { OpenProductEditor(null); };
            productToolbar.Controls.Add(addProductButton);

            _productsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                MultiSelect = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                ColumnHeadersHeight = 28,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
            };
            _productsGrid.Columns.Add(new DataGridViewTextBoxColumn
                { HeaderText = "名称", DataPropertyName = "Name", Width = 130 });
            _productsGrid.Columns.Add(new DataGridViewTextBoxColumn
                { HeaderText = "客户料号", DataPropertyName = "CustomerPartNumber", Width = 130 });
            _productsGrid.Columns.Add(new DataGridViewTextBoxColumn
                { HeaderText = "Shipcode", DataPropertyName = "Shipcode", Width = 75 });
            _productsGrid.Columns.Add(new DataGridViewTextBoxColumn
                { HeaderText = "起始流水", DataPropertyName = "SerialStartValue", Width = 75 });
            _productsGrid.Columns.Add(new DataGridViewTextBoxColumn
                { HeaderText = "生成器", DataPropertyName = "CodeGeneratorType", Width = 90 });
            _productsGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "模板", DataPropertyName = "TemplatePath",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 80
            });
            _productsGrid.Columns.Add(new DataGridViewTextBoxColumn
                { HeaderText = "Pattern", DataPropertyName = "Pattern", Width = 80 });
            _productsGrid.Columns.Add(new DataGridViewButtonColumn
            {
                Name = ProductActionColumnName,
                HeaderText = string.Empty,
                UseColumnTextForButtonValue = false,
                Width = 64,
                MinimumWidth = 64,
                Resizable = DataGridViewTriState.False
            });
            _productsGrid.CellMouseClick += delegate(object sender, DataGridViewCellMouseEventArgs e)
            {
                HandleProductActionCellClick(e);
            };
            _productsGrid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0 && !IsProductActionColumn(e.ColumnIndex))
                    OpenProductEditorAtRow(e.RowIndex);
            };
            _productsGrid.CellPainting += PaintProductActionIcon;
            _productsGrid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e)
            {
                e.ThrowException = false;
            };
            productsRoot.Controls.Add(_productsGrid, 0, 1);

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
            shell.Controls.Add(initialPage == SettingsPage.ProductConfiguration
                ? (Control)productsRoot
                : settingsRoot, 0, 0);
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
            _productsGrid.DataSource = null;
            _productsGrid.DataSource = _products;
            if (_products.Count == 0)
                _productsGrid.ClearSelection();
        }

        private void PaintProductActionIcon(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || !IsProductActionColumn(e.ColumnIndex))
                return;

            e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);

            GetProductActionIconBounds(e.CellBounds, out Rectangle editBounds, out Rectangle deleteBounds);
            _editIcon.Draw(e.Graphics, editBounds, Color.FromArgb(45, 95, 170));
            _deleteIcon.Draw(e.Graphics, deleteBounds, Color.FromArgb(180, 60, 55));
            e.Handled = true;
        }

        private void HandleProductActionCellClick(DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || !IsProductActionColumn(e.ColumnIndex))
                return;

            Rectangle cellBounds = _productsGrid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
            GetProductActionIconBounds(cellBounds, out Rectangle editBounds, out Rectangle deleteBounds);
            Point clickPoint = new Point(cellBounds.Left + e.X, cellBounds.Top + e.Y);

            if (deleteBounds.Contains(clickPoint))
            {
                BeginInvoke(new Action(delegate { DeleteProductAtRow(e.RowIndex); }));
                return;
            }

            if (editBounds.Contains(clickPoint))
                OpenProductEditorAtRow(e.RowIndex);
        }

        private bool IsProductActionColumn(int columnIndex)
        {
            return columnIndex >= 0 &&
                   columnIndex < _productsGrid.Columns.Count &&
                   string.Equals(_productsGrid.Columns[columnIndex].Name, ProductActionColumnName,
                       StringComparison.Ordinal);
        }

        private static void GetProductActionIconBounds(Rectangle cellBounds, out Rectangle editBounds,
            out Rectangle deleteBounds)
        {
            int iconSize = 18;
            int gap = 10;
            int totalWidth = iconSize * 2 + gap;
            int left = cellBounds.Left + Math.Max(0, (cellBounds.Width - totalWidth) / 2);
            int top = cellBounds.Top + Math.Max(0, (cellBounds.Height - iconSize) / 2);

            editBounds = new Rectangle(left, top, iconSize, iconSize);
            deleteBounds = new Rectangle(left + iconSize + gap, top, iconSize, iconSize);
        }

        private void OpenProductEditorAtRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _products.Count)
                return;

            OpenProductEditor(_products[rowIndex]);
        }

        private void OpenProductEditor(Product source)
        {
            Product draft = source == null ? new Product() : CopyProduct(source);
            using (ProductEditorDialog dialog = new ProductEditorDialog(draft))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                SaveProduct(dialog.Product);
            }
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
                    GetRootOwner(this),
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
            _products.Remove(product);
            RefreshProductComboBox(selectedProductId);
            RefreshProductsGrid();
        }

        private static Product CopyProduct(Product product)
        {
            return new Product
            {
                Id = product.Id,
                Name = product.Name,
                CustomerPartNumber = product.CustomerPartNumber,
                Shipcode = product.Shipcode,
                SerialStartValue = product.SerialStartValue,
                CodeGeneratorType = product.CodeGeneratorType,
                TemplatePath = product.TemplatePath,
                Pattern = product.Pattern
            };
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

        private void SaveProduct(Product product)
        {
            try
            {
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
                MessageBox.Show(GetRootOwner(this), "产品已保存。", "产品配置", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(GetRootOwner(this), $"产品保存失败：{ex.Message}", "产品配置", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
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

        private static bool IsCodeGeneratorTypeValid(string generatorType)
        {
            return string.Equals(generatorType, CodeGeneratorTypes.Normal, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(generatorType, CodeGeneratorTypes.EcoFlow, StringComparison.OrdinalIgnoreCase);
        }

        private void SaveAndClose()
        {
            if (_initialPage != SettingsPage.RunSettings)
                return;

            try
            {
                Product product = GetSelectedProduct();
                if (product == null)
                    return;

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
                MessageBox.Show(GetRootOwner(this), $"设置无效：{ex.Message}", "设置", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private static IWin32Window GetRootOwner(Form form)
        {
            Form owner = form;
            while (owner != null && owner.Owner != null)
                owner = owner.Owner;

            return owner ?? form;
        }

        private sealed class ProductEditorDialog : Form
        {
            private readonly TextBox _nameTextBox;
            private readonly TextBox _customerPartNumberTextBox;
            private readonly NumericUpDown _shipcodeBox;
            private readonly NumericUpDown _serialStartValueBox;
            private readonly ComboBox _codeGeneratorComboBox;
            private readonly TextBox _templatePathTextBox;
            private readonly TextBox _patternTextBox;

            public Product Product { get; private set; }

            public ProductEditorDialog(Product product)
            {
                Product = product ?? throw new ArgumentNullException(nameof(product));

                Text = Product.Id == 0 ? "新增产品" : "编辑产品";
                StartPosition = FormStartPosition.CenterParent;
                MinimumSize = new Size(620, 420);
                Size = new Size(700, 460);
                Font = new Font("Microsoft YaHei UI", 9F);

                TableLayoutPanel shell = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 2,
                    Padding = new Padding(14)
                };
                shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
                Controls.Add(shell);

                TableLayoutPanel grid = CreateDialogFormGrid(7);
                grid.ColumnStyles[0].Width = 110;
                shell.Controls.Add(grid, 0, 0);

                _nameTextBox = AddDialogTextBox(grid, 0, "名称");
                _customerPartNumberTextBox = AddDialogTextBox(grid, 1, "客户料号");
                _shipcodeBox = AddDialogNumeric(grid, 2, "Shipcode", 0, 999999);
                _serialStartValueBox = AddDialogNumeric(grid, 3, "起始流水", 1, 9999);
                _codeGeneratorComboBox = AddDialogComboBox(grid, 4, "生成器");
                _codeGeneratorComboBox.Items.Add(CodeGeneratorTypes.EcoFlow);
                _codeGeneratorComboBox.Items.Add(CodeGeneratorTypes.Normal);
                _templatePathTextBox = AddDialogPathTextBox(grid, 5, "打标模板",
                    delegate(TextBox textBox) { BrowseFile(textBox, "选择打标模板", "打标模板 (*.HS)|*.HS|所有文件 (*.*)|*.*"); });
                _patternTextBox = AddDialogTextBox(grid, 6, "Pattern");

                FlowLayoutPanel buttons = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.RightToLeft,
                    WrapContents = false,
                    Padding = new Padding(0, 10, 0, 0)
                };
                shell.Controls.Add(buttons, 0, 1);

                Button saveButton = new Button { Width = 90, Height = 32, Text = "保存" };
                saveButton.Click += delegate { SaveAndClose(); };
                buttons.Controls.Add(saveButton);

                Button cancelButton = new Button { Width = 90, Height = 32, Text = "取消" };
                cancelButton.Click += delegate { DialogResult = DialogResult.Cancel; };
                buttons.Controls.Add(cancelButton);

                AcceptButton = saveButton;
                CancelButton = cancelButton;

                ShowProduct();
            }

            private void ShowProduct()
            {
                _nameTextBox.Text = Product.Name;
                _customerPartNumberTextBox.Text = Product.CustomerPartNumber;
                _shipcodeBox.Value = Math.Max(_shipcodeBox.Minimum, Math.Min(_shipcodeBox.Maximum, Product.Shipcode));
                _serialStartValueBox.Value = Math.Max(_serialStartValueBox.Minimum,
                    Math.Min(_serialStartValueBox.Maximum, Product.SerialStartValue <= 0 ? 1 : Product.SerialStartValue));
                SelectCodeGenerator(Product.CodeGeneratorType);
                _templatePathTextBox.Text = Product.TemplatePath;
                _patternTextBox.Text = Product.Pattern;
            }

            private void SaveAndClose()
            {
                try
                {
                    Product.Name = _nameTextBox.Text.Trim();
                    Product.CustomerPartNumber = _customerPartNumberTextBox.Text.Trim();
                    Product.Shipcode = Convert.ToInt32(_shipcodeBox.Value);
                    Product.SerialStartValue = Convert.ToInt32(_serialStartValueBox.Value);
                    Product.CodeGeneratorType = _codeGeneratorComboBox.SelectedItem == null
                        ? CodeGeneratorTypes.EcoFlow
                        : _codeGeneratorComboBox.SelectedItem.ToString();
                    Product.TemplatePath = _templatePathTextBox.Text.Trim();
                    Product.Pattern = _patternTextBox.Text.Trim();

                    ValidateProduct(Product);
                    DialogResult = DialogResult.OK;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(GetRootOwner(this), $"产品设置无效：{ex.Message}", "产品配置",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            private void SelectCodeGenerator(string generatorType)
            {
                string value = string.IsNullOrWhiteSpace(generatorType)
                    ? CodeGeneratorTypes.EcoFlow
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

            private static TableLayoutPanel CreateDialogFormGrid(int rows)
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

            private static void AddDialogLabel(TableLayoutPanel grid, int row, string label)
            {
                Label name = new Label
                {
                    Dock = DockStyle.Fill,
                    Text = label,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                grid.Controls.Add(name, 0, row);
            }

            private static TextBox AddDialogTextBox(TableLayoutPanel grid, int row, string label)
            {
                AddDialogLabel(grid, row, label);
                TextBox textBox = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Margin = new Padding(0, 8, 0, 0)
                };
                grid.Controls.Add(textBox, 1, row);
                return textBox;
            }

            private static NumericUpDown AddDialogNumeric(TableLayoutPanel grid, int row, string label,
                decimal minimum, decimal maximum)
            {
                AddDialogLabel(grid, row, label);
                NumericUpDown numeric = new NumericUpDown
                {
                    Dock = DockStyle.Left,
                    Minimum = minimum,
                    Maximum = maximum,
                    Width = 140,
                    Margin = new Padding(0, 8, 0, 0)
                };
                grid.Controls.Add(numeric, 1, row);
                return numeric;
            }

            private static ComboBox AddDialogComboBox(TableLayoutPanel grid, int row, string label)
            {
                AddDialogLabel(grid, row, label);
                ComboBox comboBox = new ComboBox
                {
                    Dock = DockStyle.Fill,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Margin = new Padding(0, 8, 0, 0)
                };
                grid.Controls.Add(comboBox, 1, row);
                return comboBox;
            }

            private TextBox AddDialogPathTextBox(TableLayoutPanel grid, int row, string label,
                Action<TextBox> browseAction)
            {
                AddDialogLabel(grid, row, label);

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
