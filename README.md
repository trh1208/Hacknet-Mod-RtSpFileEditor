目前支持的功能：（没有指定目录的情况下，默认为游戏目录，即hacknet.exe所在目录）
在任意Action中添加：
# Hacknet 功能扩展文档

## 默认操作目录
- 当未指定目录时，默认使用**游戏目录**（即 `hacknet.exe` 所在目录）
- 所有文件操作均基于此目录执行
- 示例路径：`D:\Steam\steamapps\common\Hacknet`

---

##  Action 操作指令

### 1.  创建文件操作
```xml
<CreateFileAction 
      FileName="report.pdf" 
      FileDirectory="D:\Steam\steamapps\common\Hacknet" 
      MinSize="50KB" 
      MaxSize="100KB" 
/>
```
======================================================================
将在游戏目录下创建名为report.pdf的文件，大小介于50KB到100KB之间。
```<RunExternalFile FilePath="cmd.exe" Arguments="help" UseShellExecute="true" WaitForExit="false"/>```
效果为：运行游戏目录下的cmd.exe，并传入参数help，不等待命令执行完毕。如果需要等待命令执行完毕，请将WaitForExit属性设置为true。效果变为触发后hacknet退出，等待你关闭cmd窗口后恢复（进度会保留）。
UseShellExecute代表是否使用系统的shell运行。
=======================================================================
在missions中的goals中添加：
```<goal type="RealFileExists" FilePath="1.txt" />```
检测游戏目录是否存在1.txt文件。存在即可提交任务（只检测文件名）
```<goal type="RealFileNotExists" FilePath="D:\Steam\steamapps\common\Hacknet\report.pdf" />```
检测游戏目录是否不存在report.pdf文件。不存在即可提交任务（只检测文件名）
```<goal type="FileContentMatch" FilePath="2.txt" Pattern="^fileupload\s*=\s*true$" RequireMatch="true"/>```
检测游戏目录下是否存在2.txt文件，且内容匹配正则表达式。匹配成功即可提交任务。
```<goal type="FileContentMatch" FilePath="3.txt" Pattern="^fileupload\s*=\s*true$" RequireMatch="false"/>```
检测游戏目录下是否存在3.txt文件，且内容匹配正则表达式。匹配失败即可提交任务。
