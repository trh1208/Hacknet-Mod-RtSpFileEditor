# Hacknet 功能扩展文档

## 默认操作目录
- 当未指定目录时，默认使用**游戏目录**（即 `hacknet.exe` 所在目录）
- 所有文件操作均基于此目录执行
- 示例路径：`D:\Steam\steamapps\common\Hacknet`
- **相对路径应当为"Extension\……"而不是"\Extension\……"**
- 路径不存在时，会尝试创建目录
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
FileName：文件名\
FileDirectory：文件位置\
MinSize：最小大小 \
MaxSize：最大大小

将在游戏目录下创建名为report.pdf的文件，大小介于50KB到100KB之间。

---
### 2.  启动玩家物理系统应用
```xml
<RunExternalFile 
                 FilePath="cmd.exe" 
                 Arguments="help" 
                 UseShellExecute="true" 
                 WaitForExit="false"
/>
```

FilePath：位置\
Arguments：运行时附带参数（可选）\
UseShellExecute：是否使用系统shell运行\
WaitForExit：是否等待进程结束\
（修改为true会暂停hacknet进程并隐藏hacknet窗口，直到退出你的程序）

效果为：运行游戏目录下的cmd.exe，并传入参数help，不等待命令执行完毕。
UseShellExecute代表是否使用系统的shell运行。

---
### 3.  崩溃HN
```xml
<TerminateGame Delay="0" SaveBeforeExit="true" /> 
```
Delay：延迟（无需设置DelayHost）
SaveBeforeExit：是否在崩溃前为玩家保存进度？


---
##  Mission 任务目标
-`FileContentMatch`函数只能检测**文本文件**，但后缀可以自定义，只要你能用记事本打开查看内容就行。
### 1.  检测目标文件是否存在在指定目录
在Missions中的goals中添加：
```xml
<goal type="RealFileExists" FilePath="1.txt" />
```
检测游戏目录是否存在1.txt文件。存在即可提交任务（只检测文件名）

---
### 2.  检测目标文件是否不存在在指定目录
```xml
<goal type="RealFileNotExists" FilePath="D:\Steam\steamapps\common\Hacknet\report.pdf" />
```
检测游戏目录是否不存在report.pdf文件。不存在即可提交任务（只检测文件名）

---
### 3.  检测在指定目录下的目标文件内容是否存在指定内容（使用正在表达式匹配）
```xml
<goal type="FileContentMatch" FilePath="2.txt" Pattern="^fileupload\s*=\s*true$" RequireMatch="true"/>
```
检测游戏目录下是否存在2.txt文件，且内容匹配正则表达式。匹配成功即可提交任务。

---
### 4.  检测在指定目录下的目标文件内容是否不存在指定内容（使用正在表达式匹配）
```xml
<goal type="FileContentMatch" FilePath="3.txt" Pattern="^fileupload\s*=\s*true$" RequireMatch="false"/>
```
检测游戏目录下是否存在3.txt文件，且内容匹配正则表达式。匹配失败即可提交任务。
