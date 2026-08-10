export interface DistributedNode {
  nodeUuid: string;
  nodeName: string;
  status: "pending" | "approved" | "revoked";
  configVersion: number;
  appliedRevision: number;
  outboxBacklog: number;
  outboxDatabaseBytes: number;
  outboxDiskUsedPercent: number;
  outboxWatermark: "normal" | "warning" | "critical" | "overflow";
  outboxDroppedRecords: number;
  keyVersion: number;
  collectorStatus: string;
  storageStatus: string;
  uploaderStatus: string;
  storageQueueDepth: number;
  storageCommitP99Ms: number;
  storageErrorsTotal: number;
  clockSkewMs: number;
  clockAnomaly: boolean;
  assignmentCount: number;
  readTimeoutMs: number | null;
  lastSeenAt: string | null;
  createdAt: string | null;
  updatedAt: string | null;
}

export interface NodeAssignment {
  deviceId: number;
  deviceCode: string;
  deviceName: string;
  assignmentEpoch: number;
  status: string;
  effectiveAt: string | null;
  cutoverAt: string | null;
  graceUntil: string | null;
}

export interface AssignmentOccupancy {
  deviceId: number;
  nodeUuid: string;
  nodeName: string;
}

export interface AssignmentUpdateResult {
  nodeUuid: string;
  deviceIds: number[];
  migratedDeviceIds: number[];
  removedDeviceIds: number[];
  cutoverAt: string | null;
}

export interface DeviceStatusSyncResult {
  updatedNodeCount: number;
}

export interface RegistrationCodeResult {
  registrationCode: string;
  nodeName: string;
  expiresAt: string;
}
