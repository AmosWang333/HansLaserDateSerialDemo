# 大族激光“日期码 + 四位流水号”二次开发示例

目标编码：`年码 + 月码 + 日码 + 0001~9999`。

示例：2026-07-14 第 1 件 => `J7E0001`。

## 适用接口

按《二次开发接口》中的 `HansAdvInterface.dll` 示例，使用：

- `HS_InitialMachine`
- `HS_LoadMarkFile`
- `HS_ChangeTextByNameW`（缺失时回退 `HS_ChangeTextByName`）
- `HS_Mark`
- `HS_IsMarkEnd`
- `HS_MarkStop`
- `HS_GetMarkTime`
- `HS_CloseMachine`

## 1. 先做模板

在标准打标软件中：

1. 完成 BOX 校正、位置校正、激光器参数和层参数设置。
2. 新建一个文本对象，并设置为“可变文本/模型文本”。
3. 把可变文本别名设置为 `CODE`。
4. 保存为 `C:\HansMark\Templates\DateSerial.HS`。
5. 退出标准打标软件和校正软件；接口程序不能与其同时占用设备。

## 2. 修改现场配置

启动程序后点击工具栏中的“设置”按钮，在设置弹窗中按字段修改配置，点击“保存并应用”后会写入程序目录下的 `config.json`
，并重新初始化设备、加载模板：

```json
{
  "MachinePath": "C:\\HansLaser\\Marking",
  "TemplatePath": "C:\\HansMark\\Templates\\DateSerial.HS",
  "VariableTextAlias": "CODE",
  "UseFootPedal": false,
  "FootPedalTimeoutMs": 600000
}
```

路径尽量只使用英文、数字和反斜杠，避免旧版 ANSI 接口无法识别中文路径。

## 3. 编译

推荐 Visual Studio：

1. 打开 `HansLaserDateSerialDemo.csproj`。
2. 安装/启用 .NET Framework 4.8 Targeting Pack。
3. 配置使用 `x86` 编译。若现场 DLL 明确为 64 位，再改为 x64。
4. 生成并运行。

出现 `BadImageFormatException` 通常代表程序与原生 DLL 位数不一致。

## 4. 运行逻辑

程序启动后先通过工具栏“设置”打开弹窗并点击“保存并应用”。应用成功后，界面会显示当前编号和操作流程：

- `P 红光预览`：红光预览，不提交流水号。
- `M 激光打标`：激光打标。只有 `HS_IsMarkEnd` 返回 1（正常结束）才提交。
- `S 已用/跳过`：操作员确认该编号已打或应跳过，然后进入下一个编号。
- `Q 退出`：退出；当前待确认编号保留，下次启动继续提示。

程序先把编号写入 `sequence.state` 作为“待确认编号”，再调用激光。这样突然断电后不会静默地把该编号重新分配给下一件。断电恢复后仍需检查工件/MES，人工决定重打还是确认已用/跳过。

## 5. 脚踏触发

在设置弹窗的“脚踏触发”区域修改 `UseFootPedal` 和 `FootPedalTimeoutMs`，然后点击“保存并应用”。`FootPedalTimeoutMs`
在界面中按秒输入，保存到 `config.json` 时会转换为毫秒。按 `M 激光打标` 后，`HS_Mark` 会按该配置决定是否等待脚踏/触发信号，超时值由
`FootPedalTimeoutMs` 控制。

## 6. 生产安全

首次测试只使用红光预览，确认文本、位置、尺寸和方向正确后，再在废料上低风险试打。必须保留防护罩、门禁、急停和现场既有激光安全联锁；不要用软件逻辑替代硬件安全回路。

## 7. 产品配置（Pending）

产品字段如下表所示：

| field              | type   | description              | required                 |
|--------------------|--------|--------------------------|--------------------------|
| id                 | int    | primary key              | yes(managed by database) | 
| name               | string | 产品名称                 | no(used for display)     |
| customerPartNumber | string | 客户件号                 | no(used for display)     |
| shipcode           | int    | 发运代码                 | yes                      |
| templatePath       | string | 模板文件地址             | yes                      |
| pattern            | string | 用于生成编码的格式表达式 | yes                      |

## 8. 打标历史（Pending）

打标记录字段如下表所示：

| field     | type     | description        | required                 |
|-----------|----------|--------------------|--------------------------|
| id        | int      | primary key        | yes(managed by database) |
| code      | string   | 用于打标的明码内容 | yes                      |
| timestamp | DateTime | 打标时间           | yes                      |
| product   | Product  | 产品信息           | yes                      |
