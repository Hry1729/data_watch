import { useCallback, useEffect, useRef, useState } from "react";
import { getBackendWebSocketUrl } from "@/lib/backend-client";
import { usePageRefresh } from "@/contexts/PageRefreshContext";
import type { RealtimeWireValue } from "@/types/device-monitor";

export type RealtimeConnectionStatus =
  | "idle"
  | "connecting"
  | "connected"
  | "disconnected"
  | "error";

interface RealtimeServerMessage {
  type?: string;
  message?: string;
  code?: number;
  device_code?: string;
  group_code?: string;
  values?: Record<string, RealtimeWireValue>;
  tdengineConnected?: boolean | null;
}

interface RealtimeWebSocketOptions {
  enabled: boolean;
  token: string;
  deviceCode: string;
  groupCode: string;
  flushIntervalMs: number;
}

export function useRealtimeWebSocket({
  enabled,
  token,
  deviceCode,
  groupCode,
  flushIntervalMs,
}: RealtimeWebSocketOptions) {
  const { refreshVersion } = usePageRefresh();
  const [values, setValues] = useState<Record<string, RealtimeWireValue>>({});
  const [status, setStatus] = useState<RealtimeConnectionStatus>("idle");
  const [deviceOffline, setDeviceOffline] = useState(false);
  const [tdengineConnected, setTdengineConnected] = useState<boolean | null>(null);
  const [lastSyncTime, setLastSyncTime] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const pendingRef = useRef<Record<string, RealtimeWireValue>>({});

  const flushPending = useCallback(() => {
    const pending = pendingRef.current;
    if (Object.keys(pending).length === 0) return;
    pendingRef.current = {};
    setValues((current) => ({ ...current, ...pending }));
    setLastSyncTime(Date.now());
  }, []);

  const clearValues = useCallback(() => {
    pendingRef.current = {};
    setValues({});
    setLastSyncTime(null);
    setDeviceOffline(false);
  }, []);

  useEffect(() => {
    if (!enabled) return;
    const timer = window.setInterval(flushPending, Math.max(100, flushIntervalMs));
    return () => {
      window.clearInterval(timer);
      flushPending();
    };
  }, [enabled, flushIntervalMs, flushPending]);

  useEffect(() => {
    if (!enabled || !token || !deviceCode) {
      setStatus("idle");
      return;
    }

    if (import.meta.env.MODE !== "production" && import.meta.env.VITE_USE_MOCK === "true") {
      setStatus("connected");
      setDeviceOffline(false);
      setError(null);
      return;
    }

    let socket: WebSocket | null = null;
    let reconnectTimer: number | null = null;
    let heartbeatTimer: number | null = null;
    let reconnectAttempts = 0;
    let disposed = false;

    const clearTimers = () => {
      if (reconnectTimer !== null) window.clearTimeout(reconnectTimer);
      if (heartbeatTimer !== null) window.clearInterval(heartbeatTimer);
      reconnectTimer = null;
      heartbeatTimer = null;
    };

    const send = (message: object) => {
      if (socket?.readyState === WebSocket.OPEN) socket.send(JSON.stringify(message));
    };

    const scheduleReconnect = () => {
      if (disposed || reconnectTimer !== null) return;
      const delay = Math.min(30_000, 1_000 * 2 ** reconnectAttempts);
      reconnectAttempts += 1;
      reconnectTimer = window.setTimeout(() => {
        reconnectTimer = null;
        connect();
      }, delay);
    };

    function connect() {
      if (disposed || socket?.readyState === WebSocket.CONNECTING || socket?.readyState === WebSocket.OPEN) return;
      setStatus("connecting");
      setError(null);
      try {
        socket = new WebSocket(getBackendWebSocketUrl());
      } catch {
        setStatus("disconnected");
        scheduleReconnect();
        return;
      }

      socket.onopen = () => {
        reconnectAttempts = 0;
        setStatus("connected");
        send({ type: "auth", token });
        send({ action: "subscribe", device_code: deviceCode, group_code: groupCode });
        heartbeatTimer = window.setInterval(() => send({ action: "ping" }), 15_000);
      };

      socket.onmessage = (event) => {
        if (event.data === "pong") return;
        let message: RealtimeServerMessage;
        try {
          message = JSON.parse(String(event.data)) as RealtimeServerMessage;
        } catch {
          return;
        }
        if (message.device_code && message.device_code !== deviceCode) return;
        if (message.group_code && message.group_code !== groupCode) return;
        if (message.type === "error") {
          setError(message.message ?? "实时订阅失败");
          setStatus("error");
          return;
        }
        if (message.type === "offline") {
          pendingRef.current = {};
          setValues({});
          setDeviceOffline(true);
          return;
        }
        if (message.type === "snapshot") {
          pendingRef.current = {};
          setValues(message.values ?? {});
          setTdengineConnected(message.tdengineConnected ?? null);
          setDeviceOffline(false);
          setLastSyncTime(Date.now());
          return;
        }
        if (message.type === "diff" && message.values) {
          Object.assign(pendingRef.current, message.values);
          setDeviceOffline(false);
        }
      };

      socket.onerror = () => setStatus("disconnected");
      socket.onclose = () => {
        socket = null;
        if (heartbeatTimer !== null) window.clearInterval(heartbeatTimer);
        heartbeatTimer = null;
        if (!disposed) {
          setStatus("disconnected");
          scheduleReconnect();
        }
      };
    }

    const handleVisibilityChange = () => {
      if (!document.hidden && socket?.readyState !== WebSocket.OPEN) connect();
    };

    connect();
    document.addEventListener("visibilitychange", handleVisibilityChange);
    return () => {
      disposed = true;
      document.removeEventListener("visibilitychange", handleVisibilityChange);
      clearTimers();
      if (socket?.readyState === WebSocket.OPEN) {
        send({ action: "unsubscribe", device_code: deviceCode, group_code: groupCode });
      }
      socket?.close();
      socket = null;
    };
  }, [deviceCode, enabled, groupCode, refreshVersion, token]);

  return {
    values,
    status,
    deviceOffline,
    tdengineConnected,
    lastSyncTime,
    error,
    clearValues,
  };
}
