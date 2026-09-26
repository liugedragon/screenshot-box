# ScreenshotBox.Core

简体中文 · [English](README.md)

`LibraryStore`管理元数据和OCR任务状态。调用`AddAsync`前，截图和缩略图文件应已经存在，使用唯一的相对路径，入库后保持不变。移入回收站不删除图片文件。

## 搜索

首版在标题、备注、标签和识别文字上使用参数化SQLite查询`instr(lower(column), lower(query))`。这是字面子串匹配，包含一字和两字中文；引号、`%`、`_`不会成为SQL语法或通配符。ASCII字母不区分大小写。查询会扫描记录，不支持模糊、语义或拼音检索。

筛选包括`all`、`favorites`（`favorite`）、`trash`（`deleted`）、`recent`（过去七天，UTC）、`pending`（Pending或Processing）和`failed`。先按UTC日期排序（默认最新优先，`oldestFirst`为最早优先），再按ID升序，最后应用`limit`和`offset`。单次查询上限1,000,000，界面应分页。资料JSON和可查询字段在同一个事务中更新。

可选的`requiredTag`按完整标签成员分类，在分页前与关键词及其他条件取交集。SQLite运行注册的确定性成员函数，不使用`LIKE`。该函数和`GetTagsAsync`均按中英文逗号分隔、清除两侧空白、使用.NET ordinal规则忽略大小写；百分号、下划线和引号仍是字面内容。保留原始编辑文本；标题或OCR同词不会使图片归入标签。

## OCR持久性

`BeginOcrAsync`返回任务代数。完成结果只接受同一代数、未删除资料和Processing任务。删除、中断Processing的恢复及新的OCR任务都会使旧结果失效。初始化时，中断任务变回Pending，可重新排队。

## 备份

备份使用SQLite数据库备份接口，不直接复制正在使用的数据库文件。快照确定要包含的不可变原图和缩略图，快照之后的变更一致地排除；包含回收站资料。引用图片缺失时备份失败，不生成不完整包。既有目标ZIP不被覆盖。

恢复先验证备份，再安装到新的空目录。拒绝路径穿越、驱动器及备用数据流路径、重复文件名、数据库清单外文件、缺失图片、不支持的结构版本和SQLite完整性错误。不合并或覆盖旧库。使用恢复后的库前调用`InitializeAsync`建立标准子目录并恢复中断OCR任务。

测试使用 SQLite、文件系统和 ZIP，覆盖查询、重启和 OCR 状态。运行[构建脚本](../../scripts/build.ps1)执行测试。Windows 集成检查见[验证记录](../../docs/zh-CN/validation.md)。
