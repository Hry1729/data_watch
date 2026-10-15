import { CircleHelp, Keyboard, Mail, MonitorSmartphone } from "lucide-react";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";

const QUICK_START = [
  {
    step: "1",
    title: "连接设备",
    detail: "在「设备监控 → 设备通讯」中确认 PLC / 采集端已联通，状态指示灯为绿色即为正常。",
  },
  {
    step: "2",
    title: "配置采集",
    detail: "在「数据采集」中查看采集、汇聚、入库三个环节的运行状态与吞吐。",
  },
  {
    step: "3",
    title: "核对数据",
    detail: "在「实时监控」看最新点位值，在「历史数据」按时间区间回查。",
  },
];

const FAQ = [
  {
    question: "右上角数据库胶囊显示「异常」怎么办？",
    answer: "把鼠标停留在胶囊上可以看到 Redis / MySQL / TDengine 各自的延迟与报错信息，先按提示定位到具体服务。",
  },
  {
    question: "某个菜单看不到？",
    answer: "桌面端的菜单按账号角色下发，请联系管理员在「系统管理 → 角色管理」中确认权限。",
  },
  {
    question: "左侧导航只剩图标了？",
    answer: "左上角的开关可以展开 / 折叠导航栏，折叠状态只显示一级图标，二级页面用顶部的胶囊条切换。",
  },
];

export default function HelpPage() {
  return (
    <div className="mx-auto w-full max-w-[1680px] space-y-4 px-4 pb-4 md:px-6 md:pb-6">
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <CircleHelp className="h-4 w-4 text-brand" />
            帮助中心
          </CardTitle>
          <CardDescription>
            本页内容为占位示意，正式的使用手册与联机帮助待后续版本接入。
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          {QUICK_START.map((item) => (
            <div key={item.step} className="flex gap-3">
              <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-brand-soft text-xs font-medium text-brand">
                {item.step}
              </span>
              <div className="min-w-0">
                <p className="text-sm font-medium text-foreground">{item.title}</p>
                <p className="mt-0.5 text-sm text-muted-foreground">{item.detail}</p>
              </div>
            </div>
          ))}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <MonitorSmartphone className="h-4 w-4 text-brand" />
            常见问题
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {FAQ.map((item) => (
            <div key={item.question} className="rounded-xl bg-muted/50 px-3 py-2.5">
              <p className="text-sm font-medium text-foreground">{item.question}</p>
              <p className="mt-1 text-sm text-muted-foreground">{item.answer}</p>
            </div>
          ))}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Keyboard className="h-4 w-4 text-brand" />
            快捷键
          </CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground">
            当前版本尚未定义全局快捷键，后续会在此列出。
          </p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Mail className="h-4 w-4 text-brand" />
            联系我们
          </CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground">
            使用中遇到问题，请把操作步骤、页面截图与「系统日志」中的报错时间一并提供给现场技术支持。
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
