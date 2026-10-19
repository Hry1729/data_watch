import { useState } from "react";
import { Link } from "react-router-dom";
import { ArrowUpRight, ChevronDown, CircleHelp, FileText, Keyboard, Mail, MonitorSmartphone } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";

const QUICK_START = [
  {
    step: "1",
    title: "连接设备",
    detail: "在「设备监控 → 设备通讯」中确认 PLC / 采集端已联通，状态指示灯为绿色即为正常。",
    links: [{ label: "设备通讯", path: "/device/communication" }],
  },
  {
    step: "2",
    title: "配置采集",
    detail: "在「数据采集」中查看采集、汇聚、入库三个环节的运行状态与吞吐。",
    links: [
      { label: "采集监控", path: "/collection/collector" },
      { label: "汇聚监控", path: "/collection/ingest" },
      { label: "入库监控", path: "/collection/writer" },
    ],
  },
  {
    step: "3",
    title: "核对数据",
    detail: "在「实时监控」看最新点位值，在「历史数据」按时间区间回查。",
    links: [
      { label: "实时监控", path: "/device/realtime" },
      { label: "历史数据", path: "/device/history" },
    ],
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

const chipCls = "inline-flex h-6 items-center gap-1 rounded-full border bg-card px-2.5 text-xs text-foreground transition-colors hover:border-brand/40 hover:text-brand";

function CardTitleRow({ icon: Icon, title }: { icon: typeof CircleHelp; title: string }) {
  return (
    <p className="flex items-center gap-2 text-sm leading-5 font-semibold">
      <Icon className="size-4 text-brand" />
      {title}
    </p>
  );
}

export default function HelpPage() {
  const [openFaq, setOpenFaq] = useState<number | null>(0);

  return (
    <div className="w-full space-y-4">
      <Card className="gap-0 px-6 py-6">
        <CardTitleRow icon={CircleHelp} title="帮助中心" />
        <p className="mt-1.5 text-[13px] text-muted-foreground">
          本页内容为占位示意，正式的使用手册与联机帮助待后续版本接入。
        </p>
        <ol className="mt-5 grid grid-cols-3 gap-4">
          {QUICK_START.map((item, index) => (
            <li key={item.step} className="flex min-w-0 flex-col gap-3">
              <div className="flex items-center gap-2.5">
                <span className="grid size-7 shrink-0 place-items-center rounded-full bg-brand-soft text-xs font-semibold text-brand">
                  {item.step}
                </span>
                {index < QUICK_START.length - 1 && <span className="h-px flex-1 border-t border-dashed" />}
              </div>
              <div className="flex-1 rounded-[10px] bg-[color-mix(in_oklab,var(--muted)_55%,transparent)] p-3.5">
                <p className="text-sm font-semibold text-foreground">{item.title}</p>
                <p className="mt-1.5 text-[13px] leading-relaxed text-muted-foreground">{item.detail}</p>
                <div className="mt-3 flex flex-wrap gap-1.5">
                  {item.links.map((link) => (
                    <Link key={link.path} to={link.path} className={chipCls}>
                      {link.label}
                      {item.links.length === 1 && <ArrowUpRight className="size-3" />}
                    </Link>
                  ))}
                </div>
              </div>
            </li>
          ))}
        </ol>
      </Card>

      <div className="grid items-start gap-4 lg:grid-cols-[minmax(0,1.4fr)_minmax(0,1fr)]">
        <Card className="gap-0 px-5 py-5">
          <CardTitleRow icon={MonitorSmartphone} title="常见问题" />
          <div className="mt-4 overflow-hidden rounded-[10px] bg-[color-mix(in_oklab,var(--muted)_55%,transparent)]">
            {FAQ.map((item, index) => {
              const open = openFaq === index;
              return (
                <div key={item.question} className="border-b border-card last:border-b-0">
                  <button
                    type="button"
                    aria-expanded={open}
                    className="flex w-full items-center gap-3 px-4 py-3 text-left text-sm text-foreground"
                    onClick={() => setOpenFaq(open ? null : index)}
                  >
                    <span className="min-w-0 flex-1">{item.question}</span>
                    <ChevronDown className={`size-4 shrink-0 text-muted-foreground transition-transform ${open ? "rotate-180" : ""}`} />
                  </button>
                  {open && <p className="px-4 pb-3.5 text-[13px] leading-relaxed text-muted-foreground">{item.answer}</p>}
                </div>
              );
            })}
          </div>
        </Card>

        <div className="flex flex-col gap-4">
          <Card className="gap-0 px-5 py-5">
            <CardTitleRow icon={Keyboard} title="快捷键" />
            <p className="mt-3 text-[13px] leading-relaxed text-muted-foreground">
              当前版本尚未定义全局快捷键，后续会在此列出。
            </p>
          </Card>

          <Card className="gap-0 px-5 py-5">
            <CardTitleRow icon={Mail} title="联系我们" />
            <p className="mt-3 text-[13px] leading-relaxed text-muted-foreground">
              使用中遇到问题，请把操作步骤、页面截图与「系统日志」中的报错时间一并提供给现场技术支持。
            </p>
            <Button asChild variant="outline" className="mt-3 h-8 w-fit gap-1.5 bg-background px-3 text-[13px]">
              <Link to="/logs/system"><FileText className="size-3.5" />打开系统日志</Link>
            </Button>
          </Card>
        </div>
      </div>
    </div>
  );
}
