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
    <div className="mx-auto w-full max-w-[1400px] space-y-4">
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
        <CardContent>
          <ol className="grid gap-3 md:grid-cols-3">
            {QUICK_START.map((item) => (
              <li key={item.step} className="flex gap-3 rounded-lg bg-muted/50 p-4">
                <span className="flex size-7 shrink-0 items-center justify-center rounded-full bg-brand-soft text-xs font-semibold text-brand">
                  {item.step}
                </span>
                <div className="min-w-0">
                  <p className="text-sm font-medium text-foreground">{item.title}</p>
                  <p className="mt-1 text-xs leading-relaxed text-muted-foreground">{item.detail}</p>
                </div>
              </li>
            ))}
          </ol>
        </CardContent>
      </Card>

      <div className="grid items-stretch gap-4 lg:grid-cols-[minmax(0,1.4fr)_minmax(0,1fr)]">
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <MonitorSmartphone className="h-4 w-4 text-brand" />
              常见问题
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {FAQ.map((item) => (
              <div key={item.question} className="rounded-lg bg-muted/50 px-4 py-3">
                <p className="text-sm font-medium text-foreground">{item.question}</p>
                <p className="mt-1.5 text-xs leading-relaxed text-muted-foreground">{item.answer}</p>
              </div>
            ))}
          </CardContent>
        </Card>

        <div className="flex flex-col gap-4">
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Keyboard className="h-4 w-4 text-brand" />
                快捷键
              </CardTitle>
            </CardHeader>
            <CardContent>
              <p className="text-xs leading-relaxed text-muted-foreground">
                当前版本尚未定义全局快捷键，后续会在此列出。
              </p>
            </CardContent>
          </Card>

          <Card className="flex-1">
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Mail className="h-4 w-4 text-brand" />
                联系我们
              </CardTitle>
            </CardHeader>
            <CardContent>
              <p className="text-xs leading-relaxed text-muted-foreground">
                使用中遇到问题，请把操作步骤、页面截图与「系统日志」中的报错时间一并提供给现场技术支持。
              </p>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
