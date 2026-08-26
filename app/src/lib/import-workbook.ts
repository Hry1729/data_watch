const DEVICE_FIELDS: Record<string, string> = {
  设备类型: "deviceType",
  设备编号: "deviceCode",
  设备名称: "deviceName",
  关联驱动: "driverName",
  设备状态: "deviceStatus",
  描述: "description",
};

const POINT_FIELDS: Record<string, string> = {
  设备编号: "deviceCode",
  点位序号: "varSeq",
  点位名称: "pointName",
  IO地址: "ioAddress",
  数据类型: "dataType",
  驱动名称: "driverName",
  是否只读: "isReadonly",
  描述: "description",
  "采集周期(ms)": "scanRate",
  是否存储: "isStorage",
};

export const MAX_IMPORT_FILE_BYTES = 10 * 1024 * 1024;
export const MAX_IMPORT_SHEET_ROWS = 10_000;
export const MAX_IMPORT_SHEET_CELLS = 100_000;
// Leave room below the backend's 4 MiB HTTP limit for JSON/request framing.
export const MAX_IMPORT_REQUEST_BYTES = 3.5 * 1024 * 1024;

export function assertImportRequestSize(payload: unknown): void {
  const bytes = new TextEncoder().encode(JSON.stringify(payload)).byteLength;
  if (bytes > MAX_IMPORT_REQUEST_BYTES) {
    throw new Error("导入请求不能超过 " + MAX_IMPORT_REQUEST_BYTES / 1024 / 1024 + " MiB");
  }
}

export function validateImportFileSize(file: Pick<File, "size">): void {
  if (file.size > MAX_IMPORT_FILE_BYTES) {
    throw new Error(`导入文件不能超过 ${MAX_IMPORT_FILE_BYTES / 1024 / 1024} MiB`);
  }
}

export function validateImportSheetRange(rangeRef: string, sheetName = "数据表"): void {
  const match = rangeRef.match(/^([A-Z]+)(\d+)(?::([A-Z]+)(\d+))?$/i);
  if (!match) throw new Error(`${sheetName} 的工作表范围无效`);
  const columnNumber = (value: string) => [...value.toUpperCase()].reduce((sum, char) => sum * 26 + char.charCodeAt(0) - 64, 0);
  const rows = Number(match[4] ?? match[2]) - Number(match[2]) + 1;
  const columns = columnNumber(match[3] ?? match[1]) - columnNumber(match[1]) + 1;
  if (rows > MAX_IMPORT_SHEET_ROWS) throw new Error(`${sheetName} 行数超过 ${MAX_IMPORT_SHEET_ROWS} 行限制`);
  if (rows * columns > MAX_IMPORT_SHEET_CELLS) throw new Error(`${sheetName} 单元格超过 ${MAX_IMPORT_SHEET_CELLS} 个限制`);
}

function normalizeHeader(value: unknown) {
  return String(value ?? "").trim().replace(/[()（）]/g, "").replace(/[\s_-]+/g, "").toLowerCase();
}

export async function parseImportWorkbook(file: File, kind: "device" | "point") {
  validateImportFileSize(file);
  const XLSX = await import("xlsx");
  const workbook = XLSX.read(await file.arrayBuffer(), { type: "array" });
  for (const sheetName of workbook.SheetNames) {
    const sheet = workbook.Sheets[sheetName];
    const rangeRef = sheet?.["!ref"];
    if (rangeRef) validateImportSheetRange(rangeRef, sheetName);
  }
  const worksheet = workbook.Sheets[workbook.SheetNames[1] || workbook.SheetNames[0]];
  if (!worksheet) throw new Error("工作簿中没有可导入的数据表");
  const rangeRef = worksheet["!ref"];
  if (!rangeRef) throw new Error("数据表没有有效的单元格范围");

  const fields = kind === "device" ? DEVICE_FIELDS : POINT_FIELDS;
  const aliases = Object.fromEntries(
    Object.entries(fields).flatMap(([label, field]) => [
      [normalizeHeader(label), field],
      [normalizeHeader(field), field],
    ]),
  );

  return XLSX.utils.sheet_to_json<Record<string, unknown>>(worksheet, { defval: "" })
    .map((row) => Object.fromEntries(
      Object.entries(row)
        .map(([key, value]) => [aliases[normalizeHeader(key)], typeof value === "string" ? value.trim() : value ?? ""])
        .filter(([field]) => Boolean(field)),
    ))
    .filter((row) => Object.values(row).some((value) => String(value ?? "").trim() !== ""));
}
