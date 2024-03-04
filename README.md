# win-fail2ban
防止windows系统在公网暴露远程桌面端口后被黑客暴力破解密码
该项目并不是万无一失的,建议定期更换高强度密码!!!
## 快速开始
1. 在本地策略组中(gpedit.msc)确保Windows设置->安全设置->本地策略->审核策略->审核登录事件选择了失败.
2. 编译项目.
3. 在计算机管理(compmgmt.msc)中将WinFail2Ban.exe配置在任务计划程序中.建议15-30分钟执行一次.
## 配置
* 可在HackInfo.db中配置白名单(Table:WhiteList),防止授信终端被防火墙挡住.支持正则表达式,不支持通过配置子网掩码位数进行配置.
* 可在Appconfig中配置FailuresCount设置允许的单日最大失败次数,当超过该则会将ip加入windows防火墙中的入站黑名单拒绝所有连接.
