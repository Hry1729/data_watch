/**
 * mock-device-data.ts — 仅用于开发/测试环境（mock 模式）。
 * 集装箱码头场景的设备 / 点位 / 实时值 / 历史序列示例数据，供设备监控各页预览。
 * 只被 mock-agent.ts 与 mock 分支动态引用，不进入生产包。
 */
import type {
  DeviceListItem,
  HistoryChartResponse,
  HistoryRawMatrixResponse,
  PointListItem,
  RealtimeDataItem,
  RealtimeWireValue,
} from "@/types/device-monitor";

type PointKind = "REAL" | "BOOL" | "INT" | "DINT" | "WORD" | "DWORD" | "STRING";
interface PointTemplate { name: string; addr: string; type: PointKind; unit: string; rw?: boolean; group?: string; sim: (t: number, seed: number) => number | string | boolean }

const nz = (i: number, k: number) => { const s = Math.sin(i * 12.9898 + k * 78.233) * 43758.5453; return s - Math.floor(s) - 0.5; };
const ease = (x: number) => (x < 0 ? 0 : x > 1 ? 1 : x * x * (3 - 2 * x));
const round = (value: number, digits = 2) => Number(value.toFixed(digits));

/** 岸桥装卸循环（48s 一个周期） */
function craneCycle(t: number, seed: number) {
  const i = Math.floor(t) + seed * 7;
  const p = (i % 48) / 48;
  let trolley: number; let hoist: number; let locked: boolean;
  if (p < 0.1) { trolley = 44; hoist = 32 - 20 * ease(p / 0.1); locked = p > 0.085; }
  else if (p < 0.2) { trolley = 44; hoist = 12 + 22 * ease((p - 0.1) / 0.1); locked = true; }
  else if (p < 0.5) { trolley = 44 - 38 * ease((p - 0.2) / 0.3); hoist = 34; locked = true; }
  else if (p < 0.6) { trolley = 6; hoist = 34 - 20 * ease((p - 0.5) / 0.1); locked = p < 0.585; }
  else if (p < 0.7) { trolley = 6; hoist = 14 + 20 * ease((p - 0.6) / 0.1); locked = false; }
  else { trolley = 6 + 38 * ease((p - 0.7) / 0.3); hoist = 34; locked = false; }
  const moving = (p > 0.01 && p < 0.19) || (p > 0.51 && p < 0.69);
  const load = locked ? 28.4 + nz(i, 3) * 0.6 : 0.4 + nz(i, 4) * 0.2;
  return { trolley: round(trolley + nz(i, 1) * 0.15), hoist: round(hoist + nz(i, 2) * 0.12), load: round(Math.max(0, load)), locked, moving };
}
const slow = (base: number, amp: number, period: number, noise = 0.3) => (t: number, seed: number) =>
  round(base + seed * 0.7 + amp * Math.sin(t / period + seed) + amp * 0.35 * Math.sin(t / (period / 3.7) + seed * 2) + nz(Math.floor(t), seed) * noise, 2);
const toggle = (period: number, duty: number) => (t: number, seed: number) => ((Math.floor(t) + seed * 5) % period) / period < duty;

const QC_POINTS: PointTemplate[] = [
  { name: "小车位置", addr: "DB10.DBD0", type: "REAL", unit: "m", group: "g001", sim: (t, s) => craneCycle(t, s).trolley },
  { name: "起升高度", addr: "DB10.DBD4", type: "REAL", unit: "m", group: "g001", sim: (t, s) => craneCycle(t, s).hoist },
  { name: "吊具载荷", addr: "DB10.DBD8", type: "REAL", unit: "t", group: "g001", sim: (t, s) => craneCycle(t, s).load },
  { name: "大车风速", addr: "DB10.DBD12", type: "REAL", unit: "m/s", group: "g001", sim: (t, s) => round(6.4 + 1.1 * Math.sin(t / 11 + s) + 0.6 * Math.sin(t / 3.7) + nz(Math.floor(t), 5) * 0.5) },
  { name: "吊具闭锁", addr: "DB10.DBX20.0", type: "BOOL", unit: "", group: "g001", sim: (t, s) => craneCycle(t, s).locked },
  { name: "起升抱闸", addr: "DB10.DBX20.1", type: "BOOL", unit: "", group: "g001", sim: (t, s) => !craneCycle(t, s).moving },
  { name: "累计吊箱数", addr: "DB10.DBD24", type: "DINT", unit: "箱", group: "g001", sim: (t, s) => 1260 + s * 31 + Math.floor((t % 86400) / 96) },
  { name: "故障代码", addr: "DB10.DBW28", type: "WORD", unit: "", group: "g001", sim: () => 0 },
  { name: "起升电机温度", addr: "DB12.DBD0", type: "REAL", unit: "℃", group: "g002", sim: slow(64.8, 4.6, 2400, 0.4) },
  { name: "小车电机温度", addr: "DB12.DBD4", type: "REAL", unit: "℃", group: "g002", sim: slow(49.7, 3.6, 2100, 0.35) },
  { name: "起升变频器温度", addr: "DB12.DBD8", type: "REAL", unit: "℃", group: "g002", sim: slow(41.4, 3.4, 1800, 0.3) },
  { name: "齿轮箱油温", addr: "DB12.DBD12", type: "REAL", unit: "℃", group: "g002", sim: slow(43.7, 1.2, 3000, 0.15) },
  { name: "起升电机电流", addr: "DB12.DBD16", type: "REAL", unit: "A", group: "g002", sim: (t, s) => round(craneCycle(t, s).moving ? 412 + nz(Math.floor(t), 6) * 30 : 36 + nz(Math.floor(t), 7) * 4) },
  { name: "作业模式设定", addr: "DB10.DBW30", type: "INT", unit: "", rw: true, group: "g002", sim: () => 2 },
];
const yardPoints = (protocol: "modbus" | "opcua"): PointTemplate[] => {
  const a = (modbus: string, node: string) => (protocol === "modbus" ? modbus : `ns=2;s=${node}`);
  return [
    { name: "大车行走速度", addr: a("40001", "Gantry.Speed"), type: "REAL", unit: "m/min", sim: slow(32, 14, 40, 1.2) },
    { name: "起升高度", addr: a("40003", "Hoist.Height"), type: "REAL", unit: "m", sim: slow(12.6, 5.2, 30, 0.2) },
    { name: "吊具载荷", addr: a("40005", "Spreader.Load"), type: "REAL", unit: "t", sim: slow(18.2, 9, 55, 0.3) },
    { name: "电机温度", addr: a("40007", "Motor.Temp"), type: "REAL", unit: "℃", sim: slow(52.4, 3.2, 2200, 0.3) },
    { name: "燃油液位", addr: a("40009", "Fuel.Level"), type: "REAL", unit: "%", sim: slow(68, 2, 9000, 0.1) },
    { name: "吊具闭锁", addr: a("00001", "Spreader.Locked"), type: "BOOL", unit: "", sim: toggle(40, 0.55) },
    { name: "运行状态", addr: a("00002", "Run.State"), type: "BOOL", unit: "", sim: toggle(300, 0.92) },
    { name: "故障代码", addr: a("40011", "Fault.Code"), type: "WORD", unit: "", sim: () => 0 },
  ];
};
const agvPoints = (code: string): PointTemplate[] => {
  const id = code.replace("-", "");
  return [
    { name: "电池电量", addr: `ns=2;s=${id}.Battery.SOC`, type: "REAL", unit: "%", sim: slow(64, 6, 4000, 0.1) },
    { name: "行驶速度", addr: `ns=2;s=${id}.Speed`, type: "REAL", unit: "m/s", sim: slow(3.1, 1.8, 25, 0.2) },
    { name: "X 坐标", addr: `ns=2;s=${id}.Pos.X`, type: "REAL", unit: "m", sim: slow(410, 120, 90, 0.5) },
    { name: "Y 坐标", addr: `ns=2;s=${id}.Pos.Y`, type: "REAL", unit: "m", sim: slow(86, 30, 70, 0.4) },
    { name: "载箱状态", addr: `ns=2;s=${id}.Loaded`, type: "BOOL", unit: "", sim: toggle(180, 0.5) },
    { name: "当前任务号", addr: `ns=2;s=${id}.Task.Id`, type: "STRING", unit: "", sim: (t, s) => `T${20261019}${String(400 + s + Math.floor(t / 180) % 90).padStart(4, "0")}` },
    { name: "故障代码", addr: `ns=2;s=${id}.Fault`, type: "WORD", unit: "", sim: () => 0 },
  ];
};
const GATE_POINTS: PointTemplate[] = [
  { name: "道闸开启", addr: "%Q00001", type: "BOOL", unit: "", rw: true, sim: toggle(30, 0.3) },
  { name: "车辆到位", addr: "%I00003", type: "BOOL", unit: "", sim: toggle(30, 0.45) },
  { name: "地磅重量", addr: "%R00010", type: "REAL", unit: "t", sim: slow(31.6, 9, 20, 0.2) },
  { name: "过车计数", addr: "%R00020", type: "DINT", unit: "辆", sim: (t) => 860 + Math.floor((t % 86400) / 90) },
];
const METER_POINTS: PointTemplate[] = [
  { name: "A相电压", addr: "40001", type: "REAL", unit: "V", sim: slow(231.2, 1.6, 600, 0.3) },
  { name: "B相电压", addr: "40003", type: "REAL", unit: "V", sim: slow(230.6, 1.5, 640, 0.3) },
  { name: "C相电压", addr: "40005", type: "REAL", unit: "V", sim: slow(231.8, 1.4, 580, 0.3) },
  { name: "有功功率", addr: "40011", type: "REAL", unit: "kW", sim: slow(842, 120, 300, 6) },
  { name: "功率因数", addr: "40017", type: "REAL", unit: "", sim: slow(0.93, 0.02, 900, 0.004) },
  { name: "累计电量", addr: "40021", type: "DWORD", unit: "kWh", sim: (t) => 1284620 + Math.floor((t % 86400) / 4) },
];

interface MockDevice extends DeviceListItem { templates: PointTemplate[]; seed: number }

const UNSTABLE = new Set(["QC03", "AGV-027", "RTG09", "ASC17", "AGV-054"]);
const OFFLINE = new Set(["RTG06", "AGV-013", "AGV-041", "AGV-066", "ASC22", "METER-PS5", "PLC-GATE05", "AGV-072"]);
const DISABLED = new Set(["METER-PS2", "AGV-079", "AGV-080", "RTG20"]);
const FEATURED = ["QC01", "QC02", "QC03", "RTG05", "RTG06", "ASC11", "AGV-012", "AGV-027", "PLC-GATE01", "METER-PS2"];

function buildDevices(): MockDevice[] {
  const rows: Omit<MockDevice, "id">[] = [];
  const add = (deviceType: string, deviceCode: string, deviceName: string, driverName: string, endpoint: string, description: string, templates: PointTemplate[]) => {
    const disabled = DISABLED.has(deviceCode);
    const connectStatus = disabled || OFFLINE.has(deviceCode) ? "offline" : UNSTABLE.has(deviceCode) ? "unstable" : "online";
    rows.push({
      deviceType, deviceCode, deviceName, driverName, endpoint, endpointBackup: deviceType === "岸桥" ? endpoint.replace("10.20.1.", "10.20.2.") : "",
      deviceStatus: disabled ? "disabled" : "enabled", connectStatus,
      connectUpdateTime: "2026-10-19 21:40:12", dataStatus: connectStatus === "online" ? "normal" : connectStatus === "unstable" ? "abnormal" : "nodata",
      dataUpdateTime: "2026-10-19 21:40:12", description: disabled ? "检修中，已停用" : description, templates, seed: rows.length % 9,
    });
  };
  for (let n = 1; n <= 12; n += 1) { const c = `QC${String(n).padStart(2, "0")}`; add("岸桥", c, `${n}# 岸桥`, "Siemens S7", `10.20.1.${10 + n}:102`, `${Math.ceil(n / 2)} 号泊位岸桥主控 S7-1500`, QC_POINTS); }
  for (let n = 1; n <= 20; n += 1) { const c = `RTG${String(n).padStart(2, "0")}`; add("场桥", c, `${n}# 轮胎吊`, "Modbus TCP", `10.30.5.${20 + n}:502`, "B2 堆场轮胎吊", yardPoints("modbus")); }
  for (let n = 11; n <= 30; n += 1) { const c = `ASC${n}`; add("场桥", c, `${n}# 自动化轨道吊`, "OPC UA", `10.30.8.${n}:4840`, "自动化堆场 A 区", yardPoints("opcua")); }
  for (let n = 1; n <= 80; n += 1) { const c = `AGV-${String(n).padStart(3, "0")}`; add("AGV", c, `${n}# AGV`, "OPC UA", `10.40.0.${100 + n}:4840`, "水平运输", agvPoints(c)); }
  for (let n = 1; n <= 6; n += 1) { const c = `PLC-GATE${String(n).padStart(2, "0")}`; add("PLC", c, `${n}# 闸口控制 PLC`, "GE SRTP", `10.10.2.${30 + n}:18245`, "进港闸口道闸与地磅", GATE_POINTS); }
  for (let n = 1; n <= 8; n += 1) { const c = `METER-PS${n}`; add("仪表", c, `${n}# 变电所电力仪表`, "Modbus TCP", `10.50.2.${39 + n}:502`, "变电所能耗计量", METER_POINTS); }
  rows.sort((left, right) => {
    const li = FEATURED.indexOf(left.deviceCode); const ri = FEATURED.indexOf(right.deviceCode);
    return (li < 0 ? 999 : li) - (ri < 0 ? 999 : ri);
  });
  return rows.map((row, index) => ({ ...row, id: index + 1 }));
}

export const MOCK_DEVICES: MockDevice[] = buildDevices();

export function mockDeviceRows(): DeviceListItem[] {
  return MOCK_DEVICES.map(({ templates: _templates, seed: _seed, ...row }) => row);
}

export const MOCK_POINTS: (PointListItem & { unit: string; group: string })[] = MOCK_DEVICES.flatMap((device) =>
  device.templates.map((template, index) => ({
    id: device.id * 100 + index + 1,
    deviceId: device.id,
    deviceCode: device.deviceCode,
    varSeq: index + 1,
    pointName: template.name,
    ioAddress: template.addr,
    dataType: template.type,
    description: template.unit ? `${template.name}，${template.unit}` : template.name,
    isReadonly: !template.rw,
    driverName: device.driverName,
    pointStatus: device.deviceStatus === "disabled" || (template.type === "STRING" && device.id % 2 === 1) ? "disabled" as const : "enabled" as const,
    updateTime: template.rw ? "2026-10-18 09:12:30" : "2026-10-19 21:40:12",
    unit: template.unit,
    group: template.group ?? "g001",
  })));

function findDevice(deviceId?: number, deviceCode?: string) {
  return MOCK_DEVICES.find((device) => device.id === deviceId || device.deviceCode === deviceCode) ?? MOCK_DEVICES[0];
}

export function mockPointGroups(deviceCode: string) {
  const device = findDevice(undefined, deviceCode);
  const groups = [...new Set(device.templates.map((template) => template.group ?? "g001"))];
  const names: Record<string, string> = { g001: device.deviceType === "岸桥" ? "起升小车" : "默认分组", g002: "电气与温度" };
  return groups.map((code) => ({ group_code: code, group_name: `${code} · ${names[code] ?? code}`, active_point_count: device.templates.filter((template) => (template.group ?? "g001") === code).length, max_point_count: 3072, status: "active" }));
}

function simulate(device: MockDevice, template: PointTemplate, t: number) {
  return template.sim(t, device.seed);
}

export function mockRealtimeItems(deviceId: number, groupCode: string): { device: MockDevice; data: RealtimeDataItem[] } {
  const device = findDevice(deviceId);
  const t = Date.now() / 1000;
  const now = new Date().toISOString();
  const data = device.templates.flatMap((template, index) => (template.group ?? "g001") !== groupCode ? [] : [{
    pointId: device.id * 100 + index + 1,
    varSeq: index + 1,
    pointName: template.name,
    ioAddress: template.addr,
    value: device.connectStatus === "offline" ? "--" : simulate(device, template, t),
    updateTime: now,
    quality: 0,
    dataType: template.type,
  }]);
  return { device, data };
}

/** mock 模式下的实时推送值（按 pointId 索引） */
export function mockRealtimeWire(deviceCode: string, groupCode: string): Record<string, RealtimeWireValue> {
  const device = findDevice(undefined, deviceCode);
  return Object.fromEntries(mockRealtimeItems(device.id, groupCode).data.map((item) => [String(item.pointId), { v: item.value, t: Date.now(), q: 0 }]));
}

function parseTime(value: string) {
  const time = new Date(value.includes("T") ? value : value.replace(" ", "T")).getTime();
  return Number.isFinite(time) ? time : Date.now();
}
const pad = (value: number) => String(value).padStart(2, "0");
function formatTime(ms: number, withMs = false) {
  const d = new Date(ms);
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}${withMs ? `.${String(d.getMilliseconds()).padStart(3, "0")}` : ""}`;
}

export function mockHistoryPoints(deviceId: number) {
  const device = findDevice(deviceId);
  return device.templates.map((template, index) => ({ varSeq: index + 1, pointName: template.name, ioAddress: template.addr }));
}

/** 历史值：慢变量用 slow 曲线 + 缓慢漂移；在区间 36%~43% 处留一段缺失 */
function historyValue(device: MockDevice, template: PointTemplate, ms: number) {
  const value = simulate(device, template, ms / 1000);
  if (typeof value === "boolean") return value ? 1 : 0;
  if (typeof value === "string") return null;
  return value;
}

export function mockHistoryChart(query: { deviceId: number; varSeqs: number[]; startTime: string; endTime: string; interval?: string }): HistoryChartResponse {
  const device = findDevice(query.deviceId);
  const start = parseTime(query.startTime); const end = Math.max(parseTime(query.endTime), start + 60_000);
  const count = 360; const step = (end - start) / count;
  const gapFrom = start + (end - start) * 0.36; const gapTo = start + (end - start) * 0.43;
  return {
    interval: query.interval && query.interval !== "auto" ? query.interval : `${Math.max(1, Math.round(step / 60_000))}m`,
    series: query.varSeqs.map((varSeq) => {
      const template = device.templates[varSeq - 1] ?? device.templates[0];
      return {
        varSeq, name: template.name, unit: template.unit, dataType: template.type,
        data: Array.from({ length: count + 1 }, (_, index) => {
          const ms = start + index * step;
          return [formatTime(ms), ms > gapFrom && ms < gapTo ? null : historyValue(device, template, ms)] as [string, number | null];
        }),
      };
    }),
  };
}

export function mockHistoryMatrix(query: { deviceId: number; varSeqs: number[]; startTime: string; endTime: string; alignGranularity?: "100ms" | "500ms" | "1s"; pageSize?: number; cursorTs?: string }): HistoryRawMatrixResponse {
  const device = findDevice(query.deviceId);
  const stepMs = query.alignGranularity === "100ms" ? 100 : query.alignGranularity === "500ms" ? 500 : 1000;
  const pageSize = query.pageSize ?? 200;
  const start = query.cursorTs ? parseTime(query.cursorTs) + stepMs : parseTime(query.startTime);
  const end = parseTime(query.endTime);
  const columns = [{ prop: "ts", label: "采样时间" }, ...query.varSeqs.map((varSeq) => {
    const template = device.templates[varSeq - 1] ?? device.templates[0];
    return { prop: `v${varSeq}`, label: template.name, varSeq, unit: template.unit, dataType: template.type };
  })];
  const rows: Record<string, unknown>[] = [];
  for (let index = 0; index < pageSize; index += 1) {
    const ms = start + index * stepMs;
    if (ms > end) break;
    rows.push(Object.fromEntries([["ts", formatTime(ms, stepMs < 1000)], ...query.varSeqs.map((varSeq) => {
      const template = device.templates[varSeq - 1] ?? device.templates[0];
      const value = historyValue(device, template, ms);
      // 每 7 条缺一条模拟未补值
      return [`v${varSeq}`, (index + varSeq) % 7 === 3 || value == null ? null : template.type === "REAL" ? value.toFixed(2) : String(value)];
    })]));
  }
  const last = rows[rows.length - 1]?.ts as string | undefined;
  return { columns, rows, nextCursor: rows.length === pageSize && last ? last : null, meta: { alignGranularity: query.alignGranularity ?? "1s", fillMode: "none", pageSize } };
}
