export interface PaginatedResult<T> {
  list: T[];
  total: number;
  pageNo?: number;
  pageSize?: number;
  availableCount?: number;
  source?: "center" | "local";
}

export interface DriverListItem {
  id: number;
  driverName: string;
  driverDesc: string;
  readWriteType: string;
  updateTime: string;
  status?: string;
  boundDeviceCount?: number;
  /** 安装包内置 / 旧版内置 / 已安装 / Agent 独有未注册 / 状态异常 */
  source?: "official" | "bundled" | "builtin" | "installed" | "unregistered" | "unknown" | "missing";
  /** 内置驱动不可被外部包覆盖或卸载 */
  protected?: boolean;
  /** Manifest.id，Agent 安装目录名（可与 protocol 不同） */
  driverId?: string;
  version?: string;
  sha256?: string;
  /** Backend installed_driver.install_source（如 zip） */
  installSource?: string;
  /** Agent 本机加载状态：loaded / installed / load_failed / rollback / unknown / missing 等 */
  loadStatus?: string;
  installStatus?: string;
  capabilities?: string[];
  error?: string;
}

export interface DeviceListItem {
  id: number;
  deviceType: string;
  deviceCode: string;
  deviceName: string;
  driverName: string;
  deviceStatus: "enabled" | "disabled";
  connectStatus: "online" | "offline" | "unstable";
  connectUpdateTime: string;
  dataStatus: "normal" | "abnormal" | "nodata";
  dataUpdateTime: string;
  endpoint: string;
  endpointBackup: string;
  description?: string;
}

export interface DeviceGroupTreeNode {
  key: string;
  name: string;
  count?: number;
  nodeType?: "group" | "device";
  deviceId?: number;
  deviceCode?: string;
  connectStatus?: DeviceListItem["connectStatus"];
  children?: DeviceGroupTreeNode[];
}

export interface DeviceListQuery {
  pageNo?: number;
  pageSize?: number;
  keyword?: string;
  deviceIds?: number[];
  deviceType?: string;
  deviceTypeExact?: string;
  deviceCodeExact?: string;
  deviceStatus?: string;
  connectStatus?: string;
}

export interface DeviceSummary {
  total: number;
  onlineCount: number;
  offlineCount: number;
  unstableCount: number;
  disabledCount: number;
  enabledOnlineCount: number;
  enabledOfflineCount: number;
}

export interface DeviceForm {
  deviceType: string;
  deviceCode: string;
  deviceName: string;
  driverName: string;
  deviceStatus: "enabled" | "disabled";
  endpoint: string;
  endpointBackup?: string;
  description?: string;
}

/** 可通过界面新建/编辑的物理类型（创建后不可修改，和后端白名单一致）。 */
export type PointDataType = "BOOL" | "INT" | "DINT" | "REAL" | "STRING" | "WORD" | "DWORD";

/**
 * 列表里显示的数据类型。库里可能写成 Boolean / BYTE 等，中心与边缘统一按
 * 显示口径归一（BOOLEAN→BOOL、FLOAT→REAL），其余类型原样显示。
 */
export type PointDisplayDataType =
  | PointDataType
  | "BYTE"
  | "UINT"
  | "USINT"
  | "UDINT"
  | "TIMER_MS"
  | "COUNTER";

/** 类型筛选下拉的选项（显示口径全集，和列表显示保持一致）。 */
export const POINT_DISPLAY_DATA_TYPES: PointDisplayDataType[] = [
  "BOOL",
  "BYTE",
  "INT",
  "WORD",
  "UINT",
  "USINT",
  "REAL",
  "DINT",
  "DWORD",
  "UDINT",
  "STRING",
  "TIMER_MS",
  "COUNTER",
];

export interface PointListItem {
  id: number;
  deviceId: number;
  deviceCode: string;
  varSeq: number;
  pointName: string;
  ioAddress: string;
  dataType: PointDisplayDataType;
  description: string;
  isReadonly: boolean;
  driverName: string;
  createTime?: string;
  updateTime?: string;
  pointStatus: "enabled" | "disabled";
}

export interface PointDeviceTreeNode {
  key: string;
  name: string;
  nodeType?: "group" | "device";
  count?: number;
  deviceId?: number;
  deviceCode?: string;
  groupId?: number;
  children?: PointDeviceTreeNode[];
}

export interface PointListQuery {
  pageNo?: number;
  pageSize?: number;
  keyword?: string;
  deviceId?: number;
  deviceIds?: number[];
  deviceCode?: string;
  deviceType?: string;
  connectStatus?: string;
  dataType?: string;
  groupId?: number;
}

export interface PointForm {
  deviceId: number;
  deviceCode: string;
  varSeq: number;
  pointName: string;
  ioAddress: string;
  /** 表单里的类型；库里可能是显示口径之外的类型（如 BYTE），后端会拒绝改物理类型。 */
  dataType: PointDisplayDataType;
  driverName: string;
  isReadonly: boolean;
  description?: string;
}

export interface RealtimeDataItem {
  pointId?: number;
  varSeq: number;
  pointName: string;
  ioAddress: string;
  value: string | number | boolean;
  updateTime: string;
  quality?: number | null;
  dataType?: string;
}

export interface DeviceRealtimeStatus {
  deviceId: number;
  deviceCode: string;
  deviceName: string;
  driverName: string;
  connectStatus: DeviceListItem["connectStatus"];
  lastCollectTime: string;
  redisConnected?: boolean;
  degraded?: boolean;
  data: RealtimeDataItem[];
}

export interface RealtimePointGroup {
  group_code: string;
  group_name: string | null;
  active_point_count: number;
  max_point_count: number;
  status: string;
}

export interface RealtimeWireValue {
  v: unknown;
  t: number;
  q: number;
}

export interface HistoryDeviceItem {
  id: number | null;
  name: string;
  deviceCode?: string;
  deviceName?: string;
  typeCode?: string;
  typeName?: string;
}

export interface DeviceTypeItem {
  typeCode: string;
  typeName: string;
  deviceCount: number;
}

export interface HistoryPointItem {
  varSeq: number;
  pointName: string;
  ioAddress: string;
}

export interface HistoryChartResponse {
  interval: string;
  series: {
    varSeq: number;
    name: string;
    unit: string;
    dataType?: string;
    data: [string, number | null][];
  }[];
}

export interface HistoryMatrixColumn {
  prop: string;
  label: string;
  varSeq?: number;
  unit?: string;
  dataType?: string;
}

export interface HistoryRawMatrixResponse {
  columns: HistoryMatrixColumn[];
  rows: Record<string, unknown>[];
  nextCursor: string | null;
  meta: { alignGranularity: "100ms" | "500ms" | "1s"; fillMode: "none"; pageSize: number };
}

export interface HistoryQuery {
  deviceId: number;
  varSeqs: number[];
  startTime: string;
  endTime: string;
  interval?: string;
  aggregate?: string;
}

export interface HistoryExportPayload {
  deviceId: number;
  varSeqs: number[];
  startTime: string;
  endTime: string;
}

export interface HistoryExportStatus {
  status: "pending" | "processing" | "completed" | "failed";
  progress: number;
  error?: string;
  createdAt?: string;
}

export interface ImportResult {
  success: number;
  fail: number;
  errors?: { rowNumber: number; reason: string }[];
}
