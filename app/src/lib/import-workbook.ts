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

function normalizeHeader(value: unknown) {
  return String(value ?? "").trim().replace(/[()（）]/g, "").replace(/[\s_-]+/g, "").toLowerCase();
}

export async function parseImportWorkbook(file: File, kind: "device" | "point") {
  const XLSX = await import("xlsx");
  const workbook = XLSX.read(await file.arrayBuffer(), { type: "array" });
  const worksheet = workbook.Sheets[workbook.SheetNames[1] || workbook.SheetNames[0]];
  if (!worksheet) throw new Error("工作簿中没有可导入的数据表");

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
